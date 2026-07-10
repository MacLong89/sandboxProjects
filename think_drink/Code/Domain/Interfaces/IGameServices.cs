namespace ThinkDrink.Domain;

public interface IPersistenceStore
{
	bool Exists( string path );
	void WriteText( string path, string content );
	string ReadText( string path );
	void EnsureDirectory( string path );
}

public interface IQuestionRepository
{
	IReadOnlyList<TriviaQuestion> GetAll();
	IReadOnlyList<TriviaQuestion> GetByCategory( string category );
	TriviaQuestion GetById( string id );
}

public interface IAnswerValidator
{
	AnswerValidationResult Validate( string submission, TriviaQuestion question );
}

public sealed class AnswerValidationResult
{
	public bool IsCorrect { get; init; }
	public string MatchedAnswer { get; init; } = "";
	public float Confidence { get; init; }
}

public interface IQuestionSelector
{
	TriviaQuestion SelectNext(
		IReadOnlyList<TriviaQuestion> pool,
		IReadOnlyCollection<string> recentIds,
		string preferredCategory,
		Random random );
}

public interface IAchievementEvaluator
{
	IReadOnlyList<AchievementDefinition> Evaluate( PlayerProfile profile, MatchResult match, AchievementCatalog catalog );
}

public interface IChallengeGenerator
{
	IReadOnlyList<ChallengeProgress> GenerateDaily( DateTime utcDate );
	IReadOnlyList<ChallengeProgress> GenerateWeekly( DateTime utcDate );
}

public interface IChallengeTracker
{
	void OnMatchEnd( PlayerProfile profile, MatchResult result );
	void OnCorrectAnswer( PlayerProfile profile, TriviaQuestion question, bool buzzedFirst );
	void OnBuzzWin( PlayerProfile profile );
	List<string> GetCompletedUnclaimed( PlayerProfile profile );
}
