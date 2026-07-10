namespace Sandbox;

/// <summary>Phase 1 — heightfield from noise, coastlines, and spec-driven relief (macro regions).</summary>
public sealed class ThornsWorldGenPhaseMacroTerrain : IThornsWorldGenerationPhase
{
	public ThornsWorldGenerationPhaseId Id => ThornsWorldGenerationPhaseId.GenerateMacroTerrain;
	public string Name => "GenerateMacroTerrain";

	public void Execute( ThornsWorldGenerationContext context, ThornsWorldGenerationHostBridge host )
	{
		ThornsTerraingenTerrainRuntime.TryBindConfigsFromScene( host.Scene );
		var worldSeed = context.Spec.TerraingenWorldSeed != 0 ? context.Spec.TerraingenWorldSeed : context.Spec.Seed;
		var field = ThornsTerraingenTerrainRuntime.GetOrGenerateField( worldSeed );
		ThornsTerraingenTerrainRuntime.FillHeightmapBase( context.Spec, context.Heights, field );
	}
}
