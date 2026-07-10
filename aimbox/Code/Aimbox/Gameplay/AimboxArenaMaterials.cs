namespace Sandbox;

/// <summary>Loads and caches aim arena materials from checked-in <c>materials/*.vmat</c> assets.</summary>
public static class AimboxArenaMaterials
{
	public const string PathPrefix = "materials/building_materials/";
	public const string DefaultMaterialPath = "materials/building_materials/concrete.vmat";

	static readonly Dictionary<AimboxArenaSurface, Material> _cache = [];
	static readonly HashSet<AimboxArenaSurface> _warnedMissing = [];
	static Material _defaultMaterial;

	[ConCmd( "aimbox_material_reset" )]
	public static void ResetCacheConsole() => ResetCache( "console" );

	public static void ResetCache( string reason )
	{
		_cache.Clear();
		_warnedMissing.Clear();
		_defaultMaterial = null;
		AimboxArenaMaterialDebug.ResetCounters();
		Log.Info( $"[Aimbox MatDbg] Arena material cache reset ({reason})." );
	}

	public static Material Get( AimboxArenaSurface surface )
	{
		if ( surface == AimboxArenaSurface.Solid )
		{
			AimboxArenaMaterialDebug.LogDefaultMaterialEvent( "solid-fallback-request", _defaultMaterial );
			return DefaultMaterial();
		}

		if ( !surface.HasMaterial() )
		{
			AimboxArenaMaterialDebug.LogDefaultMaterialEvent( $"no-material-fallback:{surface}", _defaultMaterial );
			return DefaultMaterial();
		}

		if ( _cache.TryGetValue( surface, out var cached ) && IsUsable( cached ) )
		{
			if ( IsExpectedSurfaceMaterial( surface, cached ) )
			{
				AimboxArenaMaterialDebug.LogMaterialEvent( "cache-hit", surface, cached );
				return cached;
			}

			AimboxArenaMaterialDebug.LogMaterialEvent( "cache-rejected-stale-resource", surface, cached );
			_cache.Remove( surface );
		}

		if ( cached is not null && !IsUsable( cached ) )
			AimboxArenaMaterialDebug.LogMaterialEvent( "cache-stale", surface, cached );

		var material = LoadMaterial( surface );
		if ( !IsUsable( material ) )
		{
			AimboxArenaMaterialDebug.LogMaterialEvent( "load-unusable-using-default", surface, material );
			material = DefaultMaterial();
		}

		if ( !IsUsable( material ) )
		{
			AimboxArenaMaterialDebug.LogDefaultMaterialEvent( "default-unusable", material );
			return null;
		}

		_cache[surface] = material;
		AimboxArenaMaterialDebug.LogMaterialEvent( "cached", surface, material );
		return material;
	}

	public static string MaterialPath( AimboxArenaSurface surface )
	{
		var slug = surface.MaterialSlug();
		if ( string.IsNullOrEmpty( slug ) )
			return null;

		return $"{PathPrefix}{slug}.vmat";
	}

	static Material LoadMaterial( AimboxArenaSurface surface )
	{
		var path = MaterialPath( surface );
		if ( string.IsNullOrEmpty( path ) )
			return null;

		var material = Material.Load( path );
		AimboxArenaMaterialDebug.LogMaterialEvent( "load-result", surface, material );
		if ( IsUsable( material ) )
		{
			Log.Info( $"[Aimbox] Arena material ready: {surface} -> {path}" );
			return material;
		}

		LogMissingOnce( surface, $"material missing or invalid at {path}" );
		return null;
	}

	static Material DefaultMaterial()
	{
		if ( IsUsable( _defaultMaterial ) && ResourceMatches( _defaultMaterial, DefaultMaterialPath ) )
			return _defaultMaterial;

		if ( IsUsable( _defaultMaterial ) )
			AimboxArenaMaterialDebug.LogDefaultMaterialEvent( "default-rejected-stale-resource", _defaultMaterial );

		_defaultMaterial = Material.Load( DefaultMaterialPath );
		AimboxArenaMaterialDebug.LogDefaultMaterialEvent( "default-load-result", _defaultMaterial );
		return _defaultMaterial;
	}

	static bool IsUsable( Material material )
	{
		return material is not null && material.IsValid() && !material.IsError;
	}

	static bool IsExpectedSurfaceMaterial( AimboxArenaSurface surface, Material material )
	{
		var path = MaterialPath( surface );
		return string.IsNullOrWhiteSpace( path ) || ResourceMatches( material, path );
	}

	static bool ResourceMatches( Material material, string expectedPath )
	{
		if ( material is null || !material.IsValid() || string.IsNullOrWhiteSpace( expectedPath ) )
			return false;

		var expected = NormalizePath( expectedPath );
		var resourcePath = NormalizePath( SafeResource( material.ResourcePath ) );
		var resourceName = NormalizePath( SafeResource( material.ResourceName ) );

		return string.Equals( resourcePath, expected, StringComparison.OrdinalIgnoreCase )
		       || string.Equals( resourceName, expected, StringComparison.OrdinalIgnoreCase );
	}

	static string SafeResource( string resource )
	{
		return string.IsNullOrWhiteSpace( resource ) ? "" : resource.Trim();
	}

	static string NormalizePath( string path )
	{
		return string.IsNullOrWhiteSpace( path )
			? ""
			: path.Trim().Replace( '\\', '/' );
	}

	static void LogMissingOnce( AimboxArenaSurface surface, string reason )
	{
		if ( _warnedMissing.Add( surface ) )
			Log.Warning( $"[Aimbox] Arena material unavailable for {surface}: {reason}." );
	}
}
