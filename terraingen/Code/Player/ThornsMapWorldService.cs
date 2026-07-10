namespace Terraingen.Player;

using Terraingen.Buildings;
using Terraingen.GameData;
using Terraingen.Multiplayer;
using Terraingen.NpcGuild;
using Terraingen.TerrainGen;
using Terraingen.UI;
using Terraingen.World;

/// <summary>Map markers from live world state; texture generated once and cached.</summary>
public sealed class ThornsMapWorldService : Component
{
	public static ThornsMapWorldService Instance { get; private set; }

	const string MapTextureVirtualPath = "ui/map/thorns_world_map.png";

	readonly Dictionary<string, List<ThornsMapMarkerDto>> _hostWaypoints = new();
	readonly Dictionary<string, ThornsMapMarkerDto> _hostLastDeaths = new();
	string _cachedTexturePath = MapTextureVirtualPath;
	float _worldMinX;
	float _worldMinY;
	float _worldMaxX;
	float _worldMaxY;
	int _mapTextureSeed = int.MinValue;
	int _lastLoggedTownNodeVersion = -1;
	int _lastLoggedBloomSeedMarkerCount = -1;

	protected override void OnStart()
	{
		Instance = this;
		TryGenerateMapTexture();
		RefreshLocalMapSnapshots();
	}

	protected override void OnDestroy()
	{
		if ( Instance == this )
			Instance = null;
	}

	/// <summary>Called when terrain sculpt finishes so the map image can be built from the heightfield.</summary>
	public void NotifyTerrainReady()
	{
		_mapTextureSeed = int.MinValue;
		ThornsMapTextureCache.Invalidate();
		TryGenerateMapTexture();
		RefreshLocalMapSnapshots();
	}

	/// <summary>Called after procedural towns are placed so minimap / map markers include town centers.</summary>
	public void NotifyWorldMarkersChanged()
	{
		if ( ThornsMultiplayer.IsHostOrOffline )
			HostPushMapSnapshotsToAllPlayers();
		else
			RefreshLocalMapSnapshots();
	}

	void HostPushMapSnapshotsToAllPlayers()
	{
		if ( Scene is null || !Scene.IsValid() )
			return;

		foreach ( var gameplay in Scene.GetAllComponents<ThornsPlayerGameplay>() )
		{
			if ( !gameplay.IsValid() || string.IsNullOrWhiteSpace( gameplay.AccountKey ) )
				continue;

			gameplay.PushMapSnapshotToOwner();
		}
	}

	void RefreshLocalMapSnapshots() => RefreshLocalPeerMapSnapshot();

	/// <summary>
	/// Bloom seeds and airdrops only exist on clients after interest-based RPCs.
	/// Keep the last host-synced markers when rebuilding the map locally.
	/// </summary>
	public static void PreserveHostAuthoritativeMarkers( ThornsMapSnapshotDto prior, ThornsMapSnapshotDto next )
	{
		if ( prior?.Markers is null || prior.Markers.Count == 0 || next is null )
			return;

		next.Markers ??= new List<ThornsMapMarkerDto>();
		next.Markers.RemoveAll( IsHostAuthoritativeMarkerKind );

		foreach ( var marker in prior.Markers )
		{
			if ( IsHostAuthoritativeMarkerKind( marker ) )
				next.Markers.Add( marker );
		}
	}

	static bool IsHostAuthoritativeMarkerKind( ThornsMapMarkerDto marker ) =>
		marker is not null && marker.Kind is ThornsMapMarkerKind.BloomSeed or ThornsMapMarkerKind.Airdrop or ThornsMapMarkerKind.LastDeath;

	void TryGenerateMapTexture()
	{
		var bootstrap = Scene.GetAllComponents<ThornsTerrainBootstrap>().FirstOrDefault();
		var terrain = ThornsTerrainCache.Resolve( Scene );
		if ( !terrain.IsValid() )
			return;

		RefreshWorldBoundsFromTerrain( terrain );

		var config = bootstrap?.Config;
		var seed = config?.WorldSeed ?? 0;
		if ( seed == _mapTextureSeed && ThornsMapTextureCache.IsReady )
			return;

		HeightmapField field = null;
		if ( bootstrap.IsValid() )
			field = bootstrap.GetHeightFieldForMap();

		if ( field is null && config is not null && ThornsTerrainHeightCache.TryLoad( config, out var cached ) )
			field = cached;

		if ( field is null && config is not null )
		{
			try
			{
				field = HeightmapLoader.LoadFromContent( config.HeightmapPath );
			}
			catch
			{
				// Source heightmap optional when sculpt cache is not ready yet.
			}
		}

		_cachedTexturePath = $"map://{seed}";
		ThornsMapTextureCache.Ensure( terrain, seed, field, config );

		if ( ThornsMapTextureCache.IsReady )
			_mapTextureSeed = seed;
	}

	public ThornsMapSnapshotDto BuildSnapshotFor( ThornsPlayerGameplay player, string accountKey )
	{
		var terrain = ThornsTerrainCache.Resolve( Scene );
		if ( terrain.IsValid() )
			RefreshWorldBoundsFromTerrain( terrain );

		TryGenerateMapTexture();
		var markers = new List<ThornsMapMarkerDto>();

		if ( player.IsValid() )
		{
			var pos = player.GameObject.WorldPosition;
			markers.Add( new ThornsMapMarkerDto
			{
				Id = "you",
				Kind = ThornsMapMarkerKind.You,
				WorldX = pos.x,
				WorldY = pos.y,
				Label = "You"
			} );
		}

		foreach ( var session in Scene.GetAllComponents<ThornsPlayerSession>() )
		{
			if ( !session.IsValid() || session.HostPersistenceAccountKey == accountKey )
				continue;

			var go = FindPlayerObject( session.OwnerConnection );
			if ( !go.IsValid() )
				continue;

			var pos = go.WorldPosition;
			markers.Add( new ThornsMapMarkerDto
			{
				Id = $"guild_{session.HostPersistenceAccountKey}",
				Kind = ThornsMapMarkerKind.GuildMember,
				WorldX = pos.x,
				WorldY = pos.y,
				Label = session.OwnerConnection?.DisplayName ?? "Member"
			} );
		}

		var townMarkerCount = AddGeneratedTownMarkers( markers, Scene );
		AddNpcGuildOutpostMarkers( markers, Scene );
		if ( ThornsTownNodeRegistry.Version != _lastLoggedTownNodeVersion )
		{
			_lastLoggedTownNodeVersion = ThornsTownNodeRegistry.Version;
			Log.Info( $"[Thorns Map] Snapshot includes {townMarkerCount} generated POI marker(s); townNodeVersion={ThornsTownNodeRegistry.Version}." );
		}
		ThornsAirdropWorldService.Instance?.AppendMapMarkers( markers );
		var beforeBloomMarkers = markers.Count;
		ThornsBloomSeedWorldService.Instance?.AppendMapMarkers( markers );
		var bloomMarkerCount = markers.Count - beforeBloomMarkers;
		if ( bloomMarkerCount != _lastLoggedBloomSeedMarkerCount )
		{
			_lastLoggedBloomSeedMarkerCount = bloomMarkerCount;
			Log.Info( $"[Thorns Map] Snapshot includes {bloomMarkerCount} Bloom Seed marker(s)." );
		}

		if ( TryGetWaypoints( accountKey, out var waypoints ) )
		{
			foreach ( var wp in waypoints )
				markers.Add( wp );
		}

		if ( TryGetLastDeath( accountKey, out var lastDeath ) )
			markers.Add( lastDeath );

		var snap = new ThornsMapSnapshotDto
		{
			MapTexturePath = _cachedTexturePath,
			Markers = markers
		};

		if ( terrain.IsValid() )
			ThornsMapProjection.ApplyBoundsToSnapshot( snap, terrain );
		else if ( _worldMaxX > _worldMinX + 1f && _worldMaxY > _worldMinY + 1f )
		{
			snap.WorldMinX = _worldMinX;
			snap.WorldMinY = _worldMinY;
			snap.WorldMaxX = _worldMaxX;
			snap.WorldMaxY = _worldMaxY;
		}

		return snap;
	}

	void RefreshWorldBoundsFromTerrain( Terrain terrain )
	{
		if ( !ThornsMapProjection.TryGetTerrainBounds( terrain, out var minX, out var minY, out var maxX, out var maxY ) )
			return;

		_worldMinX = minX;
		_worldMinY = minY;
		_worldMaxX = maxX;
		_worldMaxY = maxY;
	}

	static void AddNpcGuildOutpostMarkers( List<ThornsMapMarkerDto> markers, Scene scene )
	{
		if ( scene is null || !scene.IsValid() )
			return;

		foreach ( var core in scene.GetAllComponents<ThornsNpcGuildCore>() )
		{
			if ( core is null || !core.IsValid() || !core.Enabled )
				continue;

			var template = ThornsNpcGuildCatalog.TryGet( core.GuildId );
			var guildName = template?.GuildName ?? "NPC Guild";
			var label = core.IsHeadquarters
				? $"{guildName} HQ"
				: $"{guildName} Outpost";

			markers.Add( new ThornsMapMarkerDto
			{
				Id = $"npc_outpost_{core.GuildId}_{core.OutpostId}",
				Kind = ThornsMapMarkerKind.NpcGuildOutpost,
				WorldX = core.CenterWorld.x,
				WorldY = core.CenterWorld.y,
				Label = label
			} );
		}
	}

	static int AddGeneratedTownMarkers( List<ThornsMapMarkerDto> markers, Scene scene )
	{
		var index = 1;
		foreach ( var node in ThornsTownNodeRegistry.TownNodes )
		{
			AddTownMarker( markers, index, node );
			index++;
		}

		if ( index > 1 )
			return index - 1;

		if ( scene is null || !scene.IsValid() )
			return 0;

		foreach ( var generator in scene.GetAllComponents<ThornsWorldBuildingGenerator>() )
		{
			if ( !generator.IsValid() )
				continue;

			foreach ( var node in generator.TownNodes )
			{
				AddTownMarker( markers, index, node );
				index++;
			}
		}

		return index - 1;
	}

	static void AddTownMarker( List<ThornsMapMarkerDto> markers, int index, ThornsTownNode node )
	{
		var def = ThornsPoiIdentityCatalog.Get( node.Identity );
		markers.Add( new ThornsMapMarkerDto
		{
			Id = $"poi_{index}_{node.Identity.ToString().ToLowerInvariant()}",
			Kind = ThornsPoiIdentityCatalog.MapMarkerKind( node.Identity ),
			WorldX = node.Center.x,
			WorldY = node.Center.y,
			Label = $"{def.DisplayName} {index} ({node.PlacedBuildingCount}/{node.TargetBuildingCount})"
		} );
	}

	static GameObject FindPlayerObject( Connection connection )
	{
		if ( connection is null )
			return null;

		foreach ( var session in Game.ActiveScene?.GetAllComponents<ThornsPlayerSession>() ?? Array.Empty<ThornsPlayerSession>() )
		{
			if ( session.OwnerConnection?.Id != connection.Id )
				continue;

			foreach ( var gameplay in Game.ActiveScene.GetAllComponents<ThornsPlayerGameplay>() )
			{
				if ( gameplay.IsValid() && gameplay.Network.Owner?.Id == connection.Id )
					return gameplay.GameObject;
			}
		}

		return null;
	}

	/// <summary>Keep host-authoritative waypoints when a client rebuilds markers from local world state.</summary>
	public void PreserveUiWaypoints( ThornsMapSnapshotDto map )
	{
		if ( map is null )
			return;

		var existing = ThornsUiClientState.Snapshot?.Map?.Markers;
		if ( existing is null || existing.Count == 0 )
			return;

		map.Markers.RemoveAll( IsWaypointMarkerKind );
		foreach ( var marker in existing )
		{
			if ( IsWaypointMarkerKind( marker ) )
				map.Markers.Add( marker );
		}
	}

	public void HostSetWaypoint( string accountKey, float worldX, float worldY )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || string.IsNullOrEmpty( accountKey ) )
			return;

		SetWaypoint( accountKey, worldX, worldY );
		RefreshLocalMapSnapshots();
	}

	public void HostSetGoalWaypoint( string accountKey, float worldX, float worldY, string label )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || string.IsNullOrEmpty( accountKey ) )
			return;

		SetWaypoint( accountKey, worldX, worldY, ThornsMapMarkerKind.Goal, "goal", string.IsNullOrWhiteSpace( label ) ? "Goal" : label );
		RefreshLocalMapSnapshots();
	}

	public void HostClearWaypoint( string accountKey )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || string.IsNullOrEmpty( accountKey ) )
			return;

		if ( !_hostWaypoints.TryGetValue( accountKey, out var list ) )
			return;

		list.RemoveAll( wp => wp.Kind == ThornsMapMarkerKind.CustomWaypoint );
		if ( list.Count == 0 )
			_hostWaypoints.Remove( accountKey );

		RefreshLocalMapSnapshots();
	}

	public void HostClearGoalWaypoint( string accountKey )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || string.IsNullOrEmpty( accountKey ) )
			return;

		if ( !_hostWaypoints.TryGetValue( accountKey, out var list ) )
			return;

		list.RemoveAll( wp => wp.Kind == ThornsMapMarkerKind.Goal );
		if ( list.Count == 0 )
			_hostWaypoints.Remove( accountKey );

		RefreshLocalMapSnapshots();
	}

	public void HostSetLastDeath( string accountKey, Vector3 worldPosition )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || string.IsNullOrEmpty( accountKey ) )
			return;

		_hostLastDeaths[accountKey] = new ThornsMapMarkerDto
		{
			Id = "last_death",
			Kind = ThornsMapMarkerKind.LastDeath,
			WorldX = worldPosition.x,
			WorldY = worldPosition.y,
			Label = "Last Death"
		};

		RefreshLocalMapSnapshots();
		ThornsWorldPersistence.RequestSave();
	}

	/// <summary>Throttled refresh for the local peer's minimap/map tab (host or client).</summary>
	public void RefreshLocalPeerMapSnapshot()
	{
		try
		{
			var local = ThornsPlayerGameplay.Local;
			if ( !local.IsValid() || !local.IsLocalPlayer() )
				return;

			if ( Networking.IsActive && !ThornsMultiplayer.IsHostOrOffline )
				local.RefreshClientWorldMapSnapshot();
			else
				local.RefreshMapSnapshot();
		}
		catch ( Exception e )
		{
			Log.Warning( e, "[Thorns Map] Local peer snapshot refresh failed." );
		}
	}

	void SetWaypoint( string accountKey, float worldX, float worldY ) =>
		SetWaypoint( accountKey, worldX, worldY, ThornsMapMarkerKind.CustomWaypoint, "waypoint", "Waypoint" );

	void SetWaypoint( string accountKey, float worldX, float worldY, ThornsMapMarkerKind kind, string id, string label )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline )
			return;

		if ( !_hostWaypoints.TryGetValue( accountKey, out var list ) )
		{
			list = new List<ThornsMapMarkerDto>();
			_hostWaypoints[accountKey] = list;
		}

		list.RemoveAll( wp => wp.Kind == kind );
		list.Add( new ThornsMapMarkerDto
		{
			Id = id,
			Kind = kind,
			WorldX = worldX,
			WorldY = worldY,
			Label = label
		} );
	}

	static bool IsWaypointMarkerKind( ThornsMapMarkerDto marker ) =>
		marker?.Kind is ThornsMapMarkerKind.CustomWaypoint or ThornsMapMarkerKind.Goal;

	bool TryGetWaypoints( string accountKey, out List<ThornsMapMarkerDto> waypoints ) =>
		_hostWaypoints.TryGetValue( accountKey, out waypoints );

	bool TryGetLastDeath( string accountKey, out ThornsMapMarkerDto marker ) =>
		_hostLastDeaths.TryGetValue( accountKey, out marker );

	public void ExportHostWaypointsTo( Dictionary<string, ThornsPersistentPlayerMapDto> target )
	{
		if ( target is null )
			return;

		foreach ( var pair in _hostWaypoints )
		{
			target[pair.Key] = new ThornsPersistentPlayerMapDto
			{
				Waypoints = pair.Value?.Select( wp => new ThornsPersistentWaypointDto
				{
					Id = wp.Id,
					Kind = wp.Kind.ToString(),
					WorldX = wp.WorldX,
					WorldY = wp.WorldY,
					Label = wp.Label
				} ).ToList() ?? new List<ThornsPersistentWaypointDto>()
			};
		}

		foreach ( var pair in _hostLastDeaths )
		{
			if ( string.IsNullOrWhiteSpace( pair.Key ) || pair.Value is null )
				continue;

			if ( !target.TryGetValue( pair.Key, out var map ) )
			{
				map = new ThornsPersistentPlayerMapDto();
				target[pair.Key] = map;
			}

			map.HasLastDeath = true;
			map.LastDeathWorldX = pair.Value.WorldX;
			map.LastDeathWorldY = pair.Value.WorldY;
		}
	}

	public void ImportHostWaypointsFrom( Dictionary<string, ThornsPersistentPlayerMapDto> source )
	{
		_hostWaypoints.Clear();
		_hostLastDeaths.Clear();
		if ( source is null )
			return;

		foreach ( var pair in source )
		{
			if ( string.IsNullOrWhiteSpace( pair.Key ) || pair.Value is null )
				continue;

			_hostWaypoints[pair.Key] = (pair.Value.Waypoints ?? new List<ThornsPersistentWaypointDto>()).Select( wp => new ThornsMapMarkerDto
			{
				Id = wp.Id,
				Kind = Enum.TryParse<ThornsMapMarkerKind>( wp.Kind, ignoreCase: true, out var kind )
					? kind
					: ThornsMapMarkerKind.CustomWaypoint,
				WorldX = wp.WorldX,
				WorldY = wp.WorldY,
				Label = wp.Label
			} ).ToList();

			if ( pair.Value.HasLastDeath )
			{
				_hostLastDeaths[pair.Key] = new ThornsMapMarkerDto
				{
					Id = "last_death",
					Kind = ThornsMapMarkerKind.LastDeath,
					WorldX = pair.Value.LastDeathWorldX,
					WorldY = pair.Value.LastDeathWorldY,
					Label = "Last Death"
				};
			}
		}
	}
}
