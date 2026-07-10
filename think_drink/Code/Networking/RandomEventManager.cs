namespace ThinkDrink;

public sealed class RandomEventManager : Component
{
	public static RandomEventManager Instance { get; private set; }

	private RandomEventService _service;

	protected override void OnAwake()
	{
		Instance = this;
		_service = new RandomEventService( Random.Shared );
	}

	protected override void OnDestroy()
	{
		if ( Instance == this ) Instance = null;
	}

	public RandomEventType Roll() =>
		_service?.Roll( MatchPhase.AnswerReveal ) ?? RandomEventType.None;
}
