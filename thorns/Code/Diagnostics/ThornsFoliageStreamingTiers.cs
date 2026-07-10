namespace Sandbox;

/// <summary>
/// Player-centered foliage/clutter distance tiers (meters). Grass/clutter never spawn globally.
/// </summary>
public static class ThornsFoliageStreamingTiers
{
	/// <summary>Tier 0 — full grass, flowers, rocks, small clutter.</summary>
	public const float Tier0MaxMeters = 35f;

	/// <summary>Tier 1 — reduced grass density, no tiny flowers, reduced small clutter.</summary>
	public const float Tier1MaxMeters = 80f;

	/// <summary>Tier 2 — trees and large bushes only.</summary>
	public const float Tier2MaxMeters = 200f;

	/// <summary>Decorative visual clutter hard cap (client-only).</summary>
	public const float VisualClutterMaxMeters = 50f;

	/// <summary>Tiny clutter (pebbles, flowers) hard cap.</summary>
	public const float TinyClutterMaxMeters = 25f;

	public static float TierForDistanceMeters( float distanceMeters )
	{
		if ( distanceMeters <= Tier0MaxMeters )
			return 0f;
		if ( distanceMeters <= Tier1MaxMeters )
			return 1f;
		if ( distanceMeters <= Tier2MaxMeters )
			return 2f;
		return 3f;
	}

	public static float GrassDensityScaleForTier( float tier )
	{
		if ( tier <= 0f )
			return 1f;
		if ( tier <= 1f )
			return 0.45f;
		return 0f;
	}
}
