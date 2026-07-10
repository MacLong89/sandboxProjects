namespace Sandbox;

/// <summary>Rolls tame <see cref="ThornsLootRarity"/> tier and bloodline stat affinities when a creature is first tamed (host-only).</summary>
public static class ThornsTameTierRoll
{
	const float AffinityCap = 0.14f;

	/// <summary>Applies rolled tier + affinities + optional legendary gift to a freshly tamed identity.</summary>
	public static void HostApplyRollToIdentity( ThornsWildlifeIdentity wid, Random rng )
	{
		if ( wid is null || !wid.IsValid() )
			return;

		var authoritative = !Networking.IsActive || Networking.IsHost;
		if ( !authoritative )
			return;

		Roll( rng, out var tier, out var hpAff, out var dmgAff, out var spdAff, out var leg );

		wid.TameQualityTierSync = (byte)tier;
		wid.TameAffinityHpSync = hpAff;
		wid.TameAffinityDmgSync = dmgAff;
		wid.TameAffinitySpdSync = spdAff;
		wid.TameLegendaryAbilitySync = (byte)leg;
	}

	public static void Roll(
		Random rng,
		out ThornsLootRarity tier,
		out float hpAffinity,
		out float dmgAffinity,
		out float spdAffinity,
		out ThornsTameLegendaryAbility legendary )
	{
		hpAffinity = 0f;
		dmgAffinity = 0f;
		spdAffinity = 0f;
		legendary = ThornsTameLegendaryAbility.None;

		var u = rng.NextDouble();
		if ( u < 0.42 )
			tier = ThornsLootRarity.Common;
		else if ( u < 0.70 )
			tier = ThornsLootRarity.Uncommon;
		else if ( u < 0.87 )
			tier = ThornsLootRarity.Rare;
		else if ( u < 0.97 )
			tier = ThornsLootRarity.Epic;
		else
			tier = ThornsLootRarity.Legendary;

		var tierIdx = (int)tier;
		var baselineHp = tierIdx * 0.012f;
		var baselineDmg = tierIdx * 0.011f;
		var baselineSpd = tierIdx * 0.010f;
		hpAffinity += baselineHp + RandFrac( rng, 0f, 0.02f + tierIdx * 0.004f );
		dmgAffinity += baselineDmg + RandFrac( rng, 0f, 0.018f + tierIdx * 0.004f );
		spdAffinity += baselineSpd + RandFrac( rng, 0f, 0.018f + tierIdx * 0.004f );

		// Emphasize one random bloodline per tier (Common–Rare); higher tiers get extra cherry-picks.
		var picks = tier switch
		{
			ThornsLootRarity.Common => 1,
			ThornsLootRarity.Uncommon => 1,
			ThornsLootRarity.Rare => 2,
			ThornsLootRarity.Epic => 2,
			ThornsLootRarity.Legendary => 3,
			_ => 1
		};

		for ( var p = 0; p < picks; p++ )
		{
			var stat = rng.Next( 3 );
			var bump = 0.02f + (float)rng.NextDouble() * ( 0.025f + tierIdx * 0.01f );
			bump = Math.Min( bump, AffinityCap );
			switch ( stat )
			{
				case 0:
					hpAffinity += bump;
					break;
				case 1:
					dmgAffinity += bump;
					break;
				default:
					spdAffinity += bump;
					break;
			}
		}

		hpAffinity = Math.Clamp( hpAffinity, 0f, AffinityCap * 2.2f );
		dmgAffinity = Math.Clamp( dmgAffinity, 0f, AffinityCap * 2.2f );
		spdAffinity = Math.Clamp( spdAffinity, 0f, AffinityCap * 2.2f );

		if ( tier == ThornsLootRarity.Legendary )
			legendary = (ThornsTameLegendaryAbility)( 1 + rng.Next( 5 ) );
	}

	static float RandFrac( Random rng, float lo, float hi )
	{
		if ( hi <= lo )
			return 0f;
		return lo + (float)rng.NextDouble() * ( hi - lo );
	}
}
