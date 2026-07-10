namespace Sandbox;

/// <summary>
/// Host: periodic refill of city defenders in procedural buildings (1% per floor, max 2 per building).
/// Initial pass runs at world-gen (<see cref="ThornsTerrainSystem.RunInteriorCityDefenderScatter"/>);
/// this timer refills killed defenders and tops up over time (~10 min cadence, like wildlife pressure).
/// </summary>
[Title( "Thorns — City defender scheduler" )]
[Category( "Thorns/AI" )]
[Icon( "shield" )]
public sealed class ThornsCityDefenderScheduler : Component
{
	[Property] public bool DisablePeriodicSpawns { get; set; }

	[Property] public float FirstWaveDelaySeconds { get; set; } = 600f;

	[Property] public float WaveIntervalSeconds { get; set; } = 600f;

	[Property] public float WaveIntervalJitterSeconds { get; set; } = 45f;

	/// <summary>When false, uses <see cref="ThornsTerrainSystem.InteriorCityDefenderFloorChance"/> from the terrain host.</summary>
	[Property] public bool OverrideFloorChance { get; set; }

	[Property, Range( 0f, 1f )] public float FloorSpawnChance { get; set; } = 0.01f;

	/// <summary>When false, uses <see cref="ThornsTerrainSystem.InteriorCityDefenderMaxPerBuilding"/>.</summary>
	[Property] public bool OverrideMaxPerBuilding { get; set; }

	[Property] public int MaxDefendersPerBuilding { get; set; } = 2;

	double _nextWaveTime;
	bool _hostClockInit;

	protected override void OnStart()
	{
		if ( !Networking.IsHost )
			return;

		HostArmInitialWave();
	}

	void HostArmInitialWave()
	{
		_hostClockInit = true;
		_nextWaveTime = Time.Now + FirstWaveDelaySeconds + Random.Shared.NextDouble() * 24.0;
	}

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost || !Game.IsPlaying || DisablePeriodicSpawns )
			return;

		if ( !_hostClockInit )
			HostArmInitialWave();

		if ( Time.Now < _nextWaveTime )
			return;

		_nextWaveTime = Time.Now + WaveIntervalSeconds + Random.Shared.NextDouble() * WaveIntervalJitterSeconds;

		var scene = GameObject.Scene;
		if ( scene is null || !scene.IsValid() )
			return;

		if ( ThornsProcBuildingNpcRegistry.HostBuildingCount == 0 )
			return;

		ThornsProcBuildingNpcRegistry.HostPruneInvalid();

		var (floorChance, maxPerBuilding) = ResolveTuning();
		if ( maxPerBuilding <= 0 )
			return;

		var rnd = Random.Shared;
		var spawned = 0;

		ThornsProcBuildingNpcRegistry.HostForEach( entry =>
		{
			if ( !entry.Root.IsValid() )
				return;

			spawned += ThornsProcBuildingCityDefenderSpawn.HostTryFillBuilding(
				scene,
				rnd,
				entry.Root,
				entry.WidthCells,
				entry.DepthCells,
				entry.Stories,
				floorChance,
				maxPerBuilding );
		} );

		if ( spawned > 0 )
			Log.Info( $"[Thorns] City defender wave: +{spawned} (next in ~{WaveIntervalSeconds:F0}s)" );
	}

	(float floorChance, int maxPerBuilding) ResolveTuning()
	{
		var floorChance = FloorSpawnChance;
		var maxPer = MaxDefendersPerBuilding;
		var scene = GameObject.Scene;
		ThornsTerrainSystem terrain = null;
		if ( scene is not null && scene.IsValid() )
		{
			foreach ( var t in scene.GetAllComponents<ThornsTerrainSystem>() )
			{
				if ( t.IsValid() )
				{
					terrain = t;
					break;
				}
			}
		}
		if ( terrain is not null && terrain.IsValid() )
		{
			if ( !OverrideFloorChance )
				floorChance = terrain.InteriorCityDefenderFloorChance;
			if ( !OverrideMaxPerBuilding )
				maxPer = terrain.InteriorCityDefenderMaxPerBuilding;
		}

		return (Math.Clamp( floorChance, 0f, 1f ), Math.Max( 0, maxPer ));
	}
}
