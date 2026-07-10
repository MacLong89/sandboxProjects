namespace Sandbox;

/// <summary>
/// Spendable skill nodes — three trees × five ranks each (<see cref="ThornsUpgradeBalance"/>).
/// Ordinals 0–14 are RPC-stable.
/// </summary>
public enum ThornsUpgradeCategory
{
	// --- PERSISTENCE (survival / sustain) ---
	Hydration,
	IronGut,
	StrongStomach,
	Weathered,
	ThickHide,

	// --- INSTINCT (stealth / stamina / wilderness / combat survival) ---
	Endurance,
	Ghost,
	Beastmaster,
	Hardened,
	LuckyChamber,

	// --- INDUSTRY (scavenging / harvesting / durability / progression) ---
	Lumberjack,
	Miner,
	Scavenger,
	Reinforced,
	Technician,
}
