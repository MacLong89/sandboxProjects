using System;
using System.Buffers;
using Sandbox.Network;
using Terraingen.TerrainGen;

namespace Sandbox;

/// <summary>
/// Server-side multiplayer root for Thorns. Spawns one networked player hierarchy per <see cref="Connection"/>.
/// Outdoor lighting uses <see cref="ThornsCelestialSystem"/> only (see <see cref="EnsureCelestialSystemInScene"/>).
/// The <see cref="GameObject.NetworkSpawn(Connection)"/> target must be the same object that <see cref="ThornsPawnMovement"/> moves,
/// or remote clients will not receive transform updates (child-of-root motion is not replicated the same way).
/// </summary>
[Title( "Thorns — Game Manager" )]
[Category( "Thorns" )]
[Icon( "hub" )]
public sealed class ThornsGameManager : Component, Component.INetworkListener
{
	[Property] public GameObject PlayerPrefab { get; set; }

	[Property] public List<GameObject> SpawnPoints { get; set; }

	[Property] public bool CreateLobbyOnLoad { get; set; } = true;

	[Property]
	public List<string> WeaponContentPackageIdents { get; set; } = new() { "facepunch.sboxweapons" };

	/// <summary>Players with <see cref="ThornsPawn"/> instantly die when world Z is below this (Z-up; ~100 under typical terrain base).</summary>
	[Property] public float PlayerVoidKillPlaneWorldZ { get; set; } = -100f;

	/// <summary>When there are no spawn points, respawn at <see cref="GetMapCenterRespawnTransform"/> instead of the death location.</summary>
	[Property] public bool RespawnAtMapCenterWhenNoSpawnPoints { get; set; } = true;

	/// <summary>Scene files often omit <see cref="WeaponContentPackageIdents"/>, which deserializes to an empty list and prevents joiners from mounting <c>facepunch.sboxweapons</c>.</summary>
	const string SboxWeaponsPackageIdent = "facepunch.sboxweapons";

	void EnsureWeaponContentPackageIdentsList()
	{
		if ( WeaponContentPackageIdents is null )
			WeaponContentPackageIdents = new List<string>();

		for ( var i = WeaponContentPackageIdents.Count - 1; i >= 0; i-- )
		{
			if ( string.IsNullOrWhiteSpace( WeaponContentPackageIdents[i] ) )
				WeaponContentPackageIdents.RemoveAt( i );
		}

		var hasSboxWeapons = false;
		foreach ( var s in WeaponContentPackageIdents )
		{
			if ( string.Equals( s, SboxWeaponsPackageIdent, StringComparison.OrdinalIgnoreCase ) )
			{
				hasSboxWeapons = true;
				break;
			}
		}

		if ( !hasSboxWeapons )
			WeaponContentPackageIdents.Add( SboxWeaponsPackageIdent );
	}

	protected override async Task OnLoad()
	{
		// Editor scene load (not playing): skip. PIE / play-from-menu: Game.IsPlaying — run lobby + mounts even if Scene.IsEditor stays true.
		if ( Scene.IsEditor && !Game.IsPlaying )
			return;

		EnsureWeaponContentPackageIdentsList();
		await ThornsWeaponContentBootstrap.MountOptionalPackagesAsync( WeaponContentPackageIdents );

		if ( CreateLobbyOnLoad && !Networking.IsActive )
		{
			ThornsLoadingScreenHero.Show( "Thorns — Creating lobby" );
			await Task.DelayRealtimeSeconds( 0.1f );
			ThornsWorldPersistence.ClearPendingRelativeSavePath();
			var hostFromLocalSave = ThornsSessionBootstrap.TakeRequestedHostFromLocalSave( out var hostLobbyOpts );
			if ( hostFromLocalSave )
			{
				ThornsWorldPersistence.SetPendingRelativeSavePath( hostLobbyOpts.PersistenceRelativePath );
				Networking.CreateLobby( new LobbyConfig
				{
					Name = hostLobbyOpts.ServerDisplayName,
					Privacy = LobbyPrivacy.Public,
					MaxPlayers = 64,
					Hidden = false
				} );
				TryTagLobbyForSteamDiscovery();
				Networking.SetData( "thorns", "local_save" );
				if ( hostLobbyOpts.RequireJoinPassword && !string.IsNullOrWhiteSpace( hostLobbyOpts.JoinPassword ) )
				{
					Networking.SetData( ThornsLobbyPasswordGate.DataKeyGate, "pwd" );
					Networking.SetData(
						ThornsLobbyPasswordGate.DataKeyHash,
						ThornsLobbyPasswordGate.ComputePasswordHashForLobby( hostLobbyOpts.JoinPassword ) );
					Log.Info( "[Thorns] Listen server: host session (password-gated join); persistence on host disk." );
				}
				else
				{
					Networking.SetData( ThornsLobbyPasswordGate.DataKeyGate, "open" );
					Networking.SetData( ThornsLobbyPasswordGate.DataKeyHash, "" );
					Log.Info( "[Thorns] Listen server: host session (open join); persistence on host disk." );
				}
			}
			else
			{
				Networking.CreateLobby( new() );
				TryTagLobbyForSteamDiscovery();
			}
		}
	}

	/// <summary>
	/// Steam lobby list queries filter on metadata key <c>game</c>. Match what <see cref="Game.Ident"/> resolves to so browser + menu see this session.
	/// </summary>
	static void TryTagLobbyForSteamDiscovery()
	{
		try
		{
			var ident = Game.Ident;
			if ( string.IsNullOrEmpty( ident ) )
				return;
			Networking.SetData( "game", ident );
			Log.Info( $"[Thorns] Lobby tagged for discovery (game={ident})." );
		}
		catch ( Exception e )
		{
			Log.Warning( e, "[Thorns] Could not SetData game ident on lobby." );
		}
	}

	protected override void OnStart()
	{
		if ( !Game.IsPlaying )
			return;

		EnsureThornsPopulationDirectorOnSelf();
		EnsureThornsPoiSceneSettingsOnSelf();
		EnsureThornsDynamicSupplyDirectorOnSelf();
		EnsureThornsBanditDirectorOnSelf();
		EnsureThornsWorldAmbienceOnSelf();
		EnsureThornsGameplayEnterAudioFadeOnSelf();
		EnsureThornsWorldBootGateOnSelf();
		EnsureCelestialSystemInScene();
		EnsureThornsWorldPersistenceOnSelf();
		EnsureWeaponContentPackageIdentsList();
		_ = RemountWeaponPackagesOnStartAsync();
	}

	/// <summary>Joining clients can miss a narrow mount window; re-run once the component is live.</summary>
	async Task RemountWeaponPackagesOnStartAsync()
	{
		await Task.DelayRealtimeSeconds( 0.02f );
		if ( !this.IsValid() || !Game.IsPlaying )
			return;
		EnsureWeaponContentPackageIdentsList();
		var needsMount = false;
		foreach ( var id in WeaponContentPackageIdents )
		{
			if ( !ThornsWeaponContentBootstrap.IsPackageMounted( id ) )
			{
				needsMount = true;
				break;
			}
		}

		if ( needsMount )
			await ThornsWeaponContentBootstrap.MountOptionalPackagesAsync( WeaponContentPackageIdents );
	}

	/// <summary>Host-only: client finished loading and is active in session.</summary>
	public void OnActive( Connection channel )
	{
		if ( !Networking.IsHost )
			return;

		if ( channel is null )
		{
			Log.Warning( "[Thorns] OnActive: null connection — skipping player spawn." );
			return;
		}

		try
		{
			OnActiveHostSpawnPlayer( channel );
		}
		catch ( Exception e )
		{
			Log.Error( e, $"[Thorns] OnActive failed for '{channel.DisplayName}' (id={channel.Id}). Player spawn aborted." );
		}
	}

	void OnActiveHostSpawnPlayer( Connection channel )
	{
		var sw = ThornsReplicationDiagnostics.StartTiming();
		Log.Info( $"[Thorns] Player connected: '{channel.DisplayName}' (id={channel.Id})" );

		if ( HostTryFindExistingPlayerRoot( channel, out var existingRoot ) )
		{
			Log.Warning(
				$"[Thorns] OnActive: pawn already exists for '{channel.DisplayName}' (id={channel.Id}) on '{existingRoot.Name}' — skipping duplicate spawn." );
			return;
		}

		EnsureThornsPoiSceneSettingsOnSelf();
		EnsureThornsWorldPersistenceOnSelf();
		var persistence = ThornsWorldPersistence.Instance;
		persistence?.HostEnsureInitializedBeforePlayerSpawn();
		ThornsWorldPersistence.HostRemapStructureOwnersForConnection( channel );
		var restoreSpawnProfile = persistence is not null && persistence.HostBeginSpawnRestore( channel );

		ThornsPoiAuthority.SpawnHostSingleton();

		GameObject root;

		if ( PlayerPrefab.IsValid() )
		{
			var start = FindSpawnLocation().WithScale( 1 );
			root = PlayerPrefab.Clone( start, name: $"Player - {channel.DisplayName}" );
		}
		else
		{
			root = ThornsPlayerSpawnBootstrap.BuildDefaultPlayerHierarchy( GameObject, channel.DisplayName );
			root.WorldTransform = FindSpawnLocation().WithScale( 1 );
		}

		if ( !root.IsValid() )
		{
			Log.Error( $"[Thorns] OnActive: failed to create player root for '{channel.DisplayName}'." );
			return;
		}

		if ( Connection.Local is not null && channel.Id == Connection.Local.Id )
			ThornsWorldBootGate.BeginLocalBoot();

		ThornsPlayerSpawnBootstrap.EnsureGameplayComponents( root );
		// Children default to Snapshot ("scene snapshot"); dynamic spawn + late join often skips those unless Object mode.
		ThornsNetworkReplication.SetSubtreeNetworkModeObject( root );

		var session = root.Components.GetInDescendantsOrSelf<ThornsPlayer>( true );
		if ( !session.IsValid() )
		{
			Log.Error( "[Thorns] Player root must include ThornsPlayer. Destroying spawn." );
			root.Destroy();
			return;
		}

		root.NetworkSpawn( channel );

		_ = DeferJoinServerChatLineAsync( channel, joined: true );

		if ( restoreSpawnProfile && persistence is not null )
		{
			try
			{
				persistence.HostApplySpawnRestoreProfile( channel, root );
			}
			catch ( Exception e )
			{
				Log.Warning( e, $"[Thorns] OnActive: spawn restore failed for '{channel.DisplayName}' — continuing with fresh loadout." );
			}
		}

		var elapsed = sw.Elapsed.TotalMilliseconds;
		ThornsReplicationDiagnostics.LogJoinSpawnTiming(
			channel.DisplayName,
			channel.Id,
			restoreSpawnProfile,
			elapsed,
			ThornsPlacedStructure.ActiveByInstanceId.Count,
			ThornsPopulationDirector.HostWildlifeGlobalCount );

		Log.Info( $"[Thorns] Pawn hierarchy network-spawned for '{channel.DisplayName}', session={session.GameObject.Name}, owner id={channel.Id}" );
	}

	static bool HostTryFindExistingPlayerRoot( Connection channel, out GameObject root )
	{
		root = default;
		if ( channel is null )
			return false;

		if ( ThornsPawnConnectionIndex.TryGetPawnGameObject( channel, out root ) )
			return true;

		var scene = Game.ActiveScene;
		if ( scene is null || !scene.IsValid() )
			return false;

		foreach ( var player in scene.GetAllComponents<ThornsPlayer>() )
		{
			if ( player is null || !player.IsValid() )
				continue;

			var owner = player.OwnerConnection;
			if ( owner is not null && owner.Id == channel.Id )
			{
				root = player.GameObject;
				return root.IsValid();
			}

			if ( player.GameObject.Network.OwnerId == channel.Id )
			{
				root = player.GameObject;
				return root.IsValid();
			}
		}

		return false;
	}

	async Task DeferJoinServerChatLineAsync( Connection channel, bool joined )
	{
		await Task.DelayRealtimeSeconds( 0.08f );
		if ( !this.IsValid() || !Networking.IsHost || channel is null || !Game.IsPlaying )
			return;

		try
		{
			var line = joined
				? $"{channel.DisplayName} joined the server."
				: $"{channel.DisplayName} left the server.";
			ThornsGameShell.HostNotifyAllPlayersServerChatLine( Scene, line, true );
		}
		catch ( Exception e )
		{
			Log.Warning( e, "[Thorns] Server chat join/leave notification failed (non-fatal)." );
		}
	}

	/// <summary>Host-only: persist disconnecting player's inventory (and world) immediately.</summary>
	public void OnDisconnected( Connection channel )
	{
		if ( !Networking.IsHost )
			return;

		Log.Info( $"[Thorns] Player disconnected: '{channel?.DisplayName}' id={channel?.Id}" );

		if ( Networking.IsHost && channel is not null && !string.IsNullOrWhiteSpace( channel.DisplayName ) )
			_ = DeferJoinServerChatLineAsync( channel, joined: false );

		ThornsWorldPersistence.Instance?.HostOnPlayerDisconnected( channel );
	}

	// Prefab / scene hooks — forward to ThornsPlayerSpawnBootstrap.
	public static void EnsurePawnWorldModel( GameObject root ) => ThornsPlayerSpawnBootstrap.EnsurePawnWorldModel( root );
	public static void EnsureDefaultWeaponWorld( GameObject root ) => ThornsPlayerSpawnBootstrap.EnsureDefaultWeaponWorld( root );
	public static void EnsureThornsHealth( GameObject root ) => ThornsPlayerSpawnBootstrap.EnsureThornsHealth( root );
	public static void EnsureThornsPlayerUpgrades( GameObject root ) => ThornsPlayerSpawnBootstrap.EnsureThornsPlayerUpgrades( root );
	public static void EnsureThornsVitals( GameObject root ) => ThornsPlayerSpawnBootstrap.EnsureThornsVitals( root );
	public static void EnsureThornsInventory( GameObject root ) => ThornsPlayerSpawnBootstrap.EnsureThornsInventory( root );
	public static void EnsureThornsWallet( GameObject root ) => ThornsPlayerSpawnBootstrap.EnsureThornsWallet( root );
	public static void EnsureThornsRadioShopInteractor( GameObject root ) => ThornsPlayerSpawnBootstrap.EnsureThornsRadioShopInteractor( root );
	public static void EnsureThornsPlayerMilestones( GameObject root ) => ThornsPlayerSpawnBootstrap.EnsureThornsPlayerMilestones( root );
	public static void EnsureThornsHotbarEquipment( GameObject root ) => ThornsPlayerSpawnBootstrap.EnsureThornsHotbarEquipment( root );
	public static void EnsureThornsArmorEquipment( GameObject root ) => ThornsPlayerSpawnBootstrap.EnsureThornsArmorEquipment( root );
	public static void EnsureThornsArmorDevControls( GameObject root ) => ThornsPlayerSpawnBootstrap.EnsureThornsArmorDevControls( root );
	public static void EnsureThornsCharacterProgression( GameObject root ) => ThornsPlayerSpawnBootstrap.EnsureThornsCharacterProgression( root );
	public static void EnsureThornsDeathCrateInteractor( GameObject root ) => ThornsPlayerSpawnBootstrap.EnsureThornsDeathCrateInteractor( root );
	public static void EnsureThornsDebugUiBridge( GameObject root ) => ThornsPlayerSpawnBootstrap.EnsureThornsDebugUiBridge( root );
	public static void EnsureThornsDebugHudHost( GameObject root ) => ThornsPlayerSpawnBootstrap.EnsureThornsDebugHudHost( root );
	public static void EnsureThornsCollisionDebugDriver( GameObject root ) => ThornsPlayerSpawnBootstrap.EnsureThornsCollisionDebugDriver( root );
	public static void EnsureThornsGameShell( GameObject root ) => ThornsPlayerSpawnBootstrap.EnsureThornsGameShell( root );
	public static void EnsureThornsHotTipDirector( GameObject root ) => ThornsPlayerSpawnBootstrap.EnsureThornsHotTipDirector( root );
	public static void EnsureThornsProximityInteractionHints( GameObject root ) => ThornsPlayerSpawnBootstrap.EnsureThornsProximityInteractionHints( root );
	public static void EnsureThornsMinimapHud( GameObject root ) => ThornsPlayerSpawnBootstrap.EnsureThornsMinimapHud( root );
	public static void EnsureThornsWaterProximityAudio( GameObject root ) => ThornsPlayerSpawnBootstrap.EnsureThornsWaterProximityAudio( root );
	public static void EnsureThornsAtmosphericMusic( GameObject root ) => ThornsPlayerSpawnBootstrap.EnsureThornsAtmosphericMusic( root );
	public static void EnsureThornsOpenWaterDrinkInteractor( GameObject root ) => ThornsPlayerSpawnBootstrap.EnsureThornsOpenWaterDrinkInteractor( root );

	void EnsureThornsPoiSceneSettingsOnSelf()
	{
		if ( !GameObject.Components.Get<ThornsPoiSceneSettings>( FindMode.EnabledInSelf ).IsValid() )
			_ = GameObject.Components.Create<ThornsPoiSceneSettings>();
	}

	void EnsureThornsDynamicSupplyDirectorOnSelf()
	{
		if ( !GameObject.Components.Get<ThornsDynamicSupplyDirector>( FindMode.EnabledInSelf ).IsValid() )
			_ = GameObject.Components.Create<ThornsDynamicSupplyDirector>();
	}

	void EnsureThornsPopulationDirectorOnSelf()
	{
		if ( !GameObject.Components.Get<ThornsPopulationDirector>( FindMode.EnabledInSelf ).IsValid() )
			_ = GameObject.Components.Create<ThornsPopulationDirector>();
	}

	void EnsureThornsBanditDirectorOnSelf()
	{
		if ( !GameObject.Components.Get<ThornsBanditDirector>( FindMode.EnabledInSelf ).IsValid() )
			_ = GameObject.Components.Create<ThornsBanditDirector>();
	}

	/// <summary>Ensures <see cref="ThornsPopulationDirector"/> exists (host population facade).</summary>
	public static void EnsureThornsPopulationDirectorForScene( Scene scene )
	{
		if ( scene is null || !scene.IsValid() )
			return;

		foreach ( var existing in scene.GetAllComponents<ThornsPopulationDirector>() )
		{
			if ( existing.IsValid() )
				return;
		}

		foreach ( var gm in scene.GetAllComponents<ThornsGameManager>() )
		{
			if ( !gm.IsValid() )
				continue;
			if ( !gm.GameObject.Components.Get<ThornsPopulationDirector>( FindMode.EnabledInSelf ).IsValid() )
				_ = gm.GameObject.Components.Create<ThornsPopulationDirector>();
			return;
		}

		var go = new GameObject( true, "ThornsPopulationDirectorAuto" );
		_ = go.Components.Create<ThornsPopulationDirector>();
	}

	/// <summary>Ensures <see cref="ThornsBanditDirector"/> exists for airdrop guards / bandit NPCs (host spawns may run before <see cref="ThornsGameManager"/>'s OnStart).</summary>
	public static void EnsureThornsBanditDirectorForScene( Scene scene )
	{
		if ( scene is null || !scene.IsValid() )
			return;

		foreach ( var existing in scene.GetAllComponents<ThornsBanditDirector>() )
		{
			if ( existing.IsValid() )
				return;
		}

		foreach ( var gm in scene.GetAllComponents<ThornsGameManager>() )
		{
			if ( !gm.IsValid() )
				continue;
			if ( !gm.GameObject.Components.Get<ThornsBanditDirector>( FindMode.EnabledInSelf ).IsValid() )
				_ = gm.GameObject.Components.Create<ThornsBanditDirector>();
			return;
		}

		var go = new GameObject( true, "ThornsBanditDirectorAuto" );
		_ = go.Components.Create<ThornsBanditDirector>();
	}

	void EnsureThornsWorldAmbienceOnSelf()
	{
		var amb = GameObject.Components.Get<ThornsWorldAmbience>( FindMode.EnabledInSelf );
		if ( !amb.IsValid() )
			amb = GameObject.Components.Create<ThornsWorldAmbience>();

		if ( amb.IsValid() )
			amb.RuntimeVolumeMultiplier = 0f;
	}

	void EnsureThornsGameplayEnterAudioFadeOnSelf()
	{
		if ( !GameObject.Components.Get<ThornsGameplayEnterAudioFade>( FindMode.EnabledInSelf ).IsValid() )
			_ = GameObject.Components.Create<ThornsGameplayEnterAudioFade>();
	}

	void EnsureThornsWorldBootGateOnSelf()
	{
		if ( !GameObject.Components.Get<ThornsWorldBootGate>( FindMode.EnabledInSelf ).IsValid() )
			_ = GameObject.Components.Create<ThornsWorldBootGate>();
	}

	void EnsureCelestialSystemInScene()
	{
		var scene = GameObject.Scene;
		if ( scene is null || !scene.IsValid() )
			return;

		ThornsCelestialSystem.EnsureInScene( scene );
	}

	void EnsureThornsWorldPersistenceOnSelf()
	{
		if ( !Networking.IsHost )
			return;

		if ( !GameObject.Components.Get<ThornsWorldPersistence>( FindMode.EnabledInSelf ).IsValid() )
			_ = GameObject.Components.Create<ThornsWorldPersistence>();
	}

	public static void EnsureThornsHarvestInteractor( GameObject root ) => ThornsPlayerSpawnBootstrap.EnsureThornsHarvestInteractor( root );
	public static void EnsureThornsGuildRoster( GameObject root ) => ThornsPlayerSpawnBootstrap.EnsureThornsGuildRoster( root );
	public static void EnsureThornsWildlifeMountInteractor( GameObject root ) => ThornsPlayerSpawnBootstrap.EnsureThornsWildlifeMountInteractor( root );
	public static void EnsureThornsWildlifeTameInteractor( GameObject root ) => ThornsPlayerSpawnBootstrap.EnsureThornsWildlifeTameInteractor( root );
	public static void EnsureThornsBuildingController( GameObject root ) => ThornsPlayerSpawnBootstrap.EnsureThornsBuildingController( root );
	public static void EnsureThornsConsumableUseInput( GameObject root ) => ThornsPlayerSpawnBootstrap.EnsureThornsConsumableUseInput( root );

	/// <summary>World Z at or below which <see cref="ThornsHealth"/> applies lethal <c>void</c> damage (reads first scene <see cref="ThornsGameManager"/>).</summary>
	public static float ResolveVoidKillPlaneWorldZ( Scene scene )
	{
		if ( scene is null || !scene.IsValid() )
			return -100f;

		if ( _cachedVoidKillPlaneScene == scene && _cachedVoidKillPlaneSceneValid )
			return _cachedVoidKillPlaneWorldZ;

		var z = -100f;
		foreach ( var gm in scene.GetAllComponents<ThornsGameManager>() )
		{
			if ( gm.IsValid() )
			{
				z = gm.PlayerVoidKillPlaneWorldZ;
				break;
			}
		}

		_cachedVoidKillPlaneScene = scene;
		_cachedVoidKillPlaneWorldZ = z;
		_cachedVoidKillPlaneSceneValid = true;
		return z;
	}

	static Scene _cachedVoidKillPlaneScene;
	static float _cachedVoidKillPlaneWorldZ = -100f;
	static bool _cachedVoidKillPlaneSceneValid;

	static float ResolveTerrainPeakWorldZRough( Scene scene )
	{
		if ( scene is not null && scene.IsValid() )
		{
			var terraPeak = ThornsTerraingenTerrainQueries.ResolvePeakWorldZRough( scene );
			if ( terraPeak > 2000f )
				return terraPeak;
		}

		var peak = 1600f;
		if ( scene is null || !scene.IsValid() )
			return peak;
		foreach ( var ts in scene.GetAllComponents<ThornsTerrainSystem>() )
		{
			if ( !ts.IsValid() )
				continue;
			var cfg = ts.TerraingenConfig ?? new ThornsTerrainConfig();
			var hm = MathF.Abs( cfg.MaxTerrainHeightInches );
			peak = MathF.Max( peak, hm + ts.WaterLevelWorldZ );
		}

		return peak + 380f;
	}

	/// <summary>
	/// Raycast downward onto <see cref="ThornsTerrainChunk"/> collision; falls back to a height-aware sky Z when the mesh isn't ready yet.
	/// Keeps planar XY from spawn points / map center defaults.
	/// </summary>
	public static Transform ClampSpawnTransformOntoTerrainSurface( Scene scene, Transform worldTransform )
	{
		if ( scene is null || !scene.IsValid() )
			return worldTransform;

		var t = worldTransform;
		var p = t.Position;
		var planarRef = new Vector3( p.x, p.y, 0f );
		var upper = ResolveTerrainPeakWorldZRough( scene );
		var startLift = Math.Clamp( upper + 920f, 2400f, 18000f );
		var segment = Math.Clamp( startLift + upper + 2000f, 6000f, 65536f );

		const float pawnFeetLift = 72f;

		if ( ThornsTerraingenTerrainQueries.TrySampleGroundWorld(
			     scene,
			     planarRef.x,
			     planarRef.y,
			     pawnFeetLift,
			     out var terraSpawn ) )
		{
			t.Position = terraSpawn;
			return t;
		}

		if ( ThornsTerrainGeometry.TrySnapWorldPositionToTerrainGround(
			     scene,
			     planarRef,
			     startLift,
			     segment,
			     out var snapped ) )
		{
			t.Position = snapped + Vector3.Up * 18f;
			return t;
		}

		t.Position = new Vector3( p.x, p.y, MathF.Max( p.z, upper + 120f ) );
		return t;
	}

	/// <summary>Map hub respawn: world XY at origin; Z clears procedural crests.</summary>
	public static Transform GetMapCenterRespawnTransform( Scene scene )
	{
		var spawnZ = ResolveTerrainPeakWorldZRough( scene );
		return new Transform( new Vector3( 0f, 0f, spawnZ ), Rotation.Identity, 1f );
	}

	/// <summary>
	/// Uniform random <see cref="SpawnPoint"/> without allocating a snapshot array (same distribution as <c>Random.Shared.FromArray</c>).
	/// </summary>
	static bool HostTryPickRandomSpawnPoint( Scene scene, out SpawnPoint picked )
	{
		picked = null;
		if ( scene is null || !scene.IsValid() )
			return false;

		var n = 0;
		foreach ( var sp in scene.GetAllComponents<SpawnPoint>() )
		{
			if ( sp is null || !sp.IsValid() )
				continue;

			n++;
			if ( Random.Shared.Next( n ) == 0 )
				picked = sp;
		}

		return picked is not null && picked.IsValid();
	}

	/// <summary>
	/// Host death respawn: dry shoreline on procedural terrain (see <see cref="ThornsPlayerCoastalSpawn"/>).
	/// </summary>
	public static bool TryFindRandomTerrainSurfaceRespawnTransform( Scene scene, out Transform transform ) =>
		ThornsPlayerCoastalSpawn.TryFindCoastalTerrainSpawnTransform( scene, 18f, out transform );

	/// <summary>
	/// Host-only: respawn pose that clears the void kill plane; re-rolls with planar jitter when terrain clamps fail
	/// (join-in / collider timing can otherwise respawn under the mesh and void-kill in a loop). Same transform is used for
	/// every peer — host authority.
	/// </summary>
	public static Transform ResolveSafeRespawnTransformForPawn(
		Scene scene,
		GameObject pawnRoot,
		Vector3 lastWorldPositionBeforeRespawn )
	{
		if ( scene is null || !scene.IsValid() )
			return new Transform( Vector3.Zero, Rotation.Identity, 1f );

		var voidZ = ResolveVoidKillPlaneWorldZ( scene );
		const float margin = 48f;
		var safeMinZ = voidZ + margin;
		var wasUnderVoid = lastWorldPositionBeforeRespawn.z < safeMinZ + 24f;

		for ( var attempt = 0; attempt < 20; attempt++ )
		{
			var tf = FindRespawnTransformInScene( scene );
			var p = tf.Position;
			if ( attempt > 0 )
			{
				var amp = 220f + attempt * 160f;
				p += new Vector3( (Random.Shared.NextSingle() * 2f - 1f) * amp, (Random.Shared.NextSingle() * 2f - 1f) * amp, 0f );
				tf = ClampSpawnTransformOntoTerrainSurface( scene, new Transform( p, tf.Rotation, 1f ) );
				p = tf.Position;
			}

			if ( p.z < safeMinZ )
				continue;

			if ( wasUnderVoid )
			{
				var planarDelta = (p - lastWorldPositionBeforeRespawn).WithZ( 0f ).Length;
				if ( planarDelta < 160f && attempt < 18 )
					continue;
			}

			return new Transform( p, tf.Rotation, 1f );
		}

		var peak = ResolveTerrainPeakWorldZRough( scene );
		var skyZ = MathF.Max( voidZ + 560f, peak + 360f );
		var planar = new Vector3(
			Random.Shared.NextSingle() * 4800f - 2400f,
			Random.Shared.NextSingle() * 4800f - 2400f,
			skyZ );
		var em = ClampSpawnTransformOntoTerrainSurface( scene, new Transform( planar, Rotation.Identity, 1f ) );
		var ep = em.Position;
		if ( ep.z < safeMinZ )
			ep = new Vector3( ep.x, ep.y, skyZ );

		return new Transform( ep, em.Rotation, 1f );
	}

	/// <summary>
	/// Host respawn after death: coastal shoreline on procedural terrain when available; otherwise random authored
	/// <see cref="SpawnPoint"/> / manager list, then map center (never the death position).
	/// </summary>
	public static Transform FindRespawnTransformInScene( Scene scene )
	{
		if ( scene is null || !scene.IsValid() )
			return GetMapCenterRespawnTransform( null );

		if ( TryFindRandomTerrainSurfaceRespawnTransform( scene, out var terrainTf ) )
			return terrainTf;

		Transform rawTf;
		ThornsGameManager firstGm = null;
		foreach ( var gm in scene.GetAllComponents<ThornsGameManager>() )
		{
			if ( !gm.IsValid() )
				continue;
			firstGm = gm;
			if ( gm.SpawnPoints is { Count: > 0 } )
			{
				rawTf = Random.Shared.FromList( gm.SpawnPoints, default ).WorldTransform;
				return ClampSpawnTransformOntoTerrainSurface( scene, rawTf );
			}

			break;
		}

		if ( HostTryPickRandomSpawnPoint( scene, out var sceneSp ) )
		{
			rawTf = sceneSp.WorldTransform;
			return ClampSpawnTransformOntoTerrainSurface( scene, rawTf );
		}

		if ( firstGm is not null && firstGm.IsValid() && !firstGm.RespawnAtMapCenterWhenNoSpawnPoints )
			return ClampSpawnTransformOntoTerrainSurface( scene, firstGm.WorldTransform );

		return ClampSpawnTransformOntoTerrainSurface( scene, GetMapCenterRespawnTransform( scene ) );
	}

	Transform FindSpawnLocation()
	{
		const float joinFeetLift = 72f;
		if ( ThornsPlayerCoastalSpawn.TryFindCoastalTerrainSpawnTransform( Scene, joinFeetLift, out var coastal ) )
			return coastal;

		Transform rawTf;
		if ( SpawnPoints is { Count: > 0 } )
			rawTf = Random.Shared.FromList( SpawnPoints, default ).WorldTransform;
		else if ( HostTryPickRandomSpawnPoint( Scene, out var sceneSp ) )
			rawTf = sceneSp.WorldTransform;
		else
		{
			rawTf = WorldTransform;
			rawTf.Position += new Vector3( 0f, 0f, 32f );
		}

		return ClampSpawnTransformOntoTerrainSurface( Scene, rawTf );
	}

	protected override void OnDestroy()
	{
		// Best-effort flush before gameplay hierarchy is torn down (component destruction order is undefined).
		if ( Networking.IsHost )
			ThornsWorldPersistence.Instance?.TryHostSaveNow( immediate: true );
	}
}
