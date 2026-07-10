#nullable disable

using System;
using System.Collections.Generic;

namespace Sandbox;

/// <summary>
/// Static item definitions (THORNS_EVERYTHING_DOCUMENT — readable tuning, stack rules, types).
/// Persistence/crafting will reference these ids.
/// </summary>
public static class ThornsItemRegistry
{
	/// <summary>Default FP root scale when <see cref="ThornsItemDefinition.FpViewmodelRootLocalScale"/> is omitted (record default is zero-length).</summary>
	public static readonly Vector3 FpViewmodelRootLocalScaleOne = new( 1f, 1f, 1f );

	/// <summary>Shared FP root offset for stone/iron axes and pickaxes (<c>WeaponViewmodel</c> local space).</summary>
	public static readonly Vector3 FpHarvestAxePickaxeViewmodelRootOffset = new( 5f, -2f, -1f );

	public static readonly Vector3 FpHarvestAxeViewmodelRootEulerDegrees = new( 20f, 180f, -60f );

	public static readonly Vector3 FpHarvestPickaxeViewmodelRootEulerDegrees = new( 20f, -180f, -40f );

	public static readonly Vector3 FpHarvestStonePickaxeViewmodelRootEulerDegrees = new( 20f, 10f, -30f );

	/// <summary>
	/// Uniform FP scale for <c>models/tools/*axe*</c> / <c>*pick*</c> after registry scale (not Facepunch <c>v_*</c> rigs — those use <see cref="ThornsViewModelController.FpWeaponMeshRootScaleMul"/>).
	/// </summary>
	public static readonly Vector3 FpHarvestToolViewmodelRootScale = new( 4f, 4f, 4f );

	/// <summary>Medkit held slightly less far than long tools so it does not dominate the frame.</summary>
	public static readonly Vector3 FpMedkitViewmodelRootOffset = new( 14f, 0f, 0f );

	// Positional parameters: avoid XML doc on each parameter — SB2000 duplicates generated inspector descriptions.
	public sealed record ThornsItemDefinition(
		string Id,
		string DisplayName,
		int MaxStack,
		ThornsItemType ItemType,
		string CombatWeaponDefinitionId = "",
		string ViewModelAsset = "",
		string WorldModelAsset = "",
		string AmmoTypeId = "",
		ThornsArmorSlotKind ArmorSlot = ThornsArmorSlotKind.None,
		float ArmorDamageReductionPercent = 0f,
		float ArmorMaxDurability = 0f,
		string ArmorRollPlaceholder = "",
		ThornsConsumableKind ConsumableKind = ThornsConsumableKind.None,
		float HungerRestore = 0f,
		float ThirstRestore = 0f,
		float HealthRestore = 0f,
		float PoisonAmount = 0f,
		float UseTimeSeconds = 0f,
		string HudIconTexture = "",
		ThornsHarvestToolKind HarvestToolKind = ThornsHarvestToolKind.None,
		float ToolHarvestYieldMultiplier = 1f,
		/// <summary>When &gt; 0, toolbar tools use <see cref="ThornsInventorySlot.HasDurability"/> / <see cref="ThornsInventorySlot.Durability"/> (harvest + tool-melee wear).</summary>
		float ToolMaxDurability = 0f,
		float ToolDurabilityLossPerStrike = 0f,
		/// <summary>Owner FP: additive offset on the View object for <see cref="ThornsViewModelController"/> weapon mesh root. +X pushes toward the world in front of the camera (same axis as <see cref="ThornsViewModelController.ViewModelAdsForwardOffset"/>).</summary>
		Vector3 FpViewmodelRootLocalOffset = default,
		/// <summary>Owner FP: local scale on that mesh root. Use <c>(1,1,1)</c> when unchanged. Ignored for stock Facepunch FP rigs (M4/MP5/etc.).</summary>
		Vector3 FpViewmodelRootLocalScale = default,
		/// <summary>Owner FP: additional local Euler degrees on the mesh root (composed with <see cref="ThornsViewModelController.ViewModelGripLocalEulerDegrees"/>). Ignored for stock Facepunch FP rigs.</summary>
		Vector3 FpViewmodelRootLocalEulerDegrees = default );

	public static bool IsUsableConsumable( ThornsItemDefinition def ) =>
		def.ItemType == ThornsItemType.Consumable
		&& def.ConsumableKind != ThornsConsumableKind.None
		&& def.ConsumableKind != ThornsConsumableKind.Explosive;

	public static bool HarvestToolMatchesResourceKind( ThornsHarvestToolKind toolKind, ThornsResourceKind resourceKind ) =>
		toolKind switch
		{
			ThornsHarvestToolKind.Axe => resourceKind is ThornsResourceKind.Wood or ThornsResourceKind.Fiber,
			ThornsHarvestToolKind.Pickaxe => resourceKind is ThornsResourceKind.Stone or ThornsResourceKind.MetalOre,
			ThornsHarvestToolKind.Primitive => resourceKind is ThornsResourceKind.Wood or ThornsResourceKind.Stone,
			_ => false
		};

	/// <summary>
	/// Starter harvest tool (wood + stone at reduced yield). Single source of truth for registry + hot-reload merge + spawn fallbacks.
	/// HUD: <c>textures/ui/item_icons/primitive_tool.png</c> on disk under <c>Assets/textures/ui/item_icons/</c> (see <see cref="ThornsItemHudIcons"/>).
	/// </summary>
	public static readonly ThornsItemDefinition PrimitiveToolDefinition = new(
		"primitive_tool",
		"Primitive Tool",
		1,
		ThornsItemType.Tool,
		HarvestToolKind: ThornsHarvestToolKind.Primitive,
		ToolHarvestYieldMultiplier: 1f,
		HudIconTexture: "textures/ui/item_icons/primitive_tool.png",
		ToolMaxDurability: 220f,
		ToolDurabilityLossPerStrike: 1.15f );

	/// <summary>Canonical placeable rows — refreshed on lookup so stale <c>thorns.dll</c> static init cannot keep old "Kit" names or missing <c>bed_kit</c>.</summary>
	public static readonly ThornsItemDefinition StorageChestKitDefinition = new(
		"storage_chest_kit",
		"Storage Chest",
		5,
		ThornsItemType.Resource,
		HudIconTexture: "textures/ui/item_icons/player_chest.png" );

	public static readonly ThornsItemDefinition CampfireKitDefinition = new(
		"campfire_kit",
		"Campfire",
		5,
		ThornsItemType.Resource,
		HudIconTexture: "textures/ui/item_icons/campfire.png" );

	public static readonly ThornsItemDefinition WorkbenchKitDefinition = new(
		"workbench_kit",
		"Workbench",
		5,
		ThornsItemType.Resource,
		HudIconTexture: "textures/ui/item_icons/workbench.png" );

	public static readonly ThornsItemDefinition BedKitDefinition = new(
		"bed_kit",
		"Bed",
		5,
		ThornsItemType.Resource,
		HudIconTexture: "textures/ui/item_icons/bed.png" );

	static ThornsItemRegistry()
	{
		RegisterPlaceableFurnitureItemsFromCatalog();
	}

	static readonly Dictionary<string, ThornsItemDefinition> _byId = new( StringComparer.OrdinalIgnoreCase )
	{
		["wood"] = new ThornsItemDefinition( "wood", "Wood", 500, ThornsItemType.Resource, HudIconTexture: "textures/ui/item_icons/wood.png" ),
		["stone"] = new ThornsItemDefinition( "stone", "Stone", 500, ThornsItemType.Resource, HudIconTexture: "textures/ui/item_icons/stone.png" ),
		["storage_chest_kit"] = StorageChestKitDefinition,
		["campfire_kit"] = CampfireKitDefinition,
		["workbench_kit"] = WorkbenchKitDefinition,
		["bed_kit"] = BedKitDefinition,
		["cloth"] = new ThornsItemDefinition( "cloth", "Cloth", 250, ThornsItemType.Resource, HudIconTexture: "textures/ui/item_icons/cloth.png" ),
		["metal"] = new ThornsItemDefinition( "metal", "Metal", 200, ThornsItemType.Resource, HudIconTexture: "textures/ui/item_icons/metal.png" ),
		["metal_ore"] = new ThornsItemDefinition( "metal_ore", "Metal Ore", 200, ThornsItemType.Resource, HudIconTexture: "textures/ui/item_icons/metal_ore.png" ),
		["animal_hide"] = new ThornsItemDefinition( "animal_hide", "Animal Hide", 120, ThornsItemType.Resource, HudIconTexture: "textures/ui/item_icons/animal_hide.png" ),
		["bone_fragments"] = new ThornsItemDefinition( "bone_fragments", "Bone Fragments", 200, ThornsItemType.Resource, HudIconTexture: "textures/ui/item_icons/bone_fragments.png" ),
		["leather_scrap"] = new ThornsItemDefinition( "leather_scrap", "Leather", 120, ThornsItemType.Resource, HudIconTexture: "textures/ui/item_icons/leather.png" ),
		["stone_hatchet"] = new ThornsItemDefinition(
			"stone_hatchet",
			"Stone Hatchet",
			1,
			ThornsItemType.Tool,
			ViewModelAsset: "models/tools/stone_axe.vmdl",
			WorldModelAsset: "models/tools/stone_axe.vmdl",
			HarvestToolKind: ThornsHarvestToolKind.Axe,
			ToolHarvestYieldMultiplier: 0.78f,
			HudIconTexture: "textures/ui/item_icons/stone_axe.png",
			ToolMaxDurability: 340f,
			ToolDurabilityLossPerStrike: 0.88f,
			FpViewmodelRootLocalOffset: FpHarvestAxePickaxeViewmodelRootOffset,
			FpViewmodelRootLocalScale: FpViewmodelRootLocalScaleOne,
			FpViewmodelRootLocalEulerDegrees: FpHarvestAxeViewmodelRootEulerDegrees ),
		["axe"] = new ThornsItemDefinition(
			"axe",
			"Iron Axe",
			1,
			ThornsItemType.Tool,
			ViewModelAsset: "models/tools/iron_axe.vmdl",
			WorldModelAsset: "models/tools/iron_axe.vmdl",
			HarvestToolKind: ThornsHarvestToolKind.Axe,
			ToolHarvestYieldMultiplier: 1f,
			HudIconTexture: "textures/ui/item_icons/metal_axe.png",
			ToolMaxDurability: 500f,
			ToolDurabilityLossPerStrike: 0.62f,
			FpViewmodelRootLocalOffset: FpHarvestAxePickaxeViewmodelRootOffset,
			FpViewmodelRootLocalScale: FpViewmodelRootLocalScaleOne,
			FpViewmodelRootLocalEulerDegrees: FpHarvestAxeViewmodelRootEulerDegrees ),
		["stone_pick"] = new ThornsItemDefinition(
			"stone_pick",
			"Stone Pick",
			1,
			ThornsItemType.Tool,
			ViewModelAsset: "models/tools/stone_pickaxe.vmdl",
			WorldModelAsset: "models/tools/stone_pickaxe.vmdl",
			HarvestToolKind: ThornsHarvestToolKind.Pickaxe,
			ToolHarvestYieldMultiplier: 0.78f,
			HudIconTexture: "textures/ui/item_icons/stone_pickaxe.png",
			ToolMaxDurability: 340f,
			ToolDurabilityLossPerStrike: 0.88f,
			FpViewmodelRootLocalOffset: FpHarvestAxePickaxeViewmodelRootOffset,
			FpViewmodelRootLocalScale: FpViewmodelRootLocalScaleOne,
			FpViewmodelRootLocalEulerDegrees: FpHarvestStonePickaxeViewmodelRootEulerDegrees ),
		["primitive_tool"] = PrimitiveToolDefinition,
		["pickaxe"] = new ThornsItemDefinition(
			"pickaxe",
			"Iron Pickaxe",
			1,
			ThornsItemType.Tool,
			ViewModelAsset: "models/tools/iron_pickaxe.vmdl",
			WorldModelAsset: "models/tools/iron_pickaxe.vmdl",
			HarvestToolKind: ThornsHarvestToolKind.Pickaxe,
			ToolHarvestYieldMultiplier: 1f,
			HudIconTexture: "textures/ui/item_icons/metal_pickaxe.png",
			ToolMaxDurability: 500f,
			ToolDurabilityLossPerStrike: 0.62f,
			FpViewmodelRootLocalOffset: FpHarvestAxePickaxeViewmodelRootOffset,
			FpViewmodelRootLocalScale: FpViewmodelRootLocalScaleOne,
			FpViewmodelRootLocalEulerDegrees: FpHarvestPickaxeViewmodelRootEulerDegrees ),
		["m4"] = new ThornsItemDefinition(
			"m4",
			"M4",
			1,
			ThornsItemType.Weapon,
			CombatWeaponDefinitionId: "m4",
			ViewModelAsset: ThornsViewModelController.M4FirstPersonViewmodelPath,
			WorldModelAsset: ThornsViewModelController.M4WorldModelPath,
			HudIconTexture: "textures/ui/item_icons/m4.png" ),
		["mp5"] = new ThornsItemDefinition(
			"mp5",
			"MP5",
			1,
			ThornsItemType.Weapon,
			CombatWeaponDefinitionId: "mp5",
			ViewModelAsset: ThornsViewModelController.Mp5FirstPersonViewmodelPath,
			WorldModelAsset: ThornsViewModelController.Mp5WorldModelPath,
			HudIconTexture: "textures/ui/item_icons/mp5.png" ),
		["shotgun"] = new ThornsItemDefinition(
			"shotgun",
			"Shotgun",
			1,
			ThornsItemType.Weapon,
			CombatWeaponDefinitionId: "shotgun",
			ViewModelAsset: ThornsViewModelController.ShotgunFirstPersonViewmodelPath,
			WorldModelAsset: ThornsViewModelController.ShotgunWorldModelPath,
			HudIconTexture: "textures/ui/item_icons/shotgun.png" ),
		["sniper"] = new ThornsItemDefinition(
			"sniper",
			"Sniper",
			1,
			ThornsItemType.Weapon,
			CombatWeaponDefinitionId: "sniper",
			ViewModelAsset: ThornsViewModelController.SniperFirstPersonViewmodelPath,
			WorldModelAsset: ThornsViewModelController.SniperWorldModelPath,
			HudIconTexture: "textures/ui/item_icons/sniper.png" ),
		["m9_bayonet"] = new ThornsItemDefinition(
			"m9_bayonet",
			"M9 Bayonet",
			1,
			ThornsItemType.Weapon,
			CombatWeaponDefinitionId: "m9_bayonet",
			ViewModelAsset: ThornsViewModelController.BayonetM9FirstPersonViewmodelPath,
			WorldModelAsset: ThornsViewModelController.BayonetM9WorldModelPath,
			HudIconTexture: "textures/ui/item_icons/m9_bayonet.png" ),
		["pistol_ammo"] = new ThornsItemDefinition(
			"pistol_ammo",
			"Pistol Ammo",
			120,
			ThornsItemType.Ammo,
			AmmoTypeId: "pistol_ammo",
			HudIconTexture: "textures/ui/item_icons/pistol_ammo.png" ),
		["shotgun_ammo"] = new ThornsItemDefinition(
			"shotgun_ammo",
			"Shotgun Ammo",
			80,
			ThornsItemType.Ammo,
			AmmoTypeId: "shotgun_ammo",
			HudIconTexture: "textures/ui/item_icons/shotgun_ammo.png" ),
		["smg_ammo"] = new ThornsItemDefinition(
			"smg_ammo",
			"SMG Ammo",
			150,
			ThornsItemType.Ammo,
			AmmoTypeId: "smg_ammo",
			HudIconTexture: "textures/ui/item_icons/smg_ammo.png" ),
		["rifle_ammo"] = new ThornsItemDefinition(
			"rifle_ammo",
			"Rifle Ammo",
			120,
			ThornsItemType.Ammo,
			AmmoTypeId: "rifle_ammo",
			HudIconTexture: "textures/ui/item_icons/rifle_ammo.png" ),
		["sniper_ammo"] = new ThornsItemDefinition(
			"sniper_ammo",
			"Sniper Ammo",
			60,
			ThornsItemType.Ammo,
			AmmoTypeId: "sniper_ammo",
			HudIconTexture: "textures/ui/item_icons/sniper_ammo.png" ),
		["apple"] = new ThornsItemDefinition(
			"apple",
			"Apple",
			50,
			ThornsItemType.Consumable,
			ConsumableKind: ThornsConsumableKind.Food,
			HungerRestore: 18f,
			UseTimeSeconds: 0.5f,
			HudIconTexture: "textures/ui/item_icons/apple.png" ),
		["water"] = new ThornsItemDefinition(
			"water",
			"Water",
			20,
			ThornsItemType.Consumable,
			ConsumableKind: ThornsConsumableKind.WaterClean,
			ThirstRestore: 35f,
			UseTimeSeconds: 0.45f,
			HudIconTexture: "textures/ui/item_icons/water_bottle.png" ),
		["bandage"] = new ThornsItemDefinition(
			"bandage",
			"Bandage",
			20,
			ThornsItemType.Consumable,
			ConsumableKind: ThornsConsumableKind.Medical,
			HealthRestore: 28f,
			UseTimeSeconds: 0.65f,
			HudIconTexture: "textures/ui/item_icons/bandage.png" ),
		["medkit_field"] = new ThornsItemDefinition(
			"medkit_field",
			"Field Medkit",
			8,
			ThornsItemType.Consumable,
			ViewModelAsset: "models/tools/medkit.vmdl",
			WorldModelAsset: "models/tools/medkit.vmdl",
			ConsumableKind: ThornsConsumableKind.Medical,
			HealthRestore: 52f,
			UseTimeSeconds: 1.15f,
			HudIconTexture: "textures/ui/item_icons/medkit.png",
			FpViewmodelRootLocalOffset: FpMedkitViewmodelRootOffset,
			FpViewmodelRootLocalScale: FpViewmodelRootLocalScaleOne ),
		["morphine_pen"] = new ThornsItemDefinition(
			"morphine_pen",
			"Morphine Pen",
			4,
			ThornsItemType.Consumable,
			ConsumableKind: ThornsConsumableKind.Medical,
			HealthRestore: 78f,
			PoisonAmount: 10f,
			UseTimeSeconds: 0.4f,
			HudIconTexture: "textures/ui/item_icons/morphine_pen.png" ),
		[ThornsC4.ItemId] = new ThornsItemDefinition(
			ThornsC4.ItemId,
			"C4",
			6,
			ThornsItemType.Consumable,
			ViewModelAsset: ThornsC4.ModelPath,
			WorldModelAsset: ThornsC4.ModelPath,
			ConsumableKind: ThornsConsumableKind.Explosive,
			HudIconTexture: "textures/ui/item_icons/c4.png",
			FpViewmodelRootLocalOffset: ThornsC4.FpViewmodelRootLocalOffset,
			FpViewmodelRootLocalScale: new Vector3( ThornsC4.FpViewmodelScale ) ),
		["ration_pack"] = new ThornsItemDefinition(
			"ration_pack",
			"Ration Pack",
			15,
			ThornsItemType.Consumable,
			ConsumableKind: ThornsConsumableKind.Food,
			HungerRestore: 40f,
			ThirstRestore: 10f,
			UseTimeSeconds: 1f,
			HudIconTexture: "textures/ui/item_icons/field_rations.png" ),
		["electrolyte_drink"] = new ThornsItemDefinition(
			"electrolyte_drink",
			"Electrolyte Drink",
			12,
			ThornsItemType.Consumable,
			ConsumableKind: ThornsConsumableKind.WaterClean,
			ThirstRestore: 44f,
			HungerRestore: 6f,
			UseTimeSeconds: 0.5f,
			HudIconTexture: "textures/ui/item_icons/electrolytes.png" ),
		["canned_stew"] = new ThornsItemDefinition(
			"canned_stew",
			"Canned Stew",
			12,
			ThornsItemType.Consumable,
			ConsumableKind: ThornsConsumableKind.Food,
			HungerRestore: 46f,
			UseTimeSeconds: 0.85f,
			HudIconTexture: "textures/ui/item_icons/canned_stew.png" ),
		["field_rations"] = new ThornsItemDefinition(
			"field_rations",
			"Field Rations",
			10,
			ThornsItemType.Consumable,
			ConsumableKind: ThornsConsumableKind.Food,
			HungerRestore: 36f,
			ThirstRestore: 16f,
			UseTimeSeconds: 0.9f,
			HudIconTexture: "textures/ui/item_icons/field_rations.png" ),
		["raw_meat"] = new ThornsItemDefinition(
			"raw_meat",
			"Raw Meat",
			40,
			ThornsItemType.Consumable,
			ConsumableKind: ThornsConsumableKind.Food,
			HungerRestore: 22f,
			PoisonAmount: 6f,
			UseTimeSeconds: 0.65f,
			HudIconTexture: "textures/ui/item_icons/raw_meat.png" ),
		["cooked_meat"] = new ThornsItemDefinition(
			"cooked_meat",
			"Cooked Meat",
			40,
			ThornsItemType.Consumable,
			ConsumableKind: ThornsConsumableKind.Food,
			HungerRestore: 52f,
			UseTimeSeconds: 0.55f,
			HudIconTexture: "textures/ui/item_icons/raw_meat.png" ),
		["dirty_water"] = new ThornsItemDefinition(
			"dirty_water",
			"Dirty Water",
			20,
			ThornsItemType.Consumable,
			ConsumableKind: ThornsConsumableKind.WaterDirty,
			ThirstRestore: 18f,
			PoisonAmount: 8f,
			UseTimeSeconds: 0.45f,
			HudIconTexture: "textures/ui/item_icons/water_bottle.png" ),
		["kevlar_helmet"] = new ThornsItemDefinition(
			"kevlar_helmet",
			"Kevlar Helmet",
			1,
			ThornsItemType.Armor,
			ArmorSlot: ThornsArmorSlotKind.Helmet,
			ArmorDamageReductionPercent: 14f,
			ArmorMaxDurability: 140f,
			HudIconTexture: "textures/ui/item_icons/kevlar_helmet.png" ),
		["kevlar_chest"] = new ThornsItemDefinition(
			"kevlar_chest",
			"Kevlar Chest",
			1,
			ThornsItemType.Armor,
			ArmorSlot: ThornsArmorSlotKind.Chest,
			ArmorDamageReductionPercent: 21f,
			ArmorMaxDurability: 175f,
			HudIconTexture: "textures/ui/item_icons/kevlar_vest.png" ),
		["kevlar_pants"] = new ThornsItemDefinition(
			"kevlar_pants",
			"Kevlar Pants",
			1,
			ThornsItemType.Armor,
			ArmorSlot: ThornsArmorSlotKind.Pants,
			ArmorDamageReductionPercent: 12f,
			ArmorMaxDurability: 140f,
			HudIconTexture: "textures/ui/item_icons/kevlar_pants.png" ),
		["scrap_helmet"] = new ThornsItemDefinition(
			"scrap_helmet",
			"Makeshift Helmet",
			1,
			ThornsItemType.Armor,
			ArmorSlot: ThornsArmorSlotKind.Helmet,
			ArmorDamageReductionPercent: 9f,
			ArmorMaxDurability: 95f,
			HudIconTexture: "textures/ui/item_icons/makeshift_helmet.png" ),
		["scrap_chest"] = new ThornsItemDefinition(
			"scrap_chest",
			"Makeshift Rig",
			1,
			ThornsItemType.Armor,
			ArmorSlot: ThornsArmorSlotKind.Chest,
			ArmorDamageReductionPercent: 14f,
			ArmorMaxDurability: 115f,
			HudIconTexture: "textures/ui/item_icons/makeshift_vest.png" ),
		["scrap_pants"] = new ThornsItemDefinition(
			"scrap_pants",
			"Makeshift Pants",
			1,
			ThornsItemType.Armor,
			ArmorSlot: ThornsArmorSlotKind.Pants,
			ArmorDamageReductionPercent: 7f,
			ArmorMaxDurability: 95f,
			HudIconTexture: "textures/ui/item_icons/makeshift_pants.png" ),
	};

	static bool _mergedLateBoundWeapons;

	/// <summary>
	/// s&amp;box sometimes runs a cached <c>thorns.dll</c> whose static field initializer predates newer dictionary keys.
	/// Merge missing rows once so <see cref="ThornsInventory.ServerAddItem"/> and dev loadouts still resolve (weapons + starter tool).
	/// </summary>
	static void MergeLateBoundWeaponDefinitionsIfMissing()
	{
		if ( _mergedLateBoundWeapons )
			return;

		_mergedLateBoundWeapons = true;

		_byId.TryAdd( "primitive_tool", PrimitiveToolDefinition ); // stale-DLL bootstrap; <see cref="EnsurePrimitiveToolDefinitionOnEveryLookup"/> overwrites when needed

		_byId.TryAdd(
			"mp5",
			new ThornsItemDefinition(
				"mp5",
				"MP5",
				1,
				ThornsItemType.Weapon,
				CombatWeaponDefinitionId: "mp5",
				ViewModelAsset: ThornsViewModelController.Mp5FirstPersonViewmodelPath,
				WorldModelAsset: ThornsViewModelController.Mp5WorldModelPath,
				HudIconTexture: "textures/ui/item_icons/mp5.png" ) );

		_byId.TryAdd(
			"shotgun",
			new ThornsItemDefinition(
				"shotgun",
				"Shotgun",
				1,
				ThornsItemType.Weapon,
				CombatWeaponDefinitionId: "shotgun",
				ViewModelAsset: ThornsViewModelController.ShotgunFirstPersonViewmodelPath,
				WorldModelAsset: ThornsViewModelController.ShotgunWorldModelPath,
				HudIconTexture: "textures/ui/item_icons/shotgun.png" ) );

		_byId.TryAdd(
			"sniper",
			new ThornsItemDefinition(
				"sniper",
				"Sniper",
				1,
				ThornsItemType.Weapon,
				CombatWeaponDefinitionId: "sniper",
				ViewModelAsset: ThornsViewModelController.SniperFirstPersonViewmodelPath,
				WorldModelAsset: ThornsViewModelController.SniperWorldModelPath,
				HudIconTexture: "textures/ui/item_icons/sniper.png" ) );

		_byId.TryAdd(
			"m9_bayonet",
			new ThornsItemDefinition(
				"m9_bayonet",
				"M9 Bayonet",
				1,
				ThornsItemType.Weapon,
				CombatWeaponDefinitionId: "m9_bayonet",
				ViewModelAsset: ThornsViewModelController.BayonetM9FirstPersonViewmodelPath,
				WorldModelAsset: ThornsViewModelController.BayonetM9WorldModelPath,
				HudIconTexture: "textures/ui/item_icons/m9_bayonet.png" ) );

	}

	/// <summary>
	/// Hot reload / cached DLL can leave an older <see cref="ThornsItemDefinition"/> instance in <c>_byId</c> (e.g. old HUD icon path) — keep the row the same reference as <see cref="PrimitiveToolDefinition"/>.
	/// </summary>
	static void EnsurePrimitiveToolDefinitionOnEveryLookup()
	{
		if ( !_byId.TryGetValue( "primitive_tool", out var existing ) || !ReferenceEquals( existing, PrimitiveToolDefinition ) )
			_byId["primitive_tool"] = PrimitiveToolDefinition;
	}

	static void RegisterPlaceableFurnitureItemsFromCatalog()
	{
		EnsureCanonicalItemRow( StorageChestKitDefinition );
		EnsureCanonicalItemRow( CampfireKitDefinition );
		EnsureCanonicalItemRow( WorkbenchKitDefinition );
		EnsureCanonicalItemRow( BedKitDefinition );

		foreach ( var entry in ThornsPlaceableFurnitureCatalog.All )
		{
			if ( !entry.AllowPlayerKitPlacement || string.IsNullOrEmpty( entry.KitItemId ) )
				continue;

			if ( entry.KitItemId is "storage_chest_kit" or "campfire_kit" or "workbench_kit" or "bed_kit" )
				continue;

			_byId[entry.KitItemId] = new ThornsItemDefinition(
				entry.KitItemId,
				ThornsPlaceableFurnitureCatalog.FormatDisplayName( entry.StructureDefId ),
				5,
				ThornsItemType.Resource,
				HudIconTexture: PlaceableKitHudIconPath( entry.StructureDefId ) );
		}
	}

	static void EnsurePlaceableKitDefinitionsOnEveryLookup() => RegisterPlaceableFurnitureItemsFromCatalog();

	static string PlaceableKitHudIconPath( string structureDefId ) =>
		structureDefId switch
		{
			"storage_chest" => "textures/ui/item_icons/player_chest.png",
			"campfire" => "textures/ui/item_icons/campfire.png",
			"workbench" => "textures/ui/item_icons/workbench.png",
			"bed" => "textures/ui/item_icons/bed.png",
			_ => "textures/ui/item_icons/wood.png"
		};

	static void EnsureCanonicalItemRow( ThornsItemDefinition canonical )
	{
		if ( !_byId.TryGetValue( canonical.Id, out var existing ) || !ReferenceEquals( existing, canonical ) )
			_byId[canonical.Id] = canonical;
	}

	/// <summary>UI-facing label — strips legacy " Kit" suffix and formats unknown ids.</summary>
	public static string ResolveDisplayName( string itemId )
	{
		if ( string.IsNullOrWhiteSpace( itemId ) )
			return "";

		if ( TryGet( itemId, out var def ) && !string.IsNullOrWhiteSpace( def.DisplayName ) )
			return StripKitSuffix( def.DisplayName );

		return StripKitSuffix( FormatItemIdFallback( itemId.Trim() ) );
	}

	static string StripKitSuffix( string name )
	{
		if ( string.IsNullOrWhiteSpace( name ) )
			return "";
		if ( name.EndsWith( " Kit", StringComparison.OrdinalIgnoreCase ) )
			return name[..^4].TrimEnd();
		return name;
	}

	static string FormatItemIdFallback( string itemId )
	{
		if ( ThornsPlaceableFurnitureCatalog.TryGetKit( itemId, out var placeable ) )
			return ThornsPlaceableFurnitureCatalog.FormatDisplayName( placeable.StructureDefId );

		if ( itemId.EndsWith( "_kit", StringComparison.OrdinalIgnoreCase ) )
		{
			var structureId = itemId[..^4];
			if ( ThornsPlaceableFurnitureCatalog.TryGet( structureId, out _ ) )
				return ThornsPlaceableFurnitureCatalog.FormatDisplayName( structureId );
		}

		return ThornsPlaceableFurnitureCatalog.FormatDisplayName( itemId );
	}

	public static bool TryGet( string itemId, out ThornsItemDefinition def )
	{
		def = default;
		if ( string.IsNullOrWhiteSpace( itemId ) )
			return false;

		itemId = itemId.Trim();
		MergeLateBoundWeaponDefinitionsIfMissing();
		EnsurePrimitiveToolDefinitionOnEveryLookup();
		EnsurePlaceableKitDefinitionsOnEveryLookup();

		if ( string.Equals( itemId, "primitive_tool", StringComparison.OrdinalIgnoreCase ) )
		{
			def = PrimitiveToolDefinition;
			return true;
		}

		return _byId.TryGetValue( itemId, out def );
	}

	public static ThornsItemDefinition GetOrNull( string itemId )
	{
		return TryGet( itemId, out var d ) ? d : null;
	}

	/// <summary>Hotbar placeables crafted from [Tab] — internal ids still end with <c>_kit</c>.</summary>
	public static bool IsPlaceableKitItem( string itemId ) =>
		ThornsPlaceableFurnitureCatalog.IsPortableKitItemId( itemId );

	/// <summary>Record default for <see cref="ThornsItemDefinition.FpViewmodelRootLocalScale"/> is zero — treat as uniform one.</summary>
	public static Vector3 ResolveFpViewmodelRootScale( Vector3 scale ) =>
		scale.LengthSquared < 1e-8f ? FpViewmodelRootLocalScaleOne : scale;

	public static bool IsHarvestToolViewModelPath( string modelPath )
	{
		if ( string.IsNullOrWhiteSpace( modelPath ) )
			return false;

		return modelPath.Trim().Replace( '\\', '/' )
			.StartsWith( "models/tools/", StringComparison.OrdinalIgnoreCase );
	}

	public static bool UsesHarvestAxeOrPickaxeFpPose( in ThornsItemDefinition def ) =>
		def.HarvestToolKind is ThornsHarvestToolKind.Axe or ThornsHarvestToolKind.Pickaxe;

	public static Vector3 ComposeFpHarvestToolViewmodelOffset( in ThornsItemDefinition def )
	{
		if ( UsesHarvestAxeOrPickaxeFpPose( in def ) )
			return FpHarvestAxePickaxeViewmodelRootOffset;

		return def.FpViewmodelRootLocalOffset;
	}

	public static Vector3 ResolveFpHarvestToolViewmodelEulerDegrees( in ThornsItemDefinition def )
	{
		if ( string.Equals( def.Id, "stone_pick", StringComparison.OrdinalIgnoreCase ) )
			return FpHarvestStonePickaxeViewmodelRootEulerDegrees;

		return def.HarvestToolKind switch
		{
			ThornsHarvestToolKind.Axe => FpHarvestAxeViewmodelRootEulerDegrees,
			ThornsHarvestToolKind.Pickaxe => FpHarvestPickaxeViewmodelRootEulerDegrees,
			_ => def.FpViewmodelRootLocalEulerDegrees
		};
	}

	/// <summary>Axes/pickaxes: registry scale × <see cref="FpHarvestToolViewmodelRootScale"/> (skips gun 10×).</summary>
	public static Vector3 ResolveFpHarvestToolViewmodelScale( in ThornsItemDefinition def )
	{
		var baseScale = ResolveFpViewmodelRootScale( def.FpViewmodelRootLocalScale );
		return UsesHarvestAxeOrPickaxeFpPose( in def )
			? baseScale * FpHarvestToolViewmodelRootScale.x
			: baseScale;
	}
}
