using System.Globalization;

namespace Sandbox;

/// <summary>
/// Serialized into <see cref="ThornsInventorySlot.WeaponRollPayload"/> / <see cref="ThornsInventorySlot.ArmorRollPayload"/>.
/// Format <c>v1;R{rarity};D{dmgMilli};F{fireMilli}</c> weapons, <c>v1;R{rarity};P{drMilli}</c> armor.
/// </summary>
public static class ThornsGearRoll
{
	public const string Prefix = "v1";

	public static string EncodeWeapon( ThornsLootRarity rarity, float damageMul, float fireRateMul )
	{
		damageMul = Math.Clamp( damageMul, 0.65f, 1.85f );
		fireRateMul = Math.Clamp( fireRateMul, 0.65f, 1.85f );
		var d = (int)Math.Round( damageMul * 1000f );
		var f = (int)Math.Round( fireRateMul * 1000f );
		return $"{Prefix};R{(byte)rarity};D{d};F{f}";
	}

	public static string EncodeArmor( ThornsLootRarity rarity, float drContributionMul )
	{
		drContributionMul = Math.Clamp( drContributionMul, 0.75f, 1.55f );
		var p = (int)Math.Round( drContributionMul * 1000f );
		return $"{Prefix};R{(byte)rarity};P{p}";
	}

	public static bool TryParseWeapon( string payload, out ThornsLootRarity rarity, out float damageMul, out float fireRateMul )
	{
		rarity = ThornsLootRarity.Common;
		damageMul = 1f;
		fireRateMul = 1f;
		if ( string.IsNullOrWhiteSpace( payload ) )
			return false;

		var parts = payload.Split( ';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries );
		if ( parts.Length < 4 || !string.Equals( parts[0], Prefix, StringComparison.OrdinalIgnoreCase ) )
			return false;

		byte rb = 0;
		var dmgOk = false;
		var frOk = false;
		foreach ( var p in parts )
		{
			if ( p.Length >= 2 && p[0] == 'R' && byte.TryParse( p.AsSpan( 1 ), out var rv ) )
				rb = rv;
			else if ( p.Length >= 2 && p[0] == 'D' && int.TryParse( p.AsSpan( 1 ), NumberStyles.Integer, CultureInfo.InvariantCulture, out var dv ) )
			{
				damageMul = Math.Clamp( dv / 1000f, 0.5f, 2.5f );
				dmgOk = true;
			}
			else if ( p.Length >= 2 && p[0] == 'F' && int.TryParse( p.AsSpan( 1 ), NumberStyles.Integer, CultureInfo.InvariantCulture, out var fv ) )
			{
				fireRateMul = Math.Clamp( fv / 1000f, 0.5f, 2.5f );
				frOk = true;
			}
		}

		rarity = rb <= (byte)ThornsLootRarity.Legendary ? (ThornsLootRarity)rb : ThornsLootRarity.Common;
		return dmgOk && frOk;
	}

	public static bool TryParseArmor( string payload, out ThornsLootRarity rarity, out float drContributionMul )
	{
		rarity = ThornsLootRarity.Common;
		drContributionMul = 1f;
		if ( string.IsNullOrWhiteSpace( payload ) )
			return false;

		var parts = payload.Split( ';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries );
		if ( parts.Length < 3 || !string.Equals( parts[0], Prefix, StringComparison.OrdinalIgnoreCase ) )
			return false;

		byte rb = 0;
		var pOk = false;
		foreach ( var p in parts )
		{
			if ( p.Length >= 2 && p[0] == 'R' && byte.TryParse( p.AsSpan( 1 ), out var rv ) )
				rb = rv;
			else if ( p.Length >= 2 && p[0] == 'P' && int.TryParse( p.AsSpan( 1 ), NumberStyles.Integer, CultureInfo.InvariantCulture, out var pv ) )
			{
				drContributionMul = Math.Clamp( pv / 1000f, 0.5f, 2f );
				pOk = true;
			}
		}

		rarity = rb <= (byte)ThornsLootRarity.Legendary ? (ThornsLootRarity)rb : ThornsLootRarity.Common;
		return pOk;
	}

	public static (float dmg, float fire) RollWeaponMultipliers( Random rng, ThornsLootRarity r ) =>
		r switch
		{
			ThornsLootRarity.Common => (Lerp( rng, 1.00f, 1.04f ), Lerp( rng, 1.00f, 1.05f )),
			ThornsLootRarity.Uncommon => (Lerp( rng, 1.04f, 1.10f ), Lerp( rng, 1.05f, 1.14f )),
			ThornsLootRarity.Rare => (Lerp( rng, 1.10f, 1.18f ), Lerp( rng, 1.12f, 1.22f )),
			ThornsLootRarity.Epic => (Lerp( rng, 1.16f, 1.28f ), Lerp( rng, 1.18f, 1.30f )),
			ThornsLootRarity.Legendary => (Lerp( rng, 1.22f, 1.38f ), Lerp( rng, 1.22f, 1.38f )),
			_ => (1f, 1f)
		};

	public static float RollArmorDrMultiplier( Random rng, ThornsLootRarity r ) =>
		r switch
		{
			ThornsLootRarity.Common => Lerp( rng, 1.00f, 1.03f ),
			ThornsLootRarity.Uncommon => Lerp( rng, 1.03f, 1.08f ),
			ThornsLootRarity.Rare => Lerp( rng, 1.07f, 1.14f ),
			ThornsLootRarity.Epic => Lerp( rng, 1.12f, 1.22f ),
			ThornsLootRarity.Legendary => Lerp( rng, 1.18f, 1.32f ),
			_ => 1f
		};

	static float Lerp( Random rng, float a, float b ) =>
		a + (float)rng.NextDouble() * (b - a);
}
