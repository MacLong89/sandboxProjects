namespace ThinkDrink.Domain;

public interface IGameMode
{
	GameModeDefinition Definition { get; }
	void Initialize( MatchManager match );
	void StartRound( int roundNumber );
	void EndRound();
	void Cleanup();
	GameModePrompt BuildPrompt( TriviaQuestion source, int roundNumber, Random random );
	AnswerValidationResult ValidateAnswer( string submission, GameModePrompt prompt, TriviaQuestion source, IAnswerValidator validator );
	int AwardPoints( TriviaQuestion question, RandomEventType activeEvent, bool isSteal, float responseTime );
	bool HandlePlayerInput( GameModeInput input );
	ThinkDrinkPlayer DetermineWinner();
	GameModeRoundSettings GetRoundSettings( int roundNumber, RandomEventType activeEvent );
	string GetScreenLabel( MatchPhase phase );
}
