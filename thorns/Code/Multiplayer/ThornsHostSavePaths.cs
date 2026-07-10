namespace Sandbox;

/// <summary>Maps a player-visible server name to a stable file under <see cref="FileSystem.Data"/> for listen-host worlds.</summary>
public static class ThornsHostSavePaths
{
	public const string SavesFolderPrefix = "Thorns/saves";

	/// <summary>Relative path (under data) for a hosted world keyed by <paramref name="serverDisplayName"/>.</summary>
	public static string PersistencePathForServerName( string serverDisplayName )
	{
		var trimmed = serverDisplayName?.Trim() ?? "";
		if ( string.IsNullOrEmpty( trimmed ) )
			trimmed = "Thorns";

		var slug = SlugFilePart( trimmed );
		return $"{SavesFolderPrefix}/world_{slug}.json";
	}

	static string SlugFilePart( string displayName )
	{
		var sb = new System.Text.StringBuilder();
		foreach ( var c in displayName.ToLowerInvariant() )
		{
			if ( char.IsLetterOrDigit( c ) )
				sb.Append( c );
			else if ( char.IsWhiteSpace( c ) || c is '_' or '-' )
				sb.Append( '_' );
		}

		var s = sb.ToString().Trim( '_' );
		while ( s.Contains( "__", StringComparison.Ordinal ) )
			s = s.Replace( "__", "_", StringComparison.Ordinal );

		if ( string.IsNullOrEmpty( s ) )
			s = "world";

		if ( s.Length > 48 )
			s = s[..48].TrimEnd( '_' );

		return s;
	}
}
