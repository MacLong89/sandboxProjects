namespace Sandbox;

public static class ThornsBanditLog
{
	public static bool EnableTransitionLogs { get; set; }

	public static void Transition( string name, ThornsBanditAiState from, ThornsBanditAiState to, string reason = null )
	{
		if ( !EnableTransitionLogs )
			return;

		var r = string.IsNullOrEmpty( reason ) ? "" : $" ({reason})";
		Log.Info( $"[Thorns][BanditAI] {name}: {from} -> {to}{r}" );
	}
}

public sealed class ThornsBanditStateMachine
{
	readonly Dictionary<ThornsBanditAiState, IThornsBanditState> _states = new();
	IThornsBanditState _current;

	public ThornsBanditAiState CurrentStateId => _current?.StateId ?? ThornsBanditAiState.Idle;

	public void Register( IThornsBanditState state ) => _states[state.StateId] = state;

	public void Initialize( ThornsBanditBrainContext ctx, ThornsBanditAiState initial )
	{
		if ( !_states.TryGetValue( initial, out var state ) )
		{
			initial = ThornsBanditAiState.Roam;
			_states.TryGetValue( initial, out state );
		}

		_current = state;
		ctx.CurrentState = initial;
		ctx.PreviousState = initial;
		ctx.LastStateChangeRealtime = Time.Now;
		_current?.OnEnter( ctx );
	}

	public bool TryTransition( ThornsBanditBrainContext ctx, ThornsBanditAiState next, string reason = null )
	{
		if ( _current is not null && _current.StateId == next )
			return false;

		if ( !_states.TryGetValue( next, out var nextState ) )
			return false;

		if ( _current is not null && !_current.CanTransition( ctx, next ) )
			return false;

		var prev = _current?.StateId ?? ctx.CurrentState;
		_current?.OnExit( ctx );

		ctx.PreviousState = prev;
		ctx.CurrentState = next;
		ctx.LastStateChangeRealtime = Time.Now;
		_current = nextState;

		ctx.Brain.StateMachineHostPrepareContextForState( ctx, next );
		ThornsBanditLog.Transition( ctx.Self?.Name ?? "bandit", prev, next, reason );

		_current.OnEnter( ctx );
		return true;
	}

	public void Tick( ThornsBanditBrainContext ctx, ThornsBanditDirector director, Vector3 selfFlat ) =>
		_current?.Tick( ctx, director, selfFlat );
}
