namespace Sandbox;

/// <summary>Inspector-driven multipliers applied after procedural sky evaluation (play-mode friendly).</summary>
public static class ThornsCelestialLiveTuning
{
	public static ThornsCelestialState ApplyFromInspector( ThornsCelestialState state, ThornsCelestialSystem sys )
	{
		if ( sys is null || !sys.IsValid() )
			return state;

		if ( sys.LiveForceTestColors )
		{
			return new ThornsCelestialState
			{
				TimeOfDay01 = state.TimeOfDay01,
				SunDirection = state.SunDirection,
				MoonDirection = state.MoonDirection,
				SunAltitudeRadians = state.SunAltitudeRadians,
				SunAltitudeDegrees = state.SunAltitudeDegrees,
				MoonAltitudeRadians = state.MoonAltitudeRadians,
				SunLightRotation = state.SunLightRotation,
				DayWeight = state.DayWeight,
				TwilightWeight = state.TwilightWeight,
				NightWeight = state.NightWeight,
				SunLightColor = state.SunLightColor,
				SunLightIntensity = state.SunLightIntensity,
				AmbientSkyColor = state.AmbientSkyColor,
				AmbientIntensity = state.AmbientIntensity,
				SkyZenith = new Color( 0.85f, 0.1f, 0.75f ),
				SkyMid = new Color( 0.95f, 0.35f, 0.15f ),
				SkyHorizon = new Color( 1f, 0.55f, 0.08f ),
				HorizonGlowColor = new Color( 1f, 0.4f, 0.05f ),
				HorizonGlowStrength = 4f,
				FogColor = new Color( 0.2f, 0.08f, 0.35f ),
				FogStrength = 0.15f,
				StarBrightness = 0f,
				StarRotation = state.StarRotation,
				MoonLightContribution = state.MoonLightContribution,
				MoonDiscColor = state.MoonDiscColor,
				MoonDiscIntensity = state.MoonDiscIntensity,
				SunDiscColor = state.SunDiscColor,
				SunDiscIntensity = state.SunDiscIntensity,
				SunDiscGlow = state.SunDiscGlow,
				CloudOpacity = 0.1f,
				CloudTint = Color.White,
				CloudDrift = state.CloudDrift,
				SkyExposure = 1.35f,
				HorizonBandPower = 0.82f,
				ShadowsEnabled = state.ShadowsEnabled,
				IsNightPhase = state.IsNightPhase
			};
		}

		var skyIntensity = Math.Max( 0.01f, sys.LiveSkyColorIntensity );
		var skyTint = sys.LiveSkyColorTint;
		var saturation = Math.Max( 0f, sys.LiveSkySaturation );

		var zenith = ScaleSkyColor( state.SkyZenith, skyTint, skyIntensity, saturation );
		var mid = ScaleSkyColor( state.SkyMid, skyTint, skyIntensity, saturation );
		var horizon = ScaleSkyColor( state.SkyHorizon, skyTint, skyIntensity, saturation );
		var glowColor = ScaleSkyColor( state.HorizonGlowColor, skyTint, skyIntensity, saturation );
		var fogColor = ScaleSkyColor( state.FogColor, sys.LiveFogColorTint, skyIntensity, saturation );
		var cloudTint = ScaleSkyColor( state.CloudTint, skyTint, skyIntensity, saturation );

		if ( sys.LiveUseSkyPaletteOverride )
		{
			var blend = Math.Clamp( sys.LivePaletteOverrideBlend, 0f, 1f );
			zenith = Color.Lerp( zenith, sys.LiveOverrideZenith, blend );
			mid = Color.Lerp( mid, sys.LiveOverrideMid, blend );
			horizon = Color.Lerp( horizon, sys.LiveOverrideHorizon, blend );
		}

		var sunColor = new Color(
			state.SunLightColor.r * sys.LiveSunLightColorTint.r,
			state.SunLightColor.g * sys.LiveSunLightColorTint.g,
			state.SunLightColor.b * sys.LiveSunLightColorTint.b,
			state.SunLightColor.a );

		return new ThornsCelestialState
		{
			TimeOfDay01 = state.TimeOfDay01,
			SunDirection = state.SunDirection,
			MoonDirection = state.MoonDirection,
			SunAltitudeRadians = state.SunAltitudeRadians,
			SunAltitudeDegrees = state.SunAltitudeDegrees,
			MoonAltitudeRadians = state.MoonAltitudeRadians,
			SunLightRotation = state.SunLightRotation,
			DayWeight = state.DayWeight,
			TwilightWeight = state.TwilightWeight,
			NightWeight = state.NightWeight,
			SunLightColor = sunColor,
			SunLightIntensity = state.SunLightIntensity * Math.Max( 0f, sys.LiveSunIntensityScale ),
			AmbientSkyColor = ScaleSkyColor( state.AmbientSkyColor, sys.LiveAmbientColorTint, sys.LiveAmbientIntensityScale, 1f ),
			AmbientIntensity = state.AmbientIntensity * Math.Max( 0f, sys.LiveAmbientIntensityScale ),
			SkyZenith = zenith,
			SkyHorizon = horizon,
			SkyMid = mid,
			HorizonGlowColor = glowColor,
			HorizonGlowStrength = state.HorizonGlowStrength * Math.Max( 0f, sys.LiveHorizonGlowScale ),
			FogColor = fogColor,
			FogStrength = state.FogStrength * Math.Max( 0f, sys.LiveFogStrengthScale ),
			StarBrightness = state.StarBrightness * Math.Max( 0f, sys.LiveStarBrightnessScale ),
			StarRotation = state.StarRotation,
			MoonLightContribution = state.MoonLightContribution * Math.Max( 0f, sys.LiveMoonLightScale ),
			MoonDiscColor = state.MoonDiscColor,
			MoonDiscIntensity = state.MoonDiscIntensity * Math.Max( 0f, sys.LiveMoonLightScale ),
			SunDiscColor = ScaleSkyColor( state.SunDiscColor, sys.LiveSunDiscColorTint, 1f, 1f ),
			SunDiscIntensity = state.SunDiscIntensity * Math.Max( 0f, sys.LiveSunDiscIntensityScale ),
			SunDiscGlow = state.SunDiscGlow * Math.Max( 0f, sys.LiveSunDiscIntensityScale ),
			CloudOpacity = state.CloudOpacity * Math.Max( 0f, sys.LiveCloudOpacityScale ),
			CloudTint = cloudTint,
			CloudDrift = state.CloudDrift,
			SkyExposure = state.SkyExposure * Math.Max( 0.01f, sys.LiveSkyExposureScale ),
			HorizonBandPower = state.HorizonBandPower,
			ShadowsEnabled = state.ShadowsEnabled,
			IsNightPhase = state.IsNightPhase
		};
	}

	static Color ScaleSkyColor( Color color, Color tint, float intensity, float saturation )
	{
		var c = new Color(
			color.r * tint.r * intensity,
			color.g * tint.g * intensity,
			color.b * tint.b * intensity,
			color.a );

		if ( saturation <= 0.001f )
			return c;

		var luma = 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
		return new Color(
			MathX.Lerp( luma, c.r, saturation ),
			MathX.Lerp( luma, c.g, saturation ),
			MathX.Lerp( luma, c.b, saturation ),
			c.a );
	}

}
