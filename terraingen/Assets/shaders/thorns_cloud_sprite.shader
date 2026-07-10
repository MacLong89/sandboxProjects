FEATURES
{
	#include "common/features.hlsl"
}

MODES
{
	Forward();
}

COMMON
{
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
	#include "common/pixel.hlsl"

	RenderState( CullMode, NONE );
	RenderState( DepthWriteEnable, false );
	RenderState( BlendEnable, true );
	RenderState( SrcBlend, SRC_ALPHA );
	RenderState( DstBlend, INV_SRC_ALPHA );

	CreateTexture2D( g_tCloudTexture ) < Attribute( "CloudTexture" ); Filter( MIN_MAG_MIP_LINEAR ); AddressU( CLAMP ); AddressV( CLAMP ); >;
	CreateTexture2D( g_tCloudAlpha ) < Attribute( "CloudAlphaTexture" ); Filter( MIN_MAG_MIP_LINEAR ); AddressU( CLAMP ); AddressV( CLAMP ); >;

	float3 g_vCloudTint < Attribute( "CloudTint" ); Default3( 1.0, 1.0, 1.0 ); >;
	float g_flCloudOpacity < Attribute( "CloudOpacity" ); Default( 1.0 ); >;

	float4 MainPs( PixelInput i ) : SV_Target
	{
		float4 tex = g_tCloudTexture.Sample( g_tCloudTexture_sampler, i.vTextureCoords.xy );
		float4 alphaTex = g_tCloudAlpha.Sample( g_tCloudAlpha_sampler, i.vTextureCoords.xy );

		float maskAlpha = saturate( max( alphaTex.r, max( alphaTex.g, alphaTex.b ) ) );
		float colorAlpha = saturate( tex.a );
		float alpha = max( maskAlpha, colorAlpha );
		alpha = alpha * alpha * ( 3.0 - 2.0 * alpha );
		alpha *= g_flCloudOpacity;
		clip( alpha - 0.01 );

		float3 color = lerp( float3( 0.58, 0.70, 0.82 ), tex.rgb, 0.70 ) * g_vCloudTint;
		float luma = max( dot( color, float3( 0.2126, 0.7152, 0.0722 ) ), 0.001 );
		color *= min( 1.0, 0.86 / luma );
		return float4( color, alpha );
	}
}
