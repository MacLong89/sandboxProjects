namespace ThinkDrink;

public readonly struct CreativeVoteOption
{
	public CreativeVoteOption( string letter, string text )
	{
		Letter = letter;
		Text = text;
	}

	public string Letter { get; }
	public string Text { get; }
}

public static class CreativeVoteCodec
{
	public const char OptionSeparator = '\n';
	public const char FieldSeparator = '|';

	public static string Encode( IEnumerable<(string Letter, string Text)> options )
	{
		var parts = options
			.Where( o => !string.IsNullOrWhiteSpace( o.Text ) )
			.Select( o => $"{o.Letter}{FieldSeparator}{Sanitize( o.Text )}" );
		return string.Join( OptionSeparator, parts );
	}

	public static List<CreativeVoteOption> Decode( string pack )
	{
		var list = new List<CreativeVoteOption>();
		if ( string.IsNullOrWhiteSpace( pack ) ) return list;

		var lines = pack.Split( OptionSeparator, StringSplitOptions.RemoveEmptyEntries );
		for ( var i = 0; i < lines.Length; i++ )
		{
			var line = lines[i];
			var split = line.IndexOf( FieldSeparator );
			if ( split <= 0 ) continue;

			var letter = line[..split].Trim();
			var text = line[(split + 1)..].Trim();
			if ( letter.Length == 0 || text.Length == 0 ) continue;

			list.Add( new CreativeVoteOption( letter, text ) );
		}

		return list;
	}

	public static string SanitizeSubmission( string text )
	{
		if ( string.IsNullOrWhiteSpace( text ) ) return "";

		text = text.Trim().Replace( '\r', ' ' ).Replace( '\n', ' ' );
		if ( text.Length > 120 )
			text = text[..120];

		return text;
	}

	static string Sanitize( string text ) => text.Replace( FieldSeparator, '/' ).Replace( OptionSeparator, ' ' );
}
