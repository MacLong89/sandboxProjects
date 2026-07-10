namespace Sandbox;

/// <summary>Phase 10 — resource nodes, boulders, and other wilderness decor on terrain.</summary>
public sealed class ThornsWorldGenPhaseEnvironmentDetails : IThornsWorldGenerationPhase
{
	public ThornsWorldGenerationPhaseId Id => ThornsWorldGenerationPhaseId.GenerateEnvironmentDetails;
	public string Name => "GenerateEnvironmentDetails";

	public void Execute( ThornsWorldGenerationContext context, ThornsWorldGenerationHostBridge host )
	{
		host.Terrain.RunEnvironmentScatter( context.Spec );
	}
}
