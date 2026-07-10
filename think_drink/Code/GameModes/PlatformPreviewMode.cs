namespace ThinkDrink.GameModes;

public sealed class PlatformPreviewMode : GameModeBase
{
	private readonly GameModeDefinition _definition;
	private readonly GameModeRoundSettings _settings;

	public PlatformPreviewMode( GameModeDefinition definition, GameModeRoundSettings settings = null )
	{
		_definition = definition;
		_settings = settings ?? new GameModeRoundSettings();
	}

	public override GameModeDefinition Definition => _definition;

	public override GameModeRoundSettings GetRoundSettings( int roundNumber, RandomEventType activeEvent ) => _settings;

	public override string GetScreenLabel( MatchPhase phase ) => phase switch
	{
		MatchPhase.CategoryReveal => _settings.CategoryLabel,
		MatchPhase.QuestionReveal => _settings.QuestionLabel,
		MatchPhase.BuzzIn => _settings.BuzzLabel,
		MatchPhase.Answering => _settings.AnswerLabel,
		_ => base.GetScreenLabel( phase )
	};
}
