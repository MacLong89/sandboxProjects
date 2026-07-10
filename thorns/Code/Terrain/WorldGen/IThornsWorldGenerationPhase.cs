namespace Sandbox;

/// <summary>Single executable step in the Thorns world-generation pipeline.</summary>
public interface IThornsWorldGenerationPhase
{
	ThornsWorldGenerationPhaseId Id { get; }
	string Name { get; }
	void Execute( ThornsWorldGenerationContext context, ThornsWorldGenerationHostBridge host );
}
