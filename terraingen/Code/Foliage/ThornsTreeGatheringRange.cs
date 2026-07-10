namespace Terraingen.Foliage;

using Terraingen.Combat;

/// <summary>Tree harvest — same reach as <see cref="ThornsGatheringRange"/>.</summary>
public static class ThornsTreeGatheringRange
{
	public static float Meters => ThornsGatheringRange.Meters;

	public static float Inches => ThornsGatheringRange.Inches;

	public static float TraceInches => ThornsGatheringRange.TraceInches;

	/// <summary>Trunk box colliders only within chop interaction range (+ buffer).</summary>
	public const float CollisionProxyInches = 3200f;
}
