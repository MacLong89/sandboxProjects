using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Plunge;

public enum GameScreen { Hub, Dive, Results }
public enum HubTab { Diver, Submarine, DiveHistory }
public enum EntityKind { Fish, RareFish, Jelly, Shark, Chest, Crate, Artifact, Drone, Decor }

public sealed record GearDef(
	string Id, string Name, string Slot, int Tier, int Cost, int RequiredLevel,
	float Health = 0, float Oxygen = 0, float Speed = 0, float Carry = 0,
	float Resistance = 0, float Light = 0, float Damage = 0
);

public sealed record SubDef(
	string Id, string Name, int Cost, int RequiredLevel, float Depth,
	float Hull, float Oxygen, float Speed, float Cargo, float Sonar
);

public sealed record LootDef(
	string Id, string Name, int Value, int Xp, string Sprite, float MinDepth,
	string Rarity = "Common"
);

public static class Catalog
{
	public static readonly string[] Slots =
	{
		"Helmet", "Suit", "Tank", "Flippers", "Tools", "Weapon", "Light", "Charm"
	};

	public static readonly GearDef[] Gear =
	{
		new("helmet_1", "Standard Helmet", "Helmet", 1, 0, 1),
		new("helmet_2", "Reinforced Helmet", "Helmet", 2, 180, 2, Health: 20, Resistance: 5, Light: 10),
		new("helmet_3", "Advanced Helmet", "Helmet", 3, 450, 5, Health: 40, Resistance: 10, Light: 25),
		new("helmet_4", "Tech Helmet", "Helmet", 4, 1200, 10, Health: 70, Resistance: 18, Light: 50),
		new("suit_1", "Canvas Suit", "Suit", 1, 0, 1),
		new("suit_2", "Reinforced Suit", "Suit", 2, 220, 2, Health: 30, Carry: 2, Resistance: 8),
		new("suit_3", "Deep Suit", "Suit", 3, 600, 6, Health: 60, Carry: 4, Resistance: 15),
		new("suit_4", "Exo Suit", "Suit", 4, 1500, 12, Health: 100, Carry: 7, Resistance: 25),
		new("tank_1", "O₂ Tank Mk I", "Tank", 1, 0, 1),
		new("tank_2", "O₂ Tank Mk II", "Tank", 2, 130, 1, Oxygen: 40),
		new("tank_3", "O₂ Tank Mk III", "Tank", 3, 380, 4, Oxygen: 90),
		new("tank_4", "Closed-Cycle Tank", "Tank", 4, 1000, 9, Oxygen: 170),
		new("flippers_1", "Rubber Flippers", "Flippers", 1, 0, 1),
		new("flippers_2", "Swift Flippers", "Flippers", 2, 110, 1, Speed: 25),
		new("flippers_3", "Jet Flippers", "Flippers", 3, 360, 5, Speed: 55),
		new("flippers_4", "Pulse Fins", "Flippers", 4, 920, 11, Speed: 90),
		new("tools_1", "Utility Belt", "Tools", 1, 0, 1),
		new("tools_2", "Catch Net", "Tools", 2, 100, 1, Carry: 2),
		new("tools_3", "Grab Claw", "Tools", 3, 350, 4, Carry: 5),
		new("weapon_1", "Dive Knife", "Weapon", 1, 0, 1, Damage: 10),
		new("weapon_2", "Harpoon Gun", "Weapon", 2, 240, 3, Damage: 25),
		new("weapon_3", "Bolt Lance", "Weapon", 3, 700, 8, Damage: 50),
		new("light_1", "Hand Torch", "Light", 1, 0, 1),
		new("light_2", "Beam Lamp", "Light", 2, 140, 2, Light: 40),
		new("light_3", "Flare Array", "Light", 3, 420, 7, Light: 90),
		new("charm_1", "Sea Glass", "Charm", 1, 0, 1),
		new("charm_2", "Lucky Shell", "Charm", 2, 200, 3, Carry: 1),
		new("charm_3", "Pearl Charm", "Charm", 3, 550, 8, Carry: 2),
	};

	public static readonly SubDef[] Subs =
	{
		new("seeker", "Seeker I", 900, 5, 220, 120, 150, 145, 14, 160),
		new("explorer", "Explorer II", 3200, 8, 340, 170, 210, 130, 22, 200),
		new("hunter", "Hunter III", 7500, 14, 300, 150, 180, 210, 16, 250),
		new("pioneer", "Pioneer IV", 15000, 20, 480, 240, 290, 165, 32, 300),
		new("voyager", "Voyager V", 35000, 35, 700, 340, 400, 180, 48, 400)
	};

	public static readonly LootDef[] Fish =
	{
		new("amberfin", "Amberfin", 12, 5, "fish_common", 0),
		new("azure_dart", "Azure Dart", 28, 10, "fish_blue", 40, "Uncommon"),
		new("violet_ghost", "Violet Ghost", 95, 32, "fish_rare", 120, "Rare"),
		new("gilded_koi", "Gilded Koi", 240, 75, "fish_rare", 250, "Legendary")
	};

	public static readonly LootDef[] Artifacts =
	{
		new("tide_crystal", "Tide Crystal", 80, 30, "crystal", 45, "Uncommon"),
		new("brass_idol", "Brass Idol", 170, 60, "idol", 130, "Rare"),
		new("research_drone", "Research Drone", 300, 100, "drone", 180, "Story")
	};

	public static GearDef GearById(string id) => Gear.FirstOrDefault(x => x.Id == id);
	public static IEnumerable<GearDef> GearFor(string slot) => Gear.Where(x => x.Slot == slot);
	public static SubDef SubById(string id) => Subs.FirstOrDefault(x => x.Id == id);

	public static string ZoneAt(float depth) => depth switch
	{
		< 80 => "Sunlit Shallows",
		< 160 => "Coral Reef",
		< 280 => "Bluewater Cavern",
		_ => "Abyssal Trench"
	};

	public static string ZoneIdAt(float depth) => depth switch
	{
		< 80 => "shallows",
		< 160 => "reef",
		< 280 => "cavern",
		_ => "abyss"
	};
}

public sealed class DiverStats
{
	public float Health { get; set; } = 100;
	public float Oxygen { get; set; } = 100;
	public float Speed { get; set; } = 110;
	public float Carry { get; set; } = 8;
	public float Resistance { get; set; }
	public float Light { get; set; } = 120;
	public float Damage { get; set; } = 10;
	public float DepthRating => 90 + Resistance * 5 + Oxygen * 0.25f;
}

public sealed class HaulItem
{
	public string Id { get; set; }
	public string Name { get; set; }
	public int Value { get; set; }
	public int Xp { get; set; }
	public string Sprite { get; set; }
	public string Rarity { get; set; }
}

public sealed class DiveRecord
{
	public int Number { get; set; }
	public int Day { get; set; }
	public string Biome { get; set; }
	public string BiomeId { get; set; }
	public bool Success { get; set; }
	public float MaxDepth { get; set; }
	public float Duration { get; set; }
	public int Items { get; set; }
	public int Credits { get; set; }
	public int OxygenUsed { get; set; }
	public List<HaulItem> Haul { get; set; } = new();
	public List<string> Discoveries { get; set; } = new();
}
