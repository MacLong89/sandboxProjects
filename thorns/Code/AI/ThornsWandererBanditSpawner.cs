namespace Sandbox;

/// <summary>
/// Host: periodic small chance to spawn 2–3 unleashed bandits near open terrain (similar cadence idea to <see cref="ThornsWildlifeSpawner"/>).
/// </summary>
[Title( "Thorns — Wanderer bandit spawner" )]
[Category( "Thorns/AI" )]
[Icon( "groups" )]
public sealed class ThornsWandererBanditSpawner : Component
{
	[Property] public bool DisableSpawns { get; set; }

	[Property] public float AttemptIntervalSeconds { get; set; } = 52f;

	[Property] public float AttemptJitterSeconds { get; set; } = 38f;

	[Property] public float GroupSpawnProbability { get; set; } = 0.11f;

	[Property] public int GlobalMaxWanderers { get; set; } = 10;

	[Property] public int MinGroupSize { get; set; } = 2;

	[Property] public int MaxGroupSize { get; set; } = 3;

	[Property] public float MinSpawnDistanceFromPlayer { get; set; } = 820f;

	[Property] public float MaxSpawnDistanceFromPlayer { get; set; } = 2400f;

	[Property] public float GroupRadius { get; set; } = 180f;

	[Property] public float SpawnTraceHeightAbovePlayer { get; set; } = 2800f;

	[Property] public float SpawnTraceMaxDistance { get; set; } = 9000f;

	double _nextAttemptTime;
	bool _hostClockInit;

	protected override void OnStart()
	{
		if ( !Networking.IsHost )
			return;

		HostArmInitialAttempt();
	}

	void HostArmInitialAttempt()
	{
		_hostClockInit = true;
		_nextAttemptTime = Time.Now + 12f + Random.Shared.NextDouble() * 24.0;
	}

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost || !Game.IsPlaying || DisableSpawns )
			return;

		if ( !_hostClockInit )
			HostArmInitialAttempt();

		if ( Time.Now < _nextAttemptTime )
			return;

		_nextAttemptTime = Time.Now + AttemptIntervalSeconds + Random.Shared.NextDouble() * AttemptJitterSeconds;

		if ( Random.Shared.NextDouble() >= GroupSpawnProbability )
			return;

		var scene = GameObject.Scene;
		if ( scene is null || !scene.IsValid() )
			return;

		if ( !ThornsPopulationSpawnEligibility.HostTryEvaluatePeriodicSpawn(
			     ThornsPopulationKind.BanditWanderer,
			     new ThornsPopulationSpawnRequest { GlobalCap = GlobalMaxWanderers, Scene = scene },
			     out var anchor,
			     out _ ) )
			return;

		var ang = Random.Shared.NextSingle() * MathF.PI * 2f;
		var dist = MinSpawnDistanceFromPlayer +
		           Random.Shared.NextSingle() * Math.Max( 64f, MaxSpawnDistanceFromPlayer - MinSpawnDistanceFromPlayer );
		var anchorPos = anchor.WorldPosition;
		var flat = anchorPos.WithZ( 0 ) + new Vector3( MathF.Cos( ang ) * dist, MathF.Sin( ang ) * dist, 0f );

		var sampleApprox = new Vector3( flat.x, flat.y, anchorPos.z );
		if ( !ThornsTerrainGeometry.TrySnapWorldPositionToTerrainGround(
			     scene,
			     sampleApprox,
			     Math.Max( 64f, SpawnTraceHeightAbovePlayer ),
			     Math.Max( 256f, SpawnTraceMaxDistance ),
			     out var groundPos ) )
			return;

		var rnd = Random.Shared;
		var lo = Math.Clamp( MinGroupSize, 2, 6 );
		var hi = Math.Clamp( MaxGroupSize, lo, 6 );
		var count = rnd.Next( lo, hi + 1 );

		ThornsGameManager.EnsureThornsPopulationDirectorForScene( scene );
		ThornsGameManager.EnsureThornsBanditDirectorForScene( scene );

		for ( var i = 0; i < count; i++ )
		{
			if ( !ThornsPopulationDirector.HostTryRequestSpawnSlot(
				     ThornsPopulationKind.BanditWanderer,
				     new ThornsPopulationSpawnRequest
				     {
					     GlobalCap = GlobalMaxWanderers,
					     Scene = scene,
				     },
				     out _ ) )
				return;

			var ring = (float)(rnd.NextDouble() * Math.PI * 2.0);
			var minRing = Math.Max( 48f, GroupRadius * 0.35f );
			var rad = minRing + rnd.NextSingle() * Math.Max( 8f, GroupRadius - minRing );
			var offset = new Vector3( MathF.Cos( ring ) * rad, MathF.Sin( ring ) * rad, 0f );
			var wp = groundPos + Vector3.Up * 12f + offset;
			ThornsBanditBrain.HostTryResolveSpawnClearOfBanditPeers( ref wp );
			var cfg = ThornsNpcHumanBanditSpawn.Wanderer();
			ThornsNpcHumanBanditSpawn.HostSpawnM4Citizen( scene, wp, rnd, cfg );
		}
	}

}
