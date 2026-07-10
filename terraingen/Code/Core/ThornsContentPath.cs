namespace Terraingen;

/// <summary>
/// Normalizes mounted content paths for published games — no <c>Assets/</c> prefix, no leading slash.
/// </summary>
public static class ThornsContentPath
{
	public static string Normalize( string path )
	{
		if ( string.IsNullOrWhiteSpace( path ) )
			return "";

		var trimmed = path.Trim().Replace( '\\', '/' );
		if ( trimmed.StartsWith( "Assets/", StringComparison.OrdinalIgnoreCase ) )
			trimmed = trimmed["Assets/".Length..];

		return trimmed.TrimStart( '/' );
	}

	public static IEnumerable<string> Candidates( string path )
	{
		var normalized = Normalize( path );
		if ( string.IsNullOrWhiteSpace( normalized ) )
			return Array.Empty<string>();

		var candidates = new List<string> { normalized };

		try
		{
			var fsNorm = FileSystem.NormalizeFilename( normalized );
			if ( !string.IsNullOrWhiteSpace( fsNorm ) && !candidates.Contains( fsNorm, StringComparer.OrdinalIgnoreCase ) )
				candidates.Add( fsNorm );
		}
		catch
		{
			// ignore
		}

		return candidates;
	}
}
