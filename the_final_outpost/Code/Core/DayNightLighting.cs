namespace FinalOutpost;

/// <summary>Fades scene lighting between bright day and dark, blood-red night when waves start/end.</summary>
public sealed class DayNightLighting : Component
{
	public static DayNightLighting Instance { get; private set; }

	private const float TransitionSeconds = 1.25f;
	private const float NightFogStrength = 0.9f;

	// Keep overall luminance low; push red channel well above green/blue for an ominous hue.
	private static readonly Color NightLightColor = new( 0.07f, 0.012f, 0.008f );
	private static readonly Color NightSkyColor = new( 0.03f, 0.006f, 0.004f );
	private static readonly Color NightSkyTint = new( 0.11f, 0.024f, 0.018f );
	private static readonly Color NightEnvmapTint = new( 0.09f, 0.02f, 0.015f );

	private DirectionalLight _sun;
	private SkyBox2D _sky;
	private EnvmapProbe _envmap;

	private Color _dayLightColor;
	private Color _daySkyColor;
	private Color _daySkyTint = Color.White;
	private Color _dayEnvmapTint = Color.White;
	private float _dayFogStrength;
	private bool _daySkyIndirect;

	private bool _nightTarget;
	private float _blend;

	protected override void OnAwake() => Instance = this;

	protected override void OnDestroy()
	{
		if ( Instance == this ) Instance = null;
	}

	protected override void OnStart()
	{
		foreach ( var light in Scene.GetAllComponents<DirectionalLight>() )
		{
			_sun = light;
			break;
		}

		foreach ( var sky in Scene.GetAllComponents<SkyBox2D>() )
		{
			_sky = sky;
			break;
		}

		foreach ( var probe in Scene.GetAllComponents<EnvmapProbe>() )
		{
			_envmap = probe;
			break;
		}

		if ( _sun is null )
		{
			Log.Warning( "[FinalOutpost] DayNightLighting: no DirectionalLight in scene." );
			return;
		}

		_dayLightColor = _sun.LightColor;
		_daySkyColor = _sun.SkyColor;
		_dayFogStrength = _sun.FogStrength;
		if ( _sky is not null )
		{
			_daySkyTint = _sky.Tint;
			_daySkyIndirect = _sky.SkyIndirectLighting;
		}

		if ( _envmap is not null )
			_dayEnvmapTint = _envmap.TintColor;

		ApplyBlend();
	}

	protected override void OnUpdate()
	{
		if ( _sun is null ) return;

		var target = _nightTarget ? 1f : 0f;
		if ( MathF.Abs( _blend - target ) < 0.001f )
		{
			_blend = target;
			ApplyBlend();
			return;
		}

		var step = Time.Delta / TransitionSeconds;
		_blend = _nightTarget
			? MathF.Min( 1f, _blend + step )
			: MathF.Max( 0f, _blend - step );

		ApplyBlend();
	}

	public void SetNight( bool night ) => _nightTarget = night;

	private void ApplyBlend()
	{
		var t = _blend;
		_sun.LightColor = Color.Lerp( _dayLightColor, NightLightColor, t );
		_sun.SkyColor = Color.Lerp( _daySkyColor, NightSkyColor, t );
		_sun.FogStrength = _dayFogStrength + (NightFogStrength - _dayFogStrength) * t;

		if ( _sky is not null )
		{
			_sky.Tint = Color.Lerp( _daySkyTint, NightSkyTint, t );
			_sky.SkyIndirectLighting = t < 0.2f && _daySkyIndirect;
		}

		if ( _envmap is not null )
			_envmap.TintColor = Color.Lerp( _dayEnvmapTint, NightEnvmapTint, t );
	}
}
