namespace Sandbox;

/// <summary>Spawns AIM drill layout for aim trainer game modes.</summary>
public static class AimboxAimDrillSpawner
{
	const string AimRootName = "Aimbox AIM Drill";

	public static void Ensure( Scene scene, AimboxAimDrill level )
	{
		if ( scene is null || !scene.IsValid() )
			return;

		Clear( scene );

		if ( !TryResolveLayout( out var playerSpawn, out _, out var feetZ ) )
			return;

		var root = new GameObject( true, AimRootName );
		var controller = root.Components.Create<AimboxAimDrillController>();
		controller.Initialize( level );

		EnsurePlayerSpawn( scene, playerSpawn, feetZ );
		Log.Info( $"[Aimbox AIM] Drill ready: {AimboxAimDrillLabels.Long( level )}." );
	}

	public static void Clear( Scene scene )
	{
		if ( scene is null || !scene.IsValid() )
			return;

		foreach ( var controller in scene.GetAllComponents<AimboxAimDrillController>().ToArray() )
		{
			if ( controller?.GameObject is not { IsValid: true } go )
				continue;

			if ( go.Name == AimRootName || go.Parent?.Name == AimRootName )
			{
				go.Parent?.Destroy();
				if ( go.Parent is null || !go.Parent.IsValid() )
					go.Destroy();
			}
		}

		foreach ( var dummy in scene.GetAllComponents<AimboxDummyTarget>().ToArray() )
		{
			if ( dummy?.GameObject is not { IsValid: true } go )
				continue;

			if ( go.Name is "AIM Circle" or "AIM Target" or "AIM Sphere" )
				go.Destroy();
		}
	}

	static void EnsurePlayerSpawn( Scene scene, Vector3 position, float feetZ )
	{
		position = position.WithZ( feetZ );
		position = AimboxSpawnClearance.ResolveClearFeetPosition( scene, default, position );
		var existing = scene.GetAllComponents<AimboxSpawnPoint>().FirstOrDefault( x => x.GameObject.Name == "AIM Player" );
		if ( existing is not null )
		{
			existing.GameObject.WorldPosition = position;
			existing.GameObject.WorldRotation = AimboxAimRoomLayout.PlayerFacing;
			existing.Team = AimboxTeam.None;
			return;
		}

		var go = new GameObject( true, "AIM Player" );
		go.WorldPosition = position;
		go.WorldRotation = AimboxAimRoomLayout.PlayerFacing;
		go.Components.Create<AimboxSpawnPoint>().Team = AimboxTeam.None;
	}

	static bool TryResolveLayout( out Vector3 playerSpawn, out Vector3 targetCenter, out float feetZ )
	{
		playerSpawn = AimboxAimRoomLayout.PlayerSpawn;
		targetCenter = AimboxAimRoomLayout.TargetCenter;
		feetZ = AimboxAimRoomLayout.FeetZ;
		return AimboxGame.Instance is not null;
	}
}
