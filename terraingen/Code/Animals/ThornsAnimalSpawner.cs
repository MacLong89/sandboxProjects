namespace Terraingen.Animals;

using Terraingen.Multiplayer;
using Terraingen.Progression;
using Terraingen.TerrainGen;

/// <summary>Optional authored spawn point (wildlife uses <see cref="ThornsAnimalManager"/> ambient rolls by default).</summary>
[Title( "Thorns Animal Spawner" )]
[Category( "Thorns/Animals" )]
[Icon( "add_location" )]
public sealed class ThornsAnimalSpawner : Component
{
	[Property] public string SpeciesKey { get; set; } = "wolf";
	[Property] public int Count { get; set; } = 4;
	[Property] public float SpawnRadius { get; set; } = 800f;
	[Property] public Vector3 SpawnCenter { get; set; }
	[Property] public bool UseTerrainCenterIfZero { get; set; } = true;

	bool _spawned;

	protected override void OnStart()
	{
		ThornsAnimalSpeciesRegistry.EnsureInitialized();
		ThornsAnimalManager.RegisterSpawner( this );
	}

	protected override void OnDestroy()
	{
		ThornsAnimalManager.UnregisterSpawner( this );
	}

	bool CanSpawnNow()
	{
		if ( !Game.IsPlaying || Scene.IsEditor )
			return false;

		if ( !ThornsMultiplayer.IsHostOrOffline )
			return false;

		var bootstrap = Scene.GetAllComponents<ThornsTerrainBootstrap>().FirstOrDefault();
		if ( bootstrap is null || !bootstrap.IsWorldApplied )
			return false;

		return ThornsAnimalManager.NavMeshReady;
	}

	internal void TrySpawn()
	{
		if ( _spawned || !CanSpawnNow() )
			return;

		var center = ResolveSpawnCenter();
		if ( ThornsNewPlayerWildlifeGrace.ShouldBlockSpawnNear( center, SpawnRadius + 400f ) )
			return;

		if ( !ThornsAnimalSpeciesRegistry.TryGet( SpeciesKey, out var species ) )
		{
			Log.Warning( $"[Thorns Animals] Spawner '{GameObject.Name}' unknown species '{SpeciesKey}'." );
			return;
		}

		var budget = Math.Min( Count, ThornsAnimalManager.RemainingSpawnSlots() );
		var spawned = 0;

		if ( species.SpawnsInGroups )
		{
			spawned = ThornsAnimalSpawnUtil.HostSpawnGroup( Scene, species, center, budget );
		}
		else
		{
			for ( var i = 0; i < budget; i++ )
			{
				if ( !ThornsAnimalManager.CanSpawnMore() )
					break;

				var offset = Vector3.Random.WithZ( 0f ).Normal * Game.Random.Float( 32f, SpawnRadius );
				spawned += ThornsAnimalSpawnUtil.HostSpawnSolitary( Scene, species, center + offset );
			}
		}

		_spawned = true;
		if ( ThornsAnimalDebug.Verbose )
			Log.Info( $"[Thorns Animals] Spawner '{GameObject.Name}' spawned {spawned}/{budget} {species.DisplayName} (cap {ThornsAnimalManager.Instance?.MaxWorldAnimals ?? 15})." );
	}

	Vector3 ResolveSpawnCenter()
	{
		if ( SpawnCenter != Vector3.Zero || !UseTerrainCenterIfZero )
			return SpawnCenter;

		var terrain = ThornsTerrainCache.Resolve( Scene );
		if ( !terrain.IsValid() )
			return GameObject.WorldPosition;

		var min = terrain.GameObject.WorldPosition;
		return min + new Vector3( terrain.TerrainSize * 0.5f, terrain.TerrainSize * 0.5f, 0f );
	}
}
