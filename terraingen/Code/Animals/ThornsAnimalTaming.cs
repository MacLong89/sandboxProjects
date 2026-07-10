namespace Terraingen.Animals;

/// <summary>Shared taming thresholds and use interaction tuning.</summary>
public static class ThornsAnimalTaming
{
	/// <summary>Wildlife enters awaiting-tame at or below this fraction of spawn HP (20%).</summary>
	public const float LowHealthFraction = 0.2f;

	/// <summary>Cap incoming damage so wildlife stops at the knockout threshold instead of dying.</summary>
	public static float ClampDamageForKnockout( float amount, float currentHealth, float spawnHealth )
	{
		if ( amount <= 0f || spawnHealth <= 0f )
			return amount;

		var knockoutHealth = spawnHealth * LowHealthFraction;
		if ( currentHealth - amount >= knockoutHealth )
			return amount;

		return Math.Max( 0f, currentHealth - knockoutHealth );
	}
	public const float UseHoldSeconds = 1f;
	public const float UseMaxRange = 300f;
	public const float UseAimDotThreshold = 0.45f;
	public const float OwnerMarkSeconds = 14f;
	public const float OwnerThreatSeconds = 14f;
	public const float FollowOwnerDistance = 200f;
	/// <summary>Resume follow only after leaving this larger leash (prevents idle/move flicker at the boundary).</summary>
	public const float FollowResumeDistance = 280f;
	/// <summary>Flat distance from owner before a following tame sprints to catch up.</summary>
	public const float FollowCatchUpDistance = 560f;
}
