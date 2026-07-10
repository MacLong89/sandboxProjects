namespace Fauna2;

/// <summary>
/// Client-local day/night atmosphere — rotates the scene sun and tints fog/sky
/// for a living world feel without touching gameplay state.
/// </summary>
public sealed class ZooAtmosphere : Component
{
	public static ZooAtmosphere Instance { get; private set; }

	[Property] public float DayLengthMinutes { get; set; } = 12f;

	private DirectionalLight _sun;
	private Color _baseSunColor = Color.White;
	private float _baseFogStrength = 1f;

	protected override void OnAwake() => Instance = this;

	protected override void OnDestroy()
	{
		if ( Instance == this ) Instance = null;
	}

	protected override void OnStart() => FindSun();

	protected override void OnUpdate()
	{
		if ( GameManager.Instance is null || !GameManager.Instance.GameStarted )
			return;

		if ( !_sun.IsValid() )
			FindSun();

		if ( !_sun.IsValid() )
			return;

		var daySeconds = DayLengthMinutes * 60f;
		var t = (Time.Now % daySeconds) / daySeconds;
		var sunAngle = t * 360f - 90f;

		_sun.WorldRotation = Rotation.From( sunAngle, 45f, 0f );

		var dayFactor = MathF.Max( 0.15f, MathF.Sin( t * MathF.PI ));
		var warm = t < 0.25f || t > 0.75f;
		var tint = warm
			? Color.Lerp( new Color( 1f, 0.78f, 0.38f ), _baseSunColor, dayFactor )
			: Color.Lerp( new Color( 0.88f, 0.94f, 1f ), _baseSunColor, dayFactor );

		_sun.LightColor = tint;
		_sun.FogStrength = _baseFogStrength * (0.55f + dayFactor * 0.45f);
	}

	private void FindSun()
	{
		foreach ( var go in Scene.GetAllObjects( true ) )
		{
			if ( go.Name != "Sun" ) continue;
			_sun = go.Components.Get<DirectionalLight>();
			if ( _sun.IsValid() )
			{
				_baseSunColor = _sun.LightColor;
				_baseFogStrength = _sun.FogStrength;
			}
			return;
		}
	}
}
