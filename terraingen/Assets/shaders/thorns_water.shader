FEATURES
{
	#include "common/features.hlsl"
}

MODES
{
	VrForward();
	Depth();
}

COMMON
{
	#define S_SPECULAR 1
	#include "common/shared.hlsl"
}

struct VertexInput
{
	#include "common/vertexinput.hlsl"
};

struct PixelInput
{
	#include "common/pixelinput.hlsl"
};

VS
{
	#include "common/vertex.hlsl"

	PixelInput MainVs( VertexInput i )
	{
		PixelInput o = ProcessVertex( i );
		return FinalizeVertex( o );
	}
}

PS
{
	#include "common/utils/Material.CommonInputs.hlsl"
	#include "common/pixel.hlsl"

	CreateTexture2D( g_tTerrainHeight ) < Attribute( "TerrainHeight" ); Filter( MIN_MAG_MIP_LINEAR ); AddressU( CLAMP ); AddressV( CLAMP ); >;
	float g_flHasTerrainHeight < Attribute( "HasTerrainHeight" ); Default( 0.0 ); UiGroup( "Terrain" ); >;

	float3 g_vTerrainOrigin < Attribute( "TerrainOrigin" ); Default3( 0.0, 0.0, 0.0 ); UiGroup( "Terrain" ); >;
	float g_flTerrainSize < Attribute( "TerrainSize" ); Default( 1.0 ); UiGroup( "Terrain" ); >;
	float g_flTerrainMaxHeight < Attribute( "TerrainMaxHeight" ); Default( 1.0 ); UiGroup( "Terrain" ); >;
	float g_flSeaLevelZ < Attribute( "SeaLevelWorldZ" ); Default( 0.0 ); UiGroup( "Terrain" ); >;

	float3 g_vShallowColor < UiType( Color ); Attribute( "ShallowColor" ); Default3( 0.149, 0.463, 0.686 ); UiGroup( "Water Color" ); >;
	float3 g_vDeepColor < UiType( Color ); Attribute( "DeepColor" ); Default3( 0.024, 0.204, 0.361 ); UiGroup( "Water Color" ); >;
	float3 g_vShoreTint < UiType( Color ); Attribute( "ShoreTint" ); Default3( 0.345, 0.647, 0.776 ); UiGroup( "Water Color" ); >;
	float3 g_vFoamColor < UiType( Color ); Attribute( "FoamColor" ); Default3( 0.745, 0.863, 0.922 ); UiGroup( "Water Color" ); >;
	float g_flColorSaturation < Attribute( "ColorSaturation" ); Default( 1.28 ); Range( 0.8, 1.8 ); UiGroup( "Water Color" ); >;
	float g_flColorBoost < Attribute( "ColorBoost" ); Default( 0.90 ); Range( 0.7, 1.3 ); UiGroup( "Water Color" ); >;
	float g_flTextureBlend < Attribute( "TextureBlend" ); Default( 0.92 ); Range( 0.0, 1.0 ); UiGroup( "Water Color" ); >;

	float g_flShallowDepth < Attribute( "ShallowDepthInches" ); Default( 72.0 ); Range( 20.0, 800.0 ); UiGroup( "Depth" ); >;
	float g_flDeepDepth < Attribute( "DeepDepthInches" ); Default( 1600.0 ); Range( 200.0, 12000.0 ); UiGroup( "Depth" ); >;
	float g_flShoreBlendDepth < Attribute( "ShoreBlendDepthInches" ); Default( 320.0 ); Range( 40.0, 1200.0 ); UiGroup( "Depth" ); >;
	float g_flShoreBlendStrength < Attribute( "ShoreBlendStrength" ); Default( 0.16 ); Range( 0.0, 1.0 ); UiGroup( "Depth" ); >;

	float g_flFoamWidth < Attribute( "FoamWidthInches" ); Default( 52.0 ); Range( 8.0, 200.0 ); UiGroup( "Foam" ); >;
	float g_flFoamStrength < Attribute( "FoamStrength" ); Default( 0.14 ); Range( 0.0, 0.5 ); UiGroup( "Foam" ); >;

	float g_flReflectionRoughness < Attribute( "ReflectionRoughness" ); Default( 0.68 ); Range( 0.2, 1.0 ); UiGroup( "Reflection" ); >;
	float g_flWaterMetalness < Attribute( "WaterMetalness" ); Default( 0.18 ); Range( 0.0, 1.0 ); UiGroup( "Reflection" ); >;
	float g_fSpecularScale < Attribute( "SpecularScale" ); Default( 0.48 ); Range( 0.0, 1.0 ); UiGroup( "Reflection" ); >;

	float3 g_vWaterFogColor < UiType( Color ); Attribute( "WaterFogColor" ); Default3( 0.376, 0.627, 0.922 ); UiGroup( "Atmosphere" ); >;
	float g_flWaterFogStart < Attribute( "WaterFogStartInches" ); Default( 4200.0 ); Range( 100.0, 20000.0 ); UiGroup( "Atmosphere" ); >;
	float g_flWaterFogEnd < Attribute( "WaterFogEndInches" ); Default( 14000.0 ); Range( 500.0, 40000.0 ); UiGroup( "Atmosphere" ); >;
	float g_flWaterFogStrength < Attribute( "WaterFogStrength" ); Default( 0.42 ); Range( 0.0, 1.0 ); UiGroup( "Atmosphere" ); >;

	float NormalScale < Default( 0.35 ); Range( 0.0, 2.0 ); UiGroup( "Waves" ); >;
	float BigWaveSize < Default( 0.22 ); Range( 0.0, 1.0 ); UiGroup( "Waves" ); >;
	float BigWaveScale < Default( 180.0 ); Range( 16.0, 512.0 ); UiGroup( "Waves" ); >;
	float BigWaveTime < Default( 0.035 ); Range( 0.0, 10.0 ); UiGroup( "Waves" ); >;
	float2 g_vWaterScrollSpeed < Attribute( "WaterScrollSpeed" ); Default2( 0.0004, 0.0003 ); UiGroup( "Waves" ); >;
	float g_flWaterUvScale < Attribute( "WaterUvScale" ); Default( 8.0 ); Range( 1.0, 64.0 ); UiGroup( "Waves" ); >;

	float SmoothStep( float edge0, float edge1, float x )
	{
		float t = saturate( (x - edge0) / max( edge1 - edge0, 0.001 ) );
		return t * t * (3.0 - 2.0 * t);
	}

	float SampleTerrainHeightNorm( float2 uv )
	{
		if ( g_flHasTerrainHeight < 0.5 )
			return 0.0;

		return g_tTerrainHeight.Sample( g_tTerrainHeight_sampler, saturate( uv ) ).r;
	}

	float SampleWaterDepth( float3 worldPos )
	{
		float2 uv = (worldPos.xy - g_vTerrainOrigin.xy) / max( g_flTerrainSize, 1.0 );
		float terrainZ = g_vTerrainOrigin.z + SampleTerrainHeightNorm( uv ) * g_flTerrainMaxHeight;
		return max( g_flSeaLevelZ - terrainZ, 0.0 );
	}

	float SampleShoreFoam( float3 worldPos, float depth )
	{
		float2 uv = (worldPos.xy - g_vTerrainOrigin.xy) / max( g_flTerrainSize, 1.0 );
		float texel = 1.5 / max( g_flTerrainSize, 1.0 );

		float hC = SampleTerrainHeightNorm( uv );
		float hX = SampleTerrainHeightNorm( uv + float2( texel, 0.0 ) );
		float hY = SampleTerrainHeightNorm( uv + float2( 0.0, texel ) );

		float slope = length( float2( hC - hX, hC - hY ) ) * g_flTerrainMaxHeight / max( g_flTerrainSize, 1.0 );
		float shallowFoam = 1.0 - SmoothStep( 0.0, g_flFoamWidth, depth );
		float slopeFoam = SmoothStep( 0.02, 0.14, slope ) * (1.0 - SmoothStep( g_flShallowDepth * 0.5, g_flShallowDepth * 2.0, depth ));

		float wave = sin( worldPos.x * 0.018 + g_flTime * 0.085 ) * sin( worldPos.y * 0.021 + g_flTime * 0.07 );
		float ripple = 0.5 + 0.5 * wave;

		return saturate( max( shallowFoam, slopeFoam * 0.65 ) * ripple ) * g_flFoamStrength;
	}

	float3 BoostWaterColor( float3 rgb, float saturation, float boost )
	{
		float luma = dot( rgb, float3( 0.299, 0.587, 0.114 ) );
		rgb = lerp( float3( luma, luma, luma ), rgb, saturation ) * boost;
		return saturate( rgb );
	}

	Material GetWaveMaterial( PixelInput ii, float waveTime, float waveScale, float scale )
	{
		PixelInput i = ii;
		float3 worldPos = g_vCameraPositionWs + i.vPositionWithOffsetWs;

		float2 uv = i.vTextureCoords.xy * g_flWaterUvScale;
		float2 warp = float2(
			sin( (worldPos.x + worldPos.y) * 0.00008 + g_flTime * waveTime ) * waveScale * 0.04,
			cos( (worldPos.x - worldPos.y) * 0.00009 + g_flTime * waveTime * 1.05 ) * waveScale * 0.04 );
		i.vTextureCoords.xy = uv + warp + g_flTime * g_vWaterScrollSpeed;

		return Material::From( i );
	}

	float4 MainPs( PixelInput i ) : SV_Target
	{
		float3 worldPos = g_vCameraPositionWs + i.vPositionWithOffsetWs;
		float depth = SampleWaterDepth( worldPos );

		Material a = GetWaveMaterial( i, BigWaveTime, BigWaveSize, BigWaveScale );
		Material b = GetWaveMaterial( i, BigWaveTime * 0.9, BigWaveSize * 0.85, BigWaveScale * -1.2 );
		Material waveMat = Material::lerp( a, b, 0.5 );

		float depthT = SmoothStep( g_flShallowDepth, g_flDeepDepth, depth );
		float3 waterColor = lerp( g_vShallowColor, g_vDeepColor, pow( depthT, 1.18 ) );
		float3 texDetail = saturate( waveMat.Albedo );
		waterColor *= lerp( float3( 1.0, 1.0, 1.0 ), texDetail, g_flTextureBlend );

		float shoreT = 1.0 - SmoothStep( 0.0, g_flShoreBlendDepth, depth );
		waterColor = lerp( waterColor, waterColor * g_vShoreTint, shoreT * g_flShoreBlendStrength );
		waterColor = BoostWaterColor( waterColor, g_flColorSaturation, g_flColorBoost );

		float foam = SampleShoreFoam( worldPos, depth );
		waterColor = lerp( waterColor, g_vFoamColor, foam );

		float3 camDir = CalculatePositionToCameraDirWs( i.vPositionWithOffsetWs.xyz + g_vHighPrecisionLightingOffsetWs.xyz );
		Material m = waveMat;
		m.Normal = lerp( i.vNormalWs, m.Normal, NormalScale );
		m.Albedo = waterColor;
		m.Opacity = 1.0;
		m.Roughness = g_flReflectionRoughness;
		m.Metalness = g_flWaterMetalness;
		m.AmbientOcclusion = 1.0;
		m.Emission = waterColor * 0.035;

		if ( DepthNormals::WantsDepthNormals() )
			return DepthNormals::Output( m.Normal, m.Roughness, 1 );

		float fresnel = pow( 1.0 - saturate( dot( m.Normal, camDir ) ), 4.0 );
		m.Emission += g_vShallowColor * fresnel * 0.03 * g_fSpecularScale;

		float4 outCol = ShadingModelStandard::Shade( i, m );
		outCol.rgb = Fog::Apply( worldPos, i.vPositionSs.xy, outCol.rgb );

		float viewDist = distance( worldPos, g_vCameraPositionWs );
		float fogT = SmoothStep( g_flWaterFogStart, g_flWaterFogEnd, viewDist );
		fogT = pow( fogT, 1.35 ) * g_flWaterFogStrength;
		float3 hazeMix = lerp( outCol.rgb, g_vWaterFogColor, fogT );
		outCol.rgb = lerp( outCol.rgb, hazeMix, saturate( fogT * 0.5 + depthT * 0.05 ) );

		return outCol;
	}
}
