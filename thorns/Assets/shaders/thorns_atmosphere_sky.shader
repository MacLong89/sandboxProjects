HEADER
{
	Description = "Thorns atmosphere sky (fork of base atmosphere_sky)";
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

	float3 g_vSkyZenithDay < Attribute( "SkyZenithDay" ); Default3( 0.22, 0.52, 0.94 ); >;
	float3 g_vSkyHorizonDay < Attribute( "SkyHorizonDay" ); Default3( 0.58, 0.80, 0.98 ); >;
	float3 g_vSkyZenithTwilight < Attribute( "SkyZenithTwilight" ); Default3( 0.75, 0.25, 0.45 ); >;
	float3 g_vSkyHorizonTwilight < Attribute( "SkyHorizonTwilight" ); Default3( 1.0, 0.45, 0.25 ); >;
	float3 g_vSkyZenithNight < Attribute( "SkyZenithNight" ); Default3( 0.02, 0.04, 0.14 ); >;
	float3 g_vSkyHorizonNight < Attribute( "SkyHorizonNight" ); Default3( 0.06, 0.10, 0.22 ); >;
	float3 g_vSunDiscColorDay < Attribute( "SunDiscColorDay" ); Default3( 1.0, 0.55, 0.10 ); >;
	float3 g_vSunDiscColorTwilight < Attribute( "SunDiscColorTwilight" ); Default3( 1.0, 0.45, 0.28 ); >;
	float g_flSunDiscIntensity < Attribute( "SunDiscIntensity" ); Default( 1.35 ); >;
	float g_flCloudOpacity < Attribute( "CloudOpacity" ); Default( 0.26 ); >;
	float g_flCloudCoverage < Attribute( "CloudCoverage" ); Default( 1.0 ); >;
	float g_flSunGlowIntensity < Attribute( "SunGlowIntensity" ); Default( 0.012 ); >;
	float g_flSunDiscAngularDiameter < Attribute( "SunDiscAngularDiameter" ); Default( 24.0 ); >;
	float g_flAtmosphereStrength < Attribute( "AtmosphereStrength" ); Default( 0.82 ); >;
	float g_flSkyFogBlend < Attribute( "SkyFogBlend" ); Default( 0.10 ); >;
	float g_flSkyTwilightTintStrength < Attribute( "SkyTwilightTintStrength" ); Default( 1.0 ); >;
	float g_flSkyTwilightScatterMix < Attribute( "SkyTwilightScatterMix" ); Default( 0.28 ); >;
	float g_flSkyTwilightPhase < Attribute( "SkyTwilightPhase" ); Default( 0.0 ); >;
	float g_flSkyNightFactor < Attribute( "SkyNightFactor" ); Default( 0.0 ); >;
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

	// Day / twilight / night weights — renormalized so colors cross-fade smoothly (no hard snap).
	void ThornsComputeSkyPhaseFromSun(
		float sunElevSigned,
		float nightFactorHint,
		out float dayAmt,
		out float twilightAmt,
		out float nightAmt )
	{
		float calendarNight = saturate( nightFactorHint );
		float nightFromSun = 1.0 - smoothstep( -0.20, 0.10, sunElevSigned );
		float calendarGated = calendarNight * ( 1.0 - smoothstep( -0.10, 0.22, sunElevSigned ) );
		nightAmt = saturate( max( nightFromSun, calendarGated ) );

		float dayCore = smoothstep( 0.22, 0.52, sunElevSigned );
		float twilightBand = smoothstep( -0.14, 0.20, sunElevSigned ) * ( 1.0 - smoothstep( 0.10, 0.46, sunElevSigned ) );
		twilightBand = max( twilightBand, saturate( g_flSkyTwilightPhase ) * ( 1.0 - nightAmt ) );

		dayAmt = dayCore * ( 1.0 - nightAmt );
		twilightAmt = twilightBand * ( 1.0 - nightAmt );

		float sum = dayAmt + twilightAmt + nightAmt + 1e-5;
		dayAmt /= sum;
		twilightAmt /= sum;
		nightAmt /= sum;
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

	float3 ThornsSkyGradient( float3 vRay, float dayFactor, float twilightFactor, float nightFactor )
	{
		float day = saturate( dayFactor );
		float twilight = saturate( twilightFactor );
		float night = saturate( nightFactor );

		float skyGradPower = lerp( 0.40, 0.24, twilight );
		float skyGrad = pow( saturate( vRay.z ), skyGradPower );

		float3 zenith = g_vSkyZenithDay * day + g_vSkyZenithTwilight * twilight + g_vSkyZenithNight * night;
		float3 horizon = g_vSkyHorizonDay * day + g_vSkyHorizonTwilight * twilight + g_vSkyHorizonNight * night;
		float3 gradient = lerp( horizon, zenith, skyGrad );

		float horizonWarmth = pow( saturate( 1.0 - vRay.z ), 2.4 ) * twilight;
		gradient += g_vSkyHorizonTwilight * horizonWarmth * 0.28 * saturate( g_flSkyTwilightTintStrength );

		float satBoost = lerp( 1.12, 1.06, twilight );
		satBoost = lerp( satBoost, 1.04, night * 0.5 );
		return BoostSkySaturation( gradient, satBoost );
	}

	float3 GetAtmosphere( float3 ray, float3 sunDirectionWs, float3 sunColor, float sunElevSigned )
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
		float dayAmt;
		float twilightAmt;
		float nightAmt;
		ThornsComputeSkyPhaseFromSun( sunElevSigned, g_flSkyNightFactor, dayAmt, twilightAmt, nightAmt );
		float day = dayAmt;
		float twilightScatter = twilightAmt;
		float sunElev = max( sunElevSigned, 0.0 );
		float3 twilightTint = GetTwilightSkyTintFromUniforms();
		sunTint = lerp( sunTint, twilightTint, twilightScatter * g_flSkyTwilightScatterMix );
		sunTint = lerp( sunTint, float3( 0.55, 0.62, 0.88 ), nightAmt * 0.65 );

		float sunUp = smoothstep( -0.02, 0.08, sunElevSigned );
		float dayStrength = lerp( 0.72, 1.35, saturate( sunElev * 2.0 + 0.12 ) ) * day;
		float twilightStrength = lerp( 0.18, 0.52, twilightScatter ) * sunUp;
		float strength = max( dayStrength, twilightStrength ) * sunUp * ( 1.0 - nightAmt * 0.92 );
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

	float3 Sun( in float3 vRay, float3 sunDirectionWs, float sunElev, float twilightAmt )
	{
		float sunDot = saturate( dot( vRay, sunDirectionWs ) );
		float discPower = 9200.0 * pow( 8.0 / max( g_flSunDiscAngularDiameter, 1.0 ), 2.0 );
		float disc = pow( sunDot, discPower ) * g_flSunDiscIntensity;
		float core = pow( sunDot, discPower * 6.0 ) * g_flSunDiscIntensity * 2.4;
		float glow = pow( sunDot, max( 48.0, discPower * 0.05 ) ) * g_flSunGlowIntensity;
		float fSun = disc + core + glow;

		float twilight = saturate( max( saturate( 1.0 - sunElev * 3.2 ), twilightAmt ) );
		float3 sunTint = lerp( g_vSunDiscColorDay, g_vSunDiscColorTwilight, twilight );

		float viewFade = saturate( vRay.z * 2.8 );
		float elevFade = saturate( sunElev * 2.5 + 0.04 );
		return sunTint * fSun * viewFade * elevFade;
	}

	float ThornsCloudFbm( float3 dir, float scale, float3 offset )
	{
		float3 p = dir * scale + offset;
		float v = noise( p );
		v += noise( p * 1.62 + float3( 2.1, 0.7, 1.3 ) ) * 0.5;
		v += noise( p * 2.55 + float3( 4.4, 1.2, 3.0 ) ) * 0.25;
		return v;
	}

	float4 ThornsCloudLayer( float3 vRay, float3 skyBase, float dayAmt, float twilightAmt, float nightFactor )
	{
		if ( g_flCloudOpacity < 0.01 )
			return float4( 0.0, 0.0, 0.0, 0.0 );

		float3 dir = normalize( vRay );
		float horizonMask = smoothstep( -0.02, 0.18, dir.z );
		float elev = saturate( ( dir.z + 0.08 ) / 1.08 );
		float elevMask = smoothstep( 0.12, 0.42, elev );

		float n1 = ThornsCloudFbm( dir, 1.6, float3( 1.7, 0.4, 2.1 ) );
		float n2 = ThornsCloudFbm( dir, 2.6, float3( 4.2, 1.9, 0.6 ) );
		float n3 = ThornsCloudFbm( dir, 4.2, float3( 2.8, 3.3, 5.0 ) );

		float coverage = max( g_flCloudCoverage, 0.35 );
		float density = 0.55 * smoothstep( 0.28, 0.52, n1 )
			+ 0.40 * smoothstep( 0.30, 0.54, n2 )
			+ 0.28 * smoothstep( 0.32, 0.56, n3 );
		density *= coverage * horizonMask * elevMask;

		float alpha = smoothstep( 0.36, 0.62, density );
		alpha = alpha * alpha * ( 3.0 - 2.0 * alpha );
		alpha *= g_flCloudOpacity * ( 1.0 - smoothstep( 0.42, 0.78, nightFactor ) );
		alpha *= saturate( dayAmt + twilightAmt * 0.85 );
		if ( alpha < 0.06 )
			return float4( 0.0, 0.0, 0.0, 0.0 );

		float twilightPeak = saturate( twilightAmt );
		float3 cloudWhite = lerp( float3( 0.94, 0.96, 1.0 ), float3( 1.0, 0.78, 0.68 ), twilightPeak );
		float3 cloudColor = lerp( skyBase, cloudWhite, 0.72 );
		return float4( cloudColor, saturate( alpha ) );
	}

	float3 ThornsNightSky( float3 vRay, float nightFactor )
	{
		float skyGrad = pow( saturate( vRay.z ), 0.28 );
		float3 horizon = min( g_vSkyHorizonNight, float3( 0.016, 0.022, 0.050 ) );
		float3 zenith = min( g_vSkyZenithNight, float3( 0.004, 0.007, 0.020 ) );
		horizon = max( horizon, float3( 0.004, 0.005, 0.010 ) );
		zenith = max( zenith, float3( 0.001, 0.001, 0.004 ) );
		float3 color = lerp( horizon, zenith, skyGrad );
		color += Stars( vRay ) * nightFactor * 0.34;
		return color * 0.30;
	}

	float3 TonemapSky( float3 color, float twilightAmt )
	{
		float tintStrength = max( g_flSkyTwilightTintStrength, 0.5 );
		float3 shoulder = lerp( g_vTonemapShoulder, g_vTonemapShoulder * float3( 0.72, 0.58, 0.62 ), twilightAmt * saturate( tintStrength * 0.22 ) );
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

		float dayAmt;
		float twilightAmt;
		float nightFactor;
		ThornsComputeSkyPhaseFromSun( sunElevSigned, g_flSkyNightFactor, dayAmt, twilightAmt, nightFactor );
		float twilightPeak = pow( twilightAmt, 0.58 );

		float3 skyGradient = ThornsSkyGradient( vRay, dayAmt, twilightAmt, nightFactor );
		float3 vColor = skyGradient;
		float4 clouds = ThornsCloudLayer( vRay, skyGradient, dayAmt, twilightAmt, nightFactor );
		vColor = lerp( vColor, clouds.rgb, clouds.a );
		const float sunDirLengthSq = dot( sunDirectionWs, sunDirectionWs );
		const float sunColorLengthSq = dot( g_DirectionalLightColor.rgb, g_DirectionalLightColor.rgb );
		float scatterFade = ( 1.0 - smoothstep( 0.22, 0.48, nightFactor ) ) * smoothstep( -0.02, 0.06, sunElevSigned );
		float dayScatter = dayAmt * saturate( sunElev * 2.2 + 0.12 );
		float twilightScatter = twilightAmt * smoothstep( 0.0, 0.14, sunElevSigned );
		float scatterDrive = saturate( max( twilightScatter * 0.32, dayScatter ) );

		float sunDiscWeight = saturate( max( dayAmt, twilightAmt * 1.1 ) ) * ( 1.0 - smoothstep( 0.40, 0.72, nightFactor ) );
		sunDiscWeight *= smoothstep( -0.05, 0.10, sunElevSigned );
		if ( sunDirLengthSq > 0.0f && sunDiscWeight > 0.001 )
		{
			float3 sunDirN = sunDirectionWs * rsqrt( sunDirLengthSq );
			vColor += Sun( vRay, sunDirN, sunElev, twilightAmt ) * sunDiscWeight;
		}

		if ( sunDirLengthSq > 0.0f && sunColorLengthSq > 0.0f && scatterFade > 0.001 && scatterDrive > 0.001 && nightFactor < 0.42 )
		{
			float horizonView = saturate( vRay.z * 3.2 );
			float scatterMask = lerp( horizonView, 1.0, twilightPeak * 0.55 ) * lerp( 1.0, 0.22, twilightPeak );
			scatterMask *= scatterFade * scatterDrive;

			float3 sunDirN = sunDirectionWs * rsqrt( sunDirLengthSq );
			float3 scatter = GetAtmosphere( vRay, sunDirN, g_DirectionalLightColor.rgb, sunElevSigned ) * scatterMask;
			vColor = lerp( vColor, vColor + scatter, saturate( dayAmt * 0.85 + twilightAmt * 0.28 ) );
		}

		vColor *= lerp( float3( 1.0, 1.0, 1.0 ), float3( 1.12, 1.14, 1.18 ), dayAmt * 0.85 );

		float visualTintWeight = saturate( g_flSkyTwilightVisualTintStrength ) * twilightAmt * ( 1.0 - smoothstep( 0.32, 0.52, nightFactor ) );
		float3 visualTint = max( g_vSkyTwilightVisualTint, float3( 0.001, 0.001, 0.001 ) );
		vColor = lerp( vColor, vColor * visualTint, visualTintWeight );

		float coolNight = smoothstep( 0.55, 0.88, nightFactor );
		float3 lum = float3( 0.299, 0.587, 0.114 );
		vColor = lerp( vColor, dot( vColor, lum ).xxx, coolNight * 0.22 );

		vColor = PreserveSkyHue( vColor, lerp( 1.55, 0.65, nightFactor ) );
		vColor = TonemapSky( vColor, twilightPeak * ( 1.0 - smoothstep( 0.40, 0.65, nightFactor ) ) );

		vColor += Stars( vRay ) * nightFactor * 0.38;

		float3 fogged = ApplyVolumetricFog( vColor, i.vPositionWs, i.vPositionSs.xy );
		float fogOnSky = g_flSkyFogBlend * lerp( 1.0, 0.42, twilightPeak ) * ( 1.0 - dayAmt * 0.92 );
		vColor = lerp( vColor, fogged, fogOnSky );

		o.vColor0 = float4( vColor, 1.0 );
		return o;
	}
}
