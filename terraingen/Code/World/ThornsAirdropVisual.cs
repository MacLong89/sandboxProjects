namespace Terraingen.World;

using Terraingen.Buildings;

/// <summary>Textured dev-box visual for supply drops (matches portable storage chest footprint).</summary>
public static class ThornsAirdropVisual
{
	public const string MaterialPath = "materials/building_materials/airdrop.vmat";
	public const string TexturePath = "materials/building_materials/airdrop.png";

	/// <summary>Same world inches as <c>storage_chest</c> portable placement.</summary>
	public static readonly Vector3 WorldSize = new( 80f, 45f, 50f );

	static Material _cachedMaterial;

	public static Material ResolveMaterial()
	{
		if ( _cachedMaterial.IsValid() )
			return _cachedMaterial;

		var mat = Material.Load( MaterialPath );
		if ( mat.IsValid() )
		{
			_cachedMaterial = mat;
			return _cachedMaterial;
		}

		var texture = Texture.Load( TexturePath );
		if ( texture.IsValid() )
		{
			var runtime = Material.FromShader( "shaders/complex.shader" );
			if ( runtime.IsValid() )
			{
				runtime.Set( "TextureColor", texture );
				runtime.Set( "TextureNormal", Texture.Load( "materials/default/default_normal.tga" ) );
				runtime.Set( "TextureRoughness", Texture.Load( "materials/default/default_rough.tga" ) );
				runtime.Set( "TextureAmbientOcclusion", Texture.Load( "materials/default/default_ao.tga" ) );
				runtime.Set( "g_flModelTintAmount", 1f );
				runtime.Set( "g_vColorTint", Color.White );
				runtime.Set( "g_flRoughnessScaleFactor", 0.68f );
				runtime.Set( "g_flMetalness", 0f );
				_cachedMaterial = runtime;
				return _cachedMaterial;
			}
		}

		_cachedMaterial = Material.Load( "materials/default/default.vmat" );
		return _cachedMaterial;
	}

	public static Vector3 ScaleBox( Model model )
	{
		if ( model.IsValid && model.Bounds.Size.LengthSquared > 1e-8f )
		{
			var bounds = model.Bounds;
			return new Vector3(
				WorldSize.x / Math.Max( 1f, bounds.Size.x ),
				WorldSize.y / Math.Max( 1f, bounds.Size.y ),
				WorldSize.z / Math.Max( 1f, bounds.Size.z ) );
		}

		return ThornsBuildingModule.ScaleBoxToWorldAxes( WorldSize.x, WorldSize.y, WorldSize.z );
	}

	public static Vector3 GroundCenterPosition( Vector3 surfacePosition ) =>
		surfacePosition + Vector3.Up * (WorldSize.z * 0.5f);

	public static BBox ResolveColliderBounds( Model model )
	{
		if ( model.IsValid && model.Bounds.Size.LengthSquared > 1e-8f )
			return model.Bounds;

		return new BBox( new Vector3( -25f, -25f, -25f ), new Vector3( 25f, 25f, 25f ) );
	}
}
