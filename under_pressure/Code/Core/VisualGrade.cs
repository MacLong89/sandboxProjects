namespace UnderPressure;

using Sandbox.Volumes;

/// <summary>
/// Live-tunable global post-processing grade. Select the "Visual Grade" object in the
/// hierarchy (during play or in the editor) and drag the inspector sliders.
/// </summary>
[Title( "Visual Grade" )]
[Category( "Rendering" )]
[Icon( "tune" )]
public sealed class VisualGrade : Component
{
	[Property, Group( "Master" )]
	public bool ApplyGrade { get; set; } = true;

	[Property, Group( "Master" ), Range( 0f, 1f ), Title( "Overall Blend" )]
	public float MasterBlend { get; set; } = 1f;

	[Property, Group( "Color" ), Range( 0f, 2f )]
	public float Saturation { get; set; } = 1.015f;

	[Property, Group( "Color" ), Range( 0f, 2f )]
	public float Contrast { get; set; } = 1f;

	[Property, Group( "Color" ), Range( 0f, 2f )]
	public float Brightness { get; set; } = 1.462f;

	[Property, Group( "Color" ), Range( -30f, 30f ), Title( "Hue Shift" )]
	public float HueRotate { get; set; } = 2.308f;

	[Property, Group( "Warmth" ), Range( 3500f, 9000f ), Title( "Color Temp (K) — lower = warmer" )]
	public float ColorTempK { get; set; } = 6715.38f;

	[Property, Group( "Warmth" ), Range( 0f, 1f ), Title( "Warmth Amount" )]
	public float WarmthBlend { get; set; } = 1f;

	[Property, Group( "Exposure" ), Range( -1f, 2f ), Title( "Exposure Boost" )]
	public float ExposureCompensation { get; set; } = 0.223f;

	[Property, Group( "Bloom" ), Range( 0f, 1f )]
	public float BloomStrength { get; set; } = 0.18f;

	[Property, Group( "Bloom" ), Range( 0f, 2f ), Title( "Bloom Threshold" )]
	public float BloomThreshold { get; set; } = 1.0f;

	PostProcessVolume _volume;
	ColorAdjustments _colorAdjustments;
	ColorGrading _colorGrading;
	Tonemapping _tonemapping;
	Bloom _bloom;
	int _lastHash = int.MinValue;

	protected override void OnStart() => ApplyIfDirty( force: true );

	protected override void OnUpdate()
	{
		ApplyIfDirty( force: false );
		EnsureCameraPostProcessing();
	}

	void ApplyIfDirty( bool force )
	{
		EnsureComponents();

		var hash = ComputeHash();
		if ( !force && hash == _lastHash )
			return;

		_lastHash = hash;
		PushToRuntime();
	}

	void EnsureComponents()
	{
		_volume ??= Components.GetOrCreate<PostProcessVolume>();
		_colorAdjustments ??= Components.GetOrCreate<ColorAdjustments>();
		_colorGrading ??= Components.GetOrCreate<ColorGrading>();
		_tonemapping ??= Components.GetOrCreate<Tonemapping>();
		_bloom ??= Components.GetOrCreate<Bloom>();

		if ( !_volume.IsInfinite )
		{
			var sceneVolume = _volume.SceneVolume;
			sceneVolume.Type = SceneVolume.VolumeTypes.Infinite;
			_volume.SceneVolume = sceneVolume;
		}

		_volume.Priority = 10;
		_volume.EditorPreview = true;
	}

	void PushToRuntime()
	{
		var active = ApplyGrade && Enabled;
		var blend = active ? MasterBlend : 0f;

		_volume.Enabled = active;
		_volume.BlendWeight = blend;

		_colorAdjustments.Enabled = active;
		_colorAdjustments.Blend = blend;
		_colorAdjustments.Saturation = Saturation;
		_colorAdjustments.Contrast = Contrast;
		_colorAdjustments.Brightness = Brightness;
		_colorAdjustments.HueRotate = HueRotate;

		_colorGrading.Enabled = active && WarmthBlend > 0.001f;
		_colorGrading.GradingMethod = ColorGrading.GradingType.TemperatureControl;
		_colorGrading.ColorTempK = ColorTempK;
		_colorGrading.BlendFactor = WarmthBlend * blend;

		_tonemapping.Enabled = active;
		_tonemapping.Mode = Tonemapping.TonemappingMode.AgX;
		_tonemapping.AutoExposureEnabled = false;
		_tonemapping.ExposureCompensation = ExposureCompensation;
		_tonemapping.MinimumExposure = 1f;
		_tonemapping.MaximumExposure = 3f;

		_bloom.Enabled = active && BloomStrength > 0.001f;
		_bloom.Strength = BloomStrength;
		_bloom.Threshold = BloomThreshold;
		_bloom.Tint = new Color( 1f, 0.92f, 0.78f );
	}

	void EnsureCameraPostProcessing()
	{
		if ( !ApplyGrade || !Enabled || Scene is null || !Scene.IsValid() )
			return;

		foreach ( var cam in Scene.GetAllComponents<CameraComponent>() )
		{
			if ( cam.IsValid() && cam.IsMainCamera && !cam.EnablePostProcessing )
				cam.EnablePostProcessing = true;
		}
	}

	int ComputeHash()
	{
		var hash = new HashCode();
		hash.Add( ApplyGrade );
		hash.Add( MasterBlend );
		hash.Add( Saturation );
		hash.Add( Contrast );
		hash.Add( Brightness );
		hash.Add( HueRotate );
		hash.Add( ColorTempK );
		hash.Add( WarmthBlend );
		hash.Add( ExposureCompensation );
		hash.Add( BloomStrength );
		hash.Add( BloomThreshold );
		return hash.ToHashCode();
	}

	/// <summary>Turn on post-processing for the active main camera.</summary>
	public static void EnableOnCamera( CameraComponent camera )
	{
		if ( camera is null || !camera.IsValid() )
			return;

		camera.EnablePostProcessing = true;
	}

	public static VisualGrade EnsureInScene( Scene scene )
	{
		foreach ( var existing in scene.GetAllComponents<VisualGrade>() )
			return existing;

		var go = scene.CreateObject();
		go.Name = "Visual Grade";
		return go.Components.Create<VisualGrade>();
	}
}
