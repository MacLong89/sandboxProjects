namespace ThinkDrink.Services;

public sealed class RandomEventService
{
	private readonly Random _random;

	public RandomEventService( Random random ) => _random = random;

	public RandomEventType Roll( MatchPhase phase )
	{
		if ( phase != MatchPhase.AnswerReveal ) return RandomEventType.None;
		if ( _random.NextSingle() > GameConstants.RandomEventChance ) return RandomEventType.None;

		var events = new[]
		{
			RandomEventType.DoublePoints,
			RandomEventType.LightningRound,
			RandomEventType.CategorySwap,
			RandomEventType.SuddenDeath
		};

		return events[_random.Next( events.Length )];
	}

	public static string GetDisplayName( RandomEventType evt ) => evt switch
	{
		RandomEventType.DoublePoints => "DOUBLE POINTS!",
		RandomEventType.LightningRound => "LIGHTNING ROUND!",
		RandomEventType.CategorySwap => "CATEGORY SWAP!",
		RandomEventType.SuddenDeath => "SUDDEN DEATH!",
		_ => ""
	};
}
