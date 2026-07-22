namespace Terraingen.GameData;

/// <summary>Survivor Journey milestones — chained via <see cref="ThornsJournalGoalDefinition.PrerequisiteGoalId"/>.</summary>
public static class ThornsMilestoneDefinitions
{
	/// <summary>Rebuilt each access so hot reload always picks up journey copy changes.</summary>
	public static IReadOnlyList<ThornsJournalGoalDefinition> All => BuildAll();

	static List<ThornsJournalGoalDefinition> BuildAll()
	{
		var order = 0;
		return new List<ThornsJournalGoalDefinition>
		{
			// —— Survival onboarding (auto-pinned until complete) ——
			// First fun: gather → tool → shelter. Drink/eat/menus are secondary tips, not the critical path.
			BareHandsGatherGoal( ref order, "goal_bare_hands_gather", "Make a Foothold", ThornsJourneyCategory.Survival,
				"Before I can shape tools, I need raw wood and stone — my fists will have to do.",
				"Punch trees and stone nodes with empty hands to gather wood and stone (LMB)",
				60, "", autoPin: true ),
			J( ref order, "goal_acquire_weapon", "Shape a Tool", ThornsJourneyCategory.Survival,
				"Animals and bandits roam these lands. A stone hatchet is a start.",
				"Craft a stone hatchet or equip a weapon", ThornsMilestoneType.Event, "acquire_weapon", 1, 80,
				prereq: "goal_bare_hands_gather", autoPin: true ),
			J( ref order, "goal_craft_bed_kit", "Rest", ThornsJourneyCategory.Survival,
				"A rolled bed is lighter than regret. I can claim a respawn point once it is built.",
				"Craft a bed", ThornsMilestoneType.Craft, "bed_kit", 1, 90,
				prereq: "goal_acquire_weapon", autoPin: true ),
			J( ref order, "goal_place_bed", "No Refuge", ThornsJourneyCategory.Survival,
				"If I die now, everything starts over. I need somewhere safe to return to.",
				"Place a bed to set your respawn", ThornsMilestoneType.Build, "bed", 1, 110,
				prereq: "goal_craft_bed_kit", autoPin: true ),
			J( ref order, "goal_drink_water", "Parched", ThornsJourneyCategory.Survival,
				"My throat is dry. I still have a canteen — or I can drink from any open water.",
				"Hold RMB on water to drink", ThornsMilestoneType.Event, "drink", 1, 40,
				prereq: "goal_place_bed", autoPin: true ),
			J( ref order, "goal_eat_food", "Empty Stomach", ThornsJourneyCategory.Survival,
				"Hunger gnaws at me. I should eat before I push deeper into the wilds.",
				"Hold RMB on food to eat", ThornsMilestoneType.Event, "eat_food", 1, 40,
				prereq: "goal_drink_water", autoPin: true ),
			ControlGoal( ref order, "goal_explore_controls", "Learn the Ropes", ThornsJourneyCategory.Survival,
				"I should learn where everything is before the wilds swallow me.",
				"Open each menu shortcut once to finish this goal",
				40, "goal_eat_food", autoPin: false ),
			J( ref order, "goal_visit_town", "Signs of Life", ThornsJourneyCategory.Survival,
				"Smoke on the horizon — there may be supplies, shelter, or danger. I should see what remains of civilization.",
				"Reach a town center", ThornsMilestoneType.Event, "visit_town", 1, 90,
				prereq: "goal_place_bed", autoPin: true ),
			J( ref order, "goal_first_blood", "Predator or Prey", ThornsJourneyCategory.Survival,
				"The wilds answer hunger with teeth. I will not be the only one hunting out here.",
				"Kill a wild animal", ThornsMilestoneType.Kill, "wildlife", 1, 100,
				prereq: "goal_visit_town", autoPin: true ),
			J( ref order, "goal_craft_workbench_kit", "Proper Tools", ThornsJourneyCategory.Survival,
				"Scrap and stone are not enough forever. A workbench would let me build like I mean to stay.",
				"Craft a workbench", ThornsMilestoneType.Craft, "workbench_kit", 1, 120,
				prereq: "goal_first_blood", autoPin: true ),
			J( ref order, "goal_place_workbench", "A Place to Build", ThornsJourneyCategory.Survival,
				"With a bench set down, I can finally shape this place into something that feels like mine.",
				"Place a workbench", ThornsMilestoneType.Build, "workbench", 1, 130,
				prereq: "goal_craft_workbench_kit", autoPin: true ),
			J( ref order, "goal_radio_shop", "Field Supply", ThornsJourneyCategory.Exploration,
				"Some towns still have a radio table — survivors trade metal for gear there.",
				"Open a radio shop in town", ThornsMilestoneType.Event, "open_radio_shop", 1, 80,
				prereq: "goal_visit_town", autoPin: true ),
			J( ref order, "goal_discover_guild_outpost", "Rival Banner", ThornsJourneyCategory.World,
				"NPC guilds claim outposts across the wastes. I should find one before they find me.",
				"Discover a guild outpost", ThornsMilestoneType.Event, "discover_guild_outpost", 1, 100,
				prereq: "goal_visit_town", autoPin: true ),

			// —— Exploration ——
			J( ref order, "goal_scavenger", "Left Behind", ThornsJourneyCategory.Exploration,
				"Crates and rubble still hold things worth taking — if something else has not claimed them first.",
				"Loot a world crate", ThornsMilestoneType.Event, "loot_crate", 1, 75,
				prereq: "goal_place_workbench" ),
			J( ref order, "goal_supply_drop", "From the Sky", ThornsJourneyCategory.Exploration,
				"That flare and crate — someone wanted survivors to fight over good gear. Watch the sky and map.",
				"Loot an airdrop", ThornsMilestoneType.Event, "loot_airdrop", 1, 500,
				prereq: "goal_scavenger" ),
			J( ref order, "goal_military_cache", "Military Cache", ThornsJourneyCategory.World,
				"Olive drab boxes do not appear by accident. Military leftovers could turn the tide.",
				"Loot a military crate", ThornsMilestoneType.Event, "loot_military", 1, 200,
				prereq: "goal_supply_drop" ),

			// —— Building & crafting ——
			J( ref order, "goal_wood_gather", "First Fuel", ThornsJourneyCategory.Building,
				"Every fire and frame starts with wood. The forest is generous if I am willing to work.",
				"Gather wood", ThornsMilestoneType.Collect, "wood", 15, 40,
				prereq: "goal_place_workbench" ),
			J( ref order, "goal_stone_gather", "Hard Scraps", ThornsJourneyCategory.Building,
				"Stone does not bend, but it endures. I will need a pile before I trust any wall.",
				"Gather stone", ThornsMilestoneType.Collect, "stone", 15, 40,
				prereq: "goal_wood_gather" ),
			J( ref order, "goal_craft_pick", "Break Ground", ThornsJourneyCategory.Building,
				"Roots and rock hide what I need underneath. A pickaxe will open the earth.",
				"Craft a stone pickaxe", ThornsMilestoneType.Craft, "stone_pickaxe", 1, 100,
				prereq: "goal_stone_gather" ),
			J( ref order, "goal_craft_hatchet", "Cleaving Wood", ThornsJourneyCategory.Building,
				"A hatchet turns trees into timber faster than bare hands ever could.",
				"Craft a stone hatchet", ThornsMilestoneType.Craft, "stone_hatchet", 1, 100,
				prereq: "goal_craft_pick" ),
			J( ref order, "goal_first_foundation", "First Shelter", ThornsJourneyCategory.Building,
				"Flat ground and a foundation — the first honest line between me and the weather.",
				"Place a wood foundation", ThornsMilestoneType.Build, "wood_foundation", 1, 150,
				prereq: "goal_craft_hatchet" ),
			J( ref order, "goal_craft_campfire_kit", "Warmth", ThornsJourneyCategory.Building,
				"Cold nights kill quietly. I should be able to spark a fire wherever I stop.",
				"Craft a campfire", ThornsMilestoneType.Craft, "campfire_kit", 1, 90,
				prereq: "goal_first_foundation" ),
			J( ref order, "goal_place_campfire", "Fire Pit", ThornsJourneyCategory.Building,
				"Flame means cooked food, light, and a reason for things in the dark to keep their distance.",
				"Place a campfire", ThornsMilestoneType.Build, "campfire", 1, 110,
				prereq: "goal_craft_campfire_kit" ),
			J( ref order, "goal_craft_storage_kit", "Stash Plans", ThornsJourneyCategory.Building,
				"I cannot carry everything. A chest would keep spoils safe while I am out scavenging.",
				"Craft a storage chest", ThornsMilestoneType.Craft, "storage_chest_kit", 1, 90,
				prereq: "goal_place_campfire" ),
			J( ref order, "goal_place_chest", "Secure Stash", ThornsJourneyCategory.Building,
				"Supplies off my back and behind a lid — that is how bases survive raids and rust.",
				"Place a storage chest", ThornsMilestoneType.Build, "storage_chest", 1, 110,
				prereq: "goal_craft_storage_kit" ),
			J( ref order, "goal_walls_up", "Walls Up", ThornsJourneyCategory.Building,
				"Walls turn a camp into something I can defend. Height buys time.",
				"Place wood walls", ThornsMilestoneType.Build, "wood_wall", 2, 120,
				prereq: "goal_first_foundation" ),
			J( ref order, "goal_door_frame", "Framed Entry", ThornsJourneyCategory.Building,
				"A doorway is control — who enters, and on whose terms.",
				"Place a door frame", ThornsMilestoneType.Build, "wood_doorframe", 1, 100,
				prereq: "goal_walls_up" ),
			J( ref order, "goal_fortify", "Hardened", ThornsJourneyCategory.World,
				"What I built can be made tougher. Reinforcement is cheaper than rebuilding from ash.",
				"Upgrade a structure", ThornsMilestoneType.Event, "fortify", 1, 200,
				prereq: "goal_door_frame" ),
			H( ref order, "goal_wood_stock", "Wood Stockpile", ThornsJourneyCategory.Building,
				"A real base eats timber. I should keep a reserve before the next project.",
				"Hold 35 wood in inventory", "wood", 35, 80,
				prereq: "goal_wood_gather" ),
			H( ref order, "goal_stone_stock", "Stone Stockpile", ThornsJourneyCategory.Building,
				"Stone piles slow raids and fast builds alike. Fill a stock before I need it urgently.",
				"Hold 35 stone in inventory", "stone", 35, 80,
				prereq: "goal_stone_gather" ),
			J( ref order, "goal_cloth_fiber", "Cloth Line", ThornsJourneyCategory.Building,
				"Cloth ties wounds, pads armor, and stitches kits together.",
				"Gather cloth", ThornsMilestoneType.Collect, "cloth", 25, 95,
				prereq: "goal_place_workbench" ),
			J( ref order, "goal_bandages", "Field Dressing", ThornsJourneyCategory.Building,
				"Bleeding out is stupid when bandages are cheap to make.",
				"Craft bandages", ThornsMilestoneType.Craft, "bandage", 2, 100,
				prereq: "goal_cloth_fiber" ),
			J( ref order, "goal_metal_ore", "Ore Run", ThornsJourneyCategory.Building,
				"Metal in the ground is future plates and bullets. I should haul ore while I can.",
				"Gather metal ore", ThornsMilestoneType.Collect, "metal_ore", 28, 130,
				prereq: "goal_craft_pick" ),
			J( ref order, "goal_smelt_metal", "Ingots", ThornsJourneyCategory.Building,
				"Raw ore is weight without purpose until it is smelted into something useful.",
				"Smelt metal ingots at a campfire", ThornsMilestoneType.Craft, "smelt_metal", 2, 140,
				prereq: "goal_metal_ore" ),
			J( ref order, "goal_hide", "Butcher's Cut", ThornsJourneyCategory.Building,
				"Hide from kills is leather waiting to happen. Waste nothing the wild gives me.",
				"Gather animal hide", ThornsMilestoneType.Collect, "animal_hide", 8, 115,
				prereq: "goal_first_blood" ),
			J( ref order, "goal_leather_scrap", "Tannery", ThornsJourneyCategory.Building,
				"Scrap leather is ugly, but it stitches into gear that beats bare skin.",
				"Craft leather scrap", ThornsMilestoneType.Craft, "leather_scrap", 2, 125,
				prereq: "goal_hide" ),
			J( ref order, "goal_scrap_armor", "Scrap Plate", ThornsJourneyCategory.Building,
				"Sheet metal on my chest beats faith when shots fly.",
				"Craft scrap chest armor", ThornsMilestoneType.Craft, "scrap_chest", 1, 150,
				prereq: "goal_smelt_metal" ),
			J( ref order, "goal_scrap_helmet", "Scrap Helm", ThornsJourneyCategory.Building,
				"A helmet from salvage beats a prayer when lead starts flying.",
				"Craft a scrap helmet", ThornsMilestoneType.Craft, "scrap_head", 1, 165,
				prereq: "goal_scrap_armor" ),
			J( ref order, "goal_scrap_legs", "Scrap Greaves", ThornsJourneyCategory.Building,
				"Leg guards from scrap — better than bare skin in a firefight.",
				"Craft scrap leg guards", ThornsMilestoneType.Craft, "scrap_legs", 1, 175,
				prereq: "goal_scrap_helmet" ),
			J( ref order, "goal_kevlar_plate", "Body Armor", ThornsJourneyCategory.Building,
				"Proper kevlar is the difference between a bruise and a grave.",
				"Craft kevlar chest armor", ThornsMilestoneType.Craft, "kevlar_chest", 1, 250,
				prereq: "goal_scrap_legs" ),
			J( ref order, "goal_rifle_ammo", "Locked & Loaded", ThornsJourneyCategory.Building,
				"A rifle is noise without rounds. I should keep ammunition crafting in mind.",
				"Craft rifle ammunition", ThornsMilestoneType.Craft, "rifle_ammo", 1, 80,
				prereq: "goal_military_cache" ),

			// —— Combat ——
			J( ref order, "goal_kill_wildlife", "Wilderness Control", ThornsJourneyCategory.Combat,
				"One kill proved I can. Clearing more would make the woods quieter around my camp.",
				"Kill wild animals", ThornsMilestoneType.Kill, "wildlife", 8, 160,
				prereq: "goal_first_blood" ),
			J( ref order, "goal_kill_bandit_first", "Bandit Hunter", ThornsJourneyCategory.Combat,
				"Raiders wear faces like mine and mean worse intentions. The first one falls today.",
				"Kill a bandit", ThornsMilestoneType.Kill, "bandit", 1, 400,
				prereq: "goal_kill_wildlife" ),
			J( ref order, "goal_kill_bandits", "Clear Raiders", ThornsJourneyCategory.Combat,
				"Bandits travel in packs. Finishing a few might buy my base a peaceful week.",
				"Kill bandits", ThornsMilestoneType.Kill, "bandit", 3, 240,
				prereq: "goal_kill_bandit_first" ),
			J( ref order, "goal_pvp_first", "Rival Down", ThornsJourneyCategory.Combat,
				"Out here, the deadliest predator wears a face like mine. One less rival.",
				"Eliminate another survivor", ThornsMilestoneType.Kill, "player", 1, 120,
				prereq: "goal_kill_bandit_first" ),

			// —— Taming ——
			J( ref order, "goal_tame_creature", "New Companion", ThornsJourneyCategory.Taming,
				"Not every beast has to be meat. Some might walk beside me if I earn their trust.",
				"Tame a creature", ThornsMilestoneType.Tame, "tame", 1, 300,
				prereq: "goal_first_blood",
				unlockDiscovery: ThornsDefinitionRegistry.DiscoveryIdForCreature( "wolf" ) )
		};
	}

	static ThornsJournalGoalDefinition J( ref int sort, string id, string title, ThornsJourneyCategory category,
		string journalEntry, string guidance, ThornsMilestoneType type, string targetKey, int targetCount, int xp,
		string prereq = "", bool autoPin = false, bool hideWhenLocked = true, string unlockDiscovery = "" ) =>
		Goal( ref sort, id, title, category, journalEntry, guidance, type, targetKey, targetCount, xp, prereq, autoPin,
			hideWhenLocked, unlockDiscovery );

	static ThornsJournalGoalDefinition H( ref int order, string id, string title, ThornsJourneyCategory category,
		string journalEntry, string guidance, string item, int count, int xp, string prereq = "" )
	{
		var g = Goal( ref order, id, title, category, journalEntry, guidance, ThornsMilestoneType.Collect, item, count,
			xp, prereq );
		g.CollectMode = ThornsCollectTrackMode.HoldInInventory;
		return g;
	}

	static ThornsJournalGoalDefinition BareHandsGatherGoal( ref int sort, string id, string title,
		ThornsJourneyCategory category, string journalEntry, string guidance, int xp, string prereq, bool autoPin )
	{
		return new ThornsJournalGoalDefinition
		{
			Id = id,
			Title = title,
			JournalEntry = journalEntry,
			Description = journalEntry,
			RequirementText = guidance,
			SortOrder = sort++,
			JourneyCategory = category,
			PrerequisiteGoalId = prereq,
			AutoPinUntilComplete = autoPin,
			MilestoneType = ThornsMilestoneType.Event,
			TargetKey = "bare_hands_gather",
			TargetCount = 2,
			XpReward = xp,
			Tasks = new List<ThornsJournalTaskDefinition>
			{
				new() { Id = "punch_wood", Label = "Punch a tree for wood (empty hands, LMB)", TargetCount = 1 },
				new() { Id = "punch_stone", Label = "Punch a stone node for stone (empty hands, LMB)", TargetCount = 1 }
			},
			Rewards = new List<ThornsJournalRewardDto>
			{
				new() { Label = $"{xp} XP", Kind = "xp", IconPath = ThornsIconRegistry.Hud( "xp" ) }
			}
		};
	}

	static ThornsJournalGoalDefinition ControlGoal( ref int sort, string id, string title, ThornsJourneyCategory category,
		string journalEntry, string guidance, int xp, string prereq, bool autoPin )
	{
		return new ThornsJournalGoalDefinition
		{
			Id = id,
			Title = title,
			JournalEntry = journalEntry,
			Description = journalEntry,
			RequirementText = guidance,
			SortOrder = sort++,
			JourneyCategory = category,
			PrerequisiteGoalId = prereq,
			AutoPinUntilComplete = autoPin,
			MilestoneType = ThornsMilestoneType.Event,
			TargetKey = "controls",
			TargetCount = 6,
			XpReward = xp,
			Tasks = new List<ThornsJournalTaskDefinition>
			{
				new() { Id = "inventory", Label = "Open Inventory (I)", TargetCount = 1 },
				new() { Id = "journal", Label = "Open Journal (J)", TargetCount = 1 },
				new() { Id = "map", Label = "Open Map (M)", TargetCount = 1 },
				new() { Id = "build", Label = "Open Build (B)", TargetCount = 1 },
				new() { Id = "skills", Label = "Open Skills (K)", TargetCount = 1 },
				new() { Id = "settings", Label = "Open Settings tab", TargetCount = 1 }
			},
			Rewards = new List<ThornsJournalRewardDto>
			{
				new() { Label = $"{xp} XP", Kind = "xp", IconPath = ThornsIconRegistry.Hud( "xp" ) }
			}
		};
	}

	static ThornsJournalGoalDefinition Goal( ref int sort, string id, string title, ThornsJourneyCategory category,
		string journalEntry, string guidance, ThornsMilestoneType type, string targetKey, int targetCount, int xp,
		string prereq = "", bool autoPin = false, bool hideWhenLocked = true, string unlockDiscovery = "" )
	{
		// guidance / RequirementText: name the item or action only — never hotbar slots, slot numbers, or container locations.
		return new ThornsJournalGoalDefinition
		{
			Id = id,
			Title = title,
			JournalEntry = journalEntry,
			Description = journalEntry,
			RequirementText = guidance,
			SortOrder = sort++,
			JourneyCategory = category,
			PrerequisiteGoalId = prereq,
			UnlockOnDiscoveryId = unlockDiscovery,
			HideWhenLocked = hideWhenLocked,
			AutoPinUntilComplete = autoPin,
			MilestoneType = type,
			TargetKey = targetKey,
			TargetCount = targetCount,
			XpReward = xp,
			Tasks = new List<ThornsJournalTaskDefinition>
			{
				new()
				{
					Id = "progress",
					Label = guidance,
					TargetCount = targetCount
				}
			},
			Rewards = new List<ThornsJournalRewardDto>
			{
				new() { Label = $"{xp} XP", Kind = "xp", IconPath = ThornsIconRegistry.Hud( "xp" ) }
			}
		};
	}
}
