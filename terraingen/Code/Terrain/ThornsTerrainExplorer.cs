namespace Terraingen.TerrainGen;

using Terraingen;
using Terraingen.Buildings;
using Terraingen.Combat;
using Terraingen.Multiplayer;
using Terraingen.Player;

// Spawns a walkable player at the center of the generated terrain for traversal testing.
[Title( "Thorns Terrain Explorer" )]
[Category( "Terrain" )]
[Icon( "directions_walk" )]
public sealed class ThornsTerrainExplorer : Component
{
	[Property] public GameObject PlayerPrefab { get; set; }

	[Property] public string PlayerPrefabPath { get; set; } = "templates/gameobject/player controller.prefab";

	[Property] public float SpawnHeightOffset { get; set; } = 72f;

	[Property] public bool SpawnOnTerrainReady { get; set; } = true;

	[Property] public bool UseFixedSpawnPoint { get; set; } = false;

	[Property] public Vector3 FixedSpawnPoint { get; set; } = new( 24744.8f, -12213.0f, 3594.6f );

	[Property] public bool UseSceneCameraForPlayer { get; set; } = true;

	[Property] public string SceneCameraObjectName { get; set; } = "Terrain Preview Camera";

	[Property] public bool FirstPerson { get; set; } = true;

	[Property, Group( "Movement" )]
	public float WalkSpeedMultiplier { get; set; } = 1f;

	[Property, Group( "Movement" )]
	public float SprintSpeedMultiplier { get; set; } = 1.05f;

	GameObject _player;

	public void SpawnAtTerrainCenter( Terrain terrain, float terrainMaxHeight )
	{
		if ( !SpawnOnTerrainReady || !terrain.IsValid() )
			return;

		if ( _player.IsValid() )
			return;

		var prefab = ResolvePlayerPrefab();
		if ( !prefab.IsValid() )
		{
			Log.Error( "[Thorns Terrain] Could not find player prefab. Assign PlayerPrefab or install base 'Player Controller' template." );
			return;
		}

		if ( UseSceneCameraForPlayer && !FirstPerson )
			AssignMainCameraToPlayer();
		else if ( FirstPerson )
			ThornsSceneObserver.SuppressTerrainPreviewMainCamera( Scene );

		Vector3 spawnPos;
		if ( UseFixedSpawnPoint )
			spawnPos = SampleFixedSpawnPosition( terrain, terrainMaxHeight, FixedSpawnPoint, SpawnHeightOffset );
		else if ( !ThornsPlayerSpawnLocations.TryPickRandom( Scene, out spawnPos ) )
			spawnPos = SampleCenterSpawnPosition( terrain, terrainMaxHeight, SpawnHeightOffset );
		_player = prefab.Clone( new Transform( spawnPos, Rotation.Identity ), name: "Terrain Explorer" );
		if ( ThornsLightingTestSceneBootstrap.IsActive )
			ConfigureLightingTestPlayer( _player );
		else if ( ThornsSettlementTestSceneBootstrap.IsActive )
			ConfigureSettlementTestPlayer( _player );
		else
		{
			ConfigurePlayerController( _player );
			ThornsPlayerPresentationBootstrap.EnsureFirstPersonPresentation( _player );
		}

		if ( FirstPerson )
			ThornsSceneObserver.FocusLocalPlayer( Scene, _player );

		if ( ThornsBanditCombatTestScene.IsActive )
			ThornsBanditCombatTestScene.EnsureTestPlayerIdentity( _player );
		else if ( ThornsBowTestScene.IsActive )
			ThornsBowTestScene.EnsureTestPlayerIdentity( _player );
		else if ( !Networking.IsActive )
			EnsureOfflinePlayerIdentity( _player );

		Log.Info( $"[Thorns Terrain] Spawned explorer at {spawnPos}" );
	}

	static void EnsureOfflinePlayerIdentity( GameObject player )
	{
		if ( !player.IsValid() )
			return;

		player.Components.Get<ThornsPlayerSession>()?.HostEnsurePersistenceKey( Connection.Local );
		player.Components.Get<Terraingen.Player.ThornsPlayerGameplay>()?.HostEnsureProgressInitialized();
	}

	GameObject ResolvePlayerPrefab()
	{
		if ( PlayerPrefab.IsValid() )
			return PlayerPrefab;

		return GameObject.GetPrefab( PlayerPrefabPath );
	}

	static Vector3 SampleCenterSpawnPosition( Terrain terrain, float terrainMaxHeight, float heightOffset )
	{
		// Terrain is centered on world origin when using default placement.
		var rayStart = new Vector3( 0f, 0f, terrainMaxHeight * 2f );
		var ray = new Ray( rayStart, Vector3.Down );

		if ( terrain.RayIntersects( ray, terrainMaxHeight * 4f, out var localHit ) )
		{
			var world = terrain.GameObject.WorldTransform.PointToWorld( localHit );
			return world + Vector3.Up * heightOffset;
		}

		Log.Warning( "[Thorns Terrain] Terrain ray miss at center — spawning at fallback height." );
		return new Vector3( 0f, 0f, terrainMaxHeight * 0.35f + heightOffset );
	}

	static Vector3 SampleFixedSpawnPosition( Terrain terrain, float terrainMaxHeight, Vector3 requestedSpawn, float heightOffset )
	{
		var half = terrain.TerrainSize * 0.5f;
		var min = terrain.GameObject.WorldPosition;
		var max = min + new Vector3( terrain.TerrainSize, terrain.TerrainSize, 0f );

		var planar = ThornsTerrainSurface.ClampToTerrainBounds( terrain, requestedSpawn );
		var clamped = new Vector3( planar.x, planar.y, requestedSpawn.z );

		var rayStart = new Vector3( clamped.x, clamped.y, terrain.GameObject.WorldPosition.z + terrainMaxHeight * 2f );
		var ray = new Ray( rayStart, Vector3.Down );

		if ( terrain.RayIntersects( ray, terrainMaxHeight * 4f, out var localHit ) )
		{
			var world = terrain.GameObject.WorldTransform.PointToWorld( localHit );
			if ( clamped != requestedSpawn )
				Log.Warning( $"[Thorns Terrain] Fixed spawn {requestedSpawn} was outside terrain bounds +/-{half:F0}; clamped to {world}." );

			return world + Vector3.Up * heightOffset;
		}

		Log.Warning( $"[Thorns Terrain] Terrain ray miss at fixed spawn {requestedSpawn}; using center fallback." );
		return SampleCenterSpawnPosition( terrain, terrainMaxHeight, heightOffset );
	}

	void ConfigurePlayerController( GameObject player )
	{
		var controller = player.Components.Get<PlayerController>( FindMode.EverythingInSelf );
		if ( !controller.IsValid() )
			return;

		if ( FirstPerson )
		{
			var locomotion = player.Components.Get<Terraingen.Player.ThornsPlayerLocomotion>()
			                 ?? player.Components.Create<Terraingen.Player.ThornsPlayerLocomotion>();
			locomotion.ConfigurePlayerController();
		}
		else
		{
			controller.ThirdPerson = true;
			controller.UseCameraControls = true;
			controller.UseLookControls = true;
		}

		controller.UseInputControls = true;
		ApplyMovementSpeedMultipliers( controller );

		EnsureStandardGameplayComponents( player );
	}

	void ConfigureLightingTestPlayer( GameObject player )
	{
		var controller = player.Components.Get<PlayerController>( FindMode.EverythingInSelf );
		if ( !controller.IsValid() )
			return;

		if ( FirstPerson )
		{
			var locomotion = player.Components.Get<Terraingen.Player.ThornsPlayerLocomotion>()
			                 ?? player.Components.Create<Terraingen.Player.ThornsPlayerLocomotion>();
			locomotion.ConfigurePlayerController();
		}
		else
		{
			controller.ThirdPerson = true;
			controller.UseCameraControls = true;
			controller.UseLookControls = true;
		}

		controller.UseInputControls = true;
		ApplyMovementSpeedMultipliers( controller );

		ThornsPlayerPresentationBootstrap.EnsureLightingTestExploration( player );
		Log.Info( $"[Thorns Lighting Test] Explorer walk={controller.WalkSpeed:F0} run={controller.RunSpeed:F0} (no viewmodel)" );
	}

	void ConfigureSettlementTestPlayer( GameObject player )
	{
		ConfigurePlayerController( player );
		ThornsPlayerPresentationBootstrap.EnsureFirstPersonPresentation( player );
		Log.Info( $"[Thorns Settlement Test] Explorer walk speed configured for city layout review." );
	}

	void ApplyMovementSpeedMultipliers( PlayerController controller )
	{
		var walkMul = WalkSpeedMultiplier > 0f ? WalkSpeedMultiplier : 1f;
		var sprintMul = SprintSpeedMultiplier > 0f ? SprintSpeedMultiplier : ThornsPlayerMovementDefaults.SprintSpeedMultiplier;
		ThornsPlayerMovementDefaults.Apply( controller, walkMul, sprintMul );
	}

	public static void EnsureStandardGameplayComponents( GameObject player )
	{
		_ = player.Components.Get<Terraingen.Combat.ThornsPlayerHealth>() ?? player.Components.Create<Terraingen.Combat.ThornsPlayerHealth>();
		_ = player.Components.Get<Terraingen.Combat.ThornsPlayerDamageReceiver>()
		    ?? player.Components.Create<Terraingen.Combat.ThornsPlayerDamageReceiver>();
		_ = player.Components.Get<Terraingen.Combat.ThornsPlayerAnimalHitscan>() ?? player.Components.Create<Terraingen.Combat.ThornsPlayerAnimalHitscan>();
		_ = player.Components.Get<Terraingen.Combat.ThornsPlayerTreeChopUse>() ?? player.Components.Create<Terraingen.Combat.ThornsPlayerTreeChopUse>();
		_ = player.Components.Get<Terraingen.Combat.ThornsPlayerResourceSalvageUse>() ?? player.Components.Create<Terraingen.Combat.ThornsPlayerResourceSalvageUse>();
		_ = player.Components.Get<Terraingen.Combat.ThornsPlayerAnimalTaming>() ?? player.Components.Create<Terraingen.Combat.ThornsPlayerAnimalTaming>();
		_ = player.Components.Get<Terraingen.Combat.ThornsPlayerSurvivalUse>() ?? player.Components.Create<Terraingen.Combat.ThornsPlayerSurvivalUse>();
		_ = player.Components.Get<Terraingen.Combat.ThornsPlayerWaterDrinkUse>() ?? player.Components.Create<Terraingen.Combat.ThornsPlayerWaterDrinkUse>();
		_ = player.Components.Get<Terraingen.Combat.ThornsPlayerNpcGuildCoreUse>() ?? player.Components.Create<Terraingen.Combat.ThornsPlayerNpcGuildCoreUse>();
		_ = player.Components.Get<Terraingen.Combat.ThornsPlayerHotbarConsumeUse>() ?? player.Components.Create<Terraingen.Combat.ThornsPlayerHotbarConsumeUse>();
		_ = player.Components.Get<ThornsPlayerWaterProximityAudio>() ?? player.Components.Create<ThornsPlayerWaterProximityAudio>();
		_ = player.Components.Get<Terraingen.Combat.ThornsPlayerRadioShopUse>() ?? player.Components.Create<Terraingen.Combat.ThornsPlayerRadioShopUse>();
		_ = player.Components.Get<Terraingen.Combat.ThornsPlayerContainerUse>() ?? player.Components.Create<Terraingen.Combat.ThornsPlayerContainerUse>();
		_ = player.Components.Get<Terraingen.Combat.ThornsPlayerDoorUse>() ?? player.Components.Create<Terraingen.Combat.ThornsPlayerDoorUse>();
		_ = player.Components.Get<Terraingen.Combat.ThornsPlayerCraftStationUse>() ?? player.Components.Create<Terraingen.Combat.ThornsPlayerCraftStationUse>();
		_ = player.Components.Get<Terraingen.Combat.ThornsPlayerResearchStationUse>() ?? player.Components.Create<Terraingen.Combat.ThornsPlayerResearchStationUse>();
		_ = player.Components.Get<Terraingen.Combat.ThornsPlayerAirdropUse>() ?? player.Components.Create<Terraingen.Combat.ThornsPlayerAirdropUse>();
		_ = player.Components.Get<Terraingen.Combat.ThornsPlayerDeathCrateUse>() ?? player.Components.Create<Terraingen.Combat.ThornsPlayerDeathCrateUse>();
		_ = player.Components.Get<Terraingen.Combat.ThornsPlayerMountController>() ?? player.Components.Create<Terraingen.Combat.ThornsPlayerMountController>();
		_ = player.Components.Get<Terraingen.Combat.ThornsPlayerMountUse>() ?? player.Components.Create<Terraingen.Combat.ThornsPlayerMountUse>();
		_ = player.Components.Get<Terraingen.Combat.ThornsPlayerUseGrabStanceDriver>() ?? player.Components.Create<Terraingen.Combat.ThornsPlayerUseGrabStanceDriver>();
		_ = player.Components.Get<Terraingen.Player.ThornsPlayerGameplay>() ?? player.Components.Create<Terraingen.Player.ThornsPlayerGameplay>();
		_ = player.Components.Get<Terraingen.Player.ThornsTerrainWaterMoveMode>() ?? player.Components.Create<Terraingen.Player.ThornsTerrainWaterMoveMode>();
	}

	void AssignMainCameraToPlayer()
	{
		foreach ( var cam in Scene.GetAllComponents<CameraComponent>() )
			cam.IsMainCamera = false;

		foreach ( var cam in Scene.GetAllComponents<CameraComponent>() )
		{
			if ( !cam.GameObject.Name.Equals( SceneCameraObjectName, StringComparison.OrdinalIgnoreCase ) )
				continue;

			cam.GameObject.Enabled = true;
			cam.IsMainCamera = true;
			cam.ZFar = Math.Max( cam.ZFar, 500000f );

			var fly = cam.Components.Get<TerrainFlyCamera>();
			if ( fly.IsValid() )
				fly.Enabled = false;

			return;
		}

		CreateFallbackMainCamera();
	}

	void CreateFallbackMainCamera()
	{
		var cameraObject = Scene.CreateObject( true );
		cameraObject.Name = "Main Camera";
		var camera = cameraObject.Components.Create<CameraComponent>();
		camera.IsMainCamera = true;
		camera.FieldOfView = 70;
		camera.ZFar = 500000;
		camera.ZNear = 10;
	}
}
