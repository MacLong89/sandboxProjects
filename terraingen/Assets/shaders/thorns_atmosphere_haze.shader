HEADER
{
	DevShader = true;
}

MODES
{
	Default();
	Forward();
}

COMMON
{
	#include "postprocess/shared.hlsl"
}

struct VertexInput
{
	float3 vPositionOs : POSITION < Semantic( PosXyz ); >;
	float2 vTexCoord : TEXCOORD0 < Semantic( LowPrecisionUv ); >;
};

struct PixelInput
{
	float2 uv : TEXCOORD0;

#if ( PROGRAM == VFX_PROGRAM_VS )
	float4 vPositionPs : SV_Position;
#endif

#if ( PROGRAM == VFX_PROGRAM_PS )
	float4 vPositionSs : SV_Position;
#endif
};

VS
{
	PixelInput MainVs( VertexInput i )
	{
		PixelInput o;
		o.vPositionPs = float4( i.vPositionOs.xy, 0.0f, 1.0f );
		o.uv = i.vTexCoord;
		return o;
	}
}

PS
{
	#include "postprocess/common.hlsl"
	#include "postprocess/functions.hlsl"
	#include "procedural.hlsl"
	#include "common/classes/Depth.hlsl"

	Texture2D g_tColorBuffer < Attribute( "ColorBuffer" ); SrgbRead( false ); >;

	float foregroundEnd < Attribute( "foregroundEnd" ); >;
	float midEnd < Attribute( "midEnd" ); >;
	float farEnd < Attribute( "farEnd" ); >;
	float curvePower < Attribute( "curvePower" ); >;
	float strength < Attribute( "strength" ); >;
	float desaturate < Attribute( "desaturate" ); >;
	float blueShift < Attribute( "blueShift" ); >;
	float contrastReduce < Attribute( "contrastReduce" ); >;
	float fgSatBoost < Attribute( "fgSatBoost" ); >;
	float fgContrastBoost < Attribute( "fgContrastBoost" ); >;
	float debugTint < Attribute( "debugTint" ); >;
	float3 hazeTint < Attribute( "hazeTint" ); >;
	float highlightWarmth < Attribute( "highlightWarmth" ); >;
	float3 highlightWarmTint < Attribute( "highlightWarmTint" ); >;
	float shadowCoolStrength < Attribute( "shadowCoolStrength" ); >;
	float3 shadowCoolTint < Attribute( "shadowCoolTint" ); >;
	float shadowDepth < Attribute( "shadowDepth" ); >;
	float highlightExposure < Attribute( "highlightExposure" ); >;
	float shadowExposure < Attribute( "shadowExposure" ); >;
	float globalSaturation < Attribute( "globalSaturation" ); >;
	float globalContrast < Attribute( "globalContrast" ); >;
	float globalBrightness < Attribute( "globalBrightness" ); >;
	float enableLookMasks < Attribute( "enableLookMasks" ); >;
	float grassSaturation < Attribute( "grassSaturation" ); >;
	float grassBrightness < Attribute( "grassBrightness" ); >;
	float rockBrightness < Attribute( "rockBrightness" ); >;
	float waterSaturation < Attribute( "waterSaturation" ); >;
	float waterBrightness < Attribute( "waterBrightness" ); >;
	float treeBrightness < Attribute( "treeBrightness" ); >;
	float skySaturation < Attribute( "skySaturation" ); >;
	float cloudBrightness < Attribute( "cloudBrightness" ); >;
	float maskBlend < Attribute( "maskBlend" ); >;
	float terrainDistanceTintStrength < Attribute( "terrainDistanceTintStrength" ); >;
	float terrainNearDistance < Attribute( "terrainNearDistance" ); >;
	float terrainMidDistance < Attribute( "terrainMidDistance" ); >;
	float terrainFarDistance < Attribute( "terrainFarDistance" ); >;
	float3 terrainNearTint < Attribute( "terrainNearTint" ); >;
	float3 terrainDistanceMidTint < Attribute( "terrainDistanceMidTint" ); >;
	float3 terrainFarTint < Attribute( "terrainFarTint" ); >;
	float terrainDistanceCurve < Attribute( "terrainDistanceCurve" ); >;
	float terrainElevationTintStrength < Attribute( "terrainElevationTintStrength" ); >;
	float terrainLowElevation < Attribute( "terrainLowElevation" ); >;
	float terrainMidElevation < Attribute( "terrainMidElevation" ); >;
	float terrainHighElevation < Attribute( "terrainHighElevation" ); >;
	float3 terrainLowTint < Attribute( "terrainLowTint" ); >;
	float3 terrainElevationMidTint < Attribute( "terrainElevationMidTint" ); >;
	float3 terrainHighTint < Attribute( "terrainHighTint" ); >;
	float terrainElevationCurve < Attribute( "terrainElevationCurve" ); >;

	float Smooth( float a, float b, float x )
	{
		float t = saturate( (x - a) / max( b - a, 0.001 ) );
		return t * t * (3.0 - 2.0 * t);
	}

	float SampleViewDistance( float2 vScreenUv )
	{
		float2 screenPos = vScreenUv * g_vViewportSize;
		float normDepth = Depth::GetNormalized( screenPos );

		if ( normDepth >= 0.9995 )
			return farEnd * 1.25;

		float3 worldPos = Depth::GetWorldPosition( screenPos );
		return distance( worldPos, g_vCameraPositionWs );
	}

	bool TrySampleWorldPosition( float2 vScreenUv, out float3 worldPos )
	{
		float2 screenPos = vScreenUv * g_vViewportSize;
		float normDepth = Depth::GetNormalized( screenPos );

		if ( normDepth >= 0.9995 )
		{
			worldPos = 0;
			return false;
		}

		worldPos = Depth::GetWorldPosition( screenPos );
		return true;
	}

	float AtmosphericFactor( float dist )
	{
		if ( dist <= foregroundEnd )
			return 0.0;

		float range = max( farEnd - foregroundEnd, 0.001 );
		float t = saturate( (dist - foregroundEnd) / range );
		float midStart = saturate( (midEnd - foregroundEnd) / range );
		float ramp = Smooth( midStart * 0.5, 1.0, t );
		return pow( ramp, curvePower ) * strength;
	}

	void ApplyCinematicGrade( inout float3 rgb )
	{
		float luma = dot( rgb, float3( 0.299, 0.587, 0.114 ) );
		float hi = Smooth( 0.5, 0.9, luma );
		float sh = 1.0 - Smooth( 0.06, 0.36, luma );

		rgb += highlightWarmTint * highlightWarmth * hi;
		rgb = lerp( rgb, rgb * shadowCoolTint, shadowCoolStrength * sh );
		rgb *= lerp( 1.0, 1.0 - shadowDepth, shadowDepth * sh );

		float expMul = exp2( highlightExposure * hi + shadowExposure * sh );
		rgb *= expMul;
	}

	float SaturationAmount( float3 rgb )
	{
		float mx = max( rgb.r, max( rgb.g, rgb.b ) );
		float mn = min( rgb.r, min( rgb.g, rgb.b ) );
		return mx - mn;
	}

	float3 ApplySaturation( float3 rgb, float multiplier )
	{
		float luma = dot( rgb, float3( 0.299, 0.587, 0.114 ) );
		return lerp( float3( luma, luma, luma ), rgb, multiplier );
	}

	void ApplyGlobalGrade( inout float3 rgb )
	{
		rgb = ApplySaturation( rgb, globalSaturation );
		rgb = 0.5 + (rgb - 0.5) * globalContrast;
		rgb *= globalBrightness;
	}

	void ApplyLookMasks( inout float3 rgb, float dist )
	{
		if ( enableLookMasks < 0.5 )
			return;

		float blend = saturate( maskBlend );
		float luma = dot( rgb, float3( 0.299, 0.587, 0.114 ) );
		float sat = SaturationAmount( rgb );
		float skyMask = dist > farEnd ? 1.0 : 0.0;
		float greenDominance = saturate( (rgb.g - max( rgb.r, rgb.b )) * 5.0 );
		float grassMask = greenDominance * smoothstep( 0.18, 0.75, luma ) * (1.0 - skyMask);
		float treeMask = greenDominance * (1.0 - smoothstep( 0.16, 0.42, luma )) * (1.0 - skyMask);
		float waterMask = smoothstep( 0.04, 0.26, rgb.b - rgb.r ) * smoothstep( 0.05, 0.28, rgb.g - rgb.r ) * smoothstep( 0.16, 0.75, luma ) * (1.0 - skyMask);
		float rockMask = (1.0 - smoothstep( 0.06, 0.22, sat )) * smoothstep( 0.22, 0.82, luma ) * (1.0 - skyMask);
		float cloudMask = skyMask * smoothstep( 0.48, 0.82, luma ) * (1.0 - smoothstep( 0.16, 0.42, sat ));

		float grass = saturate( grassMask * blend );
		float trees = saturate( treeMask * blend );
		float water = saturate( waterMask * blend );
		float rocks = saturate( rockMask * blend );
		float sky = saturate( skyMask * blend );
		float clouds = saturate( cloudMask * blend );

		rgb = lerp( rgb, ApplySaturation( rgb, grassSaturation ) * grassBrightness, grass );
		rgb = lerp( rgb, rgb * treeBrightness, trees );
		rgb = lerp( rgb, ApplySaturation( rgb, waterSaturation ) * waterBrightness, water );
		rgb = lerp( rgb, rgb * rockBrightness, rocks );
		rgb = lerp( rgb, ApplySaturation( rgb, skySaturation ), sky );
		rgb = lerp( rgb, rgb * cloudBrightness, clouds );
	}

	float3 ThreePointTint( float value, float lowValue, float midValue, float highValue, float3 lowTint, float3 midTint, float3 highTint, float curve )
	{
		float lowSpan = max( midValue - lowValue, 0.001 );
		float highSpan = max( highValue - midValue, 0.001 );
		float tLow = saturate( (value - lowValue) / lowSpan );
		float tHigh = saturate( (value - midValue) / highSpan );
		float power = max( curve, 0.001 );
		float3 lowToMid = lerp( lowTint, midTint, pow( Smooth( 0.0, 1.0, tLow ), power ) );
		float3 midToHigh = lerp( midTint, highTint, pow( Smooth( 0.0, 1.0, tHigh ), power ) );
		return value < midValue ? lowToMid : midToHigh;
	}

	float3 ApplyLumaPreservingTint( float3 rgb, float3 tint, float strength )
	{
		float amount = saturate( strength ) * 0.12;
		float3 lumaWeights = float3( 0.299, 0.587, 0.114 );
		float sourceLuma = max( dot( rgb, lumaWeights ), 0.0001 );
		float tintLuma = max( dot( tint, lumaWeights ), 0.0001 );
		float3 normalizedTint = clamp( tint / tintLuma, 0.35, 2.25 );
		float3 tinted = rgb * normalizedTint;
		float tintedLuma = max( dot( tinted, lumaWeights ), 0.0001 );
		tinted *= sourceLuma / tintedLuma;
		return lerp( rgb, tinted, amount );
	}

	void ApplyTerrainTinting( inout float3 rgb, float dist, float3 worldPos, bool hasWorldPos )
	{
		if ( !hasWorldPos )
			return;

		if ( terrainDistanceTintStrength > 0.0 )
		{
			float3 distanceTint = ThreePointTint(
				dist,
				terrainNearDistance,
				terrainMidDistance,
				terrainFarDistance,
				terrainNearTint,
				terrainDistanceMidTint,
				terrainFarTint,
				terrainDistanceCurve );

			rgb = ApplyLumaPreservingTint( rgb, distanceTint, terrainDistanceTintStrength );
		}

		if ( terrainElevationTintStrength > 0.0 )
		{
			float3 elevationTint = ThreePointTint(
				worldPos.z,
				terrainLowElevation,
				terrainMidElevation,
				terrainHighElevation,
				terrainLowTint,
				terrainElevationMidTint,
				terrainHighTint,
				terrainElevationCurve );

			rgb = ApplyLumaPreservingTint( rgb, elevationTint, terrainElevationTintStrength );
		}
	}

	float4 MainPs( PixelInput i ) : SV_Target0
	{
		float2 vScreenUv = CalculateViewportUv( i.vPositionSs.xy );
		float4 color = g_tColorBuffer.SampleLevel( g_sBilinearMirror, vScreenUv, 0 );
		float3 worldPos;
		bool hasWorldPos = TrySampleWorldPosition( vScreenUv, worldPos );

		if ( debugTint > 0.5 )
		{
			color.rgb = lerp( color.rgb, hazeTint, 0.35 * strength );
			return float4( max( color.rgb, 0.0 ), color.a );
		}

		float dist = SampleViewDistance( vScreenUv );

		if ( strength > 0.001 && dist < foregroundEnd )
		{
			float nearT = 1.0 - (dist / max( foregroundEnd, 0.001 ));
			nearT = nearT * nearT;

			float luma = dot( color.rgb, float3( 0.299, 0.587, 0.114 ) );
			color.rgb = lerp( float3( luma, luma, luma ), color.rgb, lerp( 1.0, fgSatBoost, nearT ) );

			float3 centered = color.rgb - 0.5;
			color.rgb = 0.5 + centered * lerp( 1.0, fgContrastBoost, nearT );
		}

		float atm = AtmosphericFactor( dist );
		if ( atm > 0.001 )
		{
			float lumaFar = dot( color.rgb, float3( 0.299, 0.587, 0.114 ) );
			color.rgb = lerp( color.rgb, float3( lumaFar, lumaFar, lumaFar ), desaturate * atm );

			float3 hazeBlue = lerp( color.rgb, color.rgb * float3( 0.78, 0.88, 1.12 ), 0.55 );
			hazeBlue = lerp( hazeBlue, hazeTint, 0.65 );
			color.rgb = lerp( color.rgb, hazeBlue, saturate( blueShift * atm ) );

			float3 soft = lerp( float3( 0.5, 0.5, 0.5 ), color.rgb, 1.0 - contrastReduce * atm * 0.55 );
			color.rgb = lerp( color.rgb, soft, contrastReduce * atm );
		}

		ApplyTerrainTinting( color.rgb, dist, worldPos, hasWorldPos );
		ApplyLookMasks( color.rgb, dist );
		ApplyGlobalGrade( color.rgb );

		if ( highlightWarmth > 0.001 || shadowCoolStrength > 0.001 || shadowDepth > 0.001 || abs( highlightExposure ) > 0.001 || abs( shadowExposure ) > 0.001 )
			ApplyCinematicGrade( color.rgb );

		return float4( max( color.rgb, 0.0 ), color.a );
	}
}
