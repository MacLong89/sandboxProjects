namespace Terraingen.NpcGuild;

using Terraingen.AI;
using Terraingen.Buildings;
using Terraingen.Core;
using Terraingen.GameData;
using Terraingen.Multiplayer;
using Terraingen.Player;
using Terraingen.TerrainGen;
using Terraingen.Victory;
using Terraingen.World;

/// <summary>Host-only NPC rival guilds: outposts, expansion, cores, and garrison bandits.</summary>
public sealed partial class ThornsNpcGuildWorldService : Component
{
	public const string DefaultGuildId = ThornsNpcGuildCatalog.IronWolvesId;
	public const int OutpostVictoryTarget = 10;
	public const float ExpansionIntervalMinSeconds = 10f * 60f;
	public const float ExpansionIntervalMaxSeconds = 20f * 60f;
	public const float PlayerNearOutpostInches = 7874f;
	public const float PlayerBanditCapRadiusInches = 39370f;
	public const int BanditsPerOutpost = 10;
	public const int MaxBanditsNearPlayer = 15;
	public const float GarrisonReplenishIntervalSeconds = 60f;
	public const float ExpansionMinDistanceInches = 19685f;
	public const float ExpansionMaxDistanceInches = 39370f;

	public const int XpOutpostDestroyed = 100;
	public const int XpGuildEliminated = 500;

	public static ThornsNpcGuildWorldService Instance { get; private set; }

	public static ThornsNpcGuildWorldService EnsureInstance()
	{
		if ( Instance is not null && Instance.IsValid() )
			return Instance;

		var hostObject = Game.ActiveScene?.GetAllComponents<ThornsWorldPersistence>().FirstOrDefault()?.GameObject
		                 ?? Game.ActiveScene?.GetAllComponents<ThornsTerrainBootstrap>().FirstOrDefault()?.GameObject
		                 ?? Game.ActiveScene?.GetAllComponents<ThornsNetworkGameManager>().FirstOrDefault()?.GameObject;
		if ( hostObject is null || !hostObject.IsValid() )
			return null;

		var service = hostObject.Components.Get<ThornsNpcGuildWorldService>()
		              ?? hostObject.Components.Create<ThornsNpcGuildWorldService>();
		Instance = service;
		return service;
	}

	Terrain _terrain;
	ThornsTerrainConfig _terrainConfig;
	GameObject _root;
	bool _initialized;

	readonly Dictionary<string, GuildFaction> _factions = new( StringComparer.OrdinalIgnoreCase );

	protected override void OnStart()
	{
		Instance = this;
	}

	protected override void OnDestroy()
	{
		if ( Instance == this )
			Instance = null;
	}

	protected override void OnFixedUpdate()
	{
		if ( Instance != this || !ThornsMultiplayer.IsHostOrOffline || !Game.IsPlaying || !_initialized )
			return;

		if ( HasOnlinePlayers( Scene ) )
		{
			foreach ( var faction in _factions.Values )
			{
				if ( faction.IsEliminated )
					continue;

				faction.ExpansionAccumulator += Time.Delta;
				if ( faction.ExpansionAccumulator >= faction.NextExpansionIntervalSeconds )
				{
					faction.ExpansionAccumulator = 0f;
					faction.NextExpansionIntervalSeconds = RollNextExpansionDelay();
					faction.HostTryExpandOutpost( this );
				}
			}
		}

		foreach ( var faction in _factions.Values )
			faction.TickGarrisonBandits( Scene );
	}

	public void OnWorldReady( Terrain terrain, ThornsTerrainConfig config )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || ThornsMinimalTestSceneBootstrap.IsActive )
			return;

		Instance = this;
		_terrain = terrain;
		_terrainConfig = config;
		ThornsWorldPersistence.EnsureHostReady( GameObject );

		if ( _root.IsValid() )
			_root.Destroy();

		_root = Scene.CreateObject( true );
		_root.Name = "Thorns NPC Guilds";
		_root.Parent = GameObject;
		_ = _root.Components.Create<ThornsBuildingLootWorldService>();

		_factions.Clear();
		var guildService = ThornsGuildWorldService.EnsureInstance();
		guildService?.HostEnsureAllCatalogNpcGuilds();

		foreach ( var template in ThornsNpcGuildCatalog.All )
		{
			if ( !_factions.ContainsKey( template.GuildId ) )
				_factions[template.GuildId] = new GuildFaction( template.GuildId );

			guildService?.HostEnsureNpcRivalGuild( template.GuildId );
		}

		HostRestoreOrCreateAll();
		HostRefreshGuildMenuSnapshots();
		_initialized = true;
		ThornsWorldPersistence.Instance?.HostSyncNpcGuildStateToLive( this );

		var catalogIds = string.Join( ", ", ThornsNpcGuildCatalog.All.Select( t => t.GuildId ) );
		Log.Info( $"[Thorns NPC Guild] Ready {_factions.Count}/{ThornsNpcGuildCatalog.All.Count} rival faction(s) [{catalogIds}]." );
	}

	public ThornsNpcGuildRivalDto BuildRivalSnapshot()
		=> BuildAllRivalSnapshots().FirstOrDefault( r => r.HasRival ) ?? new ThornsNpcGuildRivalDto();

	public List<ThornsNpcGuildRivalDto> BuildAllRivalSnapshots()
	{
		var snapshots = new List<ThornsNpcGuildRivalDto>( ThornsNpcGuildCatalog.All.Count );
		foreach ( var template in ThornsNpcGuildCatalog.All )
		{
			if ( _factions.TryGetValue( template.GuildId, out var faction ) )
				snapshots.Add( faction.BuildRivalSnapshot() );
			else
				snapshots.Add( BuildCatalogRivalPlaceholder( template ) );
		}

		return snapshots;
	}

	static ThornsNpcGuildRivalDto BuildCatalogRivalPlaceholder( ThornsNpcGuildCatalog.NpcGuildTemplate template )
		=> new()
		{
			HasRival = true,
			GuildId = template.GuildId,
			GuildName = template.GuildName,
			Motto = template.Motto,
			StatusLine = "Deploying — rival headquarters not yet established.",
			OutpostTarget = OutpostVictoryTarget
		};

	public void ExportToSave( ThornsPersistentWorldDto dto )
	{
		if ( dto is null || !_initialized || _factions.Count == 0 )
			return;

		dto.NpcGuilds = _factions.Values.Select( f => f.ExportSaved() ).ToList();
		dto.NpcGuild = dto.NpcGuilds.FirstOrDefault( g =>
			               string.Equals( g.GuildId, ThornsNpcGuildCatalog.IronWolvesId, StringComparison.OrdinalIgnoreCase ) )
		               ?? dto.NpcGuilds.FirstOrDefault()
		               ?? new ThornsPersistentNpcGuildDto();
	}

	void HostRestoreOrCreateAll()
	{
		var live = ThornsWorldPersistence.Instance?.Live;
		var savedGuilds = live?.NpcGuilds;
		var legacy = live?.NpcGuild;
		var occupiedHqCenters = new List<Vector3>();

		foreach ( var template in ThornsNpcGuildCatalog.All )
		{
			if ( !_factions.TryGetValue( template.GuildId, out var faction ) )
				continue;

			var saved = savedGuilds?.FirstOrDefault( g => g is not null
			                                              && string.Equals(
				                                              g.GuildId,
				                                              template.GuildId,
				                                              StringComparison.OrdinalIgnoreCase ) );

			if ( saved is null
			     && legacy is not null
			     && string.Equals( legacy.GuildId, template.GuildId, StringComparison.OrdinalIgnoreCase )
			     && (legacy.Outposts?.Count > 0 || legacy.IsEliminated) )
				saved = legacy;

			faction.HostRestoreOrCreate( this, occupiedHqCenters, saved );
		}

		ThornsBuildingLootWorldService.Instance?.HostSyncFurnitureContainers();
		ThornsMapWorldService.Instance?.NotifyWorldMarkersChanged();
		HostRefreshGuildMenuSnapshots();
	}

	void HostRefreshGuildMenuSnapshots()
	{
		ThornsGuildWorldService.EnsureInstance()?.HostEnsureAllCatalogNpcGuilds();

		foreach ( var faction in _factions.Values )
			faction.ResyncGuildMenuState();

		ThornsGuildWorldService.RefreshAllGuildSnapshotsFromWorld();
	}

	/// <summary>Host resolves the aimed core from the player pawn — avoids trusting client guild ids.</summary>
	public bool HostTryClaimCoreResolved( ThornsPlayerGameplay player )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || player is null || !player.IsValid() )
			return false;

		if ( !ThornsNpcGuildCore.TryPickAlongRay( player.GameObject, out var core, out _ ) || core is null )
		{
			HostNotifyClaimFailed( player, "No rival core in range." );
			return false;
		}

		return HostTryClaimCore( player, core.GuildId, core.OutpostId );
	}

	public bool HostTryClaimCore( ThornsPlayerGameplay player, string guildId, string outpostId )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || player is null || !player.IsValid() )
			return false;

		if ( string.IsNullOrWhiteSpace( outpostId ) )
		{
			HostNotifyClaimFailed( player, "Rival core is not linked to an outpost." );
			return false;
		}

		string reason = null;
		if ( !string.IsNullOrWhiteSpace( guildId )
		     && _factions.TryGetValue( guildId, out var faction )
		     && faction.HostTryClaimCore( player, outpostId, out reason ) )
			return true;

		foreach ( var rival in _factions.Values )
		{
			if ( rival.HostTryClaimCore( player, outpostId, out reason ) )
				return true;
		}

		HostNotifyClaimFailed( player, reason ?? "Could not claim this rival core." );
		return false;
	}

	static void HostNotifyClaimFailed( ThornsPlayerGameplay player, string reason )
		=> player?.HostNotifyNpcCoreClaimFailed( reason );

	void PersistSoon() => ThornsWorldPersistence.RequestSave();

	static bool HostValidateClaim( ThornsPlayerGameplay player, OutpostRuntime outpost, out string failReason )
	{
		failReason = null;
		if ( outpost.Core is null || !outpost.Core.IsValid() )
		{
			failReason = "Rival core is missing.";
			return false;
		}

		var dist = player.GameObject.WorldPosition.Distance( outpost.Core.CenterWorld );
		if ( dist > ThornsNpcGuildCore.InteractRangeInches + 48f )
		{
			failReason = "Move closer to the rival core.";
			return false;
		}

		PruneDeadBandits( outpost );
		if ( CountLiveOutpostBandits( outpost ) > 0 )
		{
			failReason = "Clear this outpost's defenders before claiming the core.";
			return false;
		}

		return true;
	}

	static int CountLiveOutpostBandits( OutpostRuntime outpost )
	{
		var count = 0;
		foreach ( var root in outpost.BanditRoots )
		{
			if ( !root.IsValid() )
				continue;

			var brain = root.Components.Get<ThornsBanditBrain>( FindMode.EverythingInSelf );
			if ( brain.IsValid() && !brain.IsDead )
				count++;
		}

		return count;
	}

	static int CountGarrisonBanditsNear( Vector3 center, float radius )
	{
		var radiusSqr = radius * radius;
		var count = 0;
		foreach ( var brain in ThornsBanditPopulation.HostBrainsReadOnly )
		{
			if ( brain is null || !brain.IsValid() || brain.IsDead )
				continue;

			if ( !brain.GameObject.Tags.Has( "npc_guild_garrison" ) )
				continue;

			if ( (brain.GameObject.WorldPosition - center).LengthSquared <= radiusSqr )
				count++;
		}

		return count;
	}

	static void PruneDeadBandits( OutpostRuntime outpost )
	{
		outpost.BanditRoots.RemoveAll( r => !r.IsValid() );
	}

	static IReadOnlyList<GameObject> CollectPlayerRoots( Scene scene )
	{
		if ( scene is null || !scene.IsValid() )
			return Array.Empty<GameObject>();

		ThornsPlayerRootCache.RefreshIfStale( scene );
		return ThornsPlayerRootCache.RootsReadOnly;
	}

	static bool HasOnlinePlayers( Scene scene ) => CollectPlayerRoots( scene ).Count > 0;

	sealed class OutpostRuntime
	{
		public string OutpostId;
		public bool IsHeadquarters;
		public Vector3 Center;
		public GameObject Root;
		public ThornsNpcGuildCore Core;
		public int OutpostSeed;
		public int BuildingIndexOffset;
		public List<int> FurnitureIds = new();
		public List<GameObject> BanditRoots = new();
		public bool GarrisonSeeded;
		public TimeSince SecondsSincePlayerNear;
		public TimeSince SecondsSinceLastGarrisonSpawn;

		public ThornsPersistentNpcGuildOutpostDto ToPersistent() => new()
		{
			OutpostId = OutpostId,
			IsHeadquarters = IsHeadquarters,
			OutpostSeed = OutpostSeed,
			BuildingIndexOffset = BuildingIndexOffset,
			Px = Center.x,
			Py = Center.y,
			Pz = Center.z,
			RYaw = Core.IsValid() ? Core.GameObject.WorldRotation.Yaw() : 0f
		};
	}

	static float RollNextExpansionDelay() =>
		Game.Random.Float( ExpansionIntervalMinSeconds, ExpansionIntervalMaxSeconds );
}
