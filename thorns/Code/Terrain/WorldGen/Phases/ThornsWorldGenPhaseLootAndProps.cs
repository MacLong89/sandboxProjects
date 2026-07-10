namespace Sandbox;

/// <summary>Phase 9 — interior loot crates and radios (runs after terrain chunk is spawned).</summary>
public sealed class ThornsWorldGenPhaseLootAndProps : IThornsWorldGenerationPhase
{
	public ThornsWorldGenerationPhaseId Id => ThornsWorldGenerationPhaseId.GenerateLootAndProps;
	public string Name => "GenerateLootAndProps";

	public void Execute( ThornsWorldGenerationContext context, ThornsWorldGenerationHostBridge host )
	{
		if ( !host.ChunkRoot.IsValid() )
			return;

		var spec = context.Spec;
		var queue = ThornsDeferredHostSpawnQueue.EnsureOn(
			host.ChunkRoot,
			host.Terrain.DeferredHostSpawnsPerFrame );
		queue.EnqueueOrRunNow( () => host.Terrain.RunInteriorLootScatter( spec ) );
		queue.EnqueueOrRunNow( () => host.Terrain.RunInteriorFurnitureScatter( spec ) );
		queue.EnqueueOrRunNow( () => host.Terrain.RunInteriorCityDefenderScatter( spec ) );
	}
}
