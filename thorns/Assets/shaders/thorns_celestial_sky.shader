HEADER
{
	Description = "Thorns Minecraft-style celestial sky (gradient + horizon glow + stars + clouds)";
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

	RenderState( CullMode, NONE );
	RenderState( DepthWriteEnable, false );
	RenderState( DepthEnable, true );
	RenderState( DepthFunc, GREATER_EQUAL );

	BoolAttribute( sky, true );

	float3 g_vSkyZenith < Attribute( "SkyZenith" ); Default3( 0.15, 0.36, 0.66 ); >;
	float3 g_vSkyMid < Attribute( "SkyMid" ); Default3( 0.46, 0.70, 0.89 ); >;
	float3 g_vSkyHorizon < Attribute( "SkyHorizon" ); Default3( 0.46, 0.70, 0.89 ); >;
	float3 g_vHorizonGlowColor < Attribute( "HorizonGlowColor" ); Default3( 1.0, 0.58, 0.35 ); >;
	float g_flHorizonGlowStrength < Attribute( "HorizonGlowStrength" ); Default( 0.0 ); >;
	float g_flStarBrightness < Attribute( "StarBrightness" ); Default( 0.0 ); >;
	float g_flStarRotation < Attribute( "StarRotation" ); Default( 0.0 ); >;
	float g_flCloudOpacity < Attribute( "CloudOpacity" ); Default( 0.28 ); >;
	float3 g_vCloudTint < Attribute( "CloudTint" ); Default3( 1.0, 1.0, 1.0 ); >;
	float g_flCloudDrift < Attribute( "CloudDrift" ); Default( 0.0 ); >;
	float3 g_vSunDiscColor < Attribute( "SunDiscColor" ); Default3( 1.0, 0.83, 0.62 ); >;
	float g_flSunDiscIntensity < Attribute( "SunDiscIntensity" ); Default( 1.65 ); >;
	float g_flSunDiscGlow < Attribute( "SunDiscGlow" ); Default( 0.004 ); >;
	float g_flSunDiscAngularDiameter < Attribute( "SunDiscAngularDiameter" ); Default( 18.0 ); >;
	float3 g_vCelestialSkyFog < Attribute( "FogColor" ); Default3( 0.46, 0.70, 0.89 ); >;
	float g_flFogBlend < Attribute( "FogBlend" ); Default( 0.18 ); >;
	float g_flSkyExposure < Attribute( "SkyExposure" ); Default( 1.0 ); >;
	float g_flHorizonBandPower < Attribute( "HorizonBandPower" ); Default( 0.88 ); >;

	float noise3( float3 x )
	{
		float3 p = floor( x );
		float3 f = frac( x );
		f = f * f * ( 3.0 - 2.0 * f );
		float2 uv = ( p.xy + float2( 37.0, 17.0 ) * p.z ) + f.xy;
		float2 rg = g_tBlueNoise.Sample( g_sBilinearWrap, ( uv + 0.5 ) / 256.0 ).xy;
		return lerp( rg.x, rg.y, f.z );
	}

	float3 RotateAroundY( float3 dir, float angle )
	{
		float s = sin( angle );
		float c = cos( angle );
		return float3( dir.x * c + dir.z * s, dir.y, -dir.x * s + dir.z * c );
	}

	float Stars( float3 vRay, float brightness )
	{
		if ( brightness < 0.01 )
			return 0.0;

		float3 starRay = RotateAroundY( normalize( vRay ), g_flStarRotation );
		float scale = g_vViewportSize.y * 0.28;
		float v = noise3( starRay * scale );
		v += noise3( starRay * scale * 0.55 + float3( 2.1, 0.7, 1.3 ) ) * 0.5;
		v += noise3( starRay * scale * 0.28 + float3( 4.4, 1.2, 3.0 ) ) * 0.25;
		v = 1.0 - saturate( v );
		v = pow( v, 14.0 );
		v *= saturate( vRay.z * 80.0 );
		return v * brightness;
	}

	float cloudFbm( float3 dir, float scale, float3 offset )
	{
		float3 p = dir * scale + offset;
		float v = noise3( p );
		v += noise3( p * 1.55 + float3( 2.1, 0.7, 1.3 ) ) * 0.5;
		v += noise3( p * 2.35 + float3( 4.4, 1.2, 3.0 ) ) * 0.25;
		return v;
	}

	float4 CloudLayer( float3 vRay, float3 skyBase )
	{
		if ( g_flCloudOpacity < 0.01 )
			return float4( 0, 0, 0, 0 );

		float3 dir = normalize( vRay );
		float horizonMask = smoothstep( -0.02, 0.16, dir.z );
		float elevMask = smoothstep( 0.10, 0.38, saturate( ( dir.z + 0.06 ) / 1.06 ) );

		float drift = g_flCloudDrift * 6.28318;
		float3 offset = float3( drift * 0.35, drift * 0.12, drift * 0.22 );
		float n1 = cloudFbm( dir, 1.25, offset + float3( 1.7, 0.4, 2.1 ) );
		float n2 = cloudFbm( dir, 2.05, offset + float3( 4.2, 1.9, 0.6 ) );
		float n3 = cloudFbm( dir, 3.35, offset + float3( 2.8, 3.3, 5.0 ) );

		float density = 0.52 * smoothstep( 0.30, 0.54, n1 )
			+ 0.38 * smoothstep( 0.32, 0.56, n2 )
			+ 0.26 * smoothstep( 0.34, 0.58, n3 );
		density *= horizonMask * elevMask;

		float alpha = smoothstep( 0.38, 0.60, density );
		alpha = alpha * alpha * ( 3.0 - 2.0 * alpha );
		alpha *= g_flCloudOpacity;
		if ( alpha < 0.05 )
			return float4( 0, 0, 0, 0 );

		float3 cloudColor = lerp( skyBase, g_vCloudTint, 0.88 );
		return float4( cloudColor, saturate( alpha ) );
	}

	float3 SunDisc( float3 vRay, float3 sunDir, float sunElev )
	{
		float sunDot = saturate( dot( vRay, sunDir ) );
		// Larger diameter → softer falloff exponent → bigger disc; core stays tight for a crisp center.
		float discPower = 5200.0 * pow( 10.0 / max( g_flSunDiscAngularDiameter, 8.0 ), 2.0 );
		float disc = pow( sunDot, discPower ) * g_flSunDiscIntensity;
		float core = pow( sunDot, discPower * 8.0 ) * g_flSunDiscIntensity * 3.0;
		float halo = pow( sunDot, max( discPower * 0.18, 220.0 ) ) * g_flSunDiscGlow;
		float viewFade = saturate( vRay.z * 2.6 );
		float elevFade = saturate( sunElev * 2.2 + 0.05 );
		return g_vSunDiscColor * ( disc + core + halo ) * viewFade * elevFade;
	}

	float3 HorizonGlow( float3 vRay, float3 sunDir )
	{
		float up = saturate( vRay.z );
		// Wide Minecraft-style band (lower power = more of the dome gets horizon tint).
		float bandPower = max( g_flHorizonBandPower, 0.65 );
		float band = pow( saturate( 1.0 - up ), bandPower );
		float sunFocus = pow( saturate( dot( normalize( vRay ), sunDir ) ), 4.5 );
		float glow = band * ( 0.72 + sunFocus * 1.15 ) * g_flHorizonGlowStrength;
		return g_vHorizonGlowColor * glow;
	}

	struct PS_OUTPUT
	{
		float4 vColor0 : SV_Target0;
	};

	PS_OUTPUT MainPs( PS_INPUT i )
	{
		PS_OUTPUT o;
		float3 vRay = normalize( i.vPositionWs - g_vCameraPositionWs.xyz );
		float up = saturate( vRay.z );
		float grad = pow( up, lerp( 0.38, 0.22, saturate( g_flHorizonGlowStrength * 0.35 ) ) );
		// Pull horizon / sunset colors up through ~20–40% of the visible sky.
		float horizonMix = pow( saturate( 1.0 - up ), max( g_flHorizonBandPower, 0.65 ) );
		float3 skyHigh = lerp( g_vSkyMid, g_vSkyZenith, smoothstep( 0.22, 0.92, grad ) );
		float3 sky = lerp( skyHigh, g_vSkyHorizon, horizonMix );
		sky = lerp( sky, g_vHorizonGlowColor, horizonMix * saturate( g_flHorizonGlowStrength * 0.22 ) );

		float3 sunDir = -g_DirectionalLightDirection.xyz;
		float sunDirLen = length( sunDir );
		float sunElev = 0.0;
		if ( sunDirLen > 0.001 )
		{
			sunDir /= sunDirLen;
			sunElev = sunDir.z;
			sky += HorizonGlow( vRay, sunDir );
		}

		float4 clouds = CloudLayer( vRay, sky );
		sky = lerp( sky, clouds.rgb, clouds.a );

		if ( sunDirLen > 0.001 && g_flSunDiscIntensity > 0.001 )
			sky += SunDisc( vRay, sunDir, sunElev );

		sky += Stars( vRay, g_flStarBrightness ).xxx;
		sky *= g_flSkyExposure;

		// Optional horizon haze only when FogBlend > 0 (baked presets set 0 for vivid skies).
		float distFog = pow( saturate( 1.0 - up ), 1.35 ) * g_flFogBlend;
		if ( distFog > 0.001 )
		{
			float3 shoulder = max( g_vCelestialSkyFog * 0.08, float3( 0.008, 0.01, 0.016 ) );
			sky = sky / max( sky + shoulder, float3( 1e-4, 1e-4, 1e-4 ) );
			sky = lerp( sky, g_vCelestialSkyFog, distFog * 0.55 );
		}

		o.vColor0 = float4( sky, 1.0 );
		return o;
	}
}
