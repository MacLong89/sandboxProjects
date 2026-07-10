namespace Terraingen;

/// <summary>Loads .vmdl assets without spamming the resource system when paths are not mounted.</summary>
public static class ThornsModelResourceLoad
{
	public const string DevBoxPath = "models/dev/box.vmdl";

	/// <summary>When true, load attempts log mount + model error state (floorplan material debug).</summary>
	public static bool VerboseLoadLogging { get; set; }

	public static int MaxVerboseLoadInfoLogs { get; set; } = 24;

	static int _verboseLoadInfoCount;

	public static void ResetVerboseLoadLogCount() => _verboseLoadInfoCount = 0;

	public static bool MountedVmdlExists( string path )
	{
		if ( string.IsNullOrWhiteSpace( path ) )
			return false;

		var normalized = path.Trim().Replace( '\\', '/' );
		return ThornsMountedFiles.Exists( normalized )
		       || ThornsMountedFiles.Exists( ThornsContentPath.Normalize( normalized ) );
	}

	public static bool IsUsable( Model model )
	{
		if ( model is null )
			return false;

		return model.IsValid && !model.IsError && !IsDevPlaceholderModel( model );
	}

	/// <summary>
	/// Cloud.Model and Model.Load can return the dev error mesh while still reporting IsError=false.
	/// Treat those placeholders as unusable so clutter/trees fall back to local assets.
	/// </summary>
	public static bool IsDevPlaceholderModel( Model model )
	{
		if ( model is null || !model.IsValid )
			return false;

		var path = (model.ResourcePath ?? model.Name ?? "").Replace( '\\', '/' ).ToLowerInvariant();
		if ( string.IsNullOrWhiteSpace( path ) )
			return false;

		return path.Contains( "models/dev/error" )
		       || path.Contains( "models/dev/missing" )
		       || path.EndsWith( "/error.vmdl" );
	}

	public static Model TryLoad( string path ) => TryLoadPath( path );

	public static bool TryLoadUsable( string path, out Model model )
	{
		model = TryLoadPath( path );
		return IsUsable( model );
	}

	public static Model LoadOrFallback( string path, string fallbackPath = DevBoxPath )
	{
		var primary = TryLoadPath( path );
		if ( IsUsable( primary ) )
			return primary;

		if ( !string.Equals( path, fallbackPath, StringComparison.OrdinalIgnoreCase ) )
		{
			var fallback = TryLoadPath( fallbackPath );
			if ( IsUsable( fallback ) )
				return fallback;
		}

		return default;
	}

	static Model TryLoadPath( string path )
	{
		if ( string.IsNullOrWhiteSpace( path ) )
			return default;

		if ( !ShouldAttemptModelLoad( path ) )
		{
			if ( VerboseLoadLogging )
				Log.Warning( $"[Thorns ModelLoad] skipping load for non-model path: '{path}'." );

			return default;
		}

		var model = Model.Load( path );
		if ( VerboseLoadLogging && TryVerboseLoadInfo() )
		{
			var valid = model is not null && model.IsValid;
			Log.Info(
				$"[Thorns ModelLoad] path={path} mounted={MountedVmdlExists( path )} valid={valid} error={( valid ? model.IsError : false )} name={( valid ? model.Name : "—" )}" );
		}

		if ( IsUsable( model ) )
			return model;

		if ( VerboseLoadLogging )
			Log.Warning(
				$"[Thorns ModelLoad] unusable model '{path}' (null={model is null} valid={model is not null && model.IsValid} error={model is not null && model.IsError})." );

		return default;
	}

	/// <summary>
	/// Project .vmdl compile into the game package and load via <see cref="Model.Load"/> even when absent from
	/// <see cref="FileSystem.Mounted"/> (published builds only expose loose sources on mount).
	/// </summary>
	static bool ShouldAttemptModelLoad( string path )
	{
		var normalized = path.Trim().Replace( '\\', '/' ).ToLowerInvariant();
		return normalized.StartsWith( "models/" );
	}

	static bool TryVerboseLoadInfo()
	{
		if ( _verboseLoadInfoCount >= MaxVerboseLoadInfoLogs )
			return false;

		_verboseLoadInfoCount++;
		return true;
	}
}
