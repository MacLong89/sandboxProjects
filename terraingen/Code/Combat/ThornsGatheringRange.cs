namespace Terraingen.Combat;

using Terraingen.World.Environment;

/// <summary>Melee gather reach (axe / pickaxe) — trace what you look at, in front of you only.</summary>
public static class ThornsGatheringRange
{
	/// <summary>Default aim trace length (~11.5 ft).</summary>
	public const float Meters = 3.5f;

	public static float Inches => Meters * ThornsEnvironmentUnits.InchesPerMeter;

	/// <summary>How far along the camera/aim ray we test for hits.</summary>
	public static float TraceInches => Inches;

	/// <summary>Max distance from player feet to the collider hit point.</summary>
	public static float MeleeForgivenessInches( float gatherRangeInches = 0f )
	{
		var trace = gatherRangeInches > 0f ? gatherRangeInches : Inches;
		return trace * 0.92f;
	}
}
