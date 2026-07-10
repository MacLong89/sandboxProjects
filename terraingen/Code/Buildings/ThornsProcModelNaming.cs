namespace Terraingen.Buildings;



/// <summary>

/// Sanitizes procedural <see cref="ModelBuilder.WithName"/> identifiers.

/// Avoids path-like names (e.g. terraingen/building/foo) that the resource system tries to load from disk.

/// </summary>

public static class ThornsProcModelNaming

{

	const string Prefix = "proc_building_";



	public static string Sanitize( string resourcePath )

	{

		if ( string.IsNullOrWhiteSpace( resourcePath ) )

			return Prefix + "unnamed";



		var sanitized = resourcePath

			.Replace( "terraingen/building/", "", StringComparison.OrdinalIgnoreCase )

			.Replace( "thorns/building/", "", StringComparison.OrdinalIgnoreCase )

			.Replace( '|', '_' )

			.Replace( '.', 'p' )

			.Replace( '/', '_' )

			.Replace( '\\', '_' )

			.Replace( ' ', '_' );



		if ( sanitized.StartsWith( Prefix, StringComparison.OrdinalIgnoreCase ) )

			return sanitized;



		return Prefix + sanitized;

	}

}


