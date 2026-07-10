namespace Sandbox;

public abstract class ThornsBanditStateBase : IThornsBanditState
{
	public abstract ThornsBanditAiState StateId { get; }
	public virtual string DebugName => StateId.ToString();

	public virtual void OnEnter( ThornsBanditBrainContext ctx ) { }
	public virtual void OnExit( ThornsBanditBrainContext ctx ) { }

	public abstract void Tick( ThornsBanditBrainContext ctx, ThornsBanditDirector director, Vector3 selfFlat );

	public virtual bool CanTransition( ThornsBanditBrainContext ctx, ThornsBanditAiState next ) => true;
}
