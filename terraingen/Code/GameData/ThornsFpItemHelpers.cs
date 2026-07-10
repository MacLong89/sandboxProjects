namespace Terraingen.GameData;

using System;

/// <summary>FP viewmodel pose constants and combat-id helpers shared with thorns.</summary>
public static class ThornsFpItemHelpers
{
	public static readonly Vector3 FpViewmodelRootLocalScaleOne = new( 1f, 1f, 1f );

	public static readonly Vector3 FpHarvestAxePickaxeViewmodelRootOffset = new( 5f, -2f, -1f );

	public static readonly Vector3 FpHarvestAxeViewmodelRootEulerDegrees = new( 20f, 180f, -60f );

	public static readonly Vector3 FpHarvestPickaxeViewmodelRootEulerDegrees = new( 20f, -180f, -40f );

	public static readonly Vector3 FpHarvestStonePickaxeViewmodelRootEulerDegrees = new( 20f, 10f, -30f );

	public static readonly Vector3 FpHarvestToolViewmodelRootScale = new( 4f, 4f, 4f );

	public static readonly Vector3 FpMedkitViewmodelRootOffset = new( 14f, 0f, 0f );

	public static readonly Vector3 FpBowViewmodelRootOffset = new( 30f, -9f, 1f );

	public static readonly Vector3 FpBowViewmodelRootEulerDegrees = new( 0f, 90f, 0f );

	/// <summary>Bow skips the stock <c>v_*</c> 10× FP mesh multiplier — this is the final grip scale.</summary>
	public static readonly Vector3 FpBowViewmodelRootScale = new( 0.075f, 0.075f, 0.075f );

	/// <summary>Arrow release point in bow viewmodel-local space (keep Y aligned with grip for right-screen origin).</summary>
	public static readonly Vector3 FpBowTracerLocalOffset = new( 44f, -9f, 1f );
}
