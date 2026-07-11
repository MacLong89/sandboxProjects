namespace Terraingen.World.Environment;

[Title( "Thorns Sky Controller" )]
[Category( "Environment" )]
[Icon( "landscape" )]
public sealed class ThornsSkyController : Component
{
	const string SkyMaterialPath = "materials/skybox/thorns_sky_celestial.vmat";
	const string NightSkyMaterialPath = "materials/skybox/thorns_sky_night.vmat";
	const float GlobalSkySaturation = 1.32f;

	[Property, Group( "References" )] public SkyBox2D SkyBox { get; set; }
	[Property, Group( "Material" )] public string MaterialPath { get; set; } = SkyMaterialPath;
	[Property, Group( "Material" )] public string NightMaterialPath { get; set; } = NightSkyMaterialPath;
	[Property, Group( "Sun Disc" ), Range( 0f, 8f )] public float SunDiscIntensity { get; set; } = 5.5f;
	[Property, Group( "Sun Disc" ), Range( 8f, 80f )] public float SunDiscAngularDiameter { get; set; } = 72f;
	[Property, Group( "Sun Disc" ), Range( 0f, 0.08f )] public float SunDiscGlow { get; set; } = 0.055f;

	Material _material;
	Material _nightMaterial;
	string _loadedPath;
	string _loadedNightPath;
	int _lastHash = int.MinValue;
	bool _usingNightMaterial;

	public Material MaterialInstance
	{
		get
		{
			EnsureMaterial();
			return _material;
		}
	}

	public void ApplyEnvironment( ThornsEnvironmentState state )
	{
		if ( SkyBox is null || !SkyBox.IsValid() )
			SkyBox = Components.Get<SkyBox2D>( FindMode.EverythingInSelf );

		if ( SkyBox is null || !SkyBox.IsValid() )
			return;

		EnsureMaterial();
		var nightDepth = NightDepthFromHours( state.Hours );
		var activeMaterial = nightDepth >= 0.98f && _nightMaterial is not null && _nightMaterial.IsValid()
			? _nightMaterial
			: _material;

		if ( activeMaterial is null || !activeMaterial.IsValid() )
			return;

		PushSunUniforms( activeMaterial, state, SunDiscIntensity, SunDiscAngularDiameter, SunDiscGlow );

		var hashBuilder = new HashCode();
		hashBuilder.Add( state.ZenithColor );
		hashBuilder.Add( state.SkyMidColor );
		hashBuilder.Add( state.HorizonColor );
		hashBuilder.Add( state.SunColor );
		hashBuilder.Add( state.SunFactor );
		hashBuilder.Add( state.TwilightFactor );
		hashBuilder.Add( state.FogColor );
		hashBuilder.Add( state.FogDensity );
		hashBuilder.Add( state.SkyExposure );
		hashBuilder.Add( state.StarIntensity );
		hashBuilder.Add( state.CloudOpacity );
		hashBuilder.Add( state.DayPercent );
		hashBuilder.Add( SunDiscIntensity );
		hashBuilder.Add( SunDiscAngularDiameter );
		hashBuilder.Add( SunDiscGlow );
		hashBuilder.Add( nightDepth >= 0.98f );
		hashBuilder.Add( (int)(state.CloudDrift * 1000f) );
		var hash = hashBuilder.ToHashCode();
		if ( hash == _lastHash )
			return;

		_lastHash = hash;
		SkyBox.Enabled = activeMaterial.IsValid();
		SkyBox.SkyIndirectLighting = state.SunFactor > 0.2f;
		_usingNightMaterial = activeMaterial == _nightMaterial;
		SkyBox.SkyMaterial = activeMaterial;
		SkyBox.Tint = ApplyNightDepth(
			Color.Lerp( SaturateSkyColor( Color.Lerp( state.HorizonColor, state.ZenithColor, 0.42f ), 1.85f ), Color.White, 0.18f ),
			nightDepth );
		PushUniforms( activeMaterial, state, SunDiscIntensity, SunDiscAngularDiameter, SunDiscGlow );
	}

	public void ForceReloadMaterial()
	{
		_material = null;
		_nightMaterial = null;
		_loadedPath = null;
		_loadedNightPath = null;
		_lastHash = int.MinValue;
		EnsureMaterial();
	}

	public string BuildStatusLine()
	{
		var skyValid = SkyBox is not null && SkyBox.IsValid();
		var materialValid = _material is not null && _material.IsValid();
		var nightMaterialValid = _nightMaterial is not null && _nightMaterial.IsValid();
		var tint = skyValid ? SkyBox.Tint : Color.Transparent;
		var enabled = skyValid && SkyBox.Enabled;
		var skyMaterialValid = skyValid && SkyBox.SkyMaterial is not null && SkyBox.SkyMaterial.IsValid();

		return $"skyValid={skyValid} skyEnabled={enabled} materialValid={materialValid} nightMaterialValid={nightMaterialValid} activeNightMaterial={_usingNightMaterial} skyMaterialValid={skyMaterialValid} tint={tint}";
	}

	public string BuildStatusLine( ThornsEnvironmentState state )
	{
		return $"{BuildStatusLine()} nightDepth={NightDepthFromHours( state.Hours ):F2} zenith={state.ZenithColor} horizon={state.HorizonColor}";
	}

	void EnsureMaterial()
	{
		var path = string.IsNullOrWhiteSpace( MaterialPath ) ? SkyMaterialPath : MaterialPath.Trim();
		if ( _material is null || !_material.IsValid() || !string.Equals( path, _loadedPath, StringComparison.OrdinalIgnoreCase ) )
		{
			_material = LoadMaterialCopy( path );
			_loadedPath = path;
			_lastHash = int.MinValue;
		}

		var nightPath = string.IsNullOrWhiteSpace( NightMaterialPath ) ? NightSkyMaterialPath : NightMaterialPath.Trim();
		if ( _nightMaterial is null || !_nightMaterial.IsValid() || !string.Equals( nightPath, _loadedNightPath, StringComparison.OrdinalIgnoreCase ) )
		{
			_nightMaterial = LoadMaterialCopy( nightPath );
			_loadedNightPath = nightPath;
			_lastHash = int.MinValue;
		}
	}

	Material LoadMaterialCopy( string path )
	{
		Material source = null;
		if ( !ResourceLibrary.TryGet<Material>( path, out source ) || source is null || !source.IsValid() )
			source = Material.Load( path );

		if ( source is null || !source.IsValid() )
		{
			Log.Warning( $"[Thorns Environment] Sky material '{path}' is missing or invalid." );
			return null;
		}

		var copy = source.CreateCopy();
		return copy.IsValid() ? copy : source;
	}

	public static void PushSunUniforms(
		Material material,
		ThornsEnvironmentState state,
		float sunDiscIntensity = 5.5f,
		float sunDiscAngularDiameter = 72f,
		float sunDiscGlow = 0.055f )
	{
		if ( material is null || !material.IsValid() || material.Attributes is null )
			return;

		var attrs = material.Attributes;
		var sun = SaturateSkyColor( state.SunColor, 1.18f );
		var nightDepth = NightDepthFromHours( state.Hours );
		attrs.Set( "ThornsSunDirection", state.SunDirection );
		attrs.Set( "SunDiscColor", ToRgbVector( sun ) );
		attrs.Set( "SunDiscIntensity", state.SunFactor * sunDiscIntensity );
		attrs.Set( "SunDiscGlow", MathX.Lerp( sunDiscGlow * 0.35f, sunDiscGlow, state.TwilightFactor ) );
		attrs.Set( "SunDiscAngularDiameter", sunDiscAngularDiameter );
		attrs.Set( "TimeOfDay01", state.DayPercent );
		attrs.Set( "NightDepth", nightDepth );
	}

	public static void PushUniforms(
		Material material,
		ThornsEnvironmentState state,
		float sunDiscIntensity = 5.5f,
		float sunDiscAngularDiameter = 72f,
		float sunDiscGlow = 0.055f )
	{
		if ( material is null || !material.IsValid() || material.Attributes is null )
			return;

		var attrs = material.Attributes;
		var zenith = SaturateSkyColor( state.ZenithColor );
		var mid = SaturateSkyColor( state.SkyMidColor );
		var horizon = SaturateSkyColor( state.HorizonColor );
		var cloud = SaturateSkyColor( state.CloudColor );
		var fog = SaturateSkyColor( state.FogColor, 1.12f );
		var nightDepth = NightDepthFromHours( state.Hours );

		attrs.Set( "SkyZenith", ToRgbVector( zenith ) );
		attrs.Set( "SkyMid", ToRgbVector( mid ) );
		attrs.Set( "SkyHorizon", ToRgbVector( horizon ) );
		attrs.Set( "HorizonGlowColor", ToRgbVector( horizon ) );
		attrs.Set( "HorizonGlowStrength", state.TwilightFactor * 1.2f * (1f - nightDepth) );
		attrs.Set( "StarBrightness", state.StarIntensity );
		attrs.Set( "StarRotation", state.DayPercent );
		attrs.Set( "CloudOpacity", state.CloudOpacity );
		attrs.Set( "CloudTint", ToRgbVector( cloud ) );
		attrs.Set( "CloudDrift", state.CloudDrift );
		attrs.Set( "FogColor", ToRgbVector( fog ) );
		attrs.Set( "FogBlend", state.FogDensity );
		attrs.Set( "SkyExposure", state.SkyExposure );
		PushSunUniforms( material, state, sunDiscIntensity, sunDiscAngularDiameter, sunDiscGlow );
	}

	static Vector3 ToRgbVector( Color color ) => new( color.r, color.g, color.b );

	static Color SaturateSkyColor( Color color, float saturation = 1f )
	{
		saturation *= GlobalSkySaturation;
		var luma = color.r * 0.2126f + color.g * 0.7152f + color.b * 0.0722f;
		return new Color(
			Math.Clamp( luma + (color.r - luma) * saturation, 0f, 1f ),
			Math.Clamp( luma + (color.g - luma) * saturation, 0f, 1f ),
			Math.Clamp( luma + (color.b - luma) * saturation, 0f, 1f ),
			color.a );
	}

	public static Color ApplyNightDepth( Color color, float nightDepth )
	{
		var darkBlue = new Color( 0.015f, 0.045f, 0.19f, 1f );
		var result = Color.Lerp( color, darkBlue, nightDepth );
		return result.WithAlpha( 1f );
	}

	public static Color CameraBackgroundColor( ThornsEnvironmentState state )
	{
		var nightDepth = NightDepthFromHours( state.Hours );
		if ( nightDepth > 0.001f )
		{
			var sunsetClear = SaturateSkyColor( new Color( 0.63f, 0.45f, 0.50f, 1f ) );
			var darkBlue = SaturateSkyColor( new Color( 0.015f, 0.045f, 0.19f, 1f ), 1.12f );
			return Color.Lerp( sunsetClear, darkBlue, nightDepth ).WithAlpha( 1f );
		}

		var bg = SaturateSkyColor( Color.Lerp( state.HorizonColor, state.ZenithColor, 0.35f ) ) * MathF.Max( 0.25f, state.SkyExposure );
		return bg.WithAlpha( 1f );
	}

	public static float NightDepthFromHours( float hours )
	{
		hours = ThornsEnvironmentMath.WrapHours( hours );
		if ( hours >= ThornsEnvironmentTwilightSchedule.NightBlendEndHour )
			return 1f;

		if ( hours >= ThornsEnvironmentTwilightSchedule.NightBlendStartHour )
			return ThornsEnvironmentMath.SmoothStep( (hours - ThornsEnvironmentTwilightSchedule.NightBlendStartHour) / ThornsEnvironmentTwilightSchedule.EveningNightDepthSpanHours );

		if ( hours <= ThornsEnvironmentTwilightSchedule.DeepNightEndHour )
			return 1f;

		if ( hours <= ThornsEnvironmentTwilightSchedule.SunriseBlendEndHour )
			return ThornsEnvironmentMath.SmoothStep( (ThornsEnvironmentTwilightSchedule.SunriseBlendEndHour - hours) / ThornsEnvironmentTwilightSchedule.MorningNightDepthSpanHours );

		return 0f;
	}
}
