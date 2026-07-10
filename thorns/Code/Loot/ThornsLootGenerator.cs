using System;
using System.Collections.Generic;

namespace Sandbox;

/// <summary>Host-only RNG loot composition for <see cref="ThornsLootCrate"/>.</summary>
public static class ThornsLootGenerator
{
	const int LootSlots = ThornsLootCrate.LootGridSlots;

	public static ThornsLootCrateKind PickRandomKind( Random rng )
	{
		var u = rng.NextDouble();
		if ( u < 0.13 ) return ThornsLootCrateKind.Medical;
		if ( u < 0.24 ) return ThornsLootCrateKind.Weapons;
		if ( u < 0.35 ) return ThornsLootCrateKind.Armor;
		if ( u < 0.48 ) return ThornsLootCrateKind.Provisions;
		if ( u < 0.62 ) return ThornsLootCrateKind.MilitaryMixed;
		if ( u < 0.74 ) return ThornsLootCrateKind.IndustrialScrap;
		if ( u < 0.86 ) return ThornsLootCrateKind.SalvageComponents;
		return ThornsLootCrateKind.HunterCache;
	}

	/// <summary>
	/// Procedural building interior crates: metal favors weapons / armor / military; stone is between wood and metal; wood is lower on those three.
	/// Weights are relative frequencies (Medical, Weapons, Armor, Provisions, MilitaryMixed, IndustrialScrap, SalvageComponents, HunterCache).
	/// </summary>
	public static ThornsLootCrateKind PickRandomKindForProceduralBuilding( Random rng, int buildingMaterialTier )
	{
		var tier = (ThornsBuildingMaterialTier)Math.Clamp( buildingMaterialTier, 0, 2 );
		ReadOnlySpan<float> w = tier switch
		{
			ThornsBuildingMaterialTier.Metal => InteriorWeightsMetal,
			ThornsBuildingMaterialTier.Stone => InteriorWeightsStone,
			_ => InteriorWeightsWood
		};

		return SampleWeightedCrateKind( rng, w );
	}

	/// <summary>Interior proc buildings — weighted by <see cref="ThornsProcBuildingType"/>.</summary>
	public static ThornsLootCrateKind PickKindForProcBuilding( ThornsProcBuildingType buildingType, Random rng ) =>
		ThornsProcBuildingLootAffinity.PickCrateKind( buildingType, rng );

	static ThornsLootCrateKind SampleWeightedCrateKind( Random rng, ReadOnlySpan<float> w )
	{
		var sum = 0f;
		for ( var i = 0; i < w.Length; i++ )
			sum += w[i];

		var t = (float)rng.NextDouble() * sum;
		var acc = 0f;
		for ( var i = 0; i < w.Length; i++ )
		{
			acc += w[i];
			if ( t <= acc )
				return InteriorCrateKindOrder[i];
		}

		return InteriorCrateKindOrder[InteriorCrateKindOrder.Length - 1];
	}

	public static readonly ThornsLootCrateKind[] InteriorCrateKindOrder = new ThornsLootCrateKind[]
	{
		ThornsLootCrateKind.Medical,
		ThornsLootCrateKind.Weapons,
		ThornsLootCrateKind.Armor,
		ThornsLootCrateKind.Provisions,
		ThornsLootCrateKind.MilitaryMixed,
		ThornsLootCrateKind.IndustrialScrap,
		ThornsLootCrateKind.SalvageComponents,
		ThornsLootCrateKind.HunterCache
	};

	static readonly float[] InteriorWeightsWood = { 15f, 7f, 7f, 14f, 9f, 14f, 13f, 21f };

	static readonly float[] InteriorWeightsStone = { 12f, 11f, 11f, 13f, 13f, 12f, 12f, 16f };

	static readonly float[] InteriorWeightsMetal = { 9f, 15f, 15f, 10f, 17f, 10f, 10f, 14f };

	public static ThornsInventorySlot[] GenerateLootGrid( ThornsLootCrateKind kind, Random rng )
	{
		var grid = new ThornsInventorySlot[LootSlots];
		var maxDistinct = kind == ThornsLootCrateKind.Weapons
			? 1
			: kind == ThornsLootCrateKind.AirdropPremium
				? 2 + rng.Next( 2 )
				: 1 + rng.Next( 2 );

		var usedIds = new HashSet<string>( StringComparer.OrdinalIgnoreCase );
		var slotWrite = 0;

		for ( var attempt = 0; attempt < 56 && slotWrite < maxDistinct && slotWrite < LootSlots; attempt++ )
		{
			var rarity = PickRarity( rng, kind );
			if ( !TryRollStack( rng, kind, rarity, out var stack ) )
				continue;
			if ( usedIds.Contains( stack.ItemId ) )
				continue;

			usedIds.Add( stack.ItemId );
			grid[slotWrite++] = stack;
		}

		return grid;
	}

	/// <summary>Bias Rare+ for dynamic supply drops (single contested crate).</summary>
	static ThornsLootRarity PickRarityAirdrop( Random rng )
	{
		var u = rng.NextDouble() * 100.0;
		if ( u < 4.0 )
			return ThornsLootRarity.Common;
		if ( u < 14.0 )
			return ThornsLootRarity.Uncommon;
		if ( u < 44.0 )
			return ThornsLootRarity.Rare;
		if ( u < 78.0 )
			return ThornsLootRarity.Epic;
		return ThornsLootRarity.Legendary;
	}

	static ThornsLootRarity PickRarity( Random rng, ThornsLootCrateKind kind )
	{
		if ( kind == ThornsLootCrateKind.AirdropPremium )
			return PickRarityAirdrop( rng );

		var roll = rng.NextDouble() * WeightSum( kind );
		var c = CommonWeight( kind );
		if ( roll < c )
			return ThornsLootRarity.Common;
		roll -= c;

		var u = UncommonWeight( kind );
		if ( roll < u )
			return ThornsLootRarity.Uncommon;
		roll -= u;

		var r = RareWeight( kind );
		if ( roll < r )
			return ThornsLootRarity.Rare;
		roll -= r;

		var e = EpicWeight( kind );
		if ( roll < e )
			return ThornsLootRarity.Epic;
		return ThornsLootRarity.Legendary;
	}

	static float WeightSum( ThornsLootCrateKind k ) =>
		CommonWeight( k ) + UncommonWeight( k ) + RareWeight( k ) + EpicWeight( k ) + LegendaryWeight( k );

	static float CommonWeight( ThornsLootCrateKind k ) =>
		k switch
		{
			ThornsLootCrateKind.Medical => 55f,
			ThornsLootCrateKind.Provisions => 52f,
			ThornsLootCrateKind.IndustrialScrap => 48f,
			ThornsLootCrateKind.MilitaryMixed => 35f,
			ThornsLootCrateKind.Ammo => 38f,
			ThornsLootCrateKind.SalvageComponents => 42f,
			ThornsLootCrateKind.HunterCache => 44f,
			_ => 30f
		};

	static float UncommonWeight( ThornsLootCrateKind k ) =>
		k switch
		{
			ThornsLootCrateKind.Medical => 30f,
			ThornsLootCrateKind.Weapons => 32f,
			ThornsLootCrateKind.SalvageComponents => 30f,
			ThornsLootCrateKind.HunterCache => 32f,
			_ => 28f
		};

	static float RareWeight( ThornsLootCrateKind k ) =>
		k switch
		{
			ThornsLootCrateKind.Weapons => 24f,
			ThornsLootCrateKind.Armor => 26f,
			ThornsLootCrateKind.MilitaryMixed => 28f,
			ThornsLootCrateKind.SalvageComponents => 20f,
			_ => 15f
		};

	static float EpicWeight( ThornsLootCrateKind k ) =>
		k switch
		{
			ThornsLootCrateKind.Weapons => 10f,
			ThornsLootCrateKind.Armor => 12f,
			ThornsLootCrateKind.MilitaryMixed => 15f,
			ThornsLootCrateKind.SalvageComponents => 14f,
			_ => 4f
		};

	static float LegendaryWeight( ThornsLootCrateKind k ) =>
		k switch
		{
			ThornsLootCrateKind.Weapons => 4f,
			ThornsLootCrateKind.Armor => 4f,
			ThornsLootCrateKind.MilitaryMixed => 7f,
			ThornsLootCrateKind.SalvageComponents => 8f,
			_ => 1f
		};

	static bool TryRollStack( Random rng, ThornsLootCrateKind kind, ThornsLootRarity rarity, out ThornsInventorySlot stack )
	{
		stack = default;
		if ( !PickItemId( rng, kind, out var itemId, out var qMin, out var qMax ) )
			return false;

		if ( !ThornsItemRegistry.TryGet( itemId, out var def ) )
			return false;

		var qty = Math.Max( 1, rng.Next( qMin, qMax + 1 ) );
		qty = Math.Min( qty, def.MaxStack );

		switch ( def.ItemType )
		{
			case ThornsItemType.Weapon:
			{
				var combatId = string.IsNullOrEmpty( def.CombatWeaponDefinitionId ) ? def.Id : def.CombatWeaponDefinitionId;
				var wdef = ThornsWeaponDefinitions.Get( combatId );

				var (dmg, fr) = ThornsGearRoll.RollWeaponMultipliers( rng, rarity );
				var melee = ThornsWeaponDefinitions.TreatsAsMeleeWeapon( wdef, combatId );
				stack = new ThornsInventorySlot
				{
					ItemId = def.Id,
					Quantity = 1,
					HasDurability = true,
					Durability = wdef.MaxDurability,
					WeaponInstanceId = Guid.NewGuid().ToString( "D" ),
					WeaponLoadedAmmo = melee ? 0 : wdef.ClipSize,
					WeaponRollPayload = ThornsGearRoll.EncodeWeapon( rarity, dmg, fr ),
					ArmorRollPayload = ""
				};
				return true;
			}
			case ThornsItemType.Armor:
			{
				var drMul = ThornsGearRoll.RollArmorDrMultiplier( rng, rarity );
				stack = new ThornsInventorySlot
				{
					ItemId = def.Id,
					Quantity = 1,
					HasDurability = true,
					Durability = def.ArmorMaxDurability,
					WeaponRollPayload = "",
					ArmorRollPayload = ThornsGearRoll.EncodeArmor( rarity, drMul )
				};
				return true;
			}
			case ThornsItemType.Tool:
			{
				if ( def.ToolMaxDurability <= 0.001f )
					return false;

				stack = new ThornsInventorySlot
				{
					ItemId = def.Id,
					Quantity = Math.Min( qty, def.MaxStack ),
					HasDurability = true,
					Durability = def.ToolMaxDurability,
					WeaponRollPayload = "",
					ArmorRollPayload = ""
				};
				return true;
			}
			default:
			{
				if ( def.ItemType == ThornsItemType.Ammo )
				{
					qty = RollAmmoQuantity( rng, kind, def.Id );
				}
				else if ( def.ItemType != ThornsItemType.Weapon && def.ItemType != ThornsItemType.Armor )
				{
					qty = kind == ThornsLootCrateKind.AirdropPremium
						? rng.Next( 2, 5 )
						: rng.Next( 1, 3 );
				}

				qty = Math.Min( qty, def.MaxStack );
				stack = new ThornsInventorySlot
				{
					ItemId = def.Id,
					Quantity = qty,
					HasDurability = false,
					Durability = 0f,
					WeaponRollPayload = "",
					ArmorRollPayload = ""
				};
				return true;
			}
		}
	}

	static int RollAmmoQuantity( Random rng, ThornsLootCrateKind kind, string itemId )
	{
		var (min, max) = itemId switch
		{
			"pistol_ammo" => kind switch
			{
				ThornsLootCrateKind.AirdropPremium => (36, 60),
				ThornsLootCrateKind.MilitaryMixed or ThornsLootCrateKind.Ammo => (18, 32),
				_ => (10, 20)
			},
			"smg_ammo" => kind switch
			{
				ThornsLootCrateKind.AirdropPremium => (45, 75),
				ThornsLootCrateKind.MilitaryMixed or ThornsLootCrateKind.Ammo => (20, 36),
				_ => (12, 24)
			},
			"rifle_ammo" => kind switch
			{
				ThornsLootCrateKind.AirdropPremium => (32, 54),
				ThornsLootCrateKind.MilitaryMixed or ThornsLootCrateKind.Ammo => (16, 30),
				_ => (10, 20)
			},
			"shotgun_ammo" => kind switch
			{
				ThornsLootCrateKind.AirdropPremium => (16, 28),
				ThornsLootCrateKind.MilitaryMixed or ThornsLootCrateKind.Ammo => (8, 16),
				_ => (6, 12)
			},
			"sniper_ammo" => kind switch
			{
				ThornsLootCrateKind.AirdropPremium => (8, 16),
				ThornsLootCrateKind.MilitaryMixed or ThornsLootCrateKind.Ammo => (4, 10),
				_ => (3, 7)
			},
			_ => kind switch
			{
				ThornsLootCrateKind.AirdropPremium => (28, 48),
				ThornsLootCrateKind.MilitaryMixed or ThornsLootCrateKind.Ammo => (12, 24),
				_ => (9, 18)
			}
		};

		return rng.Next( min, max + 1 );
	}

	static bool PickItemId( Random rng, ThornsLootCrateKind kind, out string itemId, out int qtyMin, out int qtyMax )
	{
		switch ( kind )
		{
			case ThornsLootCrateKind.Medical:
				return PickWeighted( rng, out itemId, out qtyMin, out qtyMax,
					("bandage", 1, 2, 34),
					("medkit_field", 1, 2, 14),
					("morphine_pen", 1, 2, 6),
					("apple", 1, 2, 22),
					("water", 1, 2, 20) );
			case ThornsLootCrateKind.Weapons:
				return PickWeighted( rng, out itemId, out qtyMin, out qtyMax,
					("m4", 1, 1, 22),
					("mp5", 1, 1, 20),
					("shotgun", 1, 1, 16),
					("sniper", 1, 1, 8),
					("m9_bayonet", 1, 1, 34),
					(ThornsC4.ItemId, 1, 1, 6) );
			case ThornsLootCrateKind.Armor:
				return PickWeighted( rng, out itemId, out qtyMin, out qtyMax,
					("scrap_helmet", 1, 1, 26),
					("scrap_chest", 1, 1, 26),
					("scrap_pants", 1, 1, 26),
					("kevlar_helmet", 1, 1, 20),
					("kevlar_chest", 1, 1, 20),
					("kevlar_pants", 1, 1, 20),
					("cloth", 1, 2, 4) );
			case ThornsLootCrateKind.Provisions:
				return PickWeighted( rng, out itemId, out qtyMin, out qtyMax,
					("apple", 1, 2, 22),
					("water", 1, 2, 22),
					("ration_pack", 1, 2, 18),
					("electrolyte_drink", 1, 2, 18),
					("canned_stew", 1, 2, 18),
					("raw_meat", 1, 2, 12) );
			case ThornsLootCrateKind.Ammo:
				return PickWeighted( rng, out itemId, out qtyMin, out qtyMax,
					("pistol_ammo", 1, 2, 18),
					("smg_ammo", 1, 2, 16),
					("rifle_ammo", 1, 2, 14),
					("shotgun_ammo", 1, 2, 12),
					("sniper_ammo", 1, 2, 10) );
			case ThornsLootCrateKind.MilitaryMixed:
				return PickWeighted( rng, out itemId, out qtyMin, out qtyMax,
					("pistol_ammo", 1, 2, 4),
					("rifle_ammo", 1, 2, 10),
					("smg_ammo", 1, 2, 9),
					("shotgun_ammo", 1, 2, 5),
					("sniper_ammo", 1, 2, 4),
					("m4", 1, 1, 10),
					("mp5", 1, 1, 10),
					("bandage", 1, 2, 14),
					("kevlar_chest", 1, 1, 8),
					("kevlar_helmet", 1, 1, 8),
					("field_rations", 1, 2, 12),
					("metal_ore", 1, 2, 10),
					(ThornsC4.ItemId, 1, 1, 8) );
			case ThornsLootCrateKind.AirdropPremium:
				return PickWeighted( rng, out itemId, out qtyMin, out qtyMax,
					("pistol_ammo", 1, 2, 3),
					("rifle_ammo", 1, 2, 7),
					("smg_ammo", 1, 2, 5),
					("shotgun_ammo", 1, 2, 4),
					("sniper_ammo", 1, 2, 4),
					("sniper", 1, 1, 14),
					("m4", 1, 1, 14),
					("mp5", 1, 1, 12),
					("shotgun", 1, 1, 10),
					("m9_bayonet", 1, 1, 10),
					("kevlar_chest", 1, 1, 18),
					("kevlar_helmet", 1, 1, 10),
					("kevlar_pants", 1, 1, 8),
					("medkit_field", 1, 2, 10),
					("morphine_pen", 1, 2, 8),
					("electrolyte_drink", 1, 2, 8),
					("ration_pack", 1, 2, 8),
					(ThornsC4.ItemId, 1, 1, 10) );
			case ThornsLootCrateKind.SalvageComponents:
				return PickWeighted( rng, out itemId, out qtyMin, out qtyMax,
					("metal_ore", 1, 2, 22),
					("cloth", 1, 2, 22),
					("stone", 1, 2, 18),
					("bone_fragments", 1, 2, 14),
					("metal", 1, 2, 12),
					("stone_hatchet", 1, 1, 8),
					("stone_pick", 1, 1, 8),
					("axe", 1, 1, 6),
					("pickaxe", 1, 1, 4) );
			case ThornsLootCrateKind.HunterCache:
				return PickWeighted( rng, out itemId, out qtyMin, out qtyMax,
					("raw_meat", 1, 2, 26),
					("animal_hide", 1, 2, 22),
					("bone_fragments", 1, 2, 18),
					("cloth", 1, 2, 28),
					("leather_scrap", 1, 2, 10) );
			case ThornsLootCrateKind.IndustrialScrap:
			default:
				return PickWeighted( rng, out itemId, out qtyMin, out qtyMax,
					("wood", 1, 2, 28),
					("stone", 1, 2, 18),
					("metal_ore", 1, 2, 14),
					("cloth", 1, 2, 34),
					("metal", 1, 2, 12),
					("stone_hatchet", 1, 1, 8),
					("stone_pick", 1, 1, 8) );
		}
	}

	static bool PickWeighted( Random rng, out string itemId, out int qtyMin, out int qtyMax,
		params (string id, int qMin, int qMax, int weight)[] rows )
	{
		var sum = 0;
		foreach ( var r in rows )
			sum += Math.Max( 0, r.weight );

		var pick = rng.Next( sum );
		foreach ( var r in rows )
		{
			var w = Math.Max( 0, r.weight );
			pick -= w;
			if ( pick < 0 )
			{
				itemId = r.id;
				qtyMin = r.qMin;
				qtyMax = r.qMax;
				return true;
			}
		}

		var last = rows[^1];
		itemId = last.id;
		qtyMin = last.qMin;
		qtyMax = last.qMax;
		return true;
	}

	/// <summary>Host: Scavenger bonus — rolls the standard rarity curve until a stack resolves.</summary>
	public static bool TryRollBonusStackForCrateKind( ThornsLootCrateKind kind, Random rng, out ThornsInventorySlot stack )
	{
		for ( var attempt = 0; attempt < 22; attempt++ )
		{
			var rarity = PickRarity( rng, kind );
			if ( TryRollStack( rng, kind, rarity, out stack ) )
				return true;
		}

		stack = default;
		return false;
	}
}
