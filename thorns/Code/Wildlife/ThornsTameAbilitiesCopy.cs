using System;
using System.Collections.Generic;

namespace Sandbox;

/// <summary>Structured copy for the Tames → Abilities tab (attack line, rarity bullets, training bullets).</summary>
public static class ThornsTameAbilitiesCopy
{
	public static string AttackTypeLabel( ThornsWildlifeSpeciesDefinition def )
	{
		if ( def is null )
			return "—";
		if ( def.MeleeDamage > 0.01f )
			return "Melee";
		return "Assist (light bite)";
	}

	/// <summary>Shown under Tames → Abilities when <see cref="ThornsWildlifeSpeciesDefinition.AllowPlayerMount"/>.</summary>
	public const string MountHowToSectionTitle = "How to mount";

	public static void CollectMountHowToLines( ThornsWildlifeSpeciesDefinition def, List<string> lines )
	{
		if ( def is null || !def.AllowPlayerMount )
			return;

		lines.Add( "Tap Use (E) while your crosshair is on this tame to mount (works at range)." );
		lines.Add( "Dismount: crouch, or tap Use (E) on this tame again while riding (Jump hops while mounted)." );
	}

	public static void CollectRarityAbilityLines(
		ThornsWildlifeSpeciesDefinition def,
		ThornsWildlifeIdentity sel,
		List<string> lines )
	{
		if ( def is not null )
		{
			if ( def.IsPredator )
			{
				lines.Add( def.UseLineOfSight
					? "Hunter — pursues threats you damage; respects terrain line-of-sight."
					: "Hunter — pursues threats you damage; broad senses through brush." );
			}
			else
			{
				lines.Add( def.FearRadius > 1f
					? $"Skittish — keeps distance from danger (fear radius ~{def.FearRadius:F0}u)."
					: "Skittish — prefers to avoid fights unless cornered." );
			}
		}

		if ( sel is null || !sel.IsValid() )
			return;

		var tier = sel.TameQualityTier;
		if ( tier == ThornsLootRarity.Legendary && sel.TameLegendaryAbility != ThornsTameLegendaryAbility.None )
		{
			var leg = sel.TameLegendaryAbility;
			lines.Add( $"{ThornsTameLegendaryAbilityDefs.DisplayName( leg )} — {ThornsTameLegendaryAbilityDefs.Description( leg )}" );
		}
		else
		{
			lines.Add( tier == ThornsLootRarity.Legendary
				? "No extra legendary gift rolled on this tame."
				: "Unique legendary gifts only roll on Legendary-quality tames." );
		}

		var hp = sel.TameAffinityHpSync;
		var dmg = sel.TameAffinityDmgSync;
		var spd = sel.TameAffinitySpdSync;
		if ( hp > 0.001f || dmg > 0.001f || spd > 0.001f )
		{
			var bits = new List<string>( 3 );
			if ( hp > 0.001f )
				bits.Add( $"+{hp * 100f:F0}% health" );
			if ( dmg > 0.001f )
				bits.Add( $"+{dmg * 100f:F0}% damage" );
			if ( spd > 0.001f )
				bits.Add( $"+{spd * 100f:F0}% speed" );
			lines.Add( $"Bloodlines ({tier.DisplayName()} roll): {string.Join( " · ", bits )}." );
		}
		else
			lines.Add( $"Bloodlines: neutral roll at {tier.DisplayName()} — upgrades still grow power (STATS tab)." );
	}

	public static void CollectTrainingLines(
		ThornsWildlifeSpeciesDefinition def,
		ThornsWildlifeIdentity sel,
		List<string> lines )
	{
		if ( sel is null || !sel.IsValid() )
			return;

		var lv = sel.ComputeTameLevel();

		if ( lv >= 2 )
			lines.Add( "Trail memory — combat marks you open on prey last longer for your pack (level 2+)." );

		if ( lv >= 8 && sel.TameDmgUpgradeSteps >= 2 )
			lines.Add( "War chant — each allied tame within ~32m adds up to +3% bite damage (capped; level 8+ & damage training)." );

		AppendStepFlavor( lines, sel.TameHpUpgradeSteps, HpStepNames );
		AppendStepFlavor( lines, sel.TameDmgUpgradeSteps, DmgStepNames );
		AppendStepFlavor( lines, sel.TameSpdUpgradeSteps, SpdStepNames );

		if ( sel.TameSpdUpgradeSteps >= 2 )
			lines.Add( "Pack stride — when another of your tames is within ~28m, this one hurries slightly to keep formation." );
	}

	static readonly string[] HpStepNames =
	{
		"Thick pelt — each health training deepens hide and stamina.",
		"Iron ribs — second health training steadies the creature in brawls.",
		"Mountain blood — further health training pushes max vitality sharply."
	};

	static readonly string[] DmgStepNames =
	{
		"Sharpened fangs — damage training adds bite weight.",
		"Crushing hold — second damage training favors finishing strikes.",
		"Apex rhythm — third damage training syncs strikes with your shots."
	};

	static readonly string[] SpdStepNames =
	{
		"Long stride — speed training widens chase arcs.",
		"Wind trot — second speed training keeps pace on slopes.",
		"Cut-corner chase — third speed training tightens intercept lines."
	};

	static void AppendStepFlavor( List<string> lines, int steps, string[] names )
	{
		for ( var i = 0; i < steps && i < names.Length; i++ )
			lines.Add( names[i] );
		if ( steps > names.Length )
			lines.Add( $"Further training ×{steps - names.Length} — cumulative STATS bonuses stack." );
	}
}
