namespace Terraingen.World.Environment;

public enum ThornsSunriseDirection
{
	East,
	North,
	West,
	South
}

public static class ThornsEnvironmentUnits
{
	public const float InchesPerMeter = 39.3701f;
}

/// <summary>Clock-hour boundaries for sunrise/sunset palette and night-depth blending.</summary>
public static class ThornsEnvironmentTwilightSchedule
{
	// Sunrise: pure vibrant color holds 5.5–8.5, then a long fade into full day by 10.5.
	public const float DeepNightEndHour = 3f;
	public const float SunriseBlendEndHour = 5.5f;
	public const float SunrisePeakEndHour = 8.5f;
	public const float DayStartHour = 10.5f;
	// Sunset: start turning warm at 13.5, pure vibrant color 15.5–19.5, fade to night by 21.5.
	public const float NoonEndHour = 13.5f;
	public const float SunsetBlendEndHour = 15.5f;
	public const float SunsetPeakEndHour = 19.5f;
	public const float NightBlendStartHour = 19.5f;
	public const float NightBlendEndHour = 21.5f;

	// Spans keep NightDepth continuous with the boundaries above (5.5-3 and 21.5-19.5).
	public const float MorningNightDepthSpanHours = 2.5f;
	public const float EveningNightDepthSpanHours = 2f;
}

static class ThornsEnvironmentMath
{
	public const float HoursPerDay = 24f;
	const float TwoPi = MathF.PI * 2f;

	public static float WrapHours( float hours )
	{
		hours %= HoursPerDay;
		return hours < 0f ? hours + HoursPerDay : hours;
	}

	/// <summary>Signed hour delta in [-12, 12] for smooth interpolation across midnight.</summary>
	public static float ShortestHourDelta( float fromHours, float toHours )
	{
		var delta = WrapHours( toHours - fromHours );
		return delta > HoursPerDay * 0.5f ? delta - HoursPerDay : delta;
	}

	public static float Saturate( float value ) => Math.Clamp( value, 0f, 1f );

	public static float SmoothStep( float value )
	{
		value = Saturate( value );
		return value * value * (3f - 2f * value);
	}

	public static float SmoothRange( float edge0, float edge1, float value )
	{
		if ( MathF.Abs( edge1 - edge0 ) < 0.0001f )
			return value >= edge1 ? 1f : 0f;

		return SmoothStep( (value - edge0) / (edge1 - edge0) );
	}

	public static Color Rgb( int r, int g, int b ) => new( r / 255f, g / 255f, b / 255f );

	public static Vector3 SunDirection( float hours, ThornsSunriseDirection sunriseDirection, float yawOffsetDegrees, float pitchOffsetDegrees )
	{
		hours = WrapHours( hours );

		var phase = ((hours - 6f) / 24f) * TwoPi;
		var z = MathF.Sin( phase );
		var baseDir = new Vector3( MathF.Cos( phase ), 0f, z ).Normal;
		var yaw = YawOffset( sunriseDirection ) + DegreesToRadians( yawOffsetDegrees );
		var sin = MathF.Sin( yaw );
		var cos = MathF.Cos( yaw );
		var dir = new Vector3(
			baseDir.x * cos - 0f * sin,
			baseDir.x * sin + 0f * cos,
			baseDir.z ).Normal;

		if ( MathF.Abs( pitchOffsetDegrees ) < 0.001f )
			return dir;

		var pitchRot = Rotation.FromAxis( Vector3.Right, pitchOffsetDegrees );
		return pitchRot * dir;
	}

	public static Vector3 MoonDirection( Vector3 sunDirection ) => -sunDirection;

	public static Rotation DirectionalLightRotation( Vector3 sunDirection )
	{
		if ( sunDirection.Length < 0.001f )
			return Rotation.Identity;

		return Rotation.LookAt( -sunDirection.Normal, Vector3.Up );
	}

	static float YawOffset( ThornsSunriseDirection direction ) => direction switch
	{
		ThornsSunriseDirection.North => DegreesToRadians( 90f ),
		ThornsSunriseDirection.West => DegreesToRadians( 180f ),
		ThornsSunriseDirection.South => DegreesToRadians( 270f ),
		_ => 0f
	};

	static float DegreesToRadians( float degrees ) => degrees * MathF.PI / 180f;
}
