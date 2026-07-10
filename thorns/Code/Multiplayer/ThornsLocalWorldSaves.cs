using System.Globalization;

namespace Sandbox;

/// <summary>Lists listen-host world files under <see cref="ThornsHostSavePaths.SavesFolderPrefix"/> (one file per map / server name).</summary>
public static class ThornsLocalWorldSaves
{
	public readonly struct Entry
	{
		public Entry( string relativePath, string displayLabel )
		{
			RelativePath = relativePath;
			DisplayLabel = displayLabel;
		}

		/// <summary><see cref="ThornsWorldPersistence.RelativeSavePath"/>-style path under <see cref="FileSystem.Data"/>.</summary>
		public string RelativePath { get; }

		/// <summary>Human label derived from the <c>world_*.json</c> filename (for lobby title + UI).</summary>
		public string DisplayLabel { get; }
	}

	/// <summary>All <c>world_*.json</c> files in the saves folder, sorted by display name.</summary>
	public static List<Entry> ListWorldSaves()
	{
		var found = new List<Entry>();
		try
		{
			if ( !FileSystem.Data.DirectoryExists( ThornsHostSavePaths.SavesFolderPrefix ) )
				return found;

			foreach ( var path in FileSystem.Data.FindFile( ThornsHostSavePaths.SavesFolderPrefix, "world_*.json" ) )
			{
				var piece = path?.ToString();
				if ( string.IsNullOrEmpty( piece ) )
					continue;

				piece = piece.Replace( '\\', '/' );
				var rel = piece.StartsWith( ThornsHostSavePaths.SavesFolderPrefix, StringComparison.OrdinalIgnoreCase )
					? piece
					: $"{ThornsHostSavePaths.SavesFolderPrefix}/{piece.TrimStart( '/' )}";
				var label = DisplayNameFromWorldRelativePath( rel );
				found.Add( new Entry( rel, label ) );
			}
		}
		catch ( Exception e )
		{
			Log.Warning( e, "[Thorns] Local world saves: failed to scan saves folder." );
		}

		found.Sort( ( a, b ) => string.Compare( a.DisplayLabel, b.DisplayLabel, StringComparison.OrdinalIgnoreCase ) );
		return found;
	}

	/// <summary>Derives a title from <c>Thorns/saves/world_my_cool_map.json</c> → "My Cool Map".</summary>
	public static string DisplayNameFromWorldRelativePath( string relativePath )
	{
		if ( string.IsNullOrWhiteSpace( relativePath ) )
			return "World";

		var file = System.IO.Path.GetFileName( relativePath );
		if ( string.IsNullOrEmpty( file ) )
			return "World";

		var noExt = System.IO.Path.GetFileNameWithoutExtension( file );
		if ( !noExt.StartsWith( "world_", StringComparison.OrdinalIgnoreCase ) )
			return noExt;

		var slug = noExt[6..];
		return SlugToDisplayName( slug );
	}

	static string SlugToDisplayName( string slug )
	{
		if ( string.IsNullOrEmpty( slug ) )
			return "World";

		var parts = slug.Split( '_', StringSplitOptions.RemoveEmptyEntries );
		if ( parts.Length == 0 )
			return "World";

		for ( var i = 0; i < parts.Length; i++ )
		{
			if ( parts[i].Length == 0 )
				continue;
			var lower = parts[i].ToLowerInvariant();
			parts[i] = char.ToUpper( lower[0], CultureInfo.InvariantCulture ) + ( lower.Length > 1 ? lower[1..] : "" );
		}

		return string.Join( " ", parts );
	}
}
