namespace ThinkDrink;

/// <summary>Decoupled game events for UI/audio/system reactions.</summary>
public static class GameEvents
{
	public static event Action<MatchPhase> PhaseChanged;
	public static event Action<TriviaQuestion> QuestionShown;
	public static event Action<string, bool> AnswerResolved;
	public static event Action<string> BuzzWinner;
	public static event Action<MatchResult> MatchEnded;
	public static event Action<AchievementDefinition> AchievementUnlocked;
	public static event Action<RandomEventType> RandomEventTriggered;
	public static event Action<GameModeId> GameModeChanged;
	public static event Action<AudioEventId> AudioRequested;
	public static event Action PlayerListChanged;
	public static event Action<string, int, bool> ScoreAwarded;
	public static event Action<string, bool> PredictionResolved;
	public static event Action<string> BuzzRejected;
	public static event Action<string, int> LevelUp;

	public static void RaisePhaseChanged( MatchPhase phase ) => PhaseChanged?.Invoke( phase );
	public static void RaiseQuestionShown( TriviaQuestion q ) => QuestionShown?.Invoke( q );
	public static void RaiseAnswerResolved( string steamId, bool correct ) => AnswerResolved?.Invoke( steamId, correct );
	public static void RaiseBuzzWinner( string steamId ) => BuzzWinner?.Invoke( steamId );
	public static void RaiseMatchEnded( MatchResult result ) => MatchEnded?.Invoke( result );
	public static void RaiseAchievementUnlocked( AchievementDefinition def ) => AchievementUnlocked?.Invoke( def );
	public static void RaiseRandomEvent( RandomEventType evt ) => RandomEventTriggered?.Invoke( evt );
	public static void RaiseGameModeChanged( GameModeId mode ) => GameModeChanged?.Invoke( mode );
	public static void RaiseAudio( AudioEventId id ) => AudioRequested?.Invoke( id );
	public static void RaisePlayerListChanged() => PlayerListChanged?.Invoke();
	public static void RaiseScoreAwarded( string steamKey, int points, bool isSteal ) => ScoreAwarded?.Invoke( steamKey, points, isSteal );
	public static void RaisePredictionResolved( string steamKey, bool correct ) => PredictionResolved?.Invoke( steamKey, correct );
	public static void RaiseBuzzRejected( string steamKey ) => BuzzRejected?.Invoke( steamKey );
	public static void RaiseLevelUp( string steamKey, int newLevel ) => LevelUp?.Invoke( steamKey, newLevel );
}
