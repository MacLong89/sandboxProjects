using System;

namespace Sandbox;

/// <summary>
/// Journal goals in gameplay order (top = first after spawn). All goals track in parallel.
/// </summary>
public static class ThornsMilestoneDefinitions
{
	static string Hint( string category, string objective ) => $"{category} · {objective}";

	public static readonly ThornsMilestoneDefinition[] All =
	[
		new(
			Id: "goal_wood_gather",
			Title: "First Fuel",
			ShortHint: Hint( "Survival", "Punch trees — collect 15 wood." ),
			Description:
			"Spawn in and walk to a tree or stump. Left-click (Attack1) with empty hands to gather wood.\nNext: Hard Scraps.",
			Kind: ThornsMilestoneKind.Collect,
			TargetValue: 15,
			RewardXp: 40,
			CollectItemId: "wood" ),

		new(
			Id: "goal_stone_gather",
			Title: "Hard Scraps",
			ShortHint: Hint( "Survival", "Punch rock nodes — collect 15 stone." ),
			Description:
			"Find stone outcrops or rubble on the ground and punch them (Attack1). Stone is for your first tools and building — skip metal ore nodes for now.\nNext: Pack Check.",
			Kind: ThornsMilestoneKind.Collect,
			TargetValue: 15,
			RewardXp: 40,
			CollectItemId: "stone" ),

		new(
			Id: "goal_open_tab",
			Title: "Pack Check",
			ShortHint: Hint( "Survival", "Open your menu with [Tab]." ),
			Description:
			"Press [Tab] to open your inventory and crafting menu. You will craft tools from the list on the right.\nNext: Stone Pick.",
			Kind: ThornsMilestoneKind.Event,
			TargetValue: 1,
			RewardXp: 35,
			EventToken: ThornsMilestoneEventTokens.OpenTab ),

		new(
			Id: "goal_craft_pick",
			Title: "Stone Pick",
			ShortHint: Hint( "Crafting", "Craft a Stone Pick ([Tab])." ),
			Description:
			"With [Tab] open, craft a Stone Pick (wood + stone). Equip it on your hotbar (1–8) for faster stone and, later, ore.\nNext: Primitive Tools.",
			Kind: ThornsMilestoneKind.Craft,
			TargetValue: 1,
			RewardXp: 100,
			CraftRecipeId: "stone_pick" ),

		new(
			Id: "goal_craft_hatchet",
			Title: "Primitive Tools",
			ShortHint: Hint( "Crafting", "Craft a Stone Hatchet ([Tab])." ),
			Description:
			"Craft a Stone Hatchet from the same [Tab] crafting column. The hatchet chops wood much faster than punching trees.\nNext: First Shelter.",
			Kind: ThornsMilestoneKind.Craft,
			TargetValue: 1,
			RewardXp: 100,
			CraftRecipeId: "stone_hatchet" ),

		new(
			Id: "goal_first_foundation",
			Title: "First Shelter",
			ShortHint: Hint( "Building", "Press [B] — place a Foundation." ),
			Description:
			"Press [B] to open the Build menu. Select Floor (foundation), aim the ghost, and left-click (Attack1) to place your first piece.\nNext: Campfire.",
			Kind: ThornsMilestoneKind.Build,
			TargetValue: 1,
			RewardXp: 150,
			BuildStructureDefId: "wood_foundation" ),

		new(
			Id: "goal_craft_campfire_kit",
			Title: "Campfire",
			ShortHint: Hint( "Crafting", "Craft a Campfire ([Tab])." ),
			Description:
			"Press [Tab] and craft a Campfire. You will place it at your base for cooking and smelting.\nNext: Fire Pit.",
			Kind: ThornsMilestoneKind.Craft,
			TargetValue: 1,
			RewardXp: 90,
			CraftRecipeId: "campfire_kit" ),

		new(
			Id: "goal_place_campfire",
			Title: "Fire Pit",
			ShortHint: Hint( "Building", "Place a Campfire — hotbar + M1." ),
			Description:
			"Equip the Campfire on your hotbar and press M1 to place it near your foundation. Press [E] on the fire to add fuel, ore, and cooked output slots.\nNext: Bed.",
			Kind: ThornsMilestoneKind.Build,
			TargetValue: 1,
			RewardXp: 110,
			BuildStructureDefId: "campfire" ),

		new(
			Id: "goal_craft_bed_kit",
			Title: "Bed",
			ShortHint: Hint( "Crafting", "Craft a Bed ([Tab]) — wood + cloth." ),
			Description:
			"Press [Tab] and craft a Bed (40 wood, 10 cloth). Loot cloth from crates and buildings.\nNext: Set Spawn.",
			Kind: ThornsMilestoneKind.Craft,
			TargetValue: 1,
			RewardXp: 90,
			CraftRecipeId: "bed_kit" ),

		new(
			Id: "goal_place_bed",
			Title: "Set Spawn",
			ShortHint: Hint( "Building", "Place a Bed — hotbar + M1." ),
			Description:
			"Equip the Bed on your hotbar and press M1 inside your base. Your latest bed becomes your respawn point after death. Placing a new bed updates it.\nNext: Storage Chest.",
			Kind: ThornsMilestoneKind.Build,
			TargetValue: 1,
			RewardXp: 110,
			BuildStructureDefId: "bed" ),

		new(
			Id: "goal_craft_storage_kit",
			Title: "Storage Chest",
			ShortHint: Hint( "Crafting", "Craft a Storage Chest ([Tab])." ),
			Description:
			"Craft a Storage Chest from [Tab] so you can stash loot safely at base.\nNext: Secure Stash.",
			Kind: ThornsMilestoneKind.Craft,
			TargetValue: 1,
			RewardXp: 90,
			CraftRecipeId: "storage_chest_kit" ),

		new(
			Id: "goal_place_chest",
			Title: "Secure Stash",
			ShortHint: Hint( "Building", "Place a Chest — hotbar + M1." ),
			Description:
			"Equip the Storage Chest on your hotbar and press M1 to place it at your camp. Press [E] to move items between your inventory and the chest.\nNext: Scavenger.",
			Kind: ThornsMilestoneKind.Build,
			TargetValue: 1,
			RewardXp: 110,
			BuildStructureDefId: "storage_chest" ),

		new(
			Id: "goal_scavenger",
			Title: "Scavenger",
			ShortHint: Hint( "Exploration", "Loot a crate — aim and press [E]." ),
			Description:
			"Walk up to a world loot crate, look at it, and press [E] to take what's inside. Mark good routes on your minimap.\nNext: First Blood.",
			Kind: ThornsMilestoneKind.Event,
			TargetValue: 1,
			RewardXp: 75,
			EventToken: ThornsMilestoneEventTokens.LootWorldCrate ),

		new(
			Id: "goal_first_blood",
			Title: "First Blood",
			ShortHint: Hint( "Combat", "Kill 1 wild animal." ),
			Description:
			"Fight wildlife with tools or guns (Attack1). You need kills for hide and to weaken animals for taming.\nNext: New Companion.",
			Kind: ThornsMilestoneKind.Kill,
			TargetValue: 1,
			RewardXp: 100,
			KillFilter: ThornsMilestoneKillFilter.Wildlife ),

		new(
			Id: "goal_tame_creature",
			Title: "New Companion",
			ShortHint: Hint( "Taming", "≤10% HP — hold [E] to tame." ),
			Description:
			"Get an animal below 10% health, then aim at it and hold [E] until the tame bar completes. Check [Tab] → Tames to set Follow or Stay.\nNext: Supply Drop.",
			Kind: ThornsMilestoneKind.Tame,
			TargetValue: 1,
			RewardXp: 300 ),

		new(
			Id: "goal_supply_drop",
			Title: "Supply Drop",
			ShortHint: Hint( "Exploration", "Loot an airdrop — press [E]." ),
			Description:
			"Watch the minimap for a supply event. Reach the drop, deal with guards if needed, and hold [E] on the crate to loot it before others.\nNext: Bandit Hunter.",
			Kind: ThornsMilestoneKind.Event,
			TargetValue: 1,
			RewardXp: 500,
			EventToken: ThornsMilestoneEventTokens.LootAirdrop ),

		new(
			Id: "goal_kill_bandit_first",
			Title: "Bandit Hunter",
			ShortHint: Hint( "Combat", "Kill your first Bandit NPC." ),
			Description:
			"Human bandits patrol the map and guard drops. Close in, aim, and shoot (Attack1). They drop ammo and gear.\nNext: Walls Up.",
			Kind: ThornsMilestoneKind.Kill,
			TargetValue: 1,
			RewardXp: 400,
			KillFilter: ThornsMilestoneKillFilter.Bandit ),

		new(
			Id: "goal_walls_up",
			Title: "Walls Up",
			ShortHint: Hint( "Building", "Place 2 Walls ([B])." ),
			Description:
			"Press [B] and place two Wall pieces on your foundation. Walls block wildlife and cut bandit sight lines.\nNext: Framed Entry.",
			Kind: ThornsMilestoneKind.Build,
			TargetValue: 2,
			RewardXp: 120,
			BuildStructureDefId: "wood_wall" ),

		new(
			Id: "goal_door_frame",
			Title: "Framed Entry",
			ShortHint: Hint( "Building", "Place a Doorway ([B])." ),
			Description:
			"Place a Door frame on a wall edge so you can seal the base later. Use [Q]/[E] to rotate the ghost.\nNext: Full Pace.",
			Kind: ThornsMilestoneKind.Build,
			TargetValue: 1,
			RewardXp: 100,
			BuildStructureDefId: "wood_doorframe" ),

		new(
			Id: "goal_sprint_shift",
			Title: "Full Pace",
			ShortHint: Hint( "Survival", "Sprint with [Shift] while moving." ),
			Description:
			"Hold [Shift] while moving to sprint. Stamina drains — walk to recover on long trips.\nNext: Wood Stockpile.",
			Kind: ThornsMilestoneKind.Event,
			TargetValue: 1,
			RewardXp: 30,
			EventToken: ThornsMilestoneEventTokens.SprintShift ),

		new(
			Id: "goal_wood_stock",
			Title: "Wood Stockpile",
			ShortHint: Hint( "Survival", "Bank 35 wood total." ),
			Description:
			"Harvest with your hatchet until you hold 35 wood for walls, fuel, and crafts.\nNext: Stone Stockpile.",
			Kind: ThornsMilestoneKind.Collect,
			TargetValue: 35,
			RewardXp: 80,
			CollectItemId: "wood" ),

		new(
			Id: "goal_stone_stock",
			Title: "Stone Stockpile",
			ShortHint: Hint( "Survival", "Bank 35 stone total." ),
			Description:
			"Mine with your pick until you hold 35 stone for upgrades and kits.\nNext: Eat Something.",
			Kind: ThornsMilestoneKind.Collect,
			TargetValue: 35,
			RewardXp: 80,
			CollectItemId: "stone" ),

		new(
			Id: "goal_eat_food",
			Title: "Eat Something",
			ShortHint: Hint( "Survival", "Eat food from hotbar + [E]." ),
			Description:
			"Loot or find food, put it on your hotbar, and press [E] to eat. Hunger affects health recovery.\nNext: Stay Hydrated.",
			Kind: ThornsMilestoneKind.Event,
			TargetValue: 1,
			RewardXp: 60,
			EventToken: ThornsMilestoneEventTokens.ConsumeFood ),

		new(
			Id: "goal_drink_water",
			Title: "Stay Hydrated",
			ShortHint: Hint( "Survival", "Drink at water or use water items." ),
			Description:
			"Stand in open water and hold [E] to sip, or drink bottled water from your hotbar.\nNext: Cloth Line.",
			Kind: ThornsMilestoneKind.Event,
			TargetValue: 1,
			RewardXp: 60,
			EventToken: ThornsMilestoneEventTokens.DrinkOpenWater ),

		new(
			Id: "goal_cloth_fiber",
			Title: "Cloth Line",
			ShortHint: Hint( "Survival", "Collect 25 cloth." ),
			Description:
			"Loot cloth from crates and buildings. Cloth is used for bandages and leather.\nNext: Field Dressing.",
			Kind: ThornsMilestoneKind.Collect,
			TargetValue: 25,
			RewardXp: 95,
			CollectItemId: "cloth" ),

		new(
			Id: "goal_bandages",
			Title: "Field Dressing",
			ShortHint: Hint( "Survival", "Craft 2 Bandages ([Tab])." ),
			Description:
			"Craft two Bandages from [Tab]. Keep them on your hotbar and press [E] after fights to heal.\nNext: Ore Run.",
			Kind: ThornsMilestoneKind.Craft,
			TargetValue: 2,
			RewardXp: 100,
			CraftRecipeId: "bandage" ),

		new(
			Id: "goal_metal_ore",
			Title: "Ore Run",
			ShortHint: Hint( "Crafting", "Mine 28 metal ore (Stone Pick)." ),
			Description:
			"Equip your Stone Pick and mine glittering metal nodes. Ore feeds smelting and guns.\nNext: Ingots.",
			Kind: ThornsMilestoneKind.Collect,
			TargetValue: 28,
			RewardXp: 130,
			CollectItemId: "metal_ore" ),

		new(
			Id: "goal_smelt_metal",
			Title: "Ingots",
			ShortHint: Hint( "Crafting", "Smelt metal twice (Campfire or [Tab])." ),
			Description:
			"Smelt at your Campfire (fuel + ore) or craft Smelt Metal from [Tab]. Two batches complete this goal.\nNext: Wilderness Control.",
			Kind: ThornsMilestoneKind.Craft,
			TargetValue: 2,
			RewardXp: 140,
			CraftRecipeId: "smelt_metal" ),

		new(
			Id: "goal_kill_wildlife",
			Title: "Wilderness Control",
			ShortHint: Hint( "Combat", "Kill 8 wild animals." ),
			Description:
			"Clear predators around your camp. Only wildlife counts — not bandits or players.\nNext: Military Cache.",
			Kind: ThornsMilestoneKind.Kill,
			TargetValue: 8,
			RewardXp: 160,
			KillFilter: ThornsMilestoneKillFilter.Wildlife ),

		new(
			Id: "goal_military_cache",
			Title: "Military Cache",
			ShortHint: Hint( "Exploration", "Loot a military crate ([E])." ),
			Description:
			"Find military-tagged crates or POIs on the minimap. Press [E] to loot — high risk, high tier gear.\nNext: Clear Raiders.",
			Kind: ThornsMilestoneKind.Event,
			TargetValue: 1,
			RewardXp: 200,
			EventToken: ThornsMilestoneEventTokens.LootMilitaryCrate ),

		new(
			Id: "goal_kill_bandits",
			Title: "Clear Raiders",
			ShortHint: Hint( "Combat", "Kill 3 Bandit NPCs." ),
			Description:
			"Strip bandits from your routes. They guard airdrops and patrol roads.\nNext: Butcher's Cut.",
			Kind: ThornsMilestoneKind.Kill,
			TargetValue: 3,
			RewardXp: 240,
			KillFilter: ThornsMilestoneKillFilter.Bandit ),

		new(
			Id: "goal_hide",
			Title: "Butcher's Cut",
			ShortHint: Hint( "Survival", "Collect 8 animal hide." ),
			Description:
			"Hide drops from wildlife kills. You need it for leather crafts.\nNext: Tannery.",
			Kind: ThornsMilestoneKind.Collect,
			TargetValue: 8,
			RewardXp: 115,
			CollectItemId: "animal_hide" ),

		new(
			Id: "goal_leather_scrap",
			Title: "Tannery",
			ShortHint: Hint( "Crafting", "Craft Leather Scrap ×2 ([Tab])." ),
			Description:
			"Combine hide and cloth into Leather Scrap at [Tab] for armor and workbench recipes.\nNext: Scrap Plate.",
			Kind: ThornsMilestoneKind.Craft,
			TargetValue: 2,
			RewardXp: 125,
			CraftRecipeId: "leather_scrap" ),

		new(
			Id: "goal_scrap_armor",
			Title: "Scrap Plate",
			ShortHint: Hint( "Crafting", "Craft Scrap Chest armor ([Tab])." ),
			Description:
			"Craft scrap chest armor when you have metal and cloth. Equip from armor slots in [Tab].\nNext: Fortify.",
			Kind: ThornsMilestoneKind.Craft,
			TargetValue: 1,
			RewardXp: 150,
			CraftRecipeId: "scrap_chest" ),

		new(
			Id: "goal_fortify",
			Title: "Fortify",
			ShortHint: Hint( "Building", "Upgrade a structure ([B] → Upgrade)." ),
			Description:
			"Press [B], switch to Upgrade, and reinforce a wall or floor. Upgrades cost metal.\nNext: Body Armor.",
			Kind: ThornsMilestoneKind.Event,
			TargetValue: 1,
			RewardXp: 200,
			EventToken: ThornsMilestoneEventTokens.StructureUpgraded ),

		new(
			Id: "goal_kevlar_plate",
			Title: "Body Armor",
			ShortHint: Hint( "Crafting", "Craft Kevlar Chest ([Tab])." ),
			Description:
			"Craft Kevlar Chest when ingots and cloth allow. Stack with helmet and pants for serious fights.\nNext: Industry Bench.",
			Kind: ThornsMilestoneKind.Craft,
			TargetValue: 1,
			RewardXp: 250,
			CraftRecipeId: "kevlar_chest" ),

		new(
			Id: "goal_craft_workbench_kit",
			Title: "Industry Bench",
			ShortHint: Hint( "Crafting", "Craft a Workbench ([Tab])." ),
			Description:
			"Craft a Workbench for advanced repairs and recipes.\nNext: Bench Online.",
			Kind: ThornsMilestoneKind.Craft,
			TargetValue: 1,
			RewardXp: 120,
			CraftRecipeId: "workbench_kit" ),

		new(
			Id: "goal_place_workbench",
			Title: "Bench Online",
			ShortHint: Hint( "Building", "Place a Workbench." ),
			Description:
			"Place the workbench in your base. Press [E] to open it for metal, cloth, and leather crafts.\nNext: Locked & Loaded.",
			Kind: ThornsMilestoneKind.Build,
			TargetValue: 1,
			RewardXp: 130,
			BuildStructureDefId: "workbench" ),

		new(
			Id: "goal_rifle_ammo",
			Title: "Locked & Loaded",
			ShortHint: Hint( "Combat", "Craft rifle ammo ([Tab])." ),
			Description:
			"After you have a rifle, craft rifle ammo from [Tab] and reload with [R]. Store spare stacks in your chest.",
			Kind: ThornsMilestoneKind.Craft,
			TargetValue: 1,
			RewardXp: 80,
			CraftRecipeId: "rifle_ammo" ),
	];

	public static int Count => All.Length;

	public static bool TryGet( int sequentialIndex, out ThornsMilestoneDefinition def )
	{
		def = default;
		if ( sequentialIndex < 0 || sequentialIndex >= All.Length )
			return false;

		def = All[sequentialIndex];
		return true;
	}

	public static bool TryGetById( string id, out int index, out ThornsMilestoneDefinition def )
	{
		index = -1;
		def = default;
		if ( string.IsNullOrWhiteSpace( id ) )
			return false;

		for ( var i = 0; i < All.Length; i++ )
		{
			if ( string.Equals( All[i].Id, id, StringComparison.OrdinalIgnoreCase ) )
			{
				index = i;
				def = All[i];
				return true;
			}
		}

		return false;
	}
}

/// <param name="CollectItemId">For <see cref="ThornsMilestoneKind.Collect"/> — resource item id.</param>
/// <param name="BuildStructureDefId">Empty = any structure placement counts.</param>
/// <param name="CraftRecipeId">For <see cref="ThornsMilestoneKind.Craft"/> — recipe id from <see cref="ThornsCraftingRecipes"/>.</param>
/// <param name="EventToken">For <see cref="ThornsMilestoneKind.Event"/> — <see cref="ThornsMilestoneEventTokens"/>.</param>
public readonly struct ThornsMilestoneDefinition
{
	public string Id { get; init; }
	public string Title { get; init; }
	public string ShortHint { get; init; }
	public string Description { get; init; }
	public ThornsMilestoneKind Kind { get; init; }
	public int TargetValue { get; init; }
	public int RewardXp { get; init; }
	public string CollectItemId { get; init; }
	public string BuildStructureDefId { get; init; }
	public string CraftRecipeId { get; init; }
	public ThornsMilestoneKillFilter KillFilter { get; init; }
	public string EventToken { get; init; }

	public ThornsMilestoneDefinition(
		string Id,
		string Title,
		string ShortHint,
		string Description,
		ThornsMilestoneKind Kind,
		int TargetValue,
		int RewardXp,
		string CollectItemId = "",
		string BuildStructureDefId = "",
		string CraftRecipeId = "",
		ThornsMilestoneKillFilter KillFilter = ThornsMilestoneKillFilter.Bandit,
		string EventToken = "" )
	{
		this.Id = Id;
		this.Title = Title;
		this.ShortHint = ShortHint;
		this.Description = Description;
		this.Kind = Kind;
		this.TargetValue = TargetValue;
		this.RewardXp = RewardXp;
		this.CollectItemId = CollectItemId ?? "";
		this.BuildStructureDefId = BuildStructureDefId ?? "";
		this.CraftRecipeId = CraftRecipeId ?? "";
		this.KillFilter = KillFilter;
		this.EventToken = EventToken ?? "";
	}
}
