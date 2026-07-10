namespace ThinkDrink.Data;

/// <summary>Loads trivia questions from mounted JSON — data-driven, separate from code.</summary>
public sealed class JsonQuestionRepository : IQuestionRepository
{
	private readonly Dictionary<string, TriviaQuestion> _byId = new();
	private readonly Dictionary<string, List<TriviaQuestion>> _byCategory = new();
	private readonly List<TriviaQuestion> _all = new();

	public IReadOnlyList<TriviaQuestion> GetAll() => _all;

	public IReadOnlyList<TriviaQuestion> GetByCategory( string category )
	{
		if ( string.IsNullOrEmpty( category ) ) return _all;
		return _byCategory.TryGetValue( category, out var list ) ? list : Array.Empty<TriviaQuestion>();
	}

	public TriviaQuestion GetById( string id )
	{
		if ( string.IsNullOrEmpty( id ) ) return null;
		_byId.TryGetValue( id, out var q );
		return q;
	}

	public bool Load()
	{
		_byId.Clear();
		_byCategory.Clear();
		_all.Clear();

		try
		{
			var json = ReadBankJson();
			if ( string.IsNullOrEmpty( json ) )
			{
				Log.Warning( "Think & Drink: question bank not found — using fallback set." );
				LoadFallback();
				return _all.Count > 0;
			}

			var bank = Json.Deserialize<QuestionBank>( json );
			if ( bank?.Questions is null || bank.Questions.Count == 0 )
			{
				LoadFallback();
				return _all.Count > 0;
			}

			RegisterQuestions( bank.Questions );
			Log.Info( $"Think & Drink: loaded {_all.Count} trivia questions." );
			return true;
		}
		catch ( Exception e )
		{
			Log.Warning( $"Think & Drink: question load failed — {e.Message}" );
			LoadFallback();
			return _all.Count > 0;
		}
	}

	private static string ReadBankJson()
	{
		const string path = "data/question_bank.json";
		if ( FileSystem.Mounted.FileExists( path ) )
			return FileSystem.Mounted.ReadAllText( path );
		return null;
	}

	private void RegisterQuestions( IEnumerable<TriviaQuestion> questions )
	{
		foreach ( var q in questions )
		{
			if ( q is null || string.IsNullOrWhiteSpace( q.Id ) ) continue;

			_byId[q.Id] = q;
			_all.Add( q );

			if ( !_byCategory.TryGetValue( q.Category, out var list ) )
			{
				list = new List<TriviaQuestion>();
				_byCategory[q.Category] = list;
			}
			list.Add( q );
		}
	}

	private void LoadFallback()
	{
		RegisterQuestions( FallbackQuestions.Create() );
	}
}
