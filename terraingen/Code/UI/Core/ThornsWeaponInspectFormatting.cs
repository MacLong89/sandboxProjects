using System.Globalization;
using Terraingen.Combat;
using Terraingen.Combat.Attachments;
using Terraingen.GameData;
using Terraingen.Player;

namespace Terraingen.UI.Core;

/// <summary>Inspect panel rows for weapons/tools (tier + combat tuning from <see cref="ThornsWeaponDefinitions"/>).</summary>
public static class ThornsWeaponInspectFormatting
{
	public readonly record struct InspectStatRow( string Label, string Value, bool Emphasize = false );
	public readonly record struct WeaponInspectBarRow( string StatKey, string ValueText, float Fill01 );

	public static IReadOnlyList<InspectStatRow> BuildWeaponStatRows(
		ThornsItemDefinition def,
		ThornsInventorySlotDto slot,
		in ThornsItemStack stack = default )
	{
		if ( def is null || slot is null || string.IsNullOrEmpty( slot.ItemId ) )
			return Array.Empty<InspectStatRow>();

		var inv = CultureInfo.InvariantCulture;
		var combatId = ThornsInventoryWeaponState.ResolveCombatId( def, slot.ItemId );
		var w = ThornsWeaponDefinitions.Get( combatId );
		var resolvedStack = stack.IsEmpty ? BuildStackFromDto( slot ) : stack;
		var effective = ThornsWeaponEffectiveStats.Resolve( w, combatId, resolvedStack );
		var tier = ThornsItemTier.ResolveTier( resolvedStack, def );
		var statMul = ThornsItemTier.ResolveStatMultiplier( resolvedStack, def );
		var rows = new List<InspectStatRow>( 12 )
		{
			new( "Tier", ThornsWeaponTierVisuals.TierDisplayName( tier ), Emphasize: true )
		};

		var scaledDamage = w.BaseDamage * statMul;

		if ( ThornsWeaponDefinitions.TreatsAsMeleeWeapon( w, combatId ) )
		{
			rows.Add( new InspectStatRow( "Damage", scaledDamage.ToString( "F1", inv ) ) );
			rows.Add( new InspectStatRow( "Swing speed", w.FireIntervalSeconds.ToString( "F2", inv ) + " s" ) );
			rows.Add( new InspectStatRow( "Reach", w.MaxRange.ToString( "F0", inv ) ) );
		}
		else
		{
			var rpm = 60f / Math.Max( 0.0001f, w.FireIntervalSeconds );
			rows.Add( new InspectStatRow( "Damage", scaledDamage.ToString( "F1", inv ) ) );
			rows.Add( new InspectStatRow( "Fire rate", rpm.ToString( "F0", inv ) + " RPM" ) );
			rows.Add( new InspectStatRow( "Fire mode", w.FireMode ) );
			if ( effective.ClipSize > 0 )
			{
				rows.Add( new InspectStatRow( "Magazine", $"{slot.WeaponLoadedAmmo} / {effective.ClipSize}" ) );
				rows.Add( new InspectStatRow( "Reserve", slot.AmmoReserve.ToString( inv ) ) );
			}

			rows.Add( new InspectStatRow( "Reload", w.ReloadTimeSeconds.ToString( "F2", inv ) + " s" ) );
			rows.Add( new InspectStatRow( "Range", w.MaxRange.ToString( "F0", inv ) ) );
		}

		if ( slot.HasDurability && w.MaxDurability > 0.001f )
		{
			var maxDur = w.MaxDurability * statMul;
			rows.Add( new InspectStatRow( "Durability", $"{slot.Durability:F0} / {maxDur:F0}" ) );
		}

		if ( slot.WeaponBroken )
			rows.Add( new InspectStatRow( "Condition", "BROKEN", Emphasize: true ) );

		return rows;
	}

	public static IReadOnlyList<WeaponInspectBarRow> BuildWeaponInspectBarRows(
		ThornsItemDefinition def,
		ThornsInventorySlotDto slot,
		in ThornsItemStack stack = default )
	{
		if ( def is null || slot is null || string.IsNullOrEmpty( slot.ItemId ) )
			return Array.Empty<WeaponInspectBarRow>();

		var inv = CultureInfo.InvariantCulture;
		var combatId = ThornsInventoryWeaponState.ResolveCombatId( def, slot.ItemId );
		var w = ThornsWeaponDefinitions.Get( combatId );
		var resolvedStack = stack.IsEmpty ? BuildStackFromDto( slot ) : stack;
		var effective = ThornsWeaponEffectiveStats.Resolve( w, combatId, resolvedStack );
		var statMul = ThornsItemTier.ResolveStatMultiplier( resolvedStack, def );

		if ( ThornsWeaponDefinitions.TreatsAsMeleeWeapon( w, combatId ) )
		{
			var stab = w.BaseDamage * statMul;
			var aps = w.FireIntervalSeconds > 0.0001f ? 1f / w.FireIntervalSeconds : 0f;
			return
			[
				new WeaponInspectBarRow( "DAMAGE", stab.ToString( "F0", inv ), Math.Clamp( stab / 120f, 0.04f, 1f ) ),
				new WeaponInspectBarRow( "ATTACK RATE", $"{aps:F1}/s", Math.Clamp( aps / 8f, 0.04f, 1f ) ),
				new WeaponInspectBarRow( "REACH", $"{w.MaxRange:F0}", Math.Clamp( w.MaxRange / 200f, 0.04f, 1f ) ),
				new WeaponInspectBarRow( "STABILITY", "—", 0.35f )
			];
		}

		var pellets = Math.Max( 1, w.PelletCount );
		var dmgPer = w.BaseDamage * statMul;
		var dmgDisplay = pellets <= 1 ? dmgPer.ToString( "F0", inv ) : $"{dmgPer:F0}×{pellets}";
		var dmgFill = pellets <= 1
			? Math.Clamp( dmgPer / 100f, 0.04f, 1f )
			: Math.Clamp( dmgPer * pellets / 220f, 0.04f, 1f );

		var interval = Math.Max( 0.0001f, w.FireIntervalSeconds );
		var rpm = 60f / interval;
		var rpmFill = Math.Clamp( rpm / 900f, 0.04f, 1f );

		var rangeFill = Math.Clamp( w.MaxRange / 22000f, 0.04f, 1f );
		var rangeDisplay = w.MaxRange >= 1000f ? $"{w.MaxRange / 1000f:F1}k" : $"{w.MaxRange:F0}";

		var hipBloom = effective.BloomHalfAngleDegrees;
		var accScore = 1f - Math.Clamp( hipBloom / 2.2f, 0f, 1f );
		var accDisplay = $"{(int)(accScore * 100f)}";

		var recScore = 1f - Math.Clamp( w.RecoilPatternScaleDegrees * effective.RecoilKickMultiplier / 1.35f, 0f, 1f );
		var recDisplay = $"{(int)(recScore * 100f)}";

		var magFill = effective.ClipSize > 0
			? Math.Clamp( effective.ClipSize / 60f, 0.04f, 1f )
			: 0.08f;
		var magDisplay = effective.ClipSize > 0 ? $"{effective.ClipSize}" : "—";

		var reloadScore = 1f - Math.Clamp( w.ReloadTimeSeconds / 4f, 0f, 1f );
		var ergScore = (accScore + recScore + reloadScore) / 3f;
		var ergDisplay = $"{(int)(ergScore * 100f)}";
		var maxDur = w.MaxDurability * statMul;

		return
		[
			new WeaponInspectBarRow( "DAMAGE", dmgDisplay, dmgFill ),
			new WeaponInspectBarRow( "RATE OF FIRE", $"{rpm:F0} RPM", rpmFill ),
			new WeaponInspectBarRow( "ACCURACY", accDisplay, Math.Clamp( accScore, 0.04f, 1f ) ),
			new WeaponInspectBarRow( "RECOIL CTL", recDisplay, Math.Clamp( recScore, 0.04f, 1f ) ),
			new WeaponInspectBarRow( "ERGONOMICS", ergDisplay, Math.Clamp( ergScore, 0.04f, 1f ) ),
			new WeaponInspectBarRow( "MAG SIZE", magDisplay, magFill ),
			new WeaponInspectBarRow( "DURABILITY", slot.HasDurability && w.MaxDurability > 0.001f
				? $"{slot.Durability:F0}/{maxDur:F0}"
				: "—",
				slot.HasDurability && w.MaxDurability > 0.001f
					? Math.Clamp( slot.Durability / maxDur, 0.04f, 1f )
					: 0.08f )
		];
	}

	static ThornsItemStack BuildStackFromDto( ThornsInventorySlotDto slot ) =>
		new()
		{
			ItemId = slot.ItemId,
			Count = slot.Count,
			HasDurability = slot.HasDurability,
			Durability = slot.Durability,
			WeaponLoadedAmmo = slot.WeaponLoadedAmmo,
			ItemTier = slot.ItemTier > 0 ? slot.ItemTier : slot.WeaponTier,
			StatRoll = slot.StatRoll,
			AttachmentId0 = slot.WeaponAttachmentIds?.Count > 0 ? slot.WeaponAttachmentIds[0] : "",
			AttachmentId1 = slot.WeaponAttachmentIds?.Count > 1 ? slot.WeaponAttachmentIds[1] : "",
			AttachmentId2 = slot.WeaponAttachmentIds?.Count > 2 ? slot.WeaponAttachmentIds[2] : ""
		};
}
