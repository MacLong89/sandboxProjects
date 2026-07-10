HEADER
{
	DevShader = true;
	Version = 1;
}

MODES
{
	Default();
	Forward();
}

FEATURES
{
	#include "ui/features.hlsl"
}

COMMON
{
	#include "ui/common.hlsl"
}

VS
{
	#include "ui/vertex.hlsl"
}

PS
{
	#include "ui/pixel.hlsl"

	Texture2D g_tColor < Attribute( "Texture" ); SrgbRead( true ); >;

	RenderState( SrgbWriteEnable0, true );
	RenderState( ColorWriteEnable0, RGBA );
	RenderState( FillMode, SOLID );
	RenderState( CullMode, NONE );
	RenderState( DepthWriteEnable, false );

	PS_OUTPUT MainPs( PS_INPUT i )
	{
		PS_OUTPUT o;
		UI_CommonProcessing_Pre( i );

		float2 centered = i.vTexCoord.xy * 2.0 - 1.0;
		float coverage = smoothstep( 1.015, 0.985, length( centered ) );
		clip( coverage - 0.001 );

		float4 vImage = g_tColor.Sample( g_sAniso, i.vTexCoord.xy );
		o.vColor = vImage * i.vColor.rgba;
		o.vColor.a *= coverage;

		return UI_CommonProcessing_Post( i, o );
	}
}
