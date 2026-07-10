namespace Dynasty.Core.Events;

/// <summary>
/// In-process event bus for decoupled system communication.
/// Thread-safe for single-threaded game tick usage; not intended for cross-process use.
/// </summary>
public sealed class LeagueEventBus
{
	private long _sequence;
	private readonly List<ILeagueEvent> _history = new();
	private readonly Dictionary<Type, List<Delegate>> _handlers = new();

	public IReadOnlyList<ILeagueEvent> History => _history;

	public long NextSequence() => ++_sequence;

	public void Subscribe<TEvent>( Action<TEvent> handler ) where TEvent : ILeagueEvent
	{
		var type = typeof( TEvent );
		if ( !_handlers.TryGetValue( type, out var list ) )
		{
			list = new List<Delegate>();
			_handlers[type] = list;
		}

		list.Add( handler );
	}

	public void Unsubscribe<TEvent>( Action<TEvent> handler ) where TEvent : ILeagueEvent
	{
		if ( !_handlers.TryGetValue( typeof( TEvent ), out var list ) )
			return;

		list.Remove( handler );
	}

	public void Publish<TEvent>( TEvent leagueEvent ) where TEvent : ILeagueEvent
	{
		_history.Add( leagueEvent );

		if ( !_handlers.TryGetValue( typeof( TEvent ), out var list ) )
			return;

		foreach ( var handler in list.ToArray() )
		{
			if ( handler is Action<TEvent> action )
			{
				action( leagueEvent );
				continue;
			}

			handler.DynamicInvoke( leagueEvent );
		}
	}

	public IEnumerable<TEvent> GetEventsSince<TEvent>( long sequence ) where TEvent : ILeagueEvent
	{
		return _history.OfType<TEvent>().Where( e => e.Sequence > sequence );
	}

	public void ClearHistory() => _history.Clear();

	public void ClearHandlers() => _handlers.Clear();
}
