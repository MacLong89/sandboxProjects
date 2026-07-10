namespace Terraingen.Rendering;

using Sandbox;
using Terraingen.TerrainGen;

/// <summary>
/// Drives stylized water material parameters from terrain bathymetry.
/// </summary>
[Title( "Thorns Water Surface" )]
[Category( "Rendering" )]
[Icon( "water" )]
public sealed class ThornsWaterSurface : Component
{
	const string PrimaryMaterialPath = "materials/water.vmat";
	const string FallbackMaterialPath = "terrain_materials/thorns_terrain_water_fallback.vmat";

	static readonly string[] WaterTexturePaths =
	[
		"materials/water.vtex",
		"terrain_materials/water.vtex",
		"materials/water.png",
	];

	[Property, Group( "Water Color" )] public Color ShallowColor { get; set; } = new( 38f / 255f, 118f / 255f, 175f / 255f );
	[Property, Group( "Water Color" )] public Color DeepColor { get; set; } = new( 6f / 255f, 52f / 255f, 92f / 255f );
	[Property, Group( "Water Color" )] public Color ShoreTint { get; set; } = new( 88f / 255f, 165f / 255f, 198f / 255f );
	[Property, Group( "Water Color" )] public Color FoamColor { get; set; } = new( 190f / 255f, 220f / 255f, 235f / 255f );
	[Property, Group( "Water Color" ), Range( 0.8f, 1.8f )] public float ColorSaturation { get; set; } = 1.28f;
	[Property, Group( "Water Color" ), Range( 0.7f, 1.3f )] public float ColorBoost { get; set; } = 0.9f;
	[Property, Group( "Water Color" ), Range( 0f, 1f )] public float TextureBlend { get; set; } = 0.72f;

	[Property, Group( "Depth" ), Range( 20f, 800f )] public float ShallowDepthInches { get; set; } = 72f;
	[Property, Group( "Depth" ), Range( 200f, 12000f )] public float DeepDepthInches { get; set; } = 1600f;
	[Property, Group( "Depth" ), Range( 40f, 1200f )] public float ShoreBlendDepthInches { get; set; } = 320f;
	[Property, Group( "Depth" ), Range( 0f, 1f )] public float ShoreBlendStrength { get; set; } = 0.16f;

	[Property, Group( "Foam" ), Range( 8f, 200f )] public float FoamWidthInches { get; set; } = 52f;
	[Property, Group( "Foam" ), Range( 0f, 0.5f )] public float FoamStrength { get; set; } = 0.14f;

	[Property, Group( "Reflection" ), Range( 0.2f, 1f )] public float ReflectionRoughness { get; set; } = 0.68f;
	[Property, Group( "Reflection" ), Range( 0f, 1f )] public float WaterMetalness { get; set; } = 0.18f;
	[Property, Group( "Reflection" ), Range( 0f, 1f )] public float SpecularScale { get; set; } = 0.48f;

	[Property, Group( "Atmosphere" ), Range( 100f, 20000f )] public float WaterFogStartInches { get; set; } = 4200f;
	[Property, Group( "Atmosphere" ), Range( 500f, 40000f )] public float WaterFogEndInches { get; set; } = 14000f;
	[Property, Group( "Atmosphere" ), Range( 0f, 1f )] public float WaterFogStrength { get; set; } = 0.42f;

	ModelRenderer _renderer;
	Material _material;
	string _materialPath;
	string _configuredMaterialPath;
	Texture _heightTexture;
	Texture _waterTexture;
	Vector3 _terrainOrigin;
	float _terrainSize;
	float _terrainMaxHeight;
	float _seaLevelWorldZ;
	float _textureTileRepeat = 192f;
	bool _usesCustomShader;
	bool _loggedSetup;

	public void Initialize(
		Terrain terrain,
		HeightmapField field,
		ThornsTerrainConfig config,
		float worldSpanInches = 0f )
	{
		try
		{
			_renderer = Components.Get<ModelRenderer>( FindMode.EverythingInSelf );
			if ( _renderer is null || !_renderer.IsValid() )
			{
				Log.Warning( "[Thorns Water] Missing ModelRenderer on water sheet." );
				return;
			}

			_terrainOrigin = terrain.GameObject.WorldPosition;
			_terrainSize = terrain.TerrainSize;
			_terrainMaxHeight = terrain.TerrainHeight;
			_seaLevelWorldZ = terrain.GameObject.WorldPosition.z
				+ config.SeaLevelNormalized * terrain.TerrainHeight;
			var worldSpan = worldSpanInches > 1f
				? worldSpanInches
				: Math.Max( terrain.TerrainSize, 1024f );
			_textureTileRepeat = ThornsWaterTextureTiling.ResolveTileRepeat( config.WaterTextureTileRepeat, worldSpan );
			_configuredMaterialPath = string.IsNullOrWhiteSpace( config.WaterSurfaceMaterial )
				? null
				: config.WaterSurfaceMaterial.Trim();

			_heightTexture = ThornsWaterHeightTexture.Create( field );
			_waterTexture = LoadWaterTexture();

			_material = default;
			_materialPath = null;
			_usesCustomShader = false;

			EnsureMaterialInstance();
			PushMaterialAttributes();

			if ( !_loggedSetup )
			{
				_loggedSetup = true;
				var mode = _usesCustomShader ? "stylized" : "fallback";
				var matLabel = string.IsNullOrWhiteSpace( _materialPath ) ? "none" : _materialPath;
				Log.Info( $"[Thorns Water] {mode} water ready — material={matLabel} waterTex={_waterTexture.IsValid()} heightTex={_heightTexture.IsValid()} seaZ={_seaLevelWorldZ:F0}" );
			}
		}
		catch ( Exception e )
		{
			Log.Warning( $"[Thorns Water] Initialize failed: {e.Message}" );
		}
	}

	protected override void OnDestroy()
	{
		_heightTexture = null;
		_waterTexture = null;
		base.OnDestroy();
	}

	protected override void OnUpdate()
	{
		if ( _material is null || !_material.IsValid() )
			return;

		if ( _usesCustomShader )
		{
			PushAtmosphereAttributes();
			return;
		}

		// complex.shader fallback — re-apply tiling every frame (material copy can lose runtime attrs).
		PushFallbackAttributes();
	}

	void EnsureMaterialInstance()
	{
		if ( _renderer is null || !_renderer.IsValid() || (_material is not null && _material.IsValid()) )
			return;

		if ( TryLoadMaterial( _configuredMaterialPath, stylized: false ) )
			return;

		if ( TryLoadMaterial( PrimaryMaterialPath, stylized: false ) )
			return;

		if ( TryLoadMaterial( FallbackMaterialPath, stylized: false ) )
			return;

		Log.Warning( "[Thorns Water] No water material found. Recompile materials/water.vtex in the asset browser." );
	}

	bool TryLoadMaterial( string path, bool stylized )
	{
		if ( string.IsNullOrWhiteSpace( path ) )
			return false;

		Material resource = null;
		if ( !ResourceLibrary.TryGet<Material>( path, out resource ) || resource is null )
		{
			if ( stylized )
				return false;

			resource = Material.Load( path );
			if ( !resource.IsValid() )
				return false;
		}

		var copy = resource.CreateCopy();
		_material = copy.IsValid() ? copy : resource;
		if ( !_material.IsValid() )
			return false;

		BindWaterTexture( _material );
		_usesCustomShader = stylized;
		_materialPath = path;
		_renderer.MaterialOverride = _material;
		return true;
	}

	static Texture LoadWaterTexture()
	{
		foreach ( var path in WaterTexturePaths )
		{
			var tex = Texture.Load( path );
			if ( tex.IsValid() )
				return tex;
		}

		return default;
	}

	void BindWaterTexture( Material material )
	{
		if ( !_waterTexture.IsValid() || material.Attributes is null )
			return;

		material.Attributes.Set( "TextureColor", _waterTexture );
		material.Attributes.Set( "g_tColor", _waterTexture );
	}

	void PushMaterialAttributes()
	{
		if ( _material is null || !_material.IsValid() )
			return;

		if ( _usesCustomShader )
			PushStylizedAttributes();
		else
			PushFallbackAttributes();
	}

	void PushStylizedAttributes()
	{
		var attrs = _material.Attributes;
		if ( attrs is null )
			return;

		BindWaterTexture( _material );

		if ( _heightTexture.IsValid() )
		{
			attrs.Set( "TerrainHeight", _heightTexture );
			attrs.Set( "HasTerrainHeight", 1f );
		}
		else
		{
			attrs.Set( "HasTerrainHeight", 0f );
		}

		attrs.Set( "TerrainOrigin", _terrainOrigin );
		attrs.Set( "TerrainSize", _terrainSize );
		attrs.Set( "TerrainMaxHeight", _terrainMaxHeight );
		attrs.Set( "SeaLevelWorldZ", _seaLevelWorldZ );
		ThornsWaterTextureTiling.ApplyTileRepeatToMaterial( _material, _textureTileRepeat );

		attrs.Set( "ShallowColor", ShallowColor );
		attrs.Set( "DeepColor", DeepColor );
		attrs.Set( "ShoreTint", ShoreTint );
		attrs.Set( "FoamColor", FoamColor );
		attrs.Set( "ColorSaturation", ColorSaturation );
		attrs.Set( "ColorBoost", ColorBoost );
		attrs.Set( "TextureBlend", TextureBlend );

		attrs.Set( "ShallowDepthInches", ShallowDepthInches );
		attrs.Set( "DeepDepthInches", DeepDepthInches );
		attrs.Set( "ShoreBlendDepthInches", ShoreBlendDepthInches );
		attrs.Set( "ShoreBlendStrength", ShoreBlendStrength );

		attrs.Set( "FoamWidthInches", FoamWidthInches );
		attrs.Set( "FoamStrength", FoamStrength );

		attrs.Set( "ReflectionRoughness", ReflectionRoughness );
		attrs.Set( "WaterMetalness", WaterMetalness );
		attrs.Set( "SpecularScale", SpecularScale );

		attrs.Set( "WaterFogStartInches", WaterFogStartInches );
		attrs.Set( "WaterFogEndInches", WaterFogEndInches );
		attrs.Set( "WaterFogStrength", WaterFogStrength );

		PushAtmosphereAttributes();
	}

	void PushFallbackAttributes()
	{
		var attrs = _material.Attributes;
		if ( attrs is null )
			return;

		BindWaterTexture( _material );
		attrs.Set( "g_vColorTint", Color.White );
		ThornsWaterTextureTiling.ApplyTileRepeatToMaterial( _material, _textureTileRepeat );
	}

	void PushAtmosphereAttributes()
	{
		if ( _material is null || !_material.IsValid() || !_usesCustomShader )
			return;

		var attrs = _material.Attributes;
		if ( attrs is null )
			return;

		var fogColor = new Color( 96f / 255f, 160f / 255f, 235f / 255f );
		var fogStrength = WaterFogStrength;
		if ( ThornsCelestialSystem.TryGet( Scene, out var celestial ) )
		{
			var s = celestial.CurrentState;
			fogColor = s.FogColor;
			fogStrength = MathX.Lerp( WaterFogStrength * 0.35f, WaterFogStrength, s.FogStrength );
		}

		attrs.Set( "WaterFogColor", fogColor );
		attrs.Set( "WaterFogStrength", fogStrength );
	}
}
