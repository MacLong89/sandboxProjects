using System;
using System.Collections.Generic;

namespace Sandbox;

/// <summary>
/// Which interior placeables get proc loot vs player storage, and how proc loot maps to <see cref="ThornsLootCrateKind"/>.
/// </summary>
public static class ThornsFurnitureLootPolicy
{
	/// <summary>Seconds after a proc furniture container is fully emptied before it rerolls loot.</summary>
	public const float ProcLootRerollSeconds = ThornsLootCrate.WorldLootRegenSeconds;

	static readonly HashSet<string> NonLootDecor = new( StringComparer.OrdinalIgnoreCase )
	{
		"chair",
		"couch",
		"bed",
		"bunk",
		"radio"
	};

	static readonly HashSet<string> ProcLootFurniture = new( StringComparer.OrdinalIgnoreCase )
	{
		"desk",
		"cabinet",
		"fridge",
		"kitchen_fridge",
		"military_supply",
		"conference",
		"dining_table",
		"pallets",
		"retail"
	};

	static readonly HashSet<string> PlayerStorableFurniture = new( StringComparer.OrdinalIgnoreCase )
	{
		"cabinet",
		"fridge",
		"kitchen_fridge"
	};

	public static bool IsNonLootDecor( string structureDefId ) =>
		!string.IsNullOrWhiteSpace( structureDefId ) && NonLootDecor.Contains( structureDefId );

	public static bool IsProcLootFurniture( string structureDefId ) =>
		!string.IsNullOrWhiteSpace( structureDefId )
		&& ProcLootFurniture.Contains( structureDefId )
		&& !IsNonLootDecor( structureDefId );

	public static bool IsPlayerStorableFurniture( string structureDefId ) =>
		!string.IsNullOrWhiteSpace( structureDefId ) && PlayerStorableFurniture.Contains( structureDefId );

	/// <summary>Proc interior scatter only — world-gen loot stash with timed reroll.</summary>
	public static bool ShouldSpawnProcLootContainer( string structureDefId ) => IsProcLootFurniture( structureDefId );

	/// <summary>Player kit placement — persistent chest grid, no auto loot.</summary>
	public static bool ShouldSpawnPlayerStorageContainer( string structureDefId ) =>
		IsPlayerStorableFurniture( structureDefId );

	public static ThornsLootCrateKind PickProcLootKind(
		string structureDefId,
		ThornsProcBuildingType buildingType,
		Random rng )
	{
		var id = structureDefId?.ToLowerInvariant() ?? "";
		return id switch
		{
			"kitchen_fridge" or "fridge" => ThornsLootCrateKind.Provisions,
			"desk" or "cabinet" or "retail" or "conference" or "dining_table" =>
				ThornsLootGenerator.PickRandomKind( rng ),
			"pallets" => SampleKind(
				rng,
				ThornsLootCrateKind.IndustrialScrap,
				ThornsLootCrateKind.SalvageComponents ),
			"military_supply" => SampleKind(
				rng,
				ThornsLootCrateKind.Weapons,
				ThornsLootCrateKind.Armor,
				ThornsLootCrateKind.Ammo ),
			_ => ThornsLootGenerator.PickKindForProcBuilding( buildingType, rng )
		};
	}

	static ThornsLootCrateKind SampleKind( Random rng, params ThornsLootCrateKind[] kinds )
	{
		if ( kinds is null || kinds.Length == 0 )
			return ThornsLootCrateKind.Provisions;

		return kinds[rng.Next( kinds.Length )];
	}
}
