namespace Sandbox;

/// <summary>
/// Minecraft-style celestial state derived from a single <see cref="TimeOfDay01"/> clock.
/// All blending is continuous — no discrete day/night/sunset branches.
/// </summary>
public readonly struct ThornsCelestialState
{
	public float TimeOfDay01 { get; init; }
	public Vector3 SunDirection { get; init; }
	public Vector3 MoonDirection { get; init; }
	public float SunAltitudeRadians { get; init; }
	public float SunAltitudeDegrees { get; init; }
	public float MoonAltitudeRadians { get; init; }
	public Rotation SunLightRotation { get; init; }

	public float DayWeight { get; init; }
	public float TwilightWeight { get; init; }
	public float NightWeight { get; init; }

	public Color SunLightColor { get; init; }
	public float SunLightIntensity { get; init; }
	public Color AmbientSkyColor { get; init; }
	public float AmbientIntensity { get; init; }

	public Color SkyZenith { get; init; }
	public Color SkyHorizon { get; init; }
	public Color SkyMid { get; init; }
	public Color HorizonGlowColor { get; init; }
	public float HorizonGlowStrength { get; init; }

	public Color FogColor { get; init; }
	public float FogStrength { get; init; }

	public float StarBrightness { get; init; }
	public float StarRotation { get; init; }
	public float MoonLightContribution { get; init; }
	public Color MoonDiscColor { get; init; }
	public float MoonDiscIntensity { get; init; }

	public Color SunDiscColor { get; init; }
	public float SunDiscIntensity { get; init; }
	public float SunDiscGlow { get; init; }

	public float CloudOpacity { get; init; }
	public Color CloudTint { get; init; }
	public float CloudDrift { get; init; }

	public float SkyExposure { get; init; }
	public float HorizonBandPower { get; init; }
	public bool ShadowsEnabled { get; init; }
	public bool IsNightPhase { get; init; }

	public static ThornsCelestialState Evaluate( float time01, ThornsCelestialTuning tuning )
	{
		time01 = (time01 % 1f + 1f) % 1f;
		EvaluateBody( time01, tuning.SunriseDirection, out var sunDir, out var moonDir, out var sunAltRad, out var moonAltRad, out var lightRot );

		var sunAltDeg = sunAltRad * ThornsCelestialCurves.RadToDeg;

		// Steep day/night ramps — noon and midnight should feel like different worlds.
		var dayLinear = ThornsCelestialCurves.SmoothRange( sunAltRad, tuning.DayStartAltRad, tuning.DayFullAltRad );
		var dayWeight = ThornsCelestialCurves.Steepen( dayLinear, tuning.DayCurvePower );
		var nightLinear = 1f - ThornsCelestialCurves.SmoothRange( sunAltRad, tuning.NightFullAltRad, tuning.NightEndAltRad );
		var nightWeight = ThornsCelestialCurves.Steepen( nightLinear, tuning.NightCurvePower );
		var twilightWeight = Math.Clamp( 1f - dayWeight - nightWeight, 0f, 1f );
		ThornsCelestialCurves.RenormalizePhaseWeights( ref dayWeight, ref twilightWeight, ref nightWeight );

		// Civil twilight: sun 0° to −10° below horizon — orange band + purple sky linger after sunset.
		var civilTwilight = ThornsCelestialCurves.SmoothRange( sunAltRad, tuning.CivilTwilightDeepAltRad, tuning.CivilTwilightHighAltRad )
			* (1f - dayWeight * 0.92f);
		var morningBell = ThornsCelestialCurves.TwilightBell( time01, 0.25f, tuning.TwilightHalfWidth );
		var eveningBell = ThornsCelestialCurves.TwilightBell( time01, 0.75f, tuning.TwilightHalfWidth );
		var morningTwilight = Math.Clamp( twilightWeight * morningBell + civilTwilight * morningBell, 0f, 1f );
		var eveningTwilight = Math.Clamp( twilightWeight * eveningBell + civilTwilight * eveningBell, 0f, 1f );
		var goldenHour = Math.Clamp( morningTwilight + eveningTwilight, 0f, 1f );

		var sunriseColor = ThornsCelestialCurves.ByteColor( 255, 188, 118 );
		var noonColor = ThornsCelestialCurves.ByteColor( 255, 228, 175 );
		var sunsetColor = ThornsCelestialCurves.ByteColor( 255, 165, 95 );

		var sunLightColor = Color.Lerp( noonColor, sunriseColor, morningTwilight );
		sunLightColor = Color.Lerp( sunLightColor, sunsetColor, eveningTwilight );

		var altIntensity = ThornsCelestialCurves.SmoothRange( sunAltRad, tuning.SunLightMinAltRad, tuning.SunLightZenithAltRad );
		var sunLightIntensity = tuning.SunPeakIntensity * altIntensity * (0.12f + 0.88f * dayWeight);

		// Distinct palettes per phase (screenshot-readable).
		var zenithNoon = ThornsCelestialCurves.ByteColor( 18, 72, 198 );
		var midNoon = ThornsCelestialCurves.ByteColor( 48, 128, 228 );
		var horizonNoon = ThornsCelestialCurves.ByteColor( 118, 188, 255 );

		var zenithSunrise = ThornsCelestialCurves.ByteColor( 52, 108, 215 );
		var midSunrise = ThornsCelestialCurves.ByteColor( 198, 88, 168 );
		var horizonSunrise = ThornsCelestialCurves.ByteColor( 255, 128, 52 );

		var zenithSunset = ThornsCelestialCurves.ByteColor( 48, 32, 108 );
		var midSunset = ThornsCelestialCurves.ByteColor( 168, 52, 128 );
		var horizonSunset = ThornsCelestialCurves.ByteColor( 255, 58, 38 );

		var zenithTwilight = ThornsCelestialCurves.ByteColor( 38, 28, 88 );
		var midTwilight = ThornsCelestialCurves.ByteColor( 98, 48, 118 );
		var horizonTwilight = ThornsCelestialCurves.ByteColor( 255, 108, 48 );

		var zenithNight = ThornsCelestialCurves.ByteColor( 8, 12, 25 );
		var midNight = ThornsCelestialCurves.ByteColor( 12, 18, 35 );
		var horizonNight = ThornsCelestialCurves.ByteColor( 18, 25, 45 );

		var skyZenith = ThornsCelestialCurves.BlendPalette( zenithNight, zenithNoon, zenithSunrise, zenithSunset, zenithTwilight,
			dayWeight, morningTwilight, eveningTwilight, civilTwilight );
		var skyMid = ThornsCelestialCurves.BlendPalette( midNight, midNoon, midSunrise, midSunset, midTwilight,
			dayWeight, morningTwilight, eveningTwilight, civilTwilight );
		var skyHorizon = ThornsCelestialCurves.BlendPalette( horizonNight, horizonNoon, horizonSunrise, horizonSunset, horizonTwilight,
			dayWeight, morningTwilight, eveningTwilight, civilTwilight );

		var glowSunrise = ThornsCelestialCurves.ByteColor( 255, 178, 72 );
		var glowSunset = ThornsCelestialCurves.ByteColor( 255, 98, 42 );
		var glowMix = eveningTwilight / Math.Max( morningTwilight + eveningTwilight, 0.001f );
		var horizonGlowColor = Color.Lerp( glowSunrise, glowSunset, glowMix );
		var glowAlt = ThornsCelestialCurves.SmoothRange( sunAltRad, tuning.HorizonGlowDeepAltRad, tuning.HorizonGlowHighAltRad )
			+ civilTwilight * 0.85f;
		var horizonGlowStrength = goldenHour * glowAlt * tuning.HorizonGlowStrengthScale;

		var ambientDay = ThornsCelestialCurves.ByteColor( 210, 188, 148 );
		var ambientTwilight = ThornsCelestialCurves.ByteColor( 148, 98, 88 );
		var ambientNight = ThornsCelestialCurves.ByteColor( 88, 98, 128 );
		var ambientColor = Color.Lerp( ambientNight, ambientDay, dayWeight );
		ambientColor = Color.Lerp( ambientColor, ambientTwilight, twilightWeight + civilTwilight * 0.45f );
		var ambientDayCurve = ThornsCelestialCurves.Steepen( dayLinear, tuning.DayCurvePower );
		var ambientIntensity = MathX.Lerp( tuning.AmbientNight, tuning.AmbientNoon, ambientDayCurve )
			+ goldenHour * (tuning.AmbientSunset - tuning.AmbientNight) * 0.65f;

		var moonLightContribution = tuning.MoonAmbientStrength * ThornsCelestialCurves.SmoothRange( moonAltRad, -0.05f, 0.22f ) * nightWeight;
		var fogNoon = horizonNoon;
		var fogSunset = Color.Lerp( horizonSunset, midSunset, 0.42f );
		var fogNight = ThornsCelestialCurves.ByteColor( 10, 14, 28 );
		var fogTwilight = Color.Lerp( horizonTwilight, zenithTwilight, 0.35f );
		var fogColor = ThornsCelestialCurves.BlendPalette( fogNight, fogNoon, fogTwilight, fogSunset, fogTwilight,
			dayWeight, morningTwilight, eveningTwilight, civilTwilight );
		fogColor = Color.Lerp( fogColor, horizonGlowColor, Math.Clamp( horizonGlowStrength * 0.38f, 0f, 0.72f ) );
		var fogStrength = MathX.Lerp( tuning.FogNight, tuning.FogDay, ambientDayCurve )
			+ goldenHour * (tuning.FogSunset - tuning.FogNight);

		var starBrightness = nightWeight * (1f - ThornsCelestialCurves.SmoothRange( sunAltRad, -0.2f, 0.08f ));
		var starRotation = time01 * MathF.Tau;

		var moonDiscIntensity = tuning.MoonDiscIntensity * ThornsCelestialCurves.SmoothRange( moonAltRad, 0.02f, 0.28f ) * (1f - dayWeight * 0.92f);
		var moonDiscColor = ThornsCelestialCurves.ByteColor( 210, 220, 255 );

		var sunDiscIntensity = tuning.SunDiscIntensity * (0.22f + 0.78f * dayWeight + goldenHour * 0.75f)
			* ThornsCelestialCurves.SmoothRange( sunAltRad, tuning.SunDiscHideAltRad, tuning.SunDiscFullAltRad );
		var sunDiscColor = Color.Lerp( sunLightColor, ThornsCelestialCurves.ByteColor( 255, 175, 72 ), goldenHour * 0.55f );
		var sunDiscGlow = tuning.SunDiscGlow * (0.15f + goldenHour * 0.42f);

		var cloudNoon = ThornsCelestialCurves.ByteColor( 252, 252, 255 );
		var cloudSunrise = ThornsCelestialCurves.ByteColor( 255, 198, 138 );
		var cloudSunset = ThornsCelestialCurves.ByteColor( 255, 128, 108 );
		var cloudNight = ThornsCelestialCurves.ByteColor( 32, 42, 62 );
		var cloudTint = Color.Lerp( cloudNight, cloudNoon, dayWeight );
		cloudTint = Color.Lerp( cloudTint, cloudSunrise, morningTwilight );
		cloudTint = Color.Lerp( cloudTint, cloudSunset, eveningTwilight );
		var cloudOpacity = tuning.CloudOpacity * (0.28f + 0.72f * (dayWeight + goldenHour * 0.85f));

		var skyBrightness = ThornsCelestialCurves.Steepen( dayLinear, tuning.DayCurvePower );
		var skyExposure = MathX.Lerp( tuning.SkyExposureNight, tuning.SkyExposureDay, skyBrightness );
		var horizonBandPower = MathX.Lerp( 1.05f, 0.82f, goldenHour );
		var shadows = sunAltRad > tuning.ShadowMinAltRad && sunLightIntensity > 0.06f;

		return new ThornsCelestialState
		{
			TimeOfDay01 = time01,
			SunDirection = sunDir,
			MoonDirection = moonDir,
			SunAltitudeRadians = sunAltRad,
			SunAltitudeDegrees = sunAltDeg,
			MoonAltitudeRadians = moonAltRad,
			SunLightRotation = lightRot,
			DayWeight = dayWeight,
			TwilightWeight = twilightWeight,
			NightWeight = nightWeight,
			SunLightColor = sunLightColor,
			SunLightIntensity = sunLightIntensity,
			AmbientSkyColor = ambientColor,
			AmbientIntensity = ambientIntensity,
			SkyZenith = skyZenith,
			SkyHorizon = skyHorizon,
			SkyMid = skyMid,
			HorizonGlowColor = horizonGlowColor,
			HorizonGlowStrength = horizonGlowStrength,
			FogColor = fogColor,
			FogStrength = fogStrength,
			StarBrightness = starBrightness,
			StarRotation = starRotation,
			MoonLightContribution = moonLightContribution,
			MoonDiscColor = moonDiscColor,
			MoonDiscIntensity = moonDiscIntensity,
			SunDiscColor = sunDiscColor,
			SunDiscIntensity = sunDiscIntensity,
			SunDiscGlow = sunDiscGlow,
			CloudOpacity = cloudOpacity,
			CloudTint = cloudTint,
			CloudDrift = time01,
			SkyExposure = skyExposure,
			HorizonBandPower = horizonBandPower,
			ShadowsEnabled = shadows,
			IsNightPhase = nightWeight > 0.52f
		};
	}

	static void EvaluateBody(
		float time01,
		ThornsCelestialSunRiseDirection sunrise,
		out Vector3 sunDirection,
		out Vector3 moonDirection,
		out float sunAltRad,
		out float moonAltRad,
		out Rotation lightRotation )
	{
		var sunArc = (time01 - 0.25f) * MathF.Tau;
		sunDirection = OrientOnArc( sunArc, sunrise );
		sunAltRad = MathF.Asin( Math.Clamp( sunDirection.z, -1f, 1f ) );

		var moonArc = (time01 + 0.5f - 0.25f) * MathF.Tau;
		moonDirection = OrientOnArc( moonArc, sunrise );
		moonAltRad = MathF.Asin( Math.Clamp( moonDirection.z, -1f, 1f ) );

		var lightTravel = -sunDirection;
		if ( lightTravel.Length < 0.001f )
			lightTravel = Vector3.Down;
		lightRotation = Rotation.LookAt( lightTravel, Vector3.Up );
	}

	static Vector3 OrientOnArc( float arcAngle, ThornsCelestialSunRiseDirection sunrise )
	{
		var baseDir = new Vector3( 0f, MathF.Cos( arcAngle ), MathF.Sin( arcAngle ) );
		return sunrise switch
		{
			ThornsCelestialSunRiseDirection.East => baseDir.Normal,
			ThornsCelestialSunRiseDirection.North => new Vector3( MathF.Cos( arcAngle ), 0f, MathF.Sin( arcAngle ) ).Normal,
			ThornsCelestialSunRiseDirection.West => new Vector3( 0f, -MathF.Cos( arcAngle ), MathF.Sin( arcAngle ) ).Normal,
			ThornsCelestialSunRiseDirection.South => new Vector3( -MathF.Cos( arcAngle ), 0f, MathF.Sin( arcAngle ) ).Normal,
			_ => baseDir.Normal
		};
	}
}

public sealed class ThornsCelestialTuning
{
	public ThornsCelestialSunRiseDirection SunriseDirection { get; set; } = ThornsCelestialSunRiseDirection.East;
	public float TwilightHalfWidth { get; set; } = 0.14f;
	public float SunPeakIntensity { get; set; } = 2.35f;
	public float AmbientNoon { get; set; } = 0.44f;
	public float AmbientSunset { get; set; } = 0.14f;
	public float AmbientNight { get; set; } = 0.05f;
	public float MoonAmbientStrength { get; set; } = 0.09f;
	public float FogDay { get; set; } = 0.26f;
	public float FogSunset { get; set; } = 0.52f;
	public float FogNight { get; set; } = 0.38f;
	public float CloudOpacity { get; set; } = 0.28f;
	public float SunDiscIntensity { get; set; } = 1.65f;
	public float SunDiscGlow { get; set; } = 0.004f;
	public float MoonDiscIntensity { get; set; } = 0.85f;
	public float SkyExposureDay { get; set; } = 1.18f;
	public float SkyExposureNight { get; set; } = 0.15f;
	public float HorizonGlowStrengthScale { get; set; } = 3.2f;
	public float DayCurvePower { get; set; } = 2.65f;
	public float NightCurvePower { get; set; } = 2.35f;

	public float DayStartAltRad { get; set; } = -0.1f;
	public float DayFullAltRad { get; set; } = 0.48f;
	public float NightFullAltRad { get; set; } = -0.38f;
	public float NightEndAltRad { get; set; } = 0.04f;
	public float CivilTwilightDeepAltRad { get; set; } = -0.175f;
	public float CivilTwilightHighAltRad { get; set; } = 0.05f;
	public float SunLightMinAltRad { get; set; } = -0.22f;
	public float SunLightZenithAltRad { get; set; } = 1.45f;
	public float HorizonGlowDeepAltRad { get; set; } = -0.22f;
	public float HorizonGlowHighAltRad { get; set; } = 0.22f;
	public float SunDiscHideAltRad { get; set; } = -0.12f;
	public float SunDiscFullAltRad { get; set; } = 0.08f;
	public float ShadowMinAltRad { get; set; } = 0.04f;
}

static class ThornsCelestialCurves
{
	public const float RadToDeg = 180f / MathF.PI;

	public static float SmoothRange( float value, float edge0, float edge1 )
	{
		if ( edge1 <= edge0 )
			return value >= edge1 ? 1f : 0f;
		var t = Math.Clamp( (value - edge0) / (edge1 - edge0), 0f, 1f );
		return t * t * (3f - 2f * t);
	}

	public static float TwilightBell( float time01, float center, float halfWidth )
	{
		if ( halfWidth <= 0f )
			return 0f;
		var dist = MathF.Abs( time01 - center );
		if ( dist >= halfWidth )
			return 0f;
		var t = 1f - dist / halfWidth;
		return t * t * (3f - 2f * t);
	}

	public static Color ByteColor( int r, int g, int b ) =>
		new Color( r / 255f, g / 255f, b / 255f, 1f );

	public static void RenormalizePhaseWeights( ref float day, ref float twilight, ref float night )
	{
		var sum = day + twilight + night;
		if ( sum <= 0.0001f )
		{
			day = 0f;
			twilight = 0f;
			night = 1f;
			return;
		}

		day /= sum;
		twilight /= sum;
		night /= sum;
	}

	public static float Steepen( float t, float power )
	{
		t = Math.Clamp( t, 0f, 1f );
		if ( power <= 0.01f )
			return t;
		return MathF.Pow( t, power );
	}

	public static Color BlendPalette(
		Color night,
		Color noon,
		Color sunrise,
		Color sunset,
		Color civil,
		float dayWeight,
		float morningTwilight,
		float eveningTwilight,
		float civilTwilight )
	{
		var c = Color.Lerp( night, noon, dayWeight );
		c = Color.Lerp( c, sunrise, morningTwilight );
		c = Color.Lerp( c, sunset, eveningTwilight );
		c = Color.Lerp( c, civil, civilTwilight * 0.75f );
		return c;
	}
}
