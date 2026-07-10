HEADER
{
	Description = "Aimbox arena world-tiled triplanar surfaces";
	DevShader = true;
	Version = 1;
}

FEATURES
{
	#include "common/features.hlsl"
}

MODES
{
	Forward();
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
	#include "common/utils/triplanar.hlsl"
	#include "common/pixel.hlsl"

	float g_flTileSizeInches < Attribute( "TileSizeInches" ); Default( 64.0 ); Range( 1.0, 512.0 ); UiGroup( "Tiling" ); >;
	float g_flRoughness < Attribute( "Roughness" ); Default( 0.88 ); Range( 0.0, 1.0 ); >;
	float g_flTriplanarSharpness < Attribute( "TriplanarSharpness" ); Default( 4.0 ); Range( 1.0, 12.0 ); >;

	float4 MainPs( PixelInput i ) : SV_Target
	{
		Material m = Material::Init( i );
		float flTileFrequency = 39.3701 / max( g_flTileSizeInches, 1e-4 );
		m.Albedo = Tex2DTriplanar( g_tColor, TextureFiltering, m.WorldPosition, i.vNormalWs, flTileFrequency.xx, g_flTriplanarSharpness ).rgb;
		m.Roughness = g_flRoughness;
		m.Metalness = 0.05;
		m.AmbientOcclusion = 1.0;
		m.Opacity = 1.0;
		m.Normal = normalize( i.vNormalWs );

		if ( DepthNormals::WantsDepthNormals() )
			return DepthNormals::Output( m.Normal, m.Roughness, 1 );

		return ShadingModelStandard::Shade( i, m );
	}
}
