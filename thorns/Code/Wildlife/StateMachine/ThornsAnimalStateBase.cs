namespace Sandbox;

/// <summary>Shared defaults for animal AI states.</summary>
public abstract class ThornsAnimalStateBase : IThornsAnimalState
{
	public abstract ThornsWildlifeAiState StateId { get; }

	public virtual void OnEnter( ThornsAnimalBrainContext ctx ) { }
	public virtual void OnExit( ThornsAnimalBrainContext ctx ) { }

	public abstract void Think( ThornsAnimalBrainContext ctx, ThornsWildlifeDirector director, Vector3 selfFlat );

	public virtual void SyncMotorWish( ThornsAnimalBrainContext ctx, ThornsWildlifeMotor motor, Vector3 selfFlat ) =>
		ctx.Brain.StateMachineHostSyncMotorWishDefault( ctx, motor, selfFlat, StateId );
}
