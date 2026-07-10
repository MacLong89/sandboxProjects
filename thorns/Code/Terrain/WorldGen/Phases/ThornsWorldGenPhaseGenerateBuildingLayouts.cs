namespace Sandbox;

/// <summary>Phase 7 — initializes blueprint layout factory (layouts compiled per slot during phase 8).</summary>
public sealed class ThornsWorldGenPhaseGenerateBuildingLayouts : IThornsWorldGenerationPhase
{
	public ThornsWorldGenerationPhaseId Id => ThornsWorldGenerationPhaseId.GenerateBuildingLayouts;
	public string Name => "GenerateBuildingLayouts";

	public void Execute( ThornsWorldGenerationContext context, ThornsWorldGenerationHostBridge host )
	{
		context.LayoutFactory = new ThornsWorldGenBuildingLayoutFactory( context.PlacementRng );
	}
}
