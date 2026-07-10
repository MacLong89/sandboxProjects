namespace Sandbox;

/// <summary>Centralized transitions with debug logging.</summary>
public sealed class ThornsAnimalStateMachine
{
	readonly Dictionary<ThornsWildlifeAiState, IThornsAnimalState> _states = new();
	IThornsAnimalState _current;

	public ThornsWildlifeAiState CurrentStateId => _current?.StateId ?? ThornsWildlifeAiState.Idle;
	public ThornsWildlifeAiState PreviousStateId { get; private set; } = ThornsWildlifeAiState.Idle;

	public void Register( IThornsAnimalState state )
	{
		_states[state.StateId] = state;
	}

	public void Initialize( ThornsAnimalBrainContext ctx, ThornsWildlifeAiState initial )
	{
		if ( !_states.TryGetValue( initial, out var state ) )
		{
			initial = ThornsWildlifeAiState.Wander;
			_states.TryGetValue( initial, out state );
		}

		_current = state;
		ctx.CurrentState = initial;
		ctx.PreviousState = initial;
		PreviousStateId = initial;
		_current?.OnEnter( ctx );
	}

	public bool TryTransition( ThornsAnimalBrainContext ctx, ThornsWildlifeAiState next, string reason = null )
	{
		if ( _current is not null && _current.StateId == next )
			return false;

		if ( !_states.TryGetValue( next, out var nextState ) )
			return false;

		if ( next != ThornsWildlifeAiState.Dead && Time.Now < ctx.NextStateChangeAllowedRealtime )
			return false;

		var prev = _current?.StateId ?? ctx.CurrentState;
		_current?.OnExit( ctx );

		PreviousStateId = prev;
		ctx.PreviousState = prev;
		ctx.CurrentState = next;
		_current = nextState;

		ctx.Brain.StateMachineHostPrepareContextForState( ctx, next );
		ThornsWildlifeLog.Transition( ctx.Self?.Name ?? "wildlife", prev, next );
		ctx.AnimSync?.HostSetAiState( next );
		ctx.Brain.HostClearConflictingLocomotionContext( next );

		var profile = ctx.BehaviorProfile;
		if ( profile.Species == default && ctx.Identity.IsValid() )
			profile = ThornsAnimalBehaviorProfile.Get( ctx.Identity.Species );
		ctx.NextStateChangeAllowedRealtime = Time.Now + Math.Max( 0.35f, profile.StateChangeCooldownSeconds );

		_current.OnEnter( ctx );
		return true;
	}

	public void Think( ThornsAnimalBrainContext ctx, ThornsWildlifeDirector director, Vector3 selfFlat )
	{
		_current?.Think( ctx, director, selfFlat );
	}

	public void SyncMotorWish( ThornsAnimalBrainContext ctx, ThornsWildlifeMotor motor, Vector3 selfFlat )
	{
		_current?.SyncMotorWish( ctx, motor, selfFlat );
	}
}
