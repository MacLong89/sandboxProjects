namespace ThinkDrink.Services;

/// <summary>
/// Intelligent answer matching with normalization, synonyms, aliases, and fuzzy tolerance.
/// Engine-agnostic — portable to Unity, Roblox, browser, etc.
/// </summary>
public sealed class AnswerValidationService : IAnswerValidator
{
	public const float DefaultFuzzyThreshold = 0.82f;
	public const int MaxSubmissionLength = 64;

	private readonly float _fuzzyThreshold;

	public AnswerValidationService( float fuzzyThreshold = DefaultFuzzyThreshold )
	{
		_fuzzyThreshold = fuzzyThreshold;
	}

	public AnswerValidationResult Validate( string submission, TriviaQuestion question )
	{
		if ( question is null || string.IsNullOrWhiteSpace( submission ) )
			return Fail();

		var input = Normalize( submission );
		if ( input.Length == 0 || input.Length > MaxSubmissionLength )
			return Fail();

		foreach ( var answer in question.AllAnswers() )
		{
			var normalized = Normalize( answer );
			if ( normalized.Length == 0 ) continue;

			if ( input == normalized )
				return Success( answer, 1f );

			if ( IsPluralMatch( input, normalized ) )
				return Success( answer, 0.98f );

			if ( IsAbbreviationMatch( input, normalized ) )
				return Success( answer, 0.95f );
		}

		var bestScore = 0f;
		var bestAnswer = "";

		foreach ( var answer in question.AllAnswers() )
		{
			var normalized = Normalize( answer );
			if ( normalized.Length == 0 ) continue;

			var score = FuzzyScore( input, normalized );
			if ( score > bestScore )
			{
				bestScore = score;
				bestAnswer = answer;
			}
		}

		if ( bestScore >= _fuzzyThreshold )
			return Success( bestAnswer, bestScore );

		return Fail();
	}

	private static AnswerValidationResult Success( string matched, float confidence ) => new()
	{
		IsCorrect = true,
		MatchedAnswer = matched,
		Confidence = confidence
	};

	private static AnswerValidationResult Fail() => new() { IsCorrect = false };

	public static string Normalize( string value )
	{
		if ( string.IsNullOrWhiteSpace( value ) ) return "";

		value = value.Trim().ToLowerInvariant();

		var chars = new char[value.Length];
		var count = 0;
		var lastSpace = false;

		for ( var i = 0; i < value.Length; i++ )
		{
			var c = value[i];
			if ( char.IsLetterOrDigit( c ) )
			{
				chars[count++] = c;
				lastSpace = false;
			}
			else if ( c is ' ' or '-' or '.' or '/' or '\'' )
			{
				if ( !lastSpace && count > 0 )
				{
					chars[count++] = ' ';
					lastSpace = true;
				}
			}
		}

		return count == 0 ? "" : new string( chars, 0, count ).Trim();
	}

	private static bool IsPluralMatch( string a, string b )
	{
		if ( a == b ) return true;
		if ( a.Length > 2 && a.EndsWith( "s" ) && a[..^1] == b ) return true;
		if ( b.Length > 2 && b.EndsWith( "s" ) && b[..^1] == a ) return true;
		if ( a.Length > 3 && a.EndsWith( "es" ) && a[..^2] == b ) return true;
		if ( b.Length > 3 && b.EndsWith( "es" ) && b[..^2] == a ) return true;
		return false;
	}

	private static bool IsAbbreviationMatch( string input, string answer )
	{
		if ( input.Length > answer.Length ) return false;

		var parts = answer.Split( ' ', StringSplitOptions.RemoveEmptyEntries );
		if ( parts.Length < 2 ) return false;

		var abbrev = "";
		for ( var i = 0; i < parts.Length; i++ )
			abbrev += parts[i][0];

		if ( input == abbrev ) return true;

		var noSpace = answer.Replace( " ", "" );
		if ( input == noSpace ) return true;

		return false;
	}

	private static float FuzzyScore( string a, string b )
	{
		if ( a == b ) return 1f;
		if ( a.Length == 0 || b.Length == 0 ) return 0f;

		var distance = LevenshteinDistance( a, b );
		var maxLen = Math.Max( a.Length, b.Length );
		return 1f - distance / (float)maxLen;
	}

	private static int LevenshteinDistance( string a, string b )
	{
		var n = a.Length;
		var m = b.Length;
		var d = new int[n + 1, m + 1];

		for ( var i = 0; i <= n; i++ ) d[i, 0] = i;
		for ( var j = 0; j <= m; j++ ) d[0, j] = j;

		for ( var i = 1; i <= n; i++ )
		{
			for ( var j = 1; j <= m; j++ )
			{
				var cost = a[i - 1] == b[j - 1] ? 0 : 1;
				d[i, j] = Math.Min(
					Math.Min( d[i - 1, j] + 1, d[i, j - 1] + 1 ),
					d[i - 1, j - 1] + cost );
			}
		}

		return d[n, m];
	}
}
