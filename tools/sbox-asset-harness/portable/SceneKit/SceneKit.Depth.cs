namespace SceneKit;

/// <summary>
/// Mild offsets so coplanar meshes do not z-fight. Prefer ±1 unit nudges — no large floating bands.
/// </summary>
public static class Depth
{
	public const float Step = 1f;
	public const float SitOnGround = 0.05f;
	public const float PropAbovePad = 1f;

	/// <summary>Center-pivoted upright box: local Z so the bottom touches the parent origin plane.</summary>
	public static float CenterPivotLift( float height ) => height * 0.5f + SitOnGround;

	public static float NextFaceDepth( ref int slot ) => Step * (1 + slot++);
}
