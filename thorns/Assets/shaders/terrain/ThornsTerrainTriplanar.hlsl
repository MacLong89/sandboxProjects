#ifndef THORNS_TERRAIN_TRIPLANAR_H
#define THORNS_TERRAIN_TRIPLANAR_H

float g_flTriplanarSharpness < Default( 4.25 ); Range( 1.0, 14.0 ); UiGroup( "Cliffs" ); >;
float g_flTriplanarSlopeStart < Default( 0.14 ); Range( 0.0, 0.85 ); UiGroup( "Cliffs" ); >;
float g_flTriplanarSlopeEnd < Default( 0.48 ); Range( 0.05, 1.0 ); UiGroup( "Cliffs" ); >;

float Thorns_CliffTriplanarBlend( float3 geoNormal )
{
	float slope = saturate( 1.0 - abs( geoNormal.z ) );
	return smoothstep( g_flTriplanarSlopeStart, g_flTriplanarSlopeEnd, slope );
}

float4 Thorns_SampleTriplanarBcr( Texture2D tBcr, float3 localPos, float3 geoNormal, float uvScale )
{
	float3 nAbs = abs( normalize( geoNormal ) );
	float3 triW = pow( max( nAbs, 1e-5 ), g_flTriplanarSharpness );
	triW /= max( dot( triW, 1.0.xxx ), 1e-4 );

	float rep = max( uvScale, 1e-4 ) / 32.0;
	float2 uv_xy = localPos.xy * rep;
	float2 uv_xz = localPos.xz * rep;
	float2 uv_yz = localPos.yz * rep;

	float4 c_xy = tBcr.Sample( g_sAnisotropic, uv_xy );
	float4 c_xz = tBcr.Sample( g_sAnisotropic, uv_xz );
	float4 c_yz = tBcr.Sample( g_sAnisotropic, uv_yz );
	return c_xy * triW.z + c_xz * triW.y + c_yz * triW.x;
}

float4 Thorns_SampleTriplanarNho( Texture2D tNho, float3 localPos, float3 geoNormal, float uvScale )
{
	float3 nAbs = abs( normalize( geoNormal ) );
	float3 triW = pow( max( nAbs, 1e-5 ), g_flTriplanarSharpness );
	triW /= max( dot( triW, 1.0.xxx ), 1e-4 );

	float rep = max( uvScale, 1e-4 ) / 32.0;
	float2 uv_xy = localPos.xy * rep;
	float2 uv_xz = localPos.xz * rep;
	float2 uv_yz = localPos.yz * rep;

	float4 c_xy = tNho.Sample( g_sAnisotropic, uv_xy );
	float4 c_xz = tNho.Sample( g_sAnisotropic, uv_xz );
	float4 c_yz = tNho.Sample( g_sAnisotropic, uv_yz );
	return c_xy * triW.z + c_xz * triW.y + c_yz * triW.x;
}

#endif
