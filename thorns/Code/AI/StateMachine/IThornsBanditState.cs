namespace Sandbox;

public interface IThornsBanditState
{
	ThornsBanditAiState StateId { get; }
	string DebugName { get; }

	void OnEnter( ThornsBanditBrainContext ctx );
	void OnExit( ThornsBanditBrainContext ctx );
	void Tick( ThornsBanditBrainContext ctx, ThornsBanditDirector director, Vector3 selfFlat );
	bool CanTransition( ThornsBanditBrainContext ctx, ThornsBanditAiState next );
}
