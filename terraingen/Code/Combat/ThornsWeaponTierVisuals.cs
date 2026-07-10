namespace Terraingen.Combat;

using Sandbox;

/// <summary>Weapon tier palette (1 = lowest … 4 = military) for inventory chrome + inspect text.</summary>
public static class ThornsWeaponTierVisuals
{
	public static Color SlotBackdropTint( int weaponTier ) =>
		weaponTier switch
		{
			1 => new Color( 0.72f, 0.74f, 0.78f, 0.35f ),
			2 => new Color( 0.28f, 0.62f, 0.38f, 0.38f ),
			3 => new Color( 0.28f, 0.48f, 0.92f, 0.40f ),
			4 => new Color( 0.92f, 0.58f, 0.18f, 0.42f ),
			_ => new Color( 0.55f, 0.58f, 0.62f, 0.28f )
		};

	public static Color TitleTint( int weaponTier ) =>
		weaponTier switch
		{
			1 => new Color( 0.82f, 0.84f, 0.88f, 1f ),
			2 => new Color( 0.45f, 0.92f, 0.55f, 1f ),
			3 => new Color( 0.55f, 0.78f, 1f, 1f ),
			4 => new Color( 1f, 0.72f, 0.32f, 1f ),
			_ => new Color( 0.88f, 0.90f, 0.94f, 1f )
		};

	public static string TierDisplayName( int weaponTier ) =>
		weaponTier switch
		{
			1 => "Tier I — Scavenged",
			2 => "Tier II — Standard",
			3 => "Tier III — Military",
			4 => "Tier IV — Elite",
			_ => "Tier — Unknown"
		};

	/// <summary>Short tier label for inspect pills and tight UI chrome.</summary>
	public static string TierName( int weaponTier ) =>
		weaponTier switch
		{
			1 => "Scavenged",
			2 => "Standard",
			3 => "Military",
			4 => "Elite",
			_ => "Unknown"
		};

	public static int ResolveWeaponTier( string combatId )
	{
		if ( string.IsNullOrWhiteSpace( combatId ) )
			return 1;

		var cid = combatId.Trim();
		if ( ThornsFpToolCombat.IsToolMeleeCombatId( cid ) )
		{
			if ( string.Equals( cid, ThornsFpToolCombat.CombatIdPrimitive, StringComparison.OrdinalIgnoreCase ) )
				return 1;
			if ( string.Equals( cid, ThornsFpToolCombat.CombatIdStone, StringComparison.OrdinalIgnoreCase ) )
				return 2;
			return 3;
		}

		if ( string.Equals( cid, "m9_bayonet", StringComparison.OrdinalIgnoreCase ) )
			return 4;

		return Math.Clamp( ThornsWeaponDefinitions.Get( cid ).WeaponTier, 1, 4 );
	}

	public static Color DurabilityStripColor( float fill01 )
	{
		if ( fill01 >= 0.55f )
			return new Color( 0.35f, 0.92f, 0.78f, 1f );
		if ( fill01 >= 0.30f )
			return new Color( 1f, 0.85f, 0.38f, 1f );
		if ( fill01 >= 0.12f )
			return new Color( 1f, 0.58f, 0.28f, 1f );
		return new Color( 1f, 0.38f, 0.38f, 1f );
	}
}
