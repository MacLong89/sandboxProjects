namespace Terraingen.World.Environment;

/// <summary>Four-key day palette exposed in the inspector.</summary>
public struct ThornsEnvironmentColorTrack
{
	[Property] public Color Midnight { get; set; }
	[Property] public Color Sunrise { get; set; }
	[Property] public Color Noon { get; set; }
	[Property] public Color Sunset { get; set; }

	public Color Evaluate( float hours )
	{
		hours = ThornsEnvironmentMath.WrapHours( hours );
		if ( hours < ThornsEnvironmentTwilightSchedule.DeepNightEndHour )
			return Midnight;
		if ( hours < ThornsEnvironmentTwilightSchedule.SunriseBlendEndHour )
			return Color.Lerp( Midnight, Sunrise, SmoothHours( hours, ThornsEnvironmentTwilightSchedule.DeepNightEndHour, ThornsEnvironmentTwilightSchedule.SunriseBlendEndHour ) );
		if ( hours < ThornsEnvironmentTwilightSchedule.SunrisePeakEndHour )
			return Sunrise;
		if ( hours < ThornsEnvironmentTwilightSchedule.DayStartHour )
			return Color.Lerp( Sunrise, Noon, SmoothHours( hours, ThornsEnvironmentTwilightSchedule.SunrisePeakEndHour, ThornsEnvironmentTwilightSchedule.DayStartHour ) );
		if ( hours < ThornsEnvironmentTwilightSchedule.NoonEndHour )
			return Noon;
		if ( hours < ThornsEnvironmentTwilightSchedule.SunsetBlendEndHour )
			return Color.Lerp( Noon, Sunset, SmoothHours( hours, ThornsEnvironmentTwilightSchedule.NoonEndHour, ThornsEnvironmentTwilightSchedule.SunsetBlendEndHour ) );
		if ( hours < ThornsEnvironmentTwilightSchedule.SunsetPeakEndHour )
			return Sunset;
		if ( hours < ThornsEnvironmentTwilightSchedule.NightBlendEndHour )
			return Color.Lerp( Sunset, Midnight, SmoothHours( hours, ThornsEnvironmentTwilightSchedule.NightBlendStartHour, ThornsEnvironmentTwilightSchedule.NightBlendEndHour ) );
		return Midnight;
	}

	static float SmoothHours( float hours, float start, float end )
		=> ThornsEnvironmentMath.SmoothStep( Math.Clamp( (hours - start) / Math.Max( 0.001f, end - start ), 0f, 1f ) );

	public static ThornsEnvironmentColorTrack ZenithDefaults => new()
	{
		Midnight = ThornsEnvironmentMath.Rgb( 4, 10, 38 ),
		Sunrise = ThornsEnvironmentMath.Rgb( 70, 88, 150 ),
		Noon = ThornsEnvironmentMath.Rgb( 62, 150, 225 ),
		Sunset = ThornsEnvironmentMath.Rgb( 58, 72, 132 )
	};

	public static ThornsEnvironmentColorTrack HorizonDefaults => new()
	{
		Midnight = ThornsEnvironmentMath.Rgb( 12, 26, 68 ),
		Sunrise = ThornsEnvironmentMath.Rgb( 255, 138, 72 ),
		Noon = ThornsEnvironmentMath.Rgb( 172, 220, 250 ),
		Sunset = ThornsEnvironmentMath.Rgb( 255, 92, 66 )
	};

	public static ThornsEnvironmentColorTrack SunDefaults => new()
	{
		Midnight = Color.Black,
		Sunrise = ThornsEnvironmentMath.Rgb( 255, 184, 120 ),
		Noon = ThornsEnvironmentMath.Rgb( 255, 244, 226 ),
		Sunset = ThornsEnvironmentMath.Rgb( 255, 156, 105 )
	};

	public static ThornsEnvironmentColorTrack AmbientDefaults => new()
	{
		Midnight = ThornsEnvironmentMath.Rgb( 52, 70, 126 ),
		Sunrise = ThornsEnvironmentMath.Rgb( 105, 122, 166 ),
		Noon = ThornsEnvironmentMath.Rgb( 164, 188, 214 ),
		Sunset = ThornsEnvironmentMath.Rgb( 104, 112, 166 )
	};

	public static ThornsEnvironmentColorTrack FogDefaults => new()
	{
		Midnight = ThornsEnvironmentMath.Rgb( 18, 36, 82 ),
		Sunrise = ThornsEnvironmentMath.Rgb( 232, 138, 92 ),
		Noon = ThornsEnvironmentMath.Rgb( 142, 190, 226 ),
		Sunset = ThornsEnvironmentMath.Rgb( 236, 100, 90 )
	};

	public static ThornsEnvironmentColorTrack CloudDefaults => new()
	{
		Midnight = ThornsEnvironmentMath.Rgb( 38, 48, 86 ),
		Sunrise = ThornsEnvironmentMath.Rgb( 255, 186, 158 ),
		Noon = ThornsEnvironmentMath.Rgb( 255, 255, 255 ),
		Sunset = ThornsEnvironmentMath.Rgb( 255, 146, 162 )
	};
}
