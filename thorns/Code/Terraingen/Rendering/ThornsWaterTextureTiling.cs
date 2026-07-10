namespace Terraingen.Rendering;

using Sandbox;

/// <summary>
/// Shared UV tiling for sea-level water. Tiling is applied <b>once</b> via material <c>g_vTexCoordScale</c> on 0–1 mesh UVs
/// (do not also multiply vertex UVs or seams read as a checkerboard).
/// </summary>
public static class ThornsWaterTextureTiling
{
	/// <summary>World inches per texture tile — lower = smaller ripples (too low exposes non-seamless edges).</summary>
	public const float WorldInchesPerTile = 56f;

	/// <summary>Floor tile count when map size is small; scales up with world span.</summary>
	public const float DefaultMinTileRepeat = 144f;

	public const float MinTileRepeatClamp = 48f;
	public const float MaxTileRepeatClamp = 1024f;

	public static float ResolveTileRepeat( float configMinRepeat, float worldSpanInches )
	{
		var span = Math.Max( worldSpanInches, 1024f );
		var sizeBased = span / WorldInchesPerTile;
		return Math.Clamp( Math.Max( configMinRepeat, sizeBased ), MinTileRepeatClamp, MaxTileRepeatClamp );
	}

	/// <summary>Material instance with tiling on <c>g_vTexCoordScale</c> only (vertex UVs stay 0–1).</summary>
	public static Material CreateTiledWaterMaterial( string materialPath, float tileRepeat )
	{
		if ( string.IsNullOrWhiteSpace( materialPath ) )
			return default;

		Material resource = null;
		if ( !ResourceLibrary.TryGet<Material>( materialPath, out resource ) || resource is null )
			resource = Material.Load( materialPath );

		if ( resource is null || !resource.IsValid() )
			return default;

		var copy = resource.CreateCopy();
		var mat = copy.IsValid() ? copy : resource;
		ApplyTileRepeatToMaterial( mat, tileRepeat );
		return mat;
	}

	public static void ApplyTileRepeatToMaterial( Material material, float tileRepeat )
	{
		if ( material is null || !material.IsValid() || material.Attributes is null )
			return;

		var t = Math.Clamp( tileRepeat, MinTileRepeatClamp, MaxTileRepeatClamp );
		material.Attributes.Set( "g_vTexCoordScale", new Vector2( t, t ) );
	}
}
