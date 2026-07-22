namespace Offshore;

public sealed class WeatherService
{
	public WeatherType Current { get; private set; } = WeatherType.Clear;
	public WeatherType Target { get; private set; } = WeatherType.Clear;
	public float Blend { get; private set; } = 1f;
	public float Wind { get; private set; }
	public float WaveIntensity { get; private set; } = 0.35f;
	public float Visibility { get; private set; } = 1f;
	public float CloudDensity { get; private set; } = 0.25f;
	public float Rain { get; private set; }
	public float Temperature { get; private set; } = 20f;
	public float WaterClarity { get; private set; } = 0.8f;

	float _timer;
	readonly Random _rng = new();

	public void Tick( float dt, float offshoreDistance, DayPhase phase )
	{
		_timer -= dt;
		if ( _timer <= 0f )
		{
			Target = PickNext( offshoreDistance, phase );
			_timer = 45f + (float)_rng.NextDouble() * 90f;
			Blend = 0f;
		}

		Blend = Math.Min( 1f, Blend + dt / 25f );
		ApplyStats( LerpWeather( Current, Target, Blend ), offshoreDistance, phase );
		if ( Blend >= 1f )
			Current = Target;
	}

	public string Label => (Blend < 0.55f ? Current : Target) switch
	{
		WeatherType.PartlyCloudy => "Partly Cloudy",
		WeatherType.LightFog => "Light Fog",
		WeatherType.HeavyFog => "Heavy Fog",
		WeatherType.LightRain => "Light Rain",
		WeatherType.HeavyRain => "Heavy Rain",
		WeatherType.Thunderstorm => "Thunderstorm",
		_ => (Blend < 0.55f ? Current : Target).ToString()
	};

	public string Icon => (Blend < 0.55f ? Current : Target) switch
	{
		WeatherType.Clear or WeatherType.PartlyCloudy => "weather_clear",
		WeatherType.Cloudy or WeatherType.Overcast => "weather_cloudy",
		WeatherType.LightRain or WeatherType.HeavyRain => "weather_rain",
		WeatherType.Thunderstorm => "weather_storm",
		WeatherType.LightFog or WeatherType.HeavyFog => "weather_fog",
		WeatherType.Windy => "weather_wind",
		_ => "weather_clear"
	};

	WeatherType PickNext( float dist, DayPhase phase )
	{
		var roll = _rng.NextDouble();
		if ( dist > 600 && roll < 0.12 ) return WeatherType.Thunderstorm;
		if ( dist > 300 && roll < 0.2 ) return WeatherType.Windy;
		if ( phase == DayPhase.Morning && roll < 0.15 ) return WeatherType.LightFog;
		if ( roll < 0.18 ) return WeatherType.LightRain;
		if ( roll < 0.28 ) return WeatherType.HeavyRain;
		if ( roll < 0.4 ) return WeatherType.Cloudy;
		if ( roll < 0.55 ) return WeatherType.PartlyCloudy;
		if ( roll < 0.65 ) return WeatherType.Overcast;
		if ( roll < 0.72 ) return WeatherType.HeavyFog;
		return WeatherType.Clear;
	}

	void ApplyStats( WeatherType w, float dist, DayPhase phase )
	{
		var baseTemp = phase switch
		{
			DayPhase.Midday => 24f,
			DayPhase.Night => 14f,
			DayPhase.Sunset or DayPhase.GoldenHour => 20f,
			_ => 18f
		};
		baseTemp -= dist / 250f;

		switch ( w )
		{
			case WeatherType.Clear:
				Wind = 0.15f; WaveIntensity = 0.25f; Visibility = 1f; CloudDensity = 0.15f; Rain = 0; WaterClarity = 0.9f; Temperature = baseTemp;
				break;
			case WeatherType.PartlyCloudy:
				Wind = 0.25f; WaveIntensity = 0.3f; Visibility = 0.95f; CloudDensity = 0.4f; Rain = 0; WaterClarity = 0.85f; Temperature = baseTemp - 0.5f;
				break;
			case WeatherType.Cloudy:
				Wind = 0.35f; WaveIntensity = 0.4f; Visibility = 0.85f; CloudDensity = 0.65f; Rain = 0; WaterClarity = 0.75f; Temperature = baseTemp - 1f;
				break;
			case WeatherType.Overcast:
				Wind = 0.4f; WaveIntensity = 0.45f; Visibility = 0.75f; CloudDensity = 0.85f; Rain = 0; WaterClarity = 0.65f; Temperature = baseTemp - 1.5f;
				break;
			case WeatherType.LightFog:
				Wind = 0.1f; WaveIntensity = 0.2f; Visibility = 0.45f; CloudDensity = 0.7f; Rain = 0; WaterClarity = 0.5f; Temperature = baseTemp - 2f;
				break;
			case WeatherType.HeavyFog:
				Wind = 0.08f; WaveIntensity = 0.18f; Visibility = 0.25f; CloudDensity = 0.9f; Rain = 0; WaterClarity = 0.35f; Temperature = baseTemp - 3f;
				break;
			case WeatherType.LightRain:
				Wind = 0.35f; WaveIntensity = 0.5f; Visibility = 0.7f; CloudDensity = 0.8f; Rain = 0.4f; WaterClarity = 0.55f; Temperature = baseTemp - 2f;
				break;
			case WeatherType.HeavyRain:
				Wind = 0.55f; WaveIntensity = 0.75f; Visibility = 0.5f; CloudDensity = 0.95f; Rain = 0.85f; WaterClarity = 0.4f; Temperature = baseTemp - 3f;
				break;
			case WeatherType.Windy:
				Wind = 0.85f; WaveIntensity = 0.8f; Visibility = 0.9f; CloudDensity = 0.45f; Rain = 0; WaterClarity = 0.7f; Temperature = baseTemp - 1f;
				break;
			case WeatherType.Thunderstorm:
				Wind = 1f; WaveIntensity = 1f; Visibility = 0.4f; CloudDensity = 1f; Rain = 1f; WaterClarity = 0.3f; Temperature = baseTemp - 4f;
				break;
		}
	}

	static WeatherType LerpWeather( WeatherType a, WeatherType b, float t ) => t < 0.5f ? a : b;
}
