namespace Terraingen.Multiplayer;

/// <summary>Maps a player-visible server name to a stable host-local save file.</summary>
public static class ThornsHostSavePaths
{
	public const string SavesFolderPrefix = "Terraingen/saves";

	public static string PersistencePathForServerName( string serverDisplayName )
	{
		var trimmed = serverDisplayName?.Trim() ?? "";
		if ( string.IsNullOrEmpty( trimmed ) )
			trimmed = "Thorns Terrain";

		return $"{SavesFolderPrefix}/world_{SlugFilePart( trimmed )}.json";
	}

	/// <summary>Stable per-catalog-id save path (display name can change without moving the file).</summary>
	public static string PersistencePathForServerId( string serverId )
	{
		var id = serverId?.Trim() ?? "";
		if ( string.IsNullOrEmpty( id ) )
			id = "world";

		return $"{SavesFolderPrefix}/world_{SlugFilePart( id )}.json";
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
