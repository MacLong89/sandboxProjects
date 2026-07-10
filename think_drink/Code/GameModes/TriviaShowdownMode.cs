namespace ThinkDrink.GameModes;

public sealed class TriviaShowdownMode : GameModeBase
{
	public override GameModeDefinition Definition { get; } = new()
	{
		Id = GameModeId.TriviaShowdown,
		DisplayName = "Trivia Showdown",
		ShortName = "SHOWDOWN",
		Description = "Classic buzz-in trivia. First correct answer scores, misses open a steal.",
		Implemented = true,
		SupportsBots = true
	};
}
