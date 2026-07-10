HEADER
{
	DevShader = true;
	Version = 1;
}

MODES
{
	Forward();
}

FEATURES
{
	#include "vr_common_features.fxc"
}

COMMON
{
	#include "system.fxc"
	#include "vr_common.fxc"

	static const float kAtmosPi = 3.141592f;
	static const int kAtmosPrimarySteps = 16;
	static const int kAtmosSecondarySteps = 2;

	float2 RaySphereIntersection( float3 origin, float3 direction, float radius )
	{
		float a = dot( direction, direction );
		float b = 2.0f * dot( direction, origin );
		float c = dot( origin, origin ) - ( radius * radius );
		float discriminant = ( b * b ) - 4.0f * a * c;

		if ( discriminant < 0.0f )
		{
			return float2( 1e5f, -1e5f );
		}

		float sqrtDiscriminant = sqrt( discriminant );
		float invDenominator = 0.5f / a;
		return float2(
			( -b - sqrtDiscriminant ) * invDenominator,
			( -b + sqrtDiscriminant ) * invDenominator
		);
	}

	float3 atmosphere( float3 viewDir, float3 rayOrigin, float3 sunDirection, float sunIntensity, float planetRadius, float atmosphereRadius, float3 rayleighCoefficients, float mieCoefficient, float rayleighScaleHeight, float mieScaleHeight, float miePreferredScatteringDirection )
	{
		sunDirection = normalize( sunDirection );
		viewDir = normalize( viewDir );

		float2 intersections = RaySphereIntersection( rayOrigin, viewDir, atmosphereRadius );
		if ( intersections.x > intersections.y )
		{
			return 0.0f;
		}

		intersections.y = min( intersections.y, RaySphereIntersection( rayOrigin, viewDir, planetRadius ).x );
		float primaryStepSize = ( intersections.y - intersections.x ) / float( kAtmosPrimarySteps );

		float rayleighOpticalDepth = 0.0f;
		float mieOpticalDepth = 0.0f;
		float3 accumulatedRayleigh = 0.0f;
		float3 accumulatedMie = 0.0f;

		float cosTheta = dot( viewDir, sunDirection );
		float cosThetaSquared = cosTheta * cosTheta;
		float g = miePreferredScatteringDirection;
		float gSquared = g * g;
		float rayleighPhase = ( 3.0f / ( 16.0f * kAtmosPi ) ) * ( 1.0f + cosThetaSquared );
		float miePhaseDenominator = pow( 1.0f + gSquared - 2.0f * cosTheta * g, 1.5f ) * ( 2.0f + gSquared );
		float miePhaseNumerator = ( 1.0f - gSquared ) * ( cosThetaSquared + 1.0f );
		float miePhase = ( 3.0f / ( 8.0f * kAtmosPi ) ) * ( miePhaseNumerator / miePhaseDenominator );

		float primaryTime = 0.0f;

		[loop]
		for ( int primaryStep = 0; primaryStep < kAtmosPrimarySteps; ++primaryStep )
		{
			float3 samplePosition = rayOrigin + viewDir * ( primaryTime + primaryStepSize * 0.5f );
			float sampleHeight = length( samplePosition ) - planetRadius;

			float rayleighStep = exp( -sampleHeight / rayleighScaleHeight ) * primaryStepSize;
			float mieStep = exp( -sampleHeight / mieScaleHeight ) * primaryStepSize;

			rayleighOpticalDepth += rayleighStep;
			mieOpticalDepth += mieStep;

			float secondaryStepSize = RaySphereIntersection( samplePosition, sunDirection, atmosphereRadius ).y / float( kAtmosSecondarySteps );
			float secondaryTime = 0.0f;
			float secondaryRayleighOpticalDepth = 0.0f;
			float secondaryMieOpticalDepth = 0.0f;

			[loop]
			for ( int secondaryStep = 0; secondaryStep < kAtmosSecondarySteps; ++secondaryStep )
			{
				float3 secondaryPosition = samplePosition + sunDirection * ( secondaryTime + secondaryStepSize * 0.5f );
				float secondaryHeight = length( secondaryPosition ) - planetRadius;

				secondaryRayleighOpticalDepth += exp( -secondaryHeight / rayleighScaleHeight ) * secondaryStepSize;
				secondaryMieOpticalDepth += exp( -secondaryHeight / mieScaleHeight ) * secondaryStepSize;

				secondaryTime += secondaryStepSize;
			}

			float3 transmittance = exp( -( mieCoefficient * ( mieOpticalDepth + secondaryMieOpticalDepth ) + rayleighCoefficients * ( rayleighOpticalDepth + secondaryRayleighOpticalDepth ) ) );

			accumulatedRayleigh += rayleighStep * transmittance;
			accumulatedMie += mieStep * transmittance;

			primaryTime += primaryStepSize;
		}

		float3 rayleighContribution = rayleighPhase * rayleighCoefficients * accumulatedRayleigh;
		float3 mieContribution = ( miePhase * mieCoefficient ) * accumulatedMie;

		return sunIntensity * ( rayleighContribution + mieContribution );
	}
}

struct VS_INPUT
{
	float4 vPositionOs : POSITION < Semantic( PosXyz ); >;
};

struct PS_INPUT
{
	float3 vPositionWs : TEXCOORD1;

	#if ( PROGRAM == VFX_PROGRAM_VS )
		float4 vPositionPs : SV_Position;
	#endif
	#if ( PROGRAM == VFX_PROGRAM_PS )
		float4 vPositionSs : SV_Position;
	#endif
};

VS
{
	#define IS_SPRITECARD 1
	#include "system.fxc"

	PS_INPUT MainVs( const VS_INPUT i )
	{
		PS_INPUT o;
		float flSkyboxScale = g_flNearPlane + g_flFarPlane;
		float3 vPositionWs = g_vCameraPositionWs.xyz + i.vPositionOs.xyz * flSkyboxScale;

		o.vPositionPs = Position3WsToPs( vPositionWs );
		o.vPositionWs = vPositionWs;

		return o;
	}
}

PS
{
	#include "vr_lighting.fxc"
	#include "volumetric_fog.fxc"
	#include "common/Shadow.hlsl"

	RenderState( CullMode, NONE );
	RenderState( DepthWriteEnable, false );
	RenderState( DepthEnable, true );
	RenderState( DepthFunc, GREATER_EQUAL );

	BoolAttribute( sky, true );

	float3 g_vSkyZenithDay < Attribute( "SkyZenithDay" ); Default3( 0.12, 0.30, 0.58 ); >;
	float3 g_vSkyHorizonDay < Attribute( "SkyHorizonDay" ); Default3( 0.34, 0.54, 0.74 ); >;
	float3 g_vSkyZenithTwilight < Attribute( "SkyZenithTwilight" ); Default3( 0.78, 0.20, 0.05 ); >;
	float3 g_vSkyHorizonTwilight < Attribute( "SkyHorizonTwilight" ); Default3( 1.0, 0.32, 0.06 ); >;
	float3 g_vSkyZenithNight < Attribute( "SkyZenithNight" ); Default3( 0.004, 0.005, 0.012 ); >;
	float3 g_vSkyHorizonNight < Attribute( "SkyHorizonNight" ); Default3( 0.012, 0.014, 0.028 ); >;
	float3 g_vSunDiscColorDay < Attribute( "SunDiscColorDay" ); Default3( 1.0, 0.55, 0.10 ); >;
	float3 g_vSunDiscColorTwilight < Attribute( "SunDiscColorTwilight" ); Default3( 1.0, 0.32, 0.05 ); >;
	float g_flSunDiscIntensity < Attribute( "SunDiscIntensity" ); Default( 1.35 ); >;
	float g_flSunGlowIntensity < Attribute( "SunGlowIntensity" ); Default( 0.012 ); >;
	float g_flSunDiscAngularDiameter < Attribute( "SunDiscAngularDiameter" ); Default( 24.0 ); >;
	float g_flAtmosphereStrength < Attribute( "AtmosphereStrength" ); Default( 1.28 ); >;
	float g_flSkyFogBlend < Attribute( "SkyFogBlend" ); Default( 0.14 ); >;
	float g_flSkyTwilightTintStrength < Attribute( "SkyTwilightTintStrength" ); Default( 1.35 ); >;
	float g_flSkyTwilightScatterMix < Attribute( "SkyTwilightScatterMix" ); Default( 0.92 ); >;
	float g_flSkyTwilightPhase < Attribute( "SkyTwilightPhase" ); Default( 0.0 ); >;
	float g_flSkyNightFactor < Attribute( "SkyNightFactor" ); Default( 0.0 ); >;
	float g_flTimeOfDay01 < Attribute( "TimeOfDay01" ); Default( 0.5 ); >;
	float3 g_vSkyTwilightVisualTint < Attribute( "SkyTwilightVisualTint" ); Default3( 1.0, 1.0, 1.0 ); >;
	float g_flSkyTwilightVisualTintStrength < Attribute( "SkyTwilightVisualTintStrength" ); Default( 0.0 ); >;
	float3 g_vTonemapShoulder < Attribute( "TonemapShoulder" ); Default3( 0.38, 0.22, 0.32 ); >;

	float3 BoostSkySaturation( float3 color, float amount )
	{
		float luma = dot( color, float3( 0.299, 0.587, 0.114 ) );
		return lerp( float3( luma, luma, luma ), color, amount );
	}

	float3 PreserveSkyHue( float3 color, float maxIntensity )
	{
		float peak = max( max( color.r, color.g ), color.b );
		if ( peak > maxIntensity )
			color *= maxIntensity / peak;
		return color;
	}

	float3 GetTwilightSkyTintFromUniforms()
	{
		float3 horizon = max( g_vSkyHorizonTwilight, float3( 1e-4, 1e-4, 1e-4 ) );
		float3 zenith = max( g_vSkyZenithTwilight, float3( 1e-4, 1e-4, 1e-4 ) );
		return normalize( horizon + zenith * 0.45 );
	}

	float DaylightFromTime( float time01 )
	{
		time01 = frac( time01 );
		return cos( ( time01 - 0.5 ) * 6.28318 ) * 0.5 + 0.5;
	}

	float2 ResolveSkyPhase( float sunElevSigned )
	{
		float sunNight = smoothstep( 0.10, -0.40, sunElevSigned );
		float sunTwilight = smoothstep( 0.28, -0.06, sunElevSigned ) * ( 1.0 - sunNight );
		float timeNight = 1.0 - DaylightFromTime( g_flTimeOfDay01 );

		float night = saturate( max( max( g_flSkyNightFactor, sunNight ), timeNight ) );
		float twilight = saturate( max( g_flSkyTwilightPhase, max( sunTwilight, timeNight * 0.18 * ( 1.0 - night ) ) ) );
		return float2( night, twilight );
	}

	struct PS_OUTPUT
	{
		float4 vColor0 : SV_Target0;
	};

	float noise( in float3 x )
	{
		float3 p = floor(x);
		float3 f = frac(x);
		f = f*f*(3.0-2.0*f);
		float2 uv = (p.xy+float2(37.0,17.0) * p.z) + f.xy;
		float2 rg = g_tBlueNoise.Sample( g_sBilinearWrap, (uv + 0.5 )/256.0 ).xy;
		return lerp( rg.x, rg.y, f.z );
	}

	float3 ThornsSkyGradient( float3 vRay, float sunElev, float nightFactor, float twilightPhase )
	{
		// Blend day / twilight / night — twilightPhase and nightFactor come from sun + time (see ResolveSkyPhase).
		float night = saturate( nightFactor );
		float twilight = saturate( twilightPhase ) * ( 1.0 - night );
		float twilightPeak = pow( twilight, 0.58 );
		float day = saturate( 1.0 - twilight - night );

		float tintStrength = max( g_flSkyTwilightTintStrength, 0.5 );
		float strengthNorm = saturate( ( tintStrength - 0.5 ) / 3.5 );
		float twilightDominance = saturate( twilightPeak * lerp( 0.28, 0.52, strengthNorm ) ) * ( 1.0 - smoothstep( 0.55, 0.78, night ) );
		day *= 1.0 - twilightDominance * 0.88;
		night = saturate( night + twilightDominance * 0.35 );
		twilight = saturate( twilight + twilightDominance * 0.42 ) * ( 1.0 - smoothstep( 0.60, 0.82, nightFactor ) );

		float skyGradPower = lerp( 0.42, 0.16, twilightPeak );
		float skyGrad = pow( saturate( vRay.z ), skyGradPower );

		float3 zenithTwilight = g_vSkyZenithTwilight;
		float3 horizonTwilight = g_vSkyHorizonTwilight;

		float twilightBlend = saturate( twilight * lerp( 0.72, 1.18, strengthNorm ) );
		float3 zenith = g_vSkyZenithDay * day + zenithTwilight * twilightBlend + g_vSkyZenithNight * night;
		float3 horizon = g_vSkyHorizonDay * day + horizonTwilight * twilightBlend + g_vSkyHorizonNight * night;

		float3 gradient = lerp( horizon, zenith, skyGrad );

		float horizonBand = pow( saturate( 1.0 - vRay.z ), lerp( 1.5, 3.6, twilightPeak ) );
		float3 horizonGlow = horizonTwilight * horizonBand * twilightPeak * lerp( 0.28, 0.62, strengthNorm );
		float3 domeGlow = lerp( zenithTwilight, horizonTwilight, 0.62 ) * ( 0.22 + 0.78 * ( 1.0 - skyGrad ) ) * twilightPeak * lerp( 0.18, 0.42, strengthNorm );

		float3 result = gradient + horizonGlow * ( 1.0 - nightFactor ) + domeGlow * ( 1.0 - nightFactor );
		float satBoost = lerp( 1.0, 1.08 + strengthNorm * 0.28, twilightPeak );
		return BoostSkySaturation( result, satBoost );
	}

	float3 GetAtmosphere( float3 ray, float3 sunDirectionWs, float3 sunColor, float sunElev, float twilightPhase, float nightFactor )
	{
		float fPlanetSize = 6371e3;
		float fAtmosphereSize = 100e3;
		float fSeaLevel = 512.0f;

		float3 color = atmosphere
		(
			ray.xzy,
			float3(0,fPlanetSize + g_vCameraPositionWs.z + fSeaLevel,0),
			sunDirectionWs.xzy,
			14,
			fPlanetSize,
			fPlanetSize + fAtmosphereSize,
			float3(5.5e-6, 13.0e-6, 22.4e-6),
			21e-6,
			8e3,
			1.2e3,
			0.758
		);

		float sunLum = max( dot( sunColor, float3( 0.299, 0.587, 0.114 ) ), 0.001 );
		float3 sunTint = normalize( sunColor / sunLum );
		float twilightScatter = saturate( twilightPhase ) * ( 1.0 - saturate( nightFactor ) );
		float3 twilightTint = GetTwilightSkyTintFromUniforms();
		sunTint = lerp( sunTint, twilightTint, twilightScatter * g_flSkyTwilightScatterMix );
		sunTint = lerp( sunTint, float3( 1.0, 0.76, 0.38 ), twilightScatter * 0.12 );

		float strength = lerp( 0.22, 0.42, twilightScatter ) * saturate( sunElev * 1.6 + 0.08 );
		return color * sunTint * strength * g_flAtmosphereStrength;
	}

	float Stars( in float3 vRay )
	{
		const float fStarScale = 0.3;
		const float fStarAmount = 1.0;

		float vStars = noise(vRay * ( g_vViewportSize.y * fStarScale ) * 0.75 );
		vStars += noise(vRay * ( g_vViewportSize.y * fStarScale ) * 0.5 );
		vStars += noise(vRay * ( g_vViewportSize.y * fStarScale ) * 0.25);
		vStars += noise(vRay * ( g_vViewportSize.y * fStarScale ) * 0.1 );
		vStars += noise(vRay * ( g_vViewportSize.y * fStarScale ) ) * (1.0 - fStarAmount);

		vStars = clamp(vStars, 0.0, 1.0);
		vStars = (1.0 - vStars);
		vStars *= saturate( vRay.z * 100 );

		return vStars;
	}

	float3 Sun( in float3 vRay, float3 sunDirectionWs, float sunElev )
	{
		float sunDot = saturate( dot( vRay, sunDirectionWs ) );
		float discPower = 9200.0 * pow( 8.0 / max( g_flSunDiscAngularDiameter, 1.0 ), 2.0 );
		float disc = pow( sunDot, discPower ) * g_flSunDiscIntensity;
		float core = pow( sunDot, discPower * 6.0 ) * g_flSunDiscIntensity * 2.4;
		float glow = pow( sunDot, max( 48.0, discPower * 0.05 ) ) * g_flSunGlowIntensity;
		float fSun = disc + core + glow;

		float twilight = saturate( 1.0 - sunElev * 2.2 );
		float3 sunTint = lerp( g_vSunDiscColorDay, g_vSunDiscColorTwilight, twilight );
		sunTint = lerp( float3( 1.0, 0.50, 0.06 ), sunTint, 0.95 );

		// Fade in above horizon; stay visible through midday (do not gate on view-ray Z).
		float elevFade = smoothstep( -0.04, 0.18, sunElev );
		return sunTint * fSun * elevFade;
	}

	float3 ThornsNightSky( float3 vRay, float nightFactor )
	{
		float skyGrad = pow( saturate( vRay.z ), 0.40 );
		float3 horizon = max( g_vSkyHorizonNight, float3( 0.045, 0.095, 0.230 ) );
		float3 zenith = max( g_vSkyZenithNight, float3( 0.018, 0.048, 0.175 ) );
		float3 color = lerp( horizon, zenith, skyGrad );
		color = BoostSkySaturation( color, 1.35 );
		color += Stars( vRay ) * nightFactor * 0.55;
		return color;
	}

	float3 TonemapSky( float3 color, float twilightAmt, float nightFactor )
	{
		float tintStrength = max( g_flSkyTwilightTintStrength, 0.5 );
		float3 twilightShoulder = g_vTonemapShoulder * float3( 0.72, 0.58, 0.62 );
		float3 nightShoulder = float3( 0.06, 0.10, 0.28 );
		float3 shoulder = lerp( lerp( g_vTonemapShoulder, twilightShoulder, twilightAmt * saturate( tintStrength * 0.22 ) ), nightShoulder, saturate( nightFactor * 1.15 ) );
		return color / ( color + shoulder );
	}

	PS_OUTPUT MainPs( PS_INPUT i )
	{
		PS_OUTPUT o;
		float3 vPositionWs = i.vPositionWs;
		float3 vRay = normalize( vPositionWs - g_vCameraPositionWs );

		float3 sunDirectionWs = -g_DirectionalLightDirection.xyz;
		float sunElevSigned = sunDirectionWs.z;
		float sunElev = max( sunElevSigned, 0.0 );

		float2 skyPhase = ResolveSkyPhase( sunElevSigned );
		float nightFactor = skyPhase.x;
		float twilightPhase = skyPhase.y;

		float twilightAmt = saturate( twilightPhase ) * ( 1.0 - nightFactor );
		float twilightPeak = pow( twilightAmt, 0.58 );

		float3 dayColor = ThornsSkyGradient( vRay, sunElev, nightFactor, twilightPhase );
		dayColor += Stars( vRay ) * saturate( nightFactor * 0.35 + sunElev * 0.08 );

		const float sunDirLengthSq = dot( sunDirectionWs, sunDirectionWs );
		const float sunColorLengthSq = dot( g_DirectionalLightColor.rgb, g_DirectionalLightColor.rgb );
		float scatterFade = 1.0 - smoothstep( 0.52, 0.78, nightFactor );
		if ( sunDirLengthSq > 0.0f && sunColorLengthSq > 0.0f && scatterFade > 0.001 && twilightAmt > 0.001 )
		{
			float horizonView = saturate( vRay.z * 3.2 );
			float scatterMask = lerp( horizonView, 1.0, twilightPeak * 0.92 ) * lerp( 1.0, 0.42, twilightPeak );
			scatterMask *= scatterFade;

			dayColor += GetAtmosphere( vRay, sunDirectionWs, g_DirectionalLightColor.rgb, sunElev, twilightPhase, nightFactor ) * scatterMask;
		}

		if ( sunDirLengthSq > 0.0f && g_flSunDiscIntensity > 0.001 && sunElev > 0.01 )
		{
			float sunDayMask = smoothstep( 0.01, 0.10, sunElev ) * ( 1.0 - smoothstep( 0.78, 0.94, nightFactor ) );
			dayColor += Sun( vRay, sunDirectionWs, sunElev ) * sunDayMask;
		}

		dayColor *= lerp( float3( 1.0, 1.0, 1.0 ), float3( 0.35, 0.48, 0.82 ), nightFactor * 0.35 );

		float3 nightSky = ThornsNightSky( vRay, nightFactor );
		float nightBlend = smoothstep( 0.18, 0.62, nightFactor );
		float3 vColor = lerp( dayColor, nightSky, nightBlend );

		vColor = PreserveSkyHue( vColor, lerp( 1.35, 1.05, nightFactor ) );
		vColor = TonemapSky( vColor, twilightPeak * ( 1.0 - nightFactor ) * ( 1.0 - nightBlend ), nightFactor );

		float deepNightBlend = smoothstep( 0.72, 0.96, nightFactor );
		float3 deepHorizon = float3( 0.055, 0.110, 0.265 );
		float3 deepZenith = float3( 0.020, 0.050, 0.195 );
		float3 deepNight = lerp( deepHorizon, deepZenith, saturate( vRay.z ) );
		vColor = lerp( vColor, deepNight, deepNightBlend * 0.75 );

		float3 fogged = ApplyVolumetricFog( vColor, i.vPositionWs, i.vPositionSs.xy );
		vColor = lerp( vColor, fogged, g_flSkyFogBlend * lerp( 0.08, 1.0, 1.0 - nightFactor ) * lerp( 1.0, 0.42, twilightPeak ) );

		o.vColor0 = float4( vColor, 1.0 );
		return o;
	}
}
