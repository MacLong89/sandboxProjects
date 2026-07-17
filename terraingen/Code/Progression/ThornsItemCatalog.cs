namespace Terraingen.GameData;

using Sandbox;
using Terraingen;

/// <summary>Items and recipes referenced by legacy milestones.</summary>
/// <remarks>
/// Crafting policy: every catalog item except raw materials in <see cref="ThornsCraftCoverage.RawMaterialIds"/>
/// must have a recipe (directly or via deployable kit output). See <see cref="ThornsCraftCoverage"/>.
/// </remarks>
public static class ThornsItemCatalog
{
	public static IEnumerable<ThornsItemDefinition> Items => new[]
	{
		Res( "wood", "Wood" ),
		Res( "stone", "Stone" ),
		Res( "cloth", "Cloth" ),
		Res( "metal_ore", "Metal Ore" ),
		Res( "animal_hide", "Animal Hide" ),
		ToolWithModel( "stone_hatchet", "Stone Hatchet", "models/tools/stone_axe.vmdl", ThornsHarvestToolKind.Axe ),
		ToolWithModel( "stone_pickaxe", "Stone Pickaxe", "models/tools/stone_pickaxe.vmdl", ThornsHarvestToolKind.Pickaxe ),
		ToolWithModel( "iron_hatchet", "Iron Hatchet", "models/tools/iron_axe.vmdl", ThornsHarvestToolKind.Axe ),
		ToolWithModel( "iron_pickaxe", "Iron Pickaxe", "models/tools/iron_pickaxe.vmdl", ThornsHarvestToolKind.Pickaxe ),
		Weapon( "m4", "M4" ),
		Weapon( "mp5", "MP5" ),
		Weapon( "shotgun", "Shotgun" ),
		Weapon( "sniper", "Sniper" ),
		Weapon( "usp", "USP" ),
		Weapon( "m9_bayonet", "M9 Bayonet" ),
		BowWeapon(),
		Attachment( Terraingen.Combat.Attachments.ThornsAttachmentItemIds.Holo, "Holo Sight" ),
		Attachment( Terraingen.Combat.Attachments.ThornsAttachmentItemIds.Ranged, "Ranged Sight" ),
		Attachment( Terraingen.Combat.Attachments.ThornsAttachmentItemIds.RedDot, "Raised Red Dot" ),
		Attachment( Terraingen.Combat.Attachments.ThornsAttachmentItemIds.ExtendedMag, "Extended Mag" ),
		Attachment( Terraingen.Combat.Attachments.ThornsAttachmentItemIds.ForegripAngled, "Foregrip (Angled)" ),
		Attachment( Terraingen.Combat.Attachments.ThornsAttachmentItemIds.Suppressor, "Suppressor" ),
		Kit( "campfire_kit", "Campfire", ThornsPlaceableModels.Campfire ),
		Kit( "bed_kit", "Bed", ThornsPlaceableModels.Bed ),
		Kit( "storage_chest_kit", "Storage Chest", ThornsPlaceableModels.Chest ),
		Kit( "workbench_kit", "Workbench", ThornsPlaceableModels.Workbench ),
		Kit( "research_kit", "Research Station", ThornsPlaceableModels.Research, iconId: "research" ),
		Cons( "bandage", "Bandage", 20 ),
		Cons( "smelt_metal", "Metal Ingot", 50 ),
		Cons( "leather_scrap", "Leather Scrap", 50 ),
		Cons( "rifle_ammo", "Rifle Ammo", 60 ),
		Cons( "smg_ammo", "SMG Ammo", 60 ),
		Cons( "shotgun_ammo", "Shotgun Shells", 48 ),
		Cons( "sniper_ammo", "Sniper Rounds", 40 ),
		Cons( "pistol_ammo", "Pistol Rounds", 60 ),
		ArrowAmmo(),
		Cons( "food", "Food", 20, "apple" ),
		Cons( "raw_meat", "Raw Meat", 20 ),
		Cons( "water", "Water", 20, "water_bottle" ),
		Cons( "canned_stew", "Canned Stew", 20 ),
		Cons( "field_rations", "Field Rations", 20 ),
		Cons( "morphine_pen", "Morphine Pen", 10 ),
		Cons( "c4", "C4 Charge", 5 ),
		Place( "wood_foundation", "Wood Foundation" ),
		Place( "wood_wall", "Wood Wall" ),
		Place( "wood_doorframe", "Wood Doorframe" ),
		Place( "wood_window", "Wood Window" ),
		Place( "wood_ramp", "Wood Ramp" ),
		Place( "campfire", "Campfire", ThornsPlaceableModels.Campfire ),
		Place( "bed", "Bed", ThornsPlaceableModels.Bed ),
		Place( "storage_chest", "Storage Chest", ThornsPlaceableModels.Chest ),
		Place( "workbench", "Workbench", ThornsPlaceableModels.Workbench ),
		Place( "research", "Research Station", ThornsPlaceableModels.Research, iconId: "research" ),
		Armor( "scrap_chest", "Scrap Chestplate", ThornsEquipSlot.Chest, 0.08f ),
		Armor( "scrap_head", "Scrap Helmet", ThornsEquipSlot.Head, 0.04f ),
		Armor( "scrap_legs", "Scrap Leg Guards", ThornsEquipSlot.Legs, 0.06f ),
		Armor( "kevlar_head", "Kevlar Helmet", ThornsEquipSlot.Head, 0.07f ),
		Armor( "kevlar_chest", "Kevlar Chestplate", ThornsEquipSlot.Chest, 0.12f ),
		Armor( "kevlar_legs", "Kevlar Leg Guards", ThornsEquipSlot.Legs, 0.09f ),
		Medkit( "medkit", "Medkit", 10 )
	};

	public static IEnumerable<ThornsRecipeDefinition> Recipes => new[]
	{
		Recipe( "stone_hatchet", "Stone Hatchet", "stone_hatchet", "tools", 5f, 1, Ing( "wood", 20 ), Ing( "stone", 15 ) ),
		Recipe( "stone_pickaxe", "Stone Pickaxe", "stone_pickaxe", "tools", 5f, 1, Ing( "wood", 15 ), Ing( "stone", 20 ) ),
		Recipe( "iron_hatchet", "Iron Hatchet", "iron_hatchet", "tools", 10f, 4, Ing( "smelt_metal", 5 ), Ing( "wood", 12 ), station: ThornsCraftStationKind.Workbench ),
		Recipe( "iron_pickaxe", "Iron Pickaxe", "iron_pickaxe", "tools", 10f, 4, Ing( "smelt_metal", 5 ), Ing( "wood", 10 ), station: ThornsCraftStationKind.Workbench ),

		Recipe( "campfire_kit", "Campfire", "campfire_kit", "build", 8f, 1, Ing( "wood", 40 ), Ing( "stone", 10 ) ),
		Recipe( "bed_kit", "Bed", "bed_kit", "build", 10f, 2, Ing( "wood", 50 ), Ing( "cloth", 10 ) ),
		Recipe( "storage_chest_kit", "Storage Chest", "storage_chest_kit", "build", 10f, 2, Ing( "wood", 45 ), Ing( "stone", 10 ) ),
		Recipe( "workbench_kit", "Workbench", "workbench_kit", "build", 12f, 3, Ing( "wood", 60 ), Ing( "stone", 25 ), Ing( "metal_ore", 5 ) ),
		Recipe( "research_kit", "Research Station", "research_kit", "build", 18f, 4, Ing( "wood", 55 ), Ing( "stone", 30 ), Ing( "metal_ore", 16 ) ),
		// Wood foundation/wall/door/window/ramp are place modes on the build menu (B), not craft recipes.
		Recipe( "c4", "C4 Charge", "c4", "build", 14f, 5, Ing( "smelt_metal", 6 ), Ing( "cloth", 8 ), Ing( "stone", 12 ), station: ThornsCraftStationKind.Workbench ),

		Recipe( "bandage", "Bandage", "bandage", "medical", 4f, 1, Ing( "cloth", 5 ) ),
		Recipe( "medkit", "Medkit", "medkit", "medical", 12f, 6, Ing( "bandage", 3 ), Ing( "cloth", 5 ), Ing( "smelt_metal", 2 ), station: ThornsCraftStationKind.Workbench ),
		Recipe( "morphine_pen", "Morphine Pen", "morphine_pen", "medical", 14f, 5, Ing( "bandage", 2 ), Ing( "cloth", 4 ), Ing( "smelt_metal", 3 ), station: ThornsCraftStationKind.Workbench ),

		Recipe( "cooked_food", "Cooked Food", "food", "food", 6f, 1, Ing( "raw_meat", 1 ), station: ThornsCraftStationKind.Campfire ),
		Recipe( "field_rations", "Field Rations", "field_rations", "food", 8f, 2, Ing( "food", 2 ), Ing( "cloth", 2 ) ),
		Recipe( "canned_stew", "Canned Stew", "canned_stew", "food", 10f, 3, Ing( "raw_meat", 2 ), Ing( "food", 1 ), Ing( "smelt_metal", 1 ), station: ThornsCraftStationKind.Campfire ),

		Recipe( "leather_scrap", "Leather Scrap", "leather_scrap", "craft", 5f, 1, Ing( "animal_hide", 3 ) ),

		Recipe( "scrap_chest", "Scrap Chest", "scrap_chest", "armor", 15f, 3, Ing( "smelt_metal", 8 ), Ing( "leather_scrap", 4 ), station: ThornsCraftStationKind.Workbench ),
		Recipe( "scrap_head", "Scrap Helmet", "scrap_head", "armor", 14f, 3, Ing( "smelt_metal", 5 ), Ing( "leather_scrap", 3 ), station: ThornsCraftStationKind.Workbench ),
		Recipe( "scrap_legs", "Scrap Leg Guards", "scrap_legs", "armor", 14f, 3, Ing( "smelt_metal", 6 ), Ing( "leather_scrap", 5 ), station: ThornsCraftStationKind.Workbench ),
		Recipe( "kevlar_chest", "Kevlar Chest", "kevlar_chest", "armor", 20f, 4, Ing( "smelt_metal", 12 ), Ing( "cloth", 20 ), station: ThornsCraftStationKind.Workbench ),
		Recipe( "kevlar_head", "Kevlar Helmet", "kevlar_head", "armor", 18f, 5, Ing( "smelt_metal", 8 ), Ing( "cloth", 10 ), station: ThornsCraftStationKind.Workbench ),
		Recipe( "kevlar_legs", "Kevlar Leg Guards", "kevlar_legs", "armor", 20f, 5, Ing( "smelt_metal", 10 ), Ing( "cloth", 12 ), Ing( "leather_scrap", 3 ), station: ThornsCraftStationKind.Workbench ),

		Recipe( "pistol_ammo", "Pistol Rounds", "pistol_ammo", "ammo", 4f, 2, Ing( "smelt_metal", 1 ), Ing( "stone", 4 ) ),
		Recipe( "rifle_ammo", "Rifle Ammo", "rifle_ammo", "ammo", 4f, 3, Ing( "smelt_metal", 2 ), Ing( "stone", 5 ), station: ThornsCraftStationKind.Workbench ),
		Recipe( "smg_ammo", "SMG Ammo", "smg_ammo", "ammo", 5f, 4, Ing( "smelt_metal", 2 ), Ing( "stone", 4 ), station: ThornsCraftStationKind.Workbench ),
		Recipe( "shotgun_ammo", "Shotgun Shells", "shotgun_ammo", "ammo", 5f, 4, Ing( "smelt_metal", 2 ), Ing( "stone", 6 ), station: ThornsCraftStationKind.Workbench ),
		Recipe( "sniper_ammo", "Sniper Rounds", "sniper_ammo", "ammo", 6f, 5, Ing( "smelt_metal", 3 ), Ing( "stone", 8 ), station: ThornsCraftStationKind.Workbench ),
		Recipe( "arrow", "Arrows", "arrow", "ammo", 4f, 1, Ing( "wood", 3 ), Ing( "stone", 2 ), outputCount: 5 ),

		Recipe( "bow", "Bow", "bow", "weapons", 8f, 1, Ing( "cloth", 8 ), Ing( "stone", 12 ) ),
		Recipe( "usp", "USP", "usp", "weapons", 18f, 3, Ing( "smelt_metal", 4 ), Ing( "wood", 5 ), Ing( "cloth", 2 ), station: ThornsCraftStationKind.Workbench ),
		Recipe( "mp5", "MP5", "mp5", "weapons", 22f, 4, Ing( "smelt_metal", 8 ), Ing( "wood", 6 ), Ing( "cloth", 4 ), station: ThornsCraftStationKind.Workbench ),
		Recipe( "m4", "M4", "m4", "weapons", 28f, 5, Ing( "smelt_metal", 12 ), Ing( "wood", 8 ), Ing( "cloth", 6 ), station: ThornsCraftStationKind.Workbench ),
		Recipe( "shotgun", "Shotgun", "shotgun", "weapons", 26f, 5, Ing( "smelt_metal", 10 ), Ing( "wood", 10 ), Ing( "stone", 8 ), station: ThornsCraftStationKind.Workbench ),
		Recipe( "sniper", "Sniper", "sniper", "weapons", 32f, 6, Ing( "smelt_metal", 14 ), Ing( "wood", 6 ), Ing( "leather_scrap", 2 ), station: ThornsCraftStationKind.Workbench ),
		Recipe( "m9_bayonet", "M9 Bayonet", "m9_bayonet", "weapons", 14f, 4, Ing( "smelt_metal", 6 ), Ing( "wood", 4 ), station: ThornsCraftStationKind.Workbench ),

		Recipe( "attachment_holo", "Holo Sight", Terraingen.Combat.Attachments.ThornsAttachmentItemIds.Holo, "attachments", 10f, 3, Ing( "smelt_metal", 2 ), Ing( "cloth", 1 ), station: ThornsCraftStationKind.Workbench ),
		Recipe( "attachment_red_dot", "Red Dot Sight", Terraingen.Combat.Attachments.ThornsAttachmentItemIds.RedDot, "attachments", 10f, 3, Ing( "smelt_metal", 2 ), Ing( "cloth", 1 ), station: ThornsCraftStationKind.Workbench ),
		Recipe( "attachment_ranged", "Ranged Sight", Terraingen.Combat.Attachments.ThornsAttachmentItemIds.Ranged, "attachments", 12f, 4, Ing( "smelt_metal", 3 ), Ing( "cloth", 2 ), station: ThornsCraftStationKind.Workbench ),
		Recipe( "attachment_extended_mag", "Extended Mag", Terraingen.Combat.Attachments.ThornsAttachmentItemIds.ExtendedMag, "attachments", 12f, 4, Ing( "smelt_metal", 3 ), Ing( "cloth", 2 ), station: ThornsCraftStationKind.Workbench ),
		Recipe( "attachment_foregrip_angled", "Angled Foregrip", Terraingen.Combat.Attachments.ThornsAttachmentItemIds.ForegripAngled, "attachments", 14f, 5, Ing( "smelt_metal", 5 ), Ing( "wood", 3 ), station: ThornsCraftStationKind.Workbench ),
		Recipe( "attachment_suppressor", "Suppressor", Terraingen.Combat.Attachments.ThornsAttachmentItemIds.Suppressor, "attachments", 16f, 5, Ing( "smelt_metal", 6 ), Ing( "cloth", 2 ), Ing( "leather_scrap", 1 ), station: ThornsCraftStationKind.Workbench )
	};

	static ThornsRecipeIngredient Ing( string item, int count ) => new() { ItemId = item, Count = count };

	static string LegacyItemIcon( string itemId ) => $"ui/icons/{itemId}.png";

	static ThornsItemDefinition Res( string id, string name ) => new()
	{
		Id = id, DisplayName = name, Category = ThornsItemCategory.Resource, MaxStack = 250,
		IconPath = ThornsIconRegistry.Item( id ),
		Description = $"{name} used for crafting, building, and survival upkeep."
	};

	static ThornsItemDefinition Tool( string id, string name ) => ToolWithModel( id, name, "", ThornsHarvestToolKind.None );

	static ThornsItemDefinition ToolWithModel( string id, string name, string modelPath, ThornsHarvestToolKind harvestKind ) => new()
	{
		Id = id,
		DisplayName = name,
		Category = ThornsItemCategory.Tool,
		MaxStack = 1,
		IconPath = ThornsIconRegistry.Item( id ),
		WeightKg = 2.5f,
		ToolMaxDurability = harvestKind == ThornsHarvestToolKind.Primitive ? 80f : harvestKind == ThornsHarvestToolKind.None ? 0f : 120f,
		ToolDurabilityLossPerStrike = harvestKind == ThornsHarvestToolKind.None ? 0f : 0.35f,
		Description = $"A {name.ToLower()} for gathering resources and working the land.",
		ViewModelAsset = modelPath,
		WorldModelAsset = modelPath,
		HarvestToolKind = harvestKind,
		FpViewmodelRootLocalScale = ThornsFpItemHelpers.FpViewmodelRootLocalScaleOne,
		FpViewmodelRootLocalOffset = harvestKind is ThornsHarvestToolKind.Axe or ThornsHarvestToolKind.Pickaxe
			? ThornsFpItemHelpers.FpHarvestAxePickaxeViewmodelRootOffset
			: default,
		FpViewmodelRootLocalEulerDegrees = harvestKind switch
		{
			ThornsHarvestToolKind.Axe => ThornsFpItemHelpers.FpHarvestAxeViewmodelRootEulerDegrees,
			ThornsHarvestToolKind.Pickaxe when string.Equals( id, "stone_pickaxe", StringComparison.OrdinalIgnoreCase )
				=> ThornsFpItemHelpers.FpHarvestStonePickaxeViewmodelRootEulerDegrees,
			ThornsHarvestToolKind.Pickaxe => ThornsFpItemHelpers.FpHarvestPickaxeViewmodelRootEulerDegrees,
			_ => default
		}
	};

	static ThornsItemDefinition ArrowAmmo() => new()
	{
		Id = "arrow",
		DisplayName = "Arrow",
		Category = ThornsItemCategory.Consumable,
		MaxStack = 60,
		IconPath = LegacyItemIcon( "arrow" ),
		WeightKg = 0.02f,
		Description = "Arrows for bows."
	};

	static ThornsItemDefinition BowWeapon() => new()
	{
		Id = "bow",
		DisplayName = "Bow",
		Category = ThornsItemCategory.Weapon,
		MaxStack = 1,
		IconPath = LegacyItemIcon( "bow" ),
		WeightKg = 2.2f,
		Description = "Hold attack to draw, release at full draw to loose an arrow.",
		CombatWeaponDefinitionId = "bow",
		ViewModelAsset = ThornsWeaponResourceLoad.BowModelPath,
		WorldModelAsset = ThornsWeaponResourceLoad.BowModelPath,
		FpViewmodelRootLocalScale = ThornsFpItemHelpers.FpBowViewmodelRootScale,
		FpViewmodelRootLocalOffset = ThornsFpItemHelpers.FpBowViewmodelRootOffset,
		FpViewmodelRootLocalEulerDegrees = ThornsFpItemHelpers.FpBowViewmodelRootEulerDegrees
	};

	static ThornsItemDefinition Weapon( string id, string name ) => new()
	{
		Id = id,
		DisplayName = name,
		Category = ThornsItemCategory.Weapon,
		MaxStack = 1,
		IconPath = LegacyItemIcon( id ),
		WeightKg = 3.8f,
		Description = WeaponDescription( id, name ),
		CombatWeaponDefinitionId = id,
		ViewModelAsset = WeaponViewModelPath( id ),
		WorldModelAsset = WeaponWorldModelPath( id )
	};

	static string WeaponDescription( string id, string name ) => id switch
	{
		"m4" => "A balanced assault rifle with moderate recoil and high versatility.",
		"mp5" => "Compact SMG with high rate of fire for close quarters.",
		"shotgun" => "Pump shotgun — devastating at close range.",
		"sniper" => "Bolt-action sniper rifle for long-range engagements.",
		"usp" => "Reliable semi-auto pistol for sidearm duty.",
		"m9_bayonet" => "M9 bayonet for silent melee takedowns.",
		_ => $"A {name} for defending yourself and your tames."
	};

	static string WeaponViewModelPath( string id ) => id switch
	{
		"mp5" => ThornsViewModelController.Mp5FirstPersonViewmodelPath,
		"shotgun" => ThornsViewModelController.ShotgunFirstPersonViewmodelPath,
		"sniper" => ThornsViewModelController.SniperFirstPersonViewmodelPath,
		"usp" => ThornsViewModelController.UspFirstPersonViewmodelPath,
		"m9_bayonet" => ThornsViewModelController.BayonetM9FirstPersonViewmodelPath,
		_ => ThornsViewModelController.M4FirstPersonViewmodelPath
	};

	static string WeaponWorldModelPath( string id ) => id switch
	{
		"mp5" => ThornsViewModelController.Mp5WorldModelPath,
		"shotgun" => ThornsViewModelController.ShotgunWorldModelPath,
		"sniper" => ThornsViewModelController.SniperWorldModelPath,
		"usp" => ThornsViewModelController.UspWorldModelPath,
		"m9_bayonet" => ThornsViewModelController.BayonetM9WorldModelPath,
		_ => ThornsViewModelController.M4WorldModelPath
	};

	static ThornsItemDefinition Attachment( string id, string name ) => new()
	{
		Id = id,
		DisplayName = name,
		Category = ThornsItemCategory.Attachment,
		MaxStack = 1,
		IconPath = LegacyItemIcon( ThornsItemIdAliases.AttachmentIconStem( id ) ),
		WeightKg = 0.35f,
		Description = $"{name} — mount on compatible weapons from the inventory inspector."
	};

	static ThornsItemDefinition Kit( string id, string name, string modelPath = "", string iconId = null ) => new()
	{
		Id = id,
		DisplayName = name,
		Category = ThornsItemCategory.Resource,
		MaxStack = 10,
		IconPath = !string.IsNullOrWhiteSpace( iconId ) ? LegacyItemIcon( iconId ) : ThornsIconRegistry.Item( id ),
		Description = $"Deployable {name.ToLower()} you can place in the world.",
		ViewModelAsset = modelPath,
		WorldModelAsset = modelPath
	};

	static ThornsItemDefinition Medkit( string id, string name, int stack ) => new()
	{
		Id = id,
		DisplayName = name,
		Category = ThornsItemCategory.Consumable,
		MaxStack = stack,
		IconPath = ThornsIconRegistry.Item( id ),
		WeightKg = 0.35f,
		Description = "Heals a large portion of health over a short time.",
		ViewModelAsset = "models/tools/medkit.vmdl",
		WorldModelAsset = "models/tools/medkit.vmdl",
		FpViewmodelRootLocalOffset = ThornsFpItemHelpers.FpMedkitViewmodelRootOffset,
		FpViewmodelRootLocalScale = ThornsFpItemHelpers.FpViewmodelRootLocalScaleOne
	};

	static ThornsItemDefinition Cons( string id, string name, int stack, string iconId = null ) => new()
	{
		Id = id, DisplayName = name, Category = ThornsItemCategory.Consumable, MaxStack = stack,
		IconPath = ThornsIconRegistry.Item( iconId ?? id ),
		WeightKg = id.Contains( "ammo" ) || string.Equals( id, "arrow", StringComparison.OrdinalIgnoreCase ) ? 0.02f : 0.35f,
		Description = id switch
		{
			"food" => "Restores hunger when consumed.",
			"water" => "Restores thirst when consumed.",
			"bandage" => "Basic medical supply that stops bleeding.",
			"medkit" => "Heals a large portion of health over a short time.",
			"rifle_ammo" => "Ammunition for rifles and similar firearms.",
			"smg_ammo" => "Ammunition for submachine guns.",
			"shotgun_ammo" => "Shells for shotguns.",
			"sniper_ammo" => "High-caliber rounds for sniper rifles.",
			"pistol_ammo" => "Ammunition for pistols.",
			"arrow" => "Arrows for bows.",
			"c4" => "Place on a surface — detonates after 3 seconds. Damages structures and survivors.",
			"canned_stew" => "Hearty preserved meal that restores hunger.",
			"field_rations" => "Compact military meal that restores hunger.",
			"morphine_pen" => "Emergency painkiller that restores health quickly.",
			_ => $"{name} consumable for survival."
		}
	};

	static ThornsItemDefinition Place( string id, string name, string modelPath = "", string iconId = null ) => new()
	{
		Id = id,
		DisplayName = name,
		Category = ThornsItemCategory.Resource,
		MaxStack = 20,
		IconPath = !string.IsNullOrWhiteSpace( iconId ) ? LegacyItemIcon( iconId ) : ThornsIconRegistry.Item( id ),
		Description = $"A placed {name.ToLower()} for your base.",
		ViewModelAsset = modelPath,
		WorldModelAsset = modelPath
	};

	static ThornsItemDefinition Armor( string id, string name, ThornsEquipSlot slot, float protection ) => new()
	{
		Id = id, DisplayName = name, Category = ThornsItemCategory.Armor, EquipSlot = slot, MaxStack = 1,
		IconPath = ThornsIconRegistry.Item( id ),
		WeightKg = 4f,
		ArmorProtection = protection,
		Description = $"Protective {slot.ToString().ToLower()} armor that reduces incoming damage."
	};

	static ThornsRecipeDefinition Recipe( string id, string name, string output, string cat, float seconds, int requiredCraftTier,
		ThornsRecipeIngredient a, ThornsRecipeIngredient b = null, ThornsRecipeIngredient c = null,
		ThornsCraftStationKind station = ThornsCraftStationKind.Hand, int outputCount = 1 )
	{
		var r = new ThornsRecipeDefinition
		{
			Id = $"recipe_{id}",
			DisplayName = name,
			Description = $"Craft {name.ToLower()} at a {station} station.",
			CategoryId = cat,
			OutputItemId = output,
			OutputCount = Math.Max( 1, outputCount ),
			CraftSeconds = seconds,
			RequiredCraftTier = Math.Max( 1, requiredCraftTier ),
			Station = station
		};
		r.Ingredients.Add( a );
		if ( b is not null )
			r.Ingredients.Add( b );
		if ( c is not null )
			r.Ingredients.Add( c );
		return r;
	}
}
