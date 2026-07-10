namespace Terraingen.World.Environment;

using Sandbox.Volumes;
using Terraingen;

[Title( "Thorns Post Process Tuner" )]
[Category( "Environment" )]
[Icon( "tune" )]
public sealed class ThornsPostProcessTuner : Component
{
	[Property, Group( "Master" ), Title( "Apply Grade" )]
	public bool ApplyGrade { get; set; } = true;

	[Property, Group( "Master" ), Title( "Enable Post Processing On Camera" )]
	public bool ForceCameraPostProcessing { get; set; } = true;

	[Property, Group( "Master" ), Range( 0f, 1f )]
	public float VolumeBlendWeight { get; set; } = 1f;

	[Property, Group( "Master" )]
	public bool EditorPreview { get; set; } = true;

	[Property, Group( "1 — Color Adjustments" ), Range( 0f, 2f )]
	public float Saturation { get; set; } = 1.333f;

	[Property, Group( "1 — Color Adjustments" ), Range( 0f, 2f )]
	public float Contrast { get; set; } = 1f;

	[Property, Group( "1 — Color Adjustments" ), Range( 0f, 2f )]
	public float Brightness { get; set; } = 1.522f;

	[Property, Group( "1 — Color Adjustments" ), Range( -180f, 180f )]
	public float HueRotate { get; set; } = 1f;

	[Property, Group( "1 — Color Adjustments" ), Range( 0f, 1f ), Title( "Effect Blend" )]
	public float ColorAdjustBlend { get; set; } = 1f;

	[Property, Group( "2 — White Balance" )]
	public bool UseWhiteBalance { get; set; } = true;

	[Property, Group( "2 — White Balance" ), Range( 3500f, 9000f ), Title( "Color Temp (K) — lower = warmer" )]
	public float ColorTempK { get; set; } = 5415.42f;

	[Property, Group( "2 — White Balance" ), Range( 0f, 1f )]
	public float WhiteBalanceBlend { get; set; } = 1f;

	[Property, Group( "3 — Tonemapping" )]
	public bool TonemappingEnabled { get; set; } = true;

	[Property, Group( "3 — Tonemapping" )]
	public Tonemapping.TonemappingMode TonemappingMode { get; set; } = Tonemapping.TonemappingMode.HableFilmic;

	[Property, Group( "3 — Tonemapping" ), Range( -2f, 2f )]
	public float ExposureCompensation { get; set; } = 0.45f;

	[Property, Group( "3 — Tonemapping" )]
	public bool AutoExposure { get; set; }

	[Property, Group( "3 — Tonemapping" ), Range( 0.01f, 8f )]
	public float MinimumExposure { get; set; } = 1f;

	[Property, Group( "3 — Tonemapping" ), Range( 0.01f, 8f )]
	public float MaximumExposure { get; set; } = 3f;

	[Property, Group( "4 — Bloom" )]
	public bool BloomEnabled { get; set; }

	[Property, Group( "4 — Bloom" ), Range( 0f, 2f )]
	public float BloomStrength { get; set; } = 0.35f;

	[Property, Group( "4 — Bloom" ), Range( 0f, 2f )]
	public float BloomThreshold { get; set; } = 0.55f;

	[Property, Group( "4 — Bloom" )]
	public Color BloomTint { get; set; } = Color.White;

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

	public void ApplyIfDirty( bool force )
	{
		EnsureRuntimeComponents();

		var hash = ComputeHash();
		if ( !force && hash == _lastHash )
			return;

		_lastHash = hash;
		PushToRuntimeComponents();
	}

	void EnsureRuntimeComponents()
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
	}

	void PushToRuntimeComponents()
	{
		var active = ApplyGrade && Enabled;

		_volume.Enabled = active;
		_volume.BlendWeight = active ? VolumeBlendWeight : 0f;
		_volume.EditorPreview = EditorPreview;

		_colorAdjustments.Enabled = active;
		_colorAdjustments.Blend = ColorAdjustBlend;
		_colorAdjustments.Saturation = Saturation;
		_colorAdjustments.Contrast = Contrast;
		_colorAdjustments.Brightness = Brightness;
		_colorAdjustments.HueRotate = HueRotate;

		_colorGrading.Enabled = active && UseWhiteBalance;
		_colorGrading.GradingMethod = UseWhiteBalance
			? ColorGrading.GradingType.TemperatureControl
			: ColorGrading.GradingType.None;
		_colorGrading.ColorTempK = ColorTempK;
		_colorGrading.BlendFactor = WhiteBalanceBlend;

		_tonemapping.Enabled = active && TonemappingEnabled;
		_tonemapping.Mode = TonemappingMode;
		_tonemapping.ExposureCompensation = ExposureCompensation;
		_tonemapping.AutoExposureEnabled = AutoExposure;
		_tonemapping.MinimumExposure = MinimumExposure;
		_tonemapping.MaximumExposure = MaximumExposure;

		_bloom.Enabled = active && BloomEnabled;
		_bloom.Strength = BloomStrength;
		_bloom.Threshold = BloomThreshold;
		_bloom.Tint = BloomTint;
	}

	void EnsureCameraPostProcessing()
	{
		if ( !ApplyGrade || !Enabled || !ForceCameraPostProcessing || Scene is null || !Scene.IsValid() )
			return;

		if ( ThornsSceneObserver.TryGetMainCamera( Scene, out var main ) && main.IsValid() && !main.EnablePostProcessing )
			main.EnablePostProcessing = true;

		if ( Scene.Camera is not null && Scene.Camera.IsValid() && Scene.Camera.IsMainCamera && !Scene.Camera.EnablePostProcessing )
			Scene.Camera.EnablePostProcessing = true;
	}

	int ComputeHash()
	{
		var hash = new HashCode();
		hash.Add( ApplyGrade );
		hash.Add( ForceCameraPostProcessing );
		hash.Add( VolumeBlendWeight );
		hash.Add( EditorPreview );
		hash.Add( Saturation );
		hash.Add( Contrast );
		hash.Add( Brightness );
		hash.Add( HueRotate );
		hash.Add( ColorAdjustBlend );
		hash.Add( UseWhiteBalance );
		hash.Add( ColorTempK );
		hash.Add( WhiteBalanceBlend );
		hash.Add( TonemappingEnabled );
		hash.Add( TonemappingMode );
		hash.Add( ExposureCompensation );
		hash.Add( AutoExposure );
		hash.Add( MinimumExposure );
		hash.Add( MaximumExposure );
		hash.Add( BloomEnabled );
		hash.Add( BloomStrength );
		hash.Add( BloomThreshold );
		hash.Add( BloomTint );
		return hash.ToHashCode();
	}

	public static ThornsPostProcessTuner EnsureOnEnvironment( GameObject environmentRoot )
	{
		if ( environmentRoot is null || !environmentRoot.IsValid() )
			return null;

		foreach ( var go in environmentRoot.Scene.GetAllObjects( true ) )
		{
			if ( !go.IsValid() || !string.Equals( go.Name, "Thorns Post Process", StringComparison.OrdinalIgnoreCase ) )
				continue;

			go.Tags.Add( ThornsEnvironmentDirector.EnvironmentTag );
			return go.Components.Get<ThornsPostProcessTuner>( FindMode.EverythingInSelf )
			       ?? go.Components.Create<ThornsPostProcessTuner>();
		}

		var created = environmentRoot.Scene.CreateObject();
		created.Name = "Thorns Post Process";
		created.Tags.Add( ThornsEnvironmentDirector.EnvironmentTag );
		if ( environmentRoot.IsValid() )
			created.SetParent( environmentRoot );

		return created.Components.Create<ThornsPostProcessTuner>();
	}
}
