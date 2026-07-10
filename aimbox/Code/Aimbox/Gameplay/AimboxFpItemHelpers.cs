namespace Sandbox;

/// <summary>First-person pose constants for custom item meshes (ported from terraingen).</summary>
public static class AimboxFpItemHelpers
{
	public static readonly Vector3 FpBowViewmodelRootOffset = new( 20f, -6f, -1f );

	public static readonly Vector3 FpBowViewmodelRootEulerDegrees = new( 180f, 180f, 0f );

	/// <summary>Bow skips the stock v_* 10× FP mesh multiplier — this is the final grip scale.</summary>
	public static readonly Vector3 FpBowViewmodelRootScale = new( 0.8f, 0.8f, 0.8f );
}
