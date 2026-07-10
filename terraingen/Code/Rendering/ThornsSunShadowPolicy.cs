namespace Terraingen.Rendering;

/// <summary>Shared directional-light shadow tuning to reduce cascade popping.</summary>
public static class ThornsSunShadowPolicy
{
	public const int DefaultCascadeCount = 4;
	public const float DefaultCascadeSplitRatio = 0.88f;
	public const float DefaultShadowBias = 0.0005f;
	public const float DefaultShadowHardness = 0.45f;

	public static void ApplyDirectionalLightSettings( DirectionalLight sun, int cascadeCount = DefaultCascadeCount )
	{
		if ( sun is null || !sun.IsValid() )
			return;

		sun.ShadowCascadeCount = Math.Clamp( cascadeCount, 1, 4 );
		sun.ShadowCascadeSplitRatio = DefaultCascadeSplitRatio;
		sun.ShadowBias = DefaultShadowBias;
		sun.ShadowHardness = DefaultShadowHardness;
	}
}
