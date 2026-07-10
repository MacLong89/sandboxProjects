namespace Sandbox;

/// <summary>Spawns passive practice dummies for <see cref="AimboxGameMode.Range"/>.</summary>
public static class AimboxRangeDummySpawner
{
	public const int DummyCount = 5;
	const string RangeRootName = "Aimbox Range Dummies";

	public static void Ensure( Scene scene )
	{
		if ( scene is null || !scene.IsValid() )
			return;

		Clear( scene );

		if ( !TryResolveLayout( out var playerSpawn, out var dummyLineX, out var spreadY, out var feetZ, out var dummySpreadOnX ) )
			return;

		var root = new GameObject( true, RangeRootName );

		for ( var i = 0; i < DummyCount; i++ )
		{
			var t = DummyCount <= 1
				? 0f
				: ( i - ( DummyCount - 1 ) * 0.5f ) / ( DummyCount - 1 );
			var position = dummySpreadOnX
				? new Vector3( t * spreadY, dummyLineX, feetZ )
				: new Vector3( dummyLineX, t * spreadY, feetZ );
			position = AimboxSpawnClearance.ResolveClearFeetPosition( scene, default, position );
			var rotation = AimboxSpawnResolve.GetRotationFacingArenaCenter( scene, position );

			var go = new GameObject( true, $"Range Dummy {i + 1:D2}" );
			go.SetParent( root );
			go.WorldPosition = position;
			go.WorldRotation = rotation;

			var dummy = go.Components.Create<AimboxDummyTarget>();
			dummy.MaxHealth = 250;
			dummy.RespawnSeconds = 1.25f;
		}

		EnsurePlayerSpawn( scene, playerSpawn, feetZ );
		Log.Info( $"[Aimbox Range] Spawned {DummyCount} practice dummies." );
	}

	public static void Clear( Scene scene )
	{
		if ( scene is null || !scene.IsValid() )
			return;

		foreach ( var dummy in scene.GetAllComponents<AimboxDummyTarget>().ToArray() )
		{
			if ( dummy?.GameObject is not { IsValid: true } go )
				continue;

			if ( !go.Name.StartsWith( "Range Dummy", StringComparison.OrdinalIgnoreCase ) )
				continue;

			var parent = go.Parent;
			if ( parent.IsValid() && parent.Name == RangeRootName )
			{
				parent.Destroy();
				return;
			}

			go.Destroy();
		}
	}

	static void EnsurePlayerSpawn( Scene scene, Vector3 position, float feetZ )
	{
		position = position.WithZ( feetZ );
		position = AimboxSpawnClearance.ResolveClearFeetPosition( scene, default, position );
		var existing = scene.GetAllComponents<AimboxSpawnPoint>().FirstOrDefault( x => x.GameObject.Name == "Range Player" );
		if ( existing is not null )
		{
			existing.GameObject.WorldPosition = position;
			existing.GameObject.WorldRotation = AimboxSpawnResolve.GetRotationFacingArenaCenter( scene, position );
			existing.Team = AimboxTeam.None;
			return;
		}

		var go = new GameObject( true, "Range Player" );
		go.WorldPosition = position;
		go.WorldRotation = AimboxSpawnResolve.GetRotationFacingArenaCenter( scene, position );
		go.Components.Create<AimboxSpawnPoint>().Team = AimboxTeam.None;
	}

	static bool TryResolveLayout( out Vector3 playerSpawn, out float dummyLineX, out float spreadY, out float feetZ, out bool dummySpreadOnX )
	{
		playerSpawn = default;
		dummyLineX = 0f;
		spreadY = 600f;
		feetZ = AimboxMapDesignRules.FloorWalkZ;
		dummySpreadOnX = false;

		var game = AimboxGame.Instance;
		if ( game is null )
			return false;

		feetZ = game.GetSpawnFeetZ();

		var cfg = AimboxMapCatalog.Get( game.ActiveArenaMap ).Layout;
		playerSpawn = new Vector3( cfg.RedSpawnX * 0.72f, -cfg.SpawnSpreadY * 0.12f, feetZ );
		dummyLineX = cfg.BlueSpawnX * 0.78f;
		spreadY = cfg.SpawnSpreadY * 0.58f;
		return true;
	}
}
