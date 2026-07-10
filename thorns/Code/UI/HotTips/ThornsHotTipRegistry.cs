using System;
using System.Collections.Generic;

namespace Sandbox;

public static class ThornsHotTipRegistry
{
	public const float DefaultGlobalCooldownSeconds = 14f;

	static readonly ThornsHotTipRule[] Rules =
	[
		Rule( ThornsHotTipIds.LowHealthBandage,
			"Bandages can stop bleeding — keep them on your hotbar.",
			ThornsHotTipCategory.Survival, 95, 5.2f,
			c => !c.SuppressCombatTips && c.Health01 > 0.02f && c.Health01 < 0.38f ),

		Rule( ThornsHotTipIds.LowThirst,
			"Water drains faster than hunger. Sip before you hike.",
			ThornsHotTipCategory.Survival, 88, 5f,
			c => c.Thirst01 < 0.28f ),

		Rule( ThornsHotTipIds.LowHunger,
			"Low hunger reduces regeneration. Eat from your hotbar.",
			ThornsHotTipCategory.Survival, 86, 5f,
			c => c.Hunger01 < 0.28f ),

		Rule( ThornsHotTipIds.AirdropContest,
			"Airdrops attract players and bandits.",
			ThornsHotTipCategory.Exploration, 82, 5.5f,
			c => c.NearAirdropCrate ),

		Rule( ThornsHotTipIds.HoldETame,
			$"Hold {ThornsHotTipKeys.Use} to tame.",
			ThornsHotTipCategory.Taming, 80, 4.5f,
			condition: c => c.LookTameWildlife.IsValid()
			                && ThornsWildlifeTamingRules.TryGetWildlifeHealthFraction( c.LookTameWildlife, out var hp )
			                && hp <= ThornsWildlifeTamingRules.GetTamingThresholdForPawnRoot( c.PawnRoot ) + 0.02f,
			minLook: 0.5f ),

		Rule( ThornsHotTipIds.HighRiskLoot,
			"High-tier loot means higher risk.",
			ThornsHotTipCategory.Exploration, 78, 5f,
			c => c.NearMilitaryCrate ),

		Rule( ThornsHotTipIds.StashRareLoot,
			"Store rare items in a Chest before exploring.",
			ThornsHotTipCategory.Building, 72, 5.2f,
			c => c.CarryingRareLoot && c.InventoryFill01 > 0.45f ),

		Rule( ThornsHotTipIds.BuildShelterNight,
			"Build a shelter to protect your loot.",
			ThornsHotTipCategory.Building, 70, 5.5f,
			c => c.IsNight && !c.FoundationPlaced ),

		Rule( ThornsHotTipIds.NightDanger,
			"Nights are more dangerous. Stay prepared.",
			ThornsHotTipCategory.Survival, 68, 5f,
			c => c.IsNight ),

		Rule( ThornsHotTipIds.InventoryNearlyFull,
			"Craft a Chest to store loot.",
			ThornsHotTipCategory.Loot, 66, 5f,
			c => c.InventoryFill01 >= 0.88f ),

		Rule( ThornsHotTipIds.LootCrateE,
			$"Press {ThornsHotTipKeys.Use} to loot containers.",
			ThornsHotTipCategory.Loot, 64, 4.8f,
			condition: c => c.LookLootCrate.IsValid(),
			minLook: 0.65f ),

		Rule( ThornsHotTipIds.WeakenBeforeTame,
			"Weaken animals before taming.",
			ThornsHotTipCategory.Taming, 62, 4.8f,
			condition: c => c.LookTameWildlife.IsValid()
			                && ThornsWildlifeTamingRules.TryGetWildlifeHealthFraction( c.LookTameWildlife, out var hp )
			                && hp > ThornsWildlifeTamingRules.GetTamingThresholdForPawnRoot( c.PawnRoot ) + 0.05f,
			minLook: 0.65f ),

		Rule( ThornsHotTipIds.NeedPickaxeOre,
			"You need a Pickaxe to mine ore.",
			ThornsHotTipCategory.Survival, 58, 5f,
			condition: c => !c.HasStonePick
			                && c.LookResourceNode.IsValid()
			                && c.LookResourceNode.ResourceKind == ThornsResourceKind.MetalOre,
			minLook: 0.7f ),

		Rule( ThornsHotTipIds.UpgradeStructures,
			$"Press {ThornsHotTipKeys.Build} → Upgrade to reinforce walls.",
			ThornsHotTipCategory.Building, 56, 5.5f,
			c => c.FoundationPlaced ),

		Rule( ThornsHotTipIds.OpenBuildMenu,
			$"Press {ThornsHotTipKeys.Build} to open the Build Menu.",
			ThornsHotTipCategory.Building, 54, 5f,
			c => c.HasBuildingMaterials && !c.FoundationPlaced ),

		Rule( ThornsHotTipIds.CraftStoneHatchet,
			$"You can now craft a Stone Hatchet — {ThornsHotTipKeys.Tab} → craft list.",
			ThornsHotTipCategory.Survival, 52, 5.2f,
			c => !c.HasStoneHatchet && c.WoodCount >= 14 && c.StoneCount >= 8 ),

		Rule( ThornsHotTipIds.OpenCraftingTab,
			$"Press {ThornsHotTipKeys.Tab} to open Crafting.",
			ThornsHotTipCategory.Survival, 50, 4.8f,
			c => c.WoodCount >= 10 && c.StoneCount >= 6 && !c.HasStoneHatchet ),

		Rule( ThornsHotTipIds.CampfireCook,
			"You need a Campfire to cook raw meat.",
			ThornsHotTipCategory.Survival, 48, 5f,
			c => !c.HasCampfirePlaced && c.WoodCount >= 20 ),

		Rule( ThornsHotTipIds.PickaxeStoneFaster,
			"Use a Pickaxe for faster stone harvesting.",
			ThornsHotTipCategory.Survival, 46, 4.8f,
			c => c.HasStonePick
			      && c.LookResourceNode.IsValid()
			      && c.LookResourceNode.ResourceKind == ThornsResourceKind.Stone ),

		Rule( ThornsHotTipIds.PunchRocks,
			"Punch rocks to gather stone.",
			ThornsHotTipCategory.Survival, 40, 4.5f,
			condition: c => c.LookResourceNode.IsValid() && c.LookResourceNode.ResourceKind == ThornsResourceKind.Stone,
			minLook: 0.75f,
			repeatable: true ),

		Rule( ThornsHotTipIds.PunchTrees,
			"Punch trees to gather wood.",
			ThornsHotTipCategory.Survival, 38, 4.5f,
			condition: c => c.LookResourceNode.IsValid() && c.LookResourceNode.ResourceKind == ThornsResourceKind.Wood,
			minLook: 0.75f,
			repeatable: true ),
	];

	public static IReadOnlyList<ThornsHotTipRule> AllRules => Rules;

	static readonly ThornsHotTipDefinition[] InstantOnly =
	[
		new(
			ThornsHotTipIds.EquipWeaponHotbar,
			"Equip weapons from your hotbar.",
			ThornsHotTipCategory.Combat,
			76,
			4.5f ),
		new(
			ThornsHotTipIds.ReloadR,
			$"Reload with {ThornsHotTipKeys.Reload}.",
			ThornsHotTipCategory.Combat,
			74,
			4.2f ),
	];

	public static bool TryGet( string id, out ThornsHotTipDefinition def )
	{
		def = default;
		if ( string.IsNullOrWhiteSpace( id ) )
			return false;

		foreach ( var r in Rules )
		{
			if ( string.Equals( r.Definition.Id, id, StringComparison.OrdinalIgnoreCase ) )
			{
				def = r.Definition;
				return true;
			}
		}

		foreach ( var d in InstantOnly )
		{
			if ( string.Equals( d.Id, id, StringComparison.OrdinalIgnoreCase ) )
			{
				def = d;
				return true;
			}
		}

		return false;
	}

	static ThornsHotTipRule Rule(
		string id,
		string message,
		ThornsHotTipCategory category,
		int priority,
		float duration,
		ThornsHotTipCondition condition,
		float minLook = 0f,
		bool repeatable = false ) =>
		new(
			new ThornsHotTipDefinition(
				id,
				message,
				category,
				priority,
				duration,
				repeatable,
				repeatable ? 180f : 0f,
				minLook ),
			condition );
}
