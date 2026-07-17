namespace FinalOutpost;

/// <summary>
/// Load helpers that never throw / spam fatal errors when an asset path is missing
/// (fresh install without optional packages, missing engine-dev meshes, etc.).
/// </summary>
public static class AssetSafe
{
	private static readonly HashSet<string> LoggedMissing = new( StringComparer.OrdinalIgnoreCase );

	public static Model Model( string path, Model fallback = null )
	{
		if ( string.IsNullOrWhiteSpace( path ) )
			return fallback;

		try
		{
			var model = Sandbox.Model.Load( path );
			if ( model is not null && !model.IsError )
				return model;
		}
		catch ( Exception e )
		{
			WarnOnce( path, e.Message );
			return fallback;
		}

		WarnOnce( path, "missing or error model" );
		return fallback;
	}

	public static Material Material( string path, Material fallback = null )
	{
		if ( string.IsNullOrWhiteSpace( path ) )
			return fallback;

		try
		{
			var mat = Sandbox.Material.Load( path );
			if ( mat is not null && mat.IsValid() )
				return mat;
		}
		catch ( Exception e )
		{
			WarnOnce( path, e.Message );
			return fallback;
		}

		WarnOnce( path, "missing or invalid material" );
		return fallback;
	}

	private static void WarnOnce( string path, string detail )
	{
		if ( !LoggedMissing.Add( path ) )
			return;

		Log.Warning( $"[FinalOutpost] Asset unavailable '{path}' ({detail}) — using fallback if available." );
	}
}
