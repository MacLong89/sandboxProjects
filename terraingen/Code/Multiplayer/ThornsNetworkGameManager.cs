namespace Terraingen.Multiplayer;

using Sandbox.Network;
using Terraingen.TerrainGen;
using Terraingen;
using Terraingen.Combat;
using Terraingen.UI;
using Terraingen.UI.Core;
using Terraingen.UI.Menu;
using Terraingen.Player;
using Terraingen.Rendering;

/// <summary>
/// Minimal joinable s&box server host for terraingen. Creates a lobby and spawns one networked player per connection.
/// </summary>
[Title( "Thorns Network Game Manager" )]
[Category( "Thorns/Multiplayer" )]
[Icon( "hub" )]
public sealed class ThornsNetworkGameManager : Component, Component.INetworkListener
{
	[Property] public bool CreateLobbyOnLoad { get; set; } = true;
	[Property] public string DefaultServerName { get; set; } = "Thorns Terrain";
	[Property] public bool RequireJoinPassword { get; set; }
	[Property] public string JoinPassword { get; set; } = "";

	[Property] public GameObject PlayerPrefab { get; set; }
	[Property] public string PlayerPrefabPath { get; set; } = "templates/gameobject/player controller.prefab";
	[Property] public Vector3 FallbackSpawnPoint { get; set; } = new( 24744.8f, -12213f, 3594.6f );
	[Property] public float SpawnHeightOffset { get; set; } = 72f;
	[Property] public float SprintSpeedMultiplier { get; set; } = ThornsPlayerMovementDefaults.SprintSpeedMultiplier;

	bool _lobbyCreateAttempted;
	bool _hasMenuHostOptions;
	ThornsHostLocalSaveLobbyOptions _menuHostOptions;
	readonly List<Connection> _pendingActiveConnections = new();
	TimeUntil _lobbyDiscoveryRefresh;

	protected override void OnStart()
	{
		if ( Scene.IsEditor || !Game.IsPlaying )
			return;

		ThornsWorldBootGate.EnsureDriver();

		var joiningRemote = ThornsSessionBootstrap.TakeJoinRemoteLobbyRequest();
		if ( joiningRemote )
			_lobbyCreateAttempted = true;

		PreparePersistenceSavePath( joiningRemote );
		EnsurePersistenceComponent()?.HostEnsureInitialized();
		TryCreateLobby();
		_ = EnsureListenHostPlayerSpawnAsync();
	}

	async System.Threading.Tasks.Task EnsureListenHostPlayerSpawnAsync()
	{
		for ( var i = 0; i < 60; i++ )
		{
			await System.Threading.Tasks.Task.Delay( 50 );

			if ( !Game.IsPlaying || !Networking.IsActive || !Networking.IsHost )
				return;

			var local = Connection.Local;
			if ( local is null )
				continue;

			if ( FindExistingSession( local ).IsValid() )
				return;

			if ( !IsTerrainReadyForSpawn() )
				continue;

			SpawnPlayerForConnection( local );
			return;
		}
	}

	protected override void OnDestroy()
	{
		ThornsWorldSession.Reset();
		ThornsLocalHostSpawnCoordinator.ResetState();
	}

	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying || !Networking.IsHost || !Networking.IsActive )
			return;

		if ( _lobbyDiscoveryRefresh > 0f )
			return;

		_lobbyDiscoveryRefresh = 30f;
		RefreshLobbyDiscoveryData();
	}

	void RefreshLobbyDiscoveryData()
	{
		Networking.SetData( "game", Game.Ident ?? "" );
		Networking.SetData( "map", "thorns_terrain" );
	}

	public void OnActive( Connection channel )
	{
		if ( !Networking.IsHost || channel is null )
			return;

		EnsurePersistenceComponent()?.HostEnsureInitialized();

		if ( !IsTerrainReadyForSpawn() )
		{
			if ( !_pendingActiveConnections.Any( c => c.Id == channel.Id ) )
				_pendingActiveConnections.Add( channel );
			return;
		}

		SpawnPlayerForConnection( channel );
	}

	public void OnDisconnected( Connection channel )
	{
		if ( !Networking.IsHost || channel is null )
			return;

		_pendingActiveConnections.RemoveAll( c => c?.Id == channel.Id );

		// Capture position/progress while the pawn still exists — destroying first drops the final save.
		ThornsWorldPersistence.Instance?.HostOnPlayerDisconnected( channel );

		var session = FindExistingSession( channel );
		if ( session.IsValid() )
			session.GameObject.Destroy();
	}

	void AnnouncePlayerJoined( Connection channel )
	{
		if ( channel is null || channel == Connection.Local )
			return;

		var name = string.IsNullOrWhiteSpace( channel.DisplayName ) ? "Player" : channel.DisplayName.Trim();
		RpcAnnouncePlayerJoined( name );
	}

	[Rpc.Broadcast]
	void RpcAnnouncePlayerJoined( string displayName )
	{
		ThornsJoinAnnouncementBus.PushPlayerJoined( displayName );
	}

	/// <summary>Called when terrain sculpt finishes so early joiners spawn on solid ground.</summary>
	public void FlushPendingPlayerSpawns()
	{
		if ( !Networking.IsHost || _pendingActiveConnections.Count == 0 )
			return;

		var pending = _pendingActiveConnections.ToArray();
		_pendingActiveConnections.Clear();

		foreach ( var channel in pending )
		{
			if ( channel is null )
				continue;

			SpawnPlayerForConnection( channel );
		}
	}

	bool IsTerrainReadyForSpawn()
	{
		var bootstrap = Scene.GetAllComponents<ThornsTerrainBootstrap>().FirstOrDefault();
		return bootstrap is null || bootstrap.IsWorldApplied;
	}

	void PreparePersistenceSavePath( bool joiningRemote )
	{
		if ( joiningRemote || (Networking.IsActive && !Networking.IsHost) )
			return;

		var serverName = DefaultServerName;
		_hasMenuHostOptions = ThornsSessionBootstrap.TakeRequestedHostFromLocalSave( out _menuHostOptions );
		if ( _hasMenuHostOptions )
			ThornsWorldPersistence.SetPendingRelativeSavePath( _menuHostOptions.PersistenceRelativePath );
		else
			ThornsWorldPersistence.SetPendingRelativeSavePath( ThornsHostSavePaths.PersistencePathForServerName( serverName ) );
	}

	void TryCreateLobby()
	{
		if ( !CreateLobbyOnLoad || _lobbyCreateAttempted || Networking.IsActive )
			return;

		_lobbyCreateAttempted = true;

		var serverName = DefaultServerName;
		var requirePassword = RequireJoinPassword;
		var password = JoinPassword ?? "";
		if ( _hasMenuHostOptions )
		{
			serverName = _menuHostOptions.ServerDisplayName;
			requirePassword = _menuHostOptions.RequireJoinPassword;
			password = _menuHostOptions.JoinPassword;
		}

		Networking.CreateLobby( new LobbyConfig
		{
			Name = serverName,
			Privacy = LobbyPrivacy.Public
		} );

		Networking.SetData( "game", Game.Ident ?? "" );
		Networking.SetData( "map", "thorns_terrain" );
		if ( requirePassword && !string.IsNullOrWhiteSpace( password ) )
		{
			Networking.SetData( ThornsLobbyPasswordGate.DataKeyGate, "pwd" );
			Networking.SetData( ThornsLobbyPasswordGate.DataKeyHash, ThornsLobbyPasswordGate.ComputePasswordHashForLobby( password ) );
		}
		else
		{
			Networking.SetData( ThornsLobbyPasswordGate.DataKeyGate, "open" );
			Networking.SetData( ThornsLobbyPasswordGate.DataKeyHash, "" );
		}

		ThornsHostMenuPreferences.SaveLastHostedServerName( serverName );
		Log.Info( $"[Thorns Terrain] Lobby created: '{serverName}' save='{ThornsWorldPersistence.Instance?.RelativeSavePath ?? ThornsWorldPersistence.DefaultRelativePath}'." );
		Log.Info( $"[Thorns Terrain] Server is joinable (public lobby). Friends can find '{serverName}' in the server browser while you are in-world." );

		var bootstrap = Scene.GetAllComponents<ThornsTerrainBootstrap>().FirstOrDefault();
		if ( bootstrap?.Config is not null )
		{
			ThornsWorldSession.PublishFromHost( bootstrap.Config );
			var biome = ThornsLobbyMetadata.InferBiomeFromName( serverName );
			var official = serverName.Contains( "[Official]", StringComparison.OrdinalIgnoreCase )
			               || serverName.Contains( "(Official)", StringComparison.OrdinalIgnoreCase );
			ThornsLobbyMetadata.PublishHostMetadata( "", biome, official );
		}
	}

	void SpawnPlayerForConnection( Connection channel )
	{
		var existing = FindExistingSession( channel );
		if ( existing is not null && existing.IsValid() )
			return;

		var prefab = ResolvePlayerPrefab();
		if ( !prefab.IsValid() )
		{
			Log.Error( "[Thorns Terrain] Could not spawn network player: missing player prefab." );
			return;
		}

		var spawn = ResolveSpawnForConnection( channel );
		var player = prefab.Clone( new Transform( spawn.Position, spawn.Rotation ), name: $"Terrain Explorer ({channel.DisplayName})" );
		player.NetworkMode = NetworkMode.Object;
		_ = player.Components.Get<ThornsPlayerSession>() ?? player.Components.Create<ThornsPlayerSession>();
		ConfigurePlayerController( player, channel );
		ThornsPlayerPresentationBootstrap.EnsureFirstPersonPresentation( player );
		ThornsNetworkReplication.SetSubtreeNetworkModeObject( player );

		player.NetworkSpawn( new NetworkSpawnOptions
		{
			Owner = channel,
			OrphanedMode = NetworkOrphaned.Destroy
		} );
		Log.Info( $"[Thorns Terrain] Spawned network explorer for '{channel.DisplayName}' at {spawn.Position}." );

		var shadowStats = ThornsWorldShadowUtil.RepairSceneWorldShadows( Scene );
		if ( shadowStats.Enabled > 0 )
			Log.Info( $"[Thorns Shadows] Repaired {shadowStats.Enabled} renderer(s) after player network spawn." );

		var local = Connection.Local;
		if ( local is not null && channel.Id == local.Id )
		{
			ThornsPawnInputIsolation.ApplyForLocalPawn( Scene, player );
			ThornsLocalHostSpawnCoordinator.Queue( Scene, player );
		}

		if ( Networking.IsHost )
			AnnouncePlayerJoined( channel );
	}

	GameObject ResolvePlayerPrefab()
	{
		if ( PlayerPrefab.IsValid() )
			return PlayerPrefab;

		return GameObject.GetPrefab( PlayerPrefabPath );
	}

	ThornsPlayerSession FindExistingSession( Connection channel )
	{
		foreach ( var session in Scene.GetAllComponents<ThornsPlayerSession>() )
		{
			if ( session.IsValid() && session.OwnerConnection?.Id == channel.Id )
				return session;
		}

		return null;
	}

	Transform ResolveSpawnForConnection( Connection channel )
	{
		if ( ThornsWorldPersistence.Instance is not null
		     && ThornsWorldPersistence.Instance.TryGetPlayer( channel, out var dto ) )
		{
			var saved = new Vector3( dto.Px, dto.Py, dto.Pz );
			if ( saved.LengthSquared > 64f )
			{
				var rotation = new Angles( dto.RPitch, dto.RYaw, dto.RRoll ).ToRotation();

				if ( ThornsPlayerSpawnLocations.TryResolveSavedReturnSpawn( Scene, saved, SpawnHeightOffset, out var returnSaved ) )
				{
					Log.Info( $"[Thorns Terrain] Spawning '{channel.DisplayName}' at saved return {returnSaved:F0}." );
					return new Transform( returnSaved, rotation );
				}

				if ( ThornsPlayerSpawnLocations.TryResolveSavedReturnNear( Scene, saved, SpawnHeightOffset, out var nearSaved ) )
				{
					Log.Info( $"[Thorns Terrain] Spawning '{channel.DisplayName}' near saved return {nearSaved:F0} (saved {saved:F0})." );
					return new Transform( nearSaved, rotation );
				}

				var snapped = SnapToTerrain( saved );
				Log.Warning( $"[Thorns Terrain] Saved spawn for '{channel.DisplayName}' at {saved:F0} needed terrain snap — using {snapped:F0}." );
				return new Transform( snapped, rotation );
			}
		}

		var coastalSeed = ResolveCoastalSpawnSeed( channel );
		if ( ThornsPlayerSpawnLocations.TryPickRandom( Scene, out var coastalSpawn, deterministicSeed: coastalSeed ) )
		{
			Log.Info( $"[Thorns Terrain] Spawning '{channel.DisplayName}' at coastal fallback {coastalSpawn:F0}." );
			return new Transform( coastalSpawn, Rotation.Identity );
		}

		return new Transform( SnapToTerrain( FallbackSpawnPoint ), Rotation.Identity );
	}

	int ResolveCoastalSpawnSeed( Connection channel )
	{
		var worldSeed = Scene.GetAllComponents<ThornsTerrainBootstrap>().FirstOrDefault()?.Config?.WorldSeed ?? 42069;
		var accountKey = ThornsPersistenceIdentity.GetStableAccountKey( channel );
		if ( string.IsNullOrWhiteSpace( accountKey ) )
			accountKey = channel?.DisplayName ?? "guest";

		return HashCode.Combine( worldSeed, StringComparer.Ordinal.GetHashCode( accountKey ) );
	}

	Vector3 SnapToTerrain( Vector3 requested )
	{
		var terrain = ThornsTerrainCache.Resolve( Scene );
		if ( !terrain.IsValid() )
			return requested + Vector3.Up * SpawnHeightOffset;

		var maxHeight = terrain.TerrainHeight;
		var clamped = ThornsTerrainSurface.ClampToTerrainBounds( terrain, requested );
		var x = clamped.x;
		var y = clamped.y;
		var rayStart = new Vector3( x, y, terrain.GameObject.WorldPosition.z + maxHeight * 2f );
		if ( terrain.RayIntersects( new Ray( rayStart, Vector3.Down ), maxHeight * 4f, out var localHit ) )
			return terrain.GameObject.WorldTransform.PointToWorld( localHit ) + Vector3.Up * SpawnHeightOffset;

		return new Vector3( x, y, requested.z + SpawnHeightOffset );
	}

	void ConfigurePlayerController( GameObject player, Connection channel )
	{
		var controller = player.Components.Get<PlayerController>( FindMode.EverythingInSelf );
		if ( !controller.IsValid() )
			return;

		var locomotion = player.Components.Get<Terraingen.Player.ThornsPlayerLocomotion>()
		                 ?? player.Components.Create<Terraingen.Player.ThornsPlayerLocomotion>();
		locomotion.ConfigurePlayerController();

		var local = Connection.Local;
		var allowInput = !Networking.IsActive || ( local is not null && channel.Id == local.Id );
		controller.UseInputControls = allowInput;
		controller.UseCameraControls = allowInput;
		controller.UseLookControls = allowInput;
		ThornsPlayerMovementDefaults.Apply( controller, sprintSpeedMultiplier: SprintSpeedMultiplier );

		EnsurePlayerHealth( player );
	}

	static void EnsurePlayerHealth( GameObject player )
	{
		_ = player.Components.Get<ThornsPlayerHealth>() ?? player.Components.Create<ThornsPlayerHealth>();
		_ = player.Components.Get<ThornsPlayerDamageReceiver>() ?? player.Components.Create<ThornsPlayerDamageReceiver>();
		_ = player.Components.Get<ThornsPlayerAnimalHitscan>() ?? player.Components.Create<ThornsPlayerAnimalHitscan>();
		_ = player.Components.Get<ThornsPlayerTreeChopUse>() ?? player.Components.Create<ThornsPlayerTreeChopUse>();
		_ = player.Components.Get<ThornsPlayerResourceSalvageUse>() ?? player.Components.Create<ThornsPlayerResourceSalvageUse>();
		_ = player.Components.Get<ThornsPlayerAnimalTaming>() ?? player.Components.Create<ThornsPlayerAnimalTaming>();
		_ = player.Components.Get<ThornsPlayerSurvivalUse>() ?? player.Components.Create<ThornsPlayerSurvivalUse>();
		_ = player.Components.Get<ThornsPlayerWaterDrinkUse>() ?? player.Components.Create<ThornsPlayerWaterDrinkUse>();
		_ = player.Components.Get<ThornsPlayerNpcGuildCoreUse>() ?? player.Components.Create<ThornsPlayerNpcGuildCoreUse>();
		_ = player.Components.Get<ThornsPlayerHotbarConsumeUse>() ?? player.Components.Create<ThornsPlayerHotbarConsumeUse>();
		_ = player.Components.Get<ThornsPlayerWaterProximityAudio>() ?? player.Components.Create<ThornsPlayerWaterProximityAudio>();
		_ = player.Components.Get<ThornsPlayerRadioShopUse>() ?? player.Components.Create<ThornsPlayerRadioShopUse>();
		_ = player.Components.Get<ThornsPlayerContainerUse>() ?? player.Components.Create<ThornsPlayerContainerUse>();
		_ = player.Components.Get<ThornsPlayerDoorUse>() ?? player.Components.Create<ThornsPlayerDoorUse>();
		_ = player.Components.Get<ThornsPlayerCraftStationUse>() ?? player.Components.Create<ThornsPlayerCraftStationUse>();
		_ = player.Components.Get<ThornsPlayerResearchStationUse>() ?? player.Components.Create<ThornsPlayerResearchStationUse>();
		_ = player.Components.Get<ThornsPlayerCampfireUse>() ?? player.Components.Create<ThornsPlayerCampfireUse>();
		_ = player.Components.Get<ThornsPlayerWorkbenchUse>() ?? player.Components.Create<ThornsPlayerWorkbenchUse>();
		_ = player.Components.Get<ThornsPlayerAirdropUse>() ?? player.Components.Create<ThornsPlayerAirdropUse>();
		_ = player.Components.Get<ThornsPlayerDeathCrateUse>() ?? player.Components.Create<ThornsPlayerDeathCrateUse>();
		_ = player.Components.Get<ThornsPlayerMountController>() ?? player.Components.Create<ThornsPlayerMountController>();
		_ = player.Components.Get<ThornsPlayerMountUse>() ?? player.Components.Create<ThornsPlayerMountUse>();
		_ = player.Components.Get<ThornsPlayerUseGrabStanceDriver>() ?? player.Components.Create<ThornsPlayerUseGrabStanceDriver>();
		_ = player.Components.Get<Terraingen.Player.ThornsPlayerGameplay>() ?? player.Components.Create<Terraingen.Player.ThornsPlayerGameplay>();
		_ = player.Components.Get<Terraingen.Player.ThornsTerrainWaterMoveMode>() ?? player.Components.Create<Terraingen.Player.ThornsTerrainWaterMoveMode>();
	}

	ThornsWorldPersistence EnsurePersistenceComponent()
	{
		var p = Components.Get<ThornsWorldPersistence>( FindMode.EnabledInSelf );
		if ( p.IsValid() )
			return p;

		return Components.Create<ThornsWorldPersistence>();
	}

	public ThornsWorldHeightCacheRpc EnsureHeightCacheRpc( ThornsTerrainBootstrap bootstrap )
	{
		var rpc = Components.Get<ThornsWorldHeightCacheRpc>( FindMode.EnabledInSelf );
		if ( !rpc.IsValid() )
			rpc = Components.Create<ThornsWorldHeightCacheRpc>();

		rpc.Bind( bootstrap );
		return rpc;
	}
}
