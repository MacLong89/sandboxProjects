namespace OffshoreFishing.Core;

public enum GameMode
{
	Dock,
	Travel,
	Fishing,
	Shop,
	CatchReveal,
	Paused
}

public enum FishingPhase
{
	Idle,
	Aiming,
	Casting,
	Waiting,
	BiteWindow,
	Fighting,
	Landing,
	Failed
}

public enum Rarity
{
	Common = 0,
	Uncommon = 1,
	Rare = 2,
	Epic = 3,
	Legendary = 4
}

public enum ItemCategory
{
	Rod,
	Spool,
	Hook,
	Bait,
	Boat,
	BoatUpgrade,
	HiredBoat,
	Consumable,
	Misc
}

public enum ObjectiveType
{
	CatchCount,
	CatchSpecies,
	EarnGold,
	ReachDistance,
	BuyItem,
	DiscoverSpecies,
	HireBoat
}
