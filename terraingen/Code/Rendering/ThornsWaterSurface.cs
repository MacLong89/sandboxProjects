namespace Terraingen.Rendering;

using Sandbox;
using Terraingen.TerrainGen;
using Terraingen.World.Environment;

/// <summary>
/// Drives stylized water material parameters from terrain bathymetry and the current environment state when present.
/// </summary>
[Title( "Thorns Water Surface" )]
[Category( "Rendering" )]
[Icon( "water" )]
public sealed class ThornsWaterSurface : Component
{
	const string WaterTexturePath = "terrain_materials/water_1024.png";
	const string WaterShaderPath = "shaders/thorns_water.shader";
	const string StylizedMaterialPath = "terrain_materials/thorns_terrain_water.vmat";
	const string PrimaryMaterialPath = "materials/water.vmat";
	const string FallbackMaterialPath = "terrain_materials/thorns_terrain_water_fallback.vmat";

	[Property, Group( "Water Color" )] public Color ShallowColor { get; set; } = new( 38f / 255f, 118f / 255f, 175f / 255f );
	[Property, Group( "Water Color" )] public Color DeepColor { get; set; } = new( 6f / 255f, 52f / 255f, 92f / 255f );
	[Property, Group( "Water Color" )] public Color ShoreTint { get; set; } = new( 88f / 255f, 165f / 255f, 198f / 255f );
	[Property, Group( "Water Color" )] public Color FoamColor { get; set; } = new( 190f / 255f, 220f / 255f, 235f / 255f );
	[Property, Group( "Water Color" ), Range( 0.8f, 1.8f )] public float ColorSaturation { get; set; } = 1.28f;
	[Property, Group( "Water Color" ), Range( 0.7f, 1.3f )] public float ColorBoost { get; set; } = 0.9f;
	[Property, Group( "Water Color" ), Range( 0f, 1f )] public float TextureBlend { get; set; } = 0.65f;

	[Property, Group( "Waves" )] public Vector2 WaterScrollSpeed { get; set; } = new( 0.003f, 0.0022f );
	[Property, Group( "Waves" ), Range( 1f, 64f )] public float WaterUvScale { get; set; } = 4f;
	[Property, Group( "Waves" ), Range( 0f, 1f )] public float BigWaveSize { get; set; } = 0.18f;
	[Property, Group( "Waves" ), Range( 0f, 10f )] public float BigWaveTime { get; set; } = 0.032f;

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

	[Property, Group( "Material" ), Title( "Override material (.vmat)" )]
	public string MaterialOverridePath { get; set; } = "";

	ModelRenderer _renderer;
	Material _material;
	string _materialPath;
	string _configuredMaterialPath;
	string _loadedWaterTexturePath;
	string _materialMode = "unknown";
	Texture _heightTexture;
	Texture _waterTexture;
	Vector3 _terrainOrigin;
	float _terrainSize;
	float _terrainMaxHeight;
	float _seaLevelWorldZ;
	bool _usesCustomShader;
	bool _loggedSetup;
	int _lastStaticMaterialKey = int.MinValue;
	int _lastAtmosphereFogKey = int.MinValue;
	double _nextMaterialPushTime;

	public void Initialize(
		Terrain terrain,
		HeightmapField field,
		ThornsTerrainConfig config )
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
			_configuredMaterialPath = string.IsNullOrWhiteSpace( config.WaterSurfaceMaterial )
				? null
				: config.WaterSurfaceMaterial.Trim();

			_heightTexture = ThornsWaterHeightTexture.Create( field );
			_waterTexture = Texture.Load( WaterTexturePath );
			_loadedWaterTexturePath = _waterTexture.IsValid() ? WaterTexturePath : "missing";

			_material = default;
			_materialPath = null;
			_usesCustomShader = false;
			_materialMode = "unknown";

			EnsureMaterialInstance();
			PushMaterialAttributes();

			if ( !_loggedSetup )
			{
				_loggedSetup = true;
				var shaderName = "(invalid material)";
				if ( _material.IsValid() )
				{
					var shader = _material.Shader;
					shaderName = shader.IsValid() ? shader.ResourceName : "(null shader)";
				}
				Log.Info(
					$"[Thorns Water] mode={_materialMode} material={_materialPath ?? "none"} shader={shaderName} " +
					$"albedo={WaterTexturePath} albedoLoaded={_waterTexture.IsValid()} heightTex={_heightTexture.IsValid()} seaZ={_seaLevelWorldZ:F0}" );
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

		var staticKey = ComputeStaticMaterialKey();
		var atmosphereKey = ComputeAtmosphereFogKey();
		var now = Time.Now;
		var staticChanged = staticKey != _lastStaticMaterialKey;
		var atmosphereChanged = atmosphereKey != _lastAtmosphereFogKey;
		if ( !staticChanged && !atmosphereChanged && now < _nextMaterialPushTime )
			return;

		_nextMaterialPushTime = now + 0.2;

		if ( _usesCustomShader )
		{
			if ( staticChanged )
				PushStylizedAttributes();
			else if ( atmosphereChanged )
				PushAtmosphereAttributes();
		}
		else if ( staticChanged )
			PushFallbackAttributes();

		_lastStaticMaterialKey = staticKey;
		_lastAtmosphereFogKey = atmosphereKey;
	}

	int ComputeStaticMaterialKey()
	{
		var hash = new HashCode();
		hash.Add( ShallowColor );
		hash.Add( DeepColor );
		hash.Add( ShoreTint );
		hash.Add( FoamColor );
		hash.Add( ShallowDepthInches );
		hash.Add( DeepDepthInches );
		hash.Add( ShoreBlendDepthInches );
		hash.Add( ReflectionRoughness );
		hash.Add( WaterMetalness );
		hash.Add( SpecularScale );
		hash.Add( TextureBlend );
		hash.Add( WaterScrollSpeed );
		hash.Add( WaterUvScale );
		hash.Add( BigWaveSize );
		hash.Add( BigWaveTime );
		hash.Add( _terrainOrigin );
		hash.Add( _terrainSize );
		hash.Add( _terrainMaxHeight );
		hash.Add( _seaLevelWorldZ );
		return hash.ToHashCode();
	}

	int ComputeAtmosphereFogKey()
	{
		if ( !ThornsTimeOfDaySystem.TryGet( Scene, out var time ) )
			return 0;

		var state = time.CurrentState;
		return HashCode.Combine( state.FogColor, state.FogDensity, WaterFogStrength );
	}

	void EnsureMaterialInstance()
	{
		if ( _renderer is null || !_renderer.IsValid() || (_material is not null && _material.IsValid()) )
			return;

		if ( TryLoadMaterial( MaterialOverridePath, "override" ) )
			return;

		if ( TryLoadMaterial( _configuredMaterialPath, "configured" ) )
			return;

		if ( TryLoadMaterial( PrimaryMaterialPath, "simple" ) )
			return;

		if ( TryLoadMaterial( FallbackMaterialPath, "simple-fallback" ) )
			return;

		if ( TryCreateTintedComplexMaterial() )
		{
			_materialMode = "runtime-complex";
			Log.Warning( "[Thorns Water] Using runtime complex material — assign materials/water.vmat on terrain config." );
			return;
		}

		Log.Warning( "[Thorns Water] No water material found. Expected materials/water.vmat with terrain_materials/water_1024.png." );
	}

	bool TryCreateTintedComplexMaterial()
	{
		var shaderMat = Material.FromShader( "shaders/complex.shader" );
		if ( !shaderMat.IsValid() )
			return false;

		_material = shaderMat.CreateCopy();
		if ( !_material.IsValid() )
			_material = shaderMat;

		_usesCustomShader = false;
		_materialPath = "(runtime complex)";
		_materialMode = "runtime-complex";
		BindWaterTexture( _material );
		_renderer.MaterialOverride = _material;
		return _material.IsValid();
	}

	bool TryLoadMaterial( string path, string mode )
	{
		if ( string.IsNullOrWhiteSpace( path ) )
			return false;

		Material resource = null;
		if ( !ResourceLibrary.TryGet<Material>( path, out resource ) || resource is null )
		{
			resource = Material.Load( path );
			if ( !resource.IsValid() )
			{
				Log.Warning( $"[Thorns Water] Material load failed: {path}" );
				return false;
			}
		}

		var copy = resource.CreateCopy();
		_material = copy.IsValid() ? copy : resource;
		if ( !_material.IsValid() )
		{
			Log.Warning( $"[Thorns Water] Material copy invalid: {path}" );
			return false;
		}

		_usesCustomShader = UsesThornsWaterShader( _material );
		_materialPath = path;
		_materialMode = _usesCustomShader ? "stylized" : mode;
		_renderer.MaterialOverride = _material;

		if ( _usesCustomShader )
			BindWaterTexture( _material );

		return true;
	}

	static bool UsesThornsWaterShader( Material material )
	{
		if ( !material.IsValid() )
			return false;

		var reference = Material.FromShader( WaterShaderPath );
		if ( !reference.IsValid() )
			return false;

		return reference.Shader == material.Shader;
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

		attrs.Set( "WaterScrollSpeed", WaterScrollSpeed );
		attrs.Set( "WaterUvScale", WaterUvScale );
		attrs.Set( "BigWaveSize", BigWaveSize );
		attrs.Set( "BigWaveTime", BigWaveTime );

		PushAtmosphereAttributes();
	}

	void PushFallbackAttributes()
	{
		var attrs = _material.Attributes;
		if ( attrs is null )
			return;

		attrs.Set( "g_vColorTint", Color.White );
		attrs.Set( "g_vTexCoordScale", new Vector2( WaterUvScale, WaterUvScale ) );
		attrs.Set( "g_vTexCoordScrollSpeed", WaterScrollSpeed );
		attrs.Set( "g_flMetalness", WaterMetalness );
		attrs.Set( "g_flRoughnessScaleFactor", ReflectionRoughness );
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
		if ( ThornsTimeOfDaySystem.TryGet( Scene, out var time ) )
		{
			var state = time.CurrentState;
			fogColor = Color.Lerp( state.FogColor, state.HorizonColor, 0.35f );
			fogStrength *= MathX.Lerp( 0.45f, 1f, state.FogDensity * 5f );
		}

		attrs.Set( "WaterFogColor", fogColor );
		attrs.Set( "WaterFogStrength", fogStrength );
	}
}
