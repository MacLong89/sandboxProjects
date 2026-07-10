namespace ThinkDrink.Domain;

/// <summary>Portable trivia question model — no engine dependencies.</summary>
public sealed class TriviaQuestion
{
	public string Id { get; set; } = "";
	public string Category { get; set; } = "";
	public Difficulty Difficulty { get; set; } = Difficulty.Medium;
	public string Question { get; set; } = "";
	public List<string> Accepted { get; set; } = new();
	public List<string> Alternatives { get; set; } = new();
	public List<string> Distractors { get; set; } = new();
	public List<string> Tags { get; set; } = new();
	public string Explanation { get; set; } = "";

	public IEnumerable<string> AllAnswers()
	{
		foreach ( var a in Accepted )
			yield return a;
		foreach ( var a in Alternatives )
			yield return a;
	}
}

public sealed class QuestionBank
{
	public int Version { get; set; } = 1;
	public List<TriviaQuestion> Questions { get; set; } = new();
}
