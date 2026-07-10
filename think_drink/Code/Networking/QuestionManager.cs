namespace ThinkDrink;

/// <summary>Loads and caches the trivia question bank on the host.</summary>
public sealed class QuestionManager : Component
{
	public static QuestionManager Instance { get; private set; }

	private JsonQuestionRepository _repository = new();
	private readonly QuestionSelectionService _selector = new();
	private readonly AnswerValidationService _validator = new();

	public IQuestionRepository Repository => _repository;
	public IQuestionSelector Selector => _selector;
	public IAnswerValidator Validator => _validator;

	protected override void OnAwake() => Instance = this;

	protected override void OnStart()
	{
		if ( Networking.IsHost )
			_repository.Load();
	}

	protected override void OnDestroy()
	{
		if ( Instance == this ) Instance = null;
	}

	public TriviaQuestion PickQuestion( Queue<string> recentIds, string category, Random random )
	{
		return PickQuestion( recentIds?.ToHashSet() ?? new HashSet<string>(), category, random );
	}

	public TriviaQuestion PickQuestion( IReadOnlyCollection<string> usedIds, string category, Random random )
	{
		var usedSet = usedIds is null ? new HashSet<string>() : new HashSet<string>( usedIds );
		var pool = string.IsNullOrEmpty( category )
			? _repository.GetAll()
			: _repository.GetByCategory( category );

		if ( pool.Count == 0 )
			pool = _repository.GetAll();

		if ( pool.Count > 0 && pool.All( q => usedSet.Contains( q.Id ) ) )
		{
			var all = _repository.GetAll();
			var globalUnused = all.Where( q => !usedSet.Contains( q.Id ) ).ToList();
			if ( globalUnused.Count > 0 )
				pool = globalUnused;
		}

		return _selector.SelectNext( pool, usedSet, category, random );
	}
}
