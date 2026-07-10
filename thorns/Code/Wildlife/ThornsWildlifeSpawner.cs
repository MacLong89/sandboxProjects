namespace Sandbox;

/// <summary>
/// Staggered spawn scheduler — global + per-player caps (THORNS_EVERYTHING_DOCUMENT §8 / §perf).
/// Periodic spawns use <see cref="Random.Shared"/> (not the terrain world seed) so fauna stays unpredictable on a fixed procedural map.
/// Default interval/caps are tuned for ~half the spawn pressure vs early-2026 scenes (see shipped scene JSON overrides too).
/// </summary>
[Title( "Thorns — Wildlife spawner" )]
[Category( "Thorns/Wildlife" )]
[Icon( "pest_control" )]
public sealed class ThornsWildlifeSpawner : Component
{
	/// <summary>
	/// When true, skips periodic wild spawns only (<see cref="ThornsWildlifeSpawn.HostCreate"/>).
	/// Instance property (not static) so hot reload / domain state cannot leave fauna stuck off.
	/// </summary>
	[Property] public bool DisablePeriodicWildlifeSpawns { get; set; }

	[Property] public float SpawnIntervalSeconds { get; set; } = 12.8f;

	[Property] public float SpawnJitterSeconds { get; set; } = 8f;

	[Property] public int GlobalMaxWildlife { get; set; } = 6;

	[Property] public int PerPlayerNearbyCap { get; set; } = 2;

	[Property] public float PerPlayerNearbyRadius { get; set; } = 2600f;

	[Property] public float MinSpawnDistanceFromPlayer { get; set; } = 720f;

	/// <summary>Ray origin is player Z + this value so traces stay above hills (broken Z=520 caused zero spawns on high terrain).</summary>
	[Property] public float SpawnTraceHeightAbovePlayer { get; set; } = 2800f;

	/// <summary>Downward ray length from <see cref="SpawnTraceHeightAbovePlayer"/>.</summary>
	[Property] public float SpawnTraceMaxDistance { get; set; } = 9000f;

	// Wolf, deer, moose, panther focus; raise others to restore mixed ecology.
	[Property] public float RabbitSpawnWeight { get; set; }
	/// <summary><c>models/deer/deer.vmdl</c>.</summary>
	[Property] public float DeerSpawnWeight { get; set; } = 1f;
	[Property] public float BoarSpawnWeight { get; set; }
	/// <summary>Skinned elk mesh <c>models/elk/elk.vmdl</c> — set 0 to disable.</summary>
	[Property] public float ElkSpawnWeight { get; set; }
	/// <summary><c>models/moose/moose.vmdl</c> — match deer/wolf weights for an even mix.</summary>
	[Property] public float MooseSpawnWeight { get; set; } = 1f;
	[Property] public float BisonSpawnWeight { get; set; }
	[Property] public float FoxSpawnWeight { get; set; }
	/// <summary><c>models/wolf/wolf.vmdl</c> — match <see cref="DeerSpawnWeight"/> / <see cref="MooseSpawnWeight"/> for thirds.</summary>
	[Property] public float WolfSpawnWeight { get; set; } = 1f;
	[Property] public float CougarSpawnWeight { get; set; }
	/// <summary>Same AI/stats as cougars — <c>models/panther/panther.vmdl</c>; match other carnivore/herbivore weights for mix.</summary>
	[Property] public float PantherSpawnWeight { get; set; } = 1f;

	[Property] public float BearSpawnWeight { get; set; }

	/// <summary>World boss fauna — same species weights as normal spawns; one attempt each interval while under cap.</summary>
	[Property] public float BossWildlifeSpawnIntervalSeconds { get; set; } = 600f;

	[Property] public int BossWildlifeMaxConcurrent { get; set; } = 4;

	[Property] public bool DisableBossWildlifeSpawns { get; set; }

	/// <summary>
	/// Logs why spawn attempts abort (throttled ~8s). Turn off after verifying panthers appear.
	/// </summary>
	[Property] public bool WildlifeSpawnDiagnostics { get; set; } = true;

	double _nextSpawnAttemptTime;
	double _nextBossSpawnAttemptRealtime = -1;
	double _nextDiagThrottleRealtime;

	ThornsDeferredHostSpawnQueue _spawnQueue;

	ThornsDeferredHostSpawnQueue EnsureSpawnQueue()
	{
		if ( _spawnQueue is null || !_spawnQueue.IsValid() )
		{
			_spawnQueue = Components.Get<ThornsDeferredHostSpawnQueue>();
			if ( !_spawnQueue.IsValid() )
				_spawnQueue = Components.Create<ThornsDeferredHostSpawnQueue>();
			_spawnQueue.WorkBudgetPerFrame = 1;
		}

		return _spawnQueue;
	}

	void QueueHostCreate( Scene scene, ThornsWildlifeSpeciesKind species, Vector3 spawnPos, bool bossAnimal = false )
	{
		if ( scene is null || !scene.IsValid() )
			return;

		var traceScene = scene;
		var spec = species;
		var pos = spawnPos;
		var boss = bossAnimal;
		EnsureSpawnQueue().TryEnqueue( () =>
		{
			if ( traceScene is null || !traceScene.IsValid() )
				return;

			ThornsWildlifeSpawn.HostCreate( traceScene, spec, pos, bossAnimal: boss );
		} );
	}

	protected override void OnStart()
	{
		if ( _nextBossSpawnAttemptRealtime < 0 )
			_nextBossSpawnAttemptRealtime = Time.Now + BossWildlifeSpawnIntervalSeconds;
	}

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost )
			return;

		MaybeSpawnBossWildlife();

		if ( DisablePeriodicWildlifeSpawns )
		{
			DiagSpawnSkip( "DisablePeriodicWildlifeSpawns=true on ThornsWildlifeSpawner" );
			return;
		}

		if ( Time.Now < _nextSpawnAttemptTime )
			return;

		_nextSpawnAttemptTime = Time.Now + SpawnIntervalSeconds + Random.Shared.NextSingle() * SpawnJitterSeconds;

		var spawnRequest = new ThornsPopulationSpawnRequest
		{
			GlobalCap = GlobalMaxWildlife,
			PerPlayerNearbyCap = PerPlayerNearbyCap,
			PerPlayerNearbyRadius = PerPlayerNearbyRadius,
		};

		if ( !ThornsPopulationSpawnEligibility.HostTryEvaluatePeriodicSpawn(
			     ThornsPopulationKind.Wildlife,
			     spawnRequest,
			     out var anchor,
			     out var deny ) )
		{
			DiagSpawnSkip( deny );
			return;
		}

		var ang = Random.Shared.NextSingle() * MathF.PI * 2f;
		var dist = MinSpawnDistanceFromPlayer + Random.Shared.NextSingle() * 1600f;
		var anchorPos = anchor.WorldPosition;
		var flat = anchorPos.WithZ( 0 )
		           + new Vector3( MathF.Cos( ang ) * dist, MathF.Sin( ang ) * dist, 0f );

		var traceScene = GameObject.Scene;
		if ( traceScene is null || !traceScene.IsValid() )
			return;

		// Same terrain snap as resource scatter: physics trace + pierce props until ThornsTerrainChunk hits.
		var sampleApprox = new Vector3( flat.x, flat.y, anchorPos.z );
		if ( !ThornsTerrainGeometry.TrySnapWorldPositionToTerrainGround(
			     traceScene,
			     sampleApprox,
			     Math.Max( 64f, SpawnTraceHeightAbovePlayer ),
			     Math.Max( 256f, SpawnTraceMaxDistance ),
			     out var groundPos ) )
		{
			DiagSpawnSkip(
				$"TrySnapWorldPositionToTerrainGround failed sample=({sampleApprox.x:0},{sampleApprox.y:0}) z={anchorPos.z:0} lift={SpawnTraceHeightAbovePlayer:0}" );
			return;
		}

		var spawnPos = groundPos + Vector3.Up * 12f;

		var species = PickSpeciesByWeight();

		QueueHostCreate( traceScene, species, spawnPos );
	}

	void MaybeSpawnBossWildlife()
	{
		if ( DisableBossWildlifeSpawns )
			return;

		if ( _nextBossSpawnAttemptRealtime < 0 )
			_nextBossSpawnAttemptRealtime = Time.Now + BossWildlifeSpawnIntervalSeconds;

		if ( Time.Now < _nextBossSpawnAttemptRealtime )
			return;

		_nextBossSpawnAttemptRealtime = Time.Now + BossWildlifeSpawnIntervalSeconds;

		var traceScene = GameObject.Scene;
		if ( traceScene is null || !traceScene.IsValid() )
			return;

		if ( HostCountLivingBossWildlife( traceScene ) >= BossWildlifeMaxConcurrent )
			return;

		var roots = ThornsPopulationDirector.HostGetCachedPlayerRoots();
		if ( roots.Count == 0 )
			return;

		if ( !ThornsHealth.HostTryPickRandomNpcSpawnAnchorPlayer( roots, out var anchor ) || !anchor.IsValid() )
			return;

		var ang = Random.Shared.NextSingle() * MathF.PI * 2f;
		var dist = MinSpawnDistanceFromPlayer + Random.Shared.NextSingle() * 2200f;
		var anchorPos = anchor.WorldPosition;
		var flat = anchorPos.WithZ( 0 )
		           + new Vector3( MathF.Cos( ang ) * dist, MathF.Sin( ang ) * dist, 0f );

		var sampleApprox = new Vector3( flat.x, flat.y, anchorPos.z );
		if ( !ThornsTerrainGeometry.TrySnapWorldPositionToTerrainGround(
			     traceScene,
			     sampleApprox,
			     Math.Max( 64f, SpawnTraceHeightAbovePlayer ),
			     Math.Max( 256f, SpawnTraceMaxDistance ),
			     out var groundPos ) )
			return;

		var spawnPos = groundPos + Vector3.Up * 12f;
		var species = PickSpeciesByWeight();
		QueueHostCreate( traceScene, species, spawnPos, bossAnimal: true );
	}

	static int HostCountLivingBossWildlife( Scene scene )
	{
		if ( scene is null || !scene.IsValid() )
			return 0;

		var n = 0;
		foreach ( var id in ThornsWildlifeIdentity.ActiveByHost.Values )
		{
			if ( !id.IsValid() || !id.IsBossWildlifeSync || id.HostIsTamed )
				continue;

			var hp = id.Components.Get<ThornsHealth>();
			if ( hp.IsValid() && hp.IsAlive && !hp.IsDeadState )
				n++;
		}

		return n;
	}

	void DiagSpawnSkip( string reason )
	{
		if ( !WildlifeSpawnDiagnostics )
			return;

		if ( Time.Now < _nextDiagThrottleRealtime )
			return;

		_nextDiagThrottleRealtime = Time.Now + 8f;
		Log.Info( $"[Thorns] Wildlife spawn skipped: {reason}" );
	}

	ThornsWildlifeSpeciesKind PickSpeciesByWeight()
	{
		var wRabbit = Math.Max( 0f, RabbitSpawnWeight );
		var wDeer = Math.Max( 0f, DeerSpawnWeight );
		var wBoar = Math.Max( 0f, BoarSpawnWeight );
		var wElk = Math.Max( 0f, ElkSpawnWeight );
		var wMoose = Math.Max( 0f, MooseSpawnWeight );
		var wBison = Math.Max( 0f, BisonSpawnWeight );
		var wFox = Math.Max( 0f, FoxSpawnWeight );
		var wWolf = Math.Max( 0f, WolfSpawnWeight );
		var wCougar = Math.Max( 0f, CougarSpawnWeight );
		var wPanther = Math.Max( 0f, PantherSpawnWeight );
		var wBear = Math.Max( 0f, BearSpawnWeight );
		var total = wRabbit + wDeer + wBoar + wElk + wMoose + wBison + wFox + wWolf + wCougar + wPanther + wBear;
		if ( total <= 0.0001f )
			return ThornsWildlifeSpeciesKind.Deer;

		var r = Random.Shared.NextSingle() * total;

		r -= wRabbit;
		if ( r <= 0f ) return ThornsWildlifeSpeciesKind.Rabbit;
		r -= wDeer;
		if ( r <= 0f ) return ThornsWildlifeSpeciesKind.Deer;
		r -= wBoar;
		if ( r <= 0f ) return ThornsWildlifeSpeciesKind.Boar;
		r -= wElk;
		if ( r <= 0f ) return ThornsWildlifeSpeciesKind.Elk;
		r -= wMoose;
		if ( r <= 0f ) return ThornsWildlifeSpeciesKind.Moose;
		r -= wBison;
		if ( r <= 0f ) return ThornsWildlifeSpeciesKind.Bison;
		r -= wFox;
		if ( r <= 0f ) return ThornsWildlifeSpeciesKind.Fox;
		r -= wWolf;
		if ( r <= 0f ) return ThornsWildlifeSpeciesKind.Wolf;
		r -= wCougar;
		if ( r <= 0f ) return ThornsWildlifeSpeciesKind.Cougar;
		r -= wPanther;
		if ( r <= 0f ) return ThornsWildlifeSpeciesKind.Panther;
		return ThornsWildlifeSpeciesKind.Bear;
	}
}
