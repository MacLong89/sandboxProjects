using System;
using System.Globalization;
using System.Text;

namespace Sandbox;

/// <summary>Inspect panel + inventory row tinting for weapon instances (def + loot roll).</summary>
public static class ThornsUiWeaponInspectFormatting
{
	public static readonly Color DefaultInventorySlotPrimaryTint = new( 238f / 255f, 240f / 255f, 244f / 255f, 0.95f );

	public static void ResolveWeaponRoll( ThornsInventorySlotNet net, out ThornsLootRarity rarity, out float damageMul, out float fireRateMul )
	{
		if ( ThornsGearRoll.TryParseWeapon( net.WeaponRollPayload ?? "", out rarity, out damageMul, out fireRateMul ) )
			return;
		rarity = ThornsLootRarity.Common;
		damageMul = 1f;
		fireRateMul = 1f;
	}

	public static bool TryGetWeaponInventoryTitleTint( ThornsInventorySlotNet net, out Color tint )
	{
		tint = default;
		if ( string.IsNullOrWhiteSpace( net.ItemId ) || net.Quantity <= 0 )
			return false;
		if ( !ThornsItemRegistry.TryGet( net.ItemId, out var def ) || def.ItemType != ThornsItemType.Weapon )
			return false;
		ResolveWeaponRoll( net, out var r, out _, out _ );
		tint = r.TintApprox();
		return true;
	}

	public static Color ResolveAbbrevToolbarTint( ThornsInventorySlotNet net )
	{
		ResolveWeaponRoll( net, out var r, out _, out _ );
		return r.TintApprox();
	}

	public static string BuildWeaponInspectStatsBlock(
		ThornsItemRegistry.ThornsItemDefinition itemDef,
		ThornsInventorySlotNet net )
	{
		var cid = string.IsNullOrEmpty( itemDef.CombatWeaponDefinitionId )
			? net.ItemId
			: itemDef.CombatWeaponDefinitionId;
		cid = cid?.Trim() ?? "";
		var w = ThornsWeaponDefinitions.Get( cid );
		ResolveWeaponRoll( net, out var rarity, out var dmgMul, out var frMul );

		dmgMul = Math.Clamp( dmgMul, 0.5f, 2f );
		frMul = Math.Clamp( frMul, 0.5f, 2f );

		var sb = new StringBuilder( 512 );
		var inv = CultureInfo.InvariantCulture;
		sb.Append( "Rarity · " ).AppendLine( rarity.DisplayName() );
		if ( Math.Abs( dmgMul - 1f ) > 0.004f || Math.Abs( frMul - 1f ) > 0.004f )
			sb.Append( "Roll · " )
				.Append( ( dmgMul * 100f ).ToString( "F1", inv ) )
				.Append( "% dmg · " )
				.Append( ( frMul * 100f ).ToString( "F1", inv ) )
				.AppendLine( "% fire rate" );

		var melee = ThornsWeaponDefinitions.TreatsAsMeleeWeapon( w, cid );
		if ( melee )
		{
			var stab = w.BaseDamage * dmgMul;
			sb.Append( "Damage · " ).AppendLine( stab.ToString( "F1", inv ) );
			if ( w.SecondaryAttackBaseDamage > 0.01f )
			{
				var heavy = w.SecondaryAttackBaseDamage * dmgMul;
				sb.Append( "Heavy swing · " ).AppendLine( heavy.ToString( "F1", inv ) );
			}

			sb.Append( "Attack cadence · " )
				.Append( ( w.FireIntervalSeconds / frMul ).ToString( "F3", inv ) )
				.AppendLine( "s (primary)" );
			if ( w.SecondaryAttackBaseDamage > 0.01f )
			{
				sb.Append( "Heavy cooldown · " )
					.Append( ( w.SecondaryAttackFireIntervalSeconds / frMul ).ToString( "F2", inv ) )
					.AppendLine( "s" );
			}

			sb.Append( "Reach · " ).AppendLine( w.MaxRange.ToString( "F0", inv ) );
			sb.Append( "Fire mode · " ).AppendLine( w.FireMode );
			var meleeCrit = ThornsWeaponDefinitions.ResolveCriticalHitChance( w, cid ) * 100f;
			sb.Append( "Crit chance (body) · " )
				.Append( meleeCrit.ToString( "F1", inv ) )
				.AppendLine( "%" );
		}
		else
		{
			var pellets = Math.Max( 1, w.PelletCount );
			var dmgPerPellet = w.BaseDamage * dmgMul;
			if ( pellets <= 1 )
				sb.Append( "Damage · " ).AppendLine( dmgPerPellet.ToString( "F1", inv ) );
			else
			{
				var volley = dmgPerPellet * pellets;
				sb.Append( "Damage · " )
					.Append( dmgPerPellet.ToString( "F1", inv ) )
					.Append( " per pellet × " )
					.Append( pellets )
					.Append( " · volley ~" )
					.AppendLine( volley.ToString( "F0", inv ) );
				sb.Append( "Pellet spread · ±" )
					.AppendLine( w.PelletSpreadHalfAngleDegrees.ToString( "F1", inv ) + "° half-angle" );
			}

			var interval = Math.Max( 0.0001f, w.FireIntervalSeconds / frMul );
			var sps = 1f / interval;
			var rpm = 60f / interval;
			sb.Append( "Fire rate · " )
				.Append( sps.ToString( "F2", inv ) )
				.Append( "/s · " )
				.Append( rpm.ToString( "F0", inv ) )
				.AppendLine( " RPM" );
			sb.Append( "Fire mode · " ).AppendLine( w.FireMode );

			if ( w.ClipSize > 0 )
			{
				sb.Append( "Magazine · " )
					.Append( net.WeaponLoadedAmmo )
					.Append( " / " )
					.AppendLine( w.ClipSize.ToString( inv ) );
			}
			else
				sb.AppendLine( "Magazine · —" );

			if ( ThornsWeaponDefinitions.UsesPerShellReloadCycle( w, cid ) )
			{
				sb.Append( "Reload · " )
					.Append( w.ReloadTimeSeconds.ToString( "F2", inv ) )
					.AppendLine( "s per shell (tube)" );
			}
			else
			{
				sb.Append( "Reload · " )
					.Append( w.ReloadTimeSeconds.ToString( "F2", inv ) )
					.AppendLine( "s" );
			}

			sb.Append( "Hitscan range · " ).AppendLine( w.MaxRange.ToString( "F0", inv ) );
			var hipBloom = w.BloomHalfAngleDegreesBase;
			var adsBloom = hipBloom * w.AdsBloomMul;
			sb.Append( "Spread (bloom) · hip " )
				.Append( hipBloom.ToString( "F3", inv ) )
				.Append( "° · ADS " )
				.AppendLine( adsBloom.ToString( "F3", inv ) + "°" );
			sb.Append( "Headshot · ×" ).AppendLine( w.HeadshotMultiplier.ToString( "F2", inv ) );
			var critPct = ThornsWeaponDefinitions.ResolveCriticalHitChance( w, cid ) * 100f;
			sb.Append( "Crit chance (body) · " )
				.Append( critPct.ToString( "F1", inv ) )
				.AppendLine( "%" );

			var ammoId = string.IsNullOrEmpty( w.AmmoTypeId ) ? itemDef.AmmoTypeId : w.AmmoTypeId;
			if ( !string.IsNullOrWhiteSpace( ammoId ) )
				sb.Append( "Ammo type · " ).AppendLine( ammoId );
		}

		if ( net.HasDurability != 0 )
		{
			sb.Append( "Durability · " )
				.Append( net.Durability.ToString( "F0", inv ) )
				.Append( " / " )
				.AppendLine( w.MaxDurability.ToString( "F0", inv ) );
		}

		sb.Append( "Wear · " )
			.Append( w.DurabilityLossPerShot.ToString( "F2", inv ) )
			.AppendLine( " per shot" );
		sb.Append( "Recoil scale · " ).AppendLine( w.RecoilPatternScaleDegrees.ToString( "F3", inv ) + "° (authoring step)" );

		return sb.ToString().TrimEnd();
	}

	/// <summary>One row for the inspect stat bar grid (label + fill + value).</summary>
	public readonly record struct ThornsWeaponInspectBarRow( string StatKey, string ValueText, float Fill01 );

	public static string GetWeaponInspectArchetypeLabel( ThornsWeaponDefinitions.WeaponDefinition w, string combatId )
	{
		if ( w is null )
			return "Weapon";
		combatId = combatId?.Trim() ?? "";
		if ( ThornsWeaponDefinitions.TreatsAsMeleeWeapon( w, combatId ) )
			return "Melee";
		if ( w.PelletCount > 1 || string.Equals( combatId, "shotgun", StringComparison.OrdinalIgnoreCase ) )
			return "Shotgun";
		if ( string.Equals( combatId, "sniper", StringComparison.OrdinalIgnoreCase ) || w.MaxRange >= 15000f )
			return "Sniper Rifle";
		if ( w.FireIntervalSeconds <= 0.085f && w.ClipSize >= 25 )
			return "SMG";
		if ( string.Equals( w.FireMode, "auto", StringComparison.OrdinalIgnoreCase ) )
			return "Assault Rifle";
		if ( string.Equals( w.FireMode, "semi", StringComparison.OrdinalIgnoreCase ) )
			return "Marksman Rifle";
		return "Rifle";
	}

	public static ThornsWeaponInspectBarRow[] BuildWeaponInspectBarRows(
		ThornsItemRegistry.ThornsItemDefinition itemDef,
		ThornsInventorySlotNet net )
	{
		var cid = string.IsNullOrEmpty( itemDef.CombatWeaponDefinitionId )
			? net.ItemId
			: itemDef.CombatWeaponDefinitionId;
		cid = cid?.Trim() ?? "";
		var w = ThornsWeaponDefinitions.Get( cid );
		ResolveWeaponRoll( net, out _, out var dmgMul, out var frMul );
		dmgMul = Math.Clamp( dmgMul, 0.5f, 2f );
		frMul = Math.Clamp( frMul, 0.5f, 2f );
		var inv = CultureInfo.InvariantCulture;

		if ( ThornsWeaponDefinitions.TreatsAsMeleeWeapon( w, cid ) )
		{
			var stab = w.BaseDamage * dmgMul;
			var aps = w.FireIntervalSeconds > 0.0001f ? 1f / w.FireIntervalSeconds : 0f;
			return
			[
				new ThornsWeaponInspectBarRow( "DAMAGE", stab.ToString( "F0", inv ), Math.Clamp( stab / 120f, 0.04f, 1f ) ),
				new ThornsWeaponInspectBarRow( "ATTACK RATE", $"{aps:F1}/s", Math.Clamp( aps / 8f, 0.04f, 1f ) ),
				new ThornsWeaponInspectBarRow( "REACH", $"{w.MaxRange:F0}", Math.Clamp( w.MaxRange / 200f, 0.04f, 1f ) ),
				new ThornsWeaponInspectBarRow( "HEAVY", w.SecondaryAttackBaseDamage > 0.01f
					? ( w.SecondaryAttackBaseDamage * dmgMul ).ToString( "F0", inv )
					: "—", w.SecondaryAttackBaseDamage > 0.01f ? Math.Clamp( w.SecondaryAttackBaseDamage * dmgMul / 150f, 0.04f, 1f ) : 0.08f ),
				new ThornsWeaponInspectBarRow( "STABILITY", "—", 0.35f )
			];
		}

		var pellets = Math.Max( 1, w.PelletCount );
		var dmgPer = w.BaseDamage * dmgMul;
		var dmgDisplay = pellets <= 1
			? dmgPer.ToString( "F0", inv )
			: $"{dmgPer:F0}×{pellets}";
		var dmgFill = pellets <= 1
			? Math.Clamp( dmgPer / 100f, 0.04f, 1f )
			: Math.Clamp( dmgPer * pellets / 220f, 0.04f, 1f );

		var interval = Math.Max( 0.0001f, w.FireIntervalSeconds / frMul );
		var rpm = 60f / interval;
		var rpmFill = Math.Clamp( rpm / 900f, 0.04f, 1f );

		var rangeFill = Math.Clamp( w.MaxRange / 22000f, 0.04f, 1f );
		var rangeDisplay = w.MaxRange >= 1000f ? $"{w.MaxRange / 1000f:F1}k" : $"{w.MaxRange:F0}";

		var hip = w.BloomHalfAngleDegreesBase;
		var accScore = 1f - Math.Clamp( hip / 2.2f, 0f, 1f );
		var accDisplay = $"{(int)(accScore * 100f)}";

		var recScore = 1f - Math.Clamp( w.RecoilPatternScaleDegrees / 1.35f, 0f, 1f );
		var recDisplay = w.RecoilPatternScaleDegrees.ToString( "F2", inv );

		return
		[
			new ThornsWeaponInspectBarRow( "DAMAGE", dmgDisplay, dmgFill ),
			new ThornsWeaponInspectBarRow( "FIRE RATE", $"{rpm:F0}", rpmFill ),
			new ThornsWeaponInspectBarRow( "RANGE", rangeDisplay, rangeFill ),
			new ThornsWeaponInspectBarRow( "ACCURACY", accDisplay, Math.Clamp( accScore, 0.04f, 1f ) ),
			new ThornsWeaponInspectBarRow( "RECOIL", recDisplay, Math.Clamp( recScore, 0.04f, 1f ) )
		];
	}
}
