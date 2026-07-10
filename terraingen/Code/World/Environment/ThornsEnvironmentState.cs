namespace Terraingen.World.Environment;

public readonly struct ThornsEnvironmentState
{
	public readonly float Hours;
	public readonly float DayPercent;
	public readonly Vector3 SunDirection;
	public readonly Vector3 MoonDirection;
	public readonly Rotation SunLightRotation;
	public readonly float SunHeight;
	public readonly float SunFactor;
	public readonly float NightFactor;
	public readonly float TwilightFactor;
	public readonly string Phase;
	public readonly Color ZenithColor;
	public readonly Color HorizonColor;
	public readonly Color SkyMidColor;
	public readonly Color SunColor;
	public readonly Color AmbientColor;
	public readonly Color FogColor;
	public readonly Color CloudColor;
	public readonly float SunIntensity;
	public readonly float AmbientIntensity;
	public readonly float FogDensity;
	public readonly float FogStartDistance;
	public readonly float FogEndDistance;
	public readonly float SkyExposure;
	public readonly float StarIntensity;
	public readonly float CloudOpacity;
	public readonly float CloudDrift;

	public ThornsEnvironmentState(
		float hours,
		Vector3 sunDirection,
		Vector3 moonDirection,
		Rotation sunLightRotation,
		float sunHeight,
		float sunFactor,
		float nightFactor,
		float twilightFactor,
		string phase,
		Color zenithColor,
		Color horizonColor,
		Color skyMidColor,
		Color sunColor,
		Color ambientColor,
		Color fogColor,
		Color cloudColor,
		float sunIntensity,
		float ambientIntensity,
		float fogDensity,
		float fogStartDistance,
		float fogEndDistance,
		float skyExposure,
		float starIntensity,
		float cloudOpacity,
		float cloudDrift )
	{
		Hours = hours;
		DayPercent = hours / ThornsEnvironmentMath.HoursPerDay;
		SunDirection = sunDirection;
		MoonDirection = moonDirection;
		SunLightRotation = sunLightRotation;
		SunHeight = sunHeight;
		SunFactor = sunFactor;
		NightFactor = nightFactor;
		TwilightFactor = twilightFactor;
		Phase = phase;
		ZenithColor = zenithColor;
		HorizonColor = horizonColor;
		SkyMidColor = skyMidColor;
		SunColor = sunColor;
		AmbientColor = ambientColor;
		FogColor = fogColor;
		CloudColor = cloudColor;
		SunIntensity = sunIntensity;
		AmbientIntensity = ambientIntensity;
		FogDensity = fogDensity;
		FogStartDistance = fogStartDistance;
		FogEndDistance = fogEndDistance;
		SkyExposure = skyExposure;
		StarIntensity = starIntensity;
		CloudOpacity = cloudOpacity;
		CloudDrift = cloudDrift;
	}

	public static ThornsEnvironmentState Evaluate( ThornsTimeOfDaySystem time )
	{
		var hours = ThornsEnvironmentMath.WrapHours( time.ResolvedHours );
		var sunDir = ThornsEnvironmentMath.SunDirection( hours, time.SunriseDirection, time.SunYawOffset, time.SunPitchOffset );
		var moonDir = ThornsEnvironmentMath.MoonDirection( sunDir );
		var sunHeight = Math.Clamp( sunDir.z, -1f, 1f );
		var sunFactor = ThornsEnvironmentMath.SmoothRange( -0.05f, 0.35f, sunHeight );
		// Widened so the golden-hour glow ramps in while the sun is still well below the
		// horizon and only fully clears once the sun is fairly high — this both lengthens
		// twilight and lets it peak stronger (product tops out near ~0.85 instead of ~0.7).
		var fullDay = ThornsEnvironmentMath.SmoothRange( 0f, 0.85f, sunHeight );
		var nightFactor = 1f - ThornsEnvironmentMath.SmoothRange( -0.55f, 0.30f, sunHeight );
		var twilightFactor = (1f - fullDay) * (1f - nightFactor);

		var zenith = time.ZenithColors.Evaluate( hours );
		var horizon = time.HorizonColors.Evaluate( hours );
		var sun = time.SunColors.Evaluate( hours );
		var ambient = time.AmbientColors.Evaluate( hours );
		var fog = time.FogColors.Evaluate( hours );
		var cloud = time.CloudColors.Evaluate( hours );
		var mid = Color.Lerp( horizon, zenith, 0.55f );

		var sunIntensity = sunFactor * time.MaxSunIntensity;
		var ambientIntensity = MathX.Lerp( time.NightAmbientIntensity, time.MaxAmbientIntensity, ThornsEnvironmentMath.SmoothRange( -0.15f, 0.45f, sunHeight ) );
		var fogDensity = MathX.Lerp( time.NightFogDensity, time.DayFogDensity, fullDay );
		fogDensity = MathX.Lerp( fogDensity, time.TwilightFogDensity, twilightFactor );
		var exposure = MathX.Lerp( time.NightSkyExposure, time.DaySkyExposure, fullDay );
		exposure = MathX.Lerp( exposure, time.TwilightSkyExposure, twilightFactor );
		var stars = nightFactor * time.MaxStarIntensity;
		var cloudOpacity = MathX.Lerp( time.NightCloudOpacity, time.DayCloudOpacity, fullDay );
		cloudOpacity = MathX.Lerp( cloudOpacity, time.TwilightCloudOpacity, twilightFactor );
		var drift = (float)(Time.Now * time.CloudDriftSpeed);

		return new ThornsEnvironmentState(
			hours,
			sunDir,
			moonDir,
			ThornsEnvironmentMath.DirectionalLightRotation( sunDir ),
			sunHeight,
			sunFactor,
			nightFactor,
			twilightFactor,
			DescribePhase( hours, sunHeight ),
			zenith,
			horizon,
			mid,
			sun,
			ambient,
			fog,
			cloud,
			sunIntensity,
			ambientIntensity,
			fogDensity,
			time.FogStartDistance,
			time.FogEndDistance,
			exposure,
			stars,
			cloudOpacity,
			drift );
	}

	static string DescribePhase( float hours, float sunHeight )
	{
		if ( hours >= ThornsEnvironmentTwilightSchedule.DeepNightEndHour && hours < ThornsEnvironmentTwilightSchedule.DayStartHour )
			return "Sunrise";
		if ( hours >= ThornsEnvironmentTwilightSchedule.NoonEndHour && hours < ThornsEnvironmentTwilightSchedule.NightBlendEndHour )
			return "Sunset";
		if ( sunHeight < -0.08f )
			return "Night";
		return "Day";
	}

}
