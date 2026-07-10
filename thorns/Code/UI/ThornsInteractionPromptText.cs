namespace Sandbox;

/// <summary>Title-style copy for crosshair / proximity / tame prompts (every word capitalized).</summary>
public static class ThornsInteractionPromptText
{
	public static string Format( string text )
	{
		if ( string.IsNullOrEmpty( text ) )
			return text ?? "";

		if ( !text.Contains( '\n' ) )
			return FormatLine( text );

		var lines = text.Split( '\n' );
		for ( var i = 0; i < lines.Length; i++ )
			lines[i] = FormatLine( lines[i] );

		return string.Join( "\n", lines );
	}

	static string FormatLine( string line )
	{
		if ( string.IsNullOrWhiteSpace( line ) )
			return line;

		var parts = line.Split( ' ' );
		for ( var i = 0; i < parts.Length; i++ )
			parts[i] = FormatToken( parts[i] );

		return string.Join( " ", parts );
	}

	static string FormatToken( string token )
	{
		if ( string.IsNullOrEmpty( token ) )
			return token;

		var letterCount = 0;
		var upperCount = 0;
		foreach ( var c in token )
		{
			if ( !char.IsLetter( c ) )
				continue;

			letterCount++;
			if ( char.IsUpper( c ) )
				upperCount++;
		}

		// Keep short acronyms / key labels (LMB, TAB, HP, E).
		if ( letterCount > 0 && upperCount == letterCount && letterCount <= 4 )
			return token;

		var chars = token.ToCharArray();
		var capNext = true;
		for ( var i = 0; i < chars.Length; i++ )
		{
			if ( char.IsLetter( chars[i] ) )
			{
				chars[i] = capNext ? char.ToUpperInvariant( chars[i] ) : char.ToLowerInvariant( chars[i] );
				capNext = false;
			}
			else if ( chars[i] is '-' or '—' or '/' or '·' )
			{
				capNext = true;
			}
		}

		return new string( chars );
	}
}
