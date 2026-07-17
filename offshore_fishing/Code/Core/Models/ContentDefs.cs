namespace OffshoreFishing.Core;

public sealed class GameContent
{
	public List<FishDef> Fish { get; set; } = new();
	public List<ZoneDef> Zones { get; set; } = new();
	public List<ItemDef> Items { get; set; } = new();
	public List<BoatDef> Boats { get; set; } = new();
	public List<HiredBoatDef> HiredBoats { get; set; } = new();
	public List<ObjectiveDef> Objectives { get; set; } = new();
	public EconomyDef Economy { get; set; } = new();
	public TutorialDef Tutorial { get; set; } = new();

	public FishDef GetFish( string id ) => Fish.First( f => f.Id == id );
	public ZoneDef GetZone( string id ) => Zones.First( z => z.Id == id );
	public ItemDef GetItem( string id ) => Items.First( i => i.Id == id );
	public BoatDef GetBoat( string id ) => Boats.First( b => b.Id == id );
	public HiredBoatDef GetHiredBoat( string id ) => HiredBoats.First( h => h.Id == id );

	public bool TryGetFish( string id, out FishDef fish )
	{
		fish = Fish.FirstOrDefault( f => f.Id == id );
		return fish != null;
	}

	public bool TryGetItem( string id, out ItemDef item )
	{
		item = Items.FirstOrDefault( i => i.Id == id );
		return item != null;
	}
}

public sealed class FishDef
{
	public string Id { get; set; }
	public string Name { get; set; }
	public string Description { get; set; }
	public string ZoneId { get; set; }
	public Rarity Rarity { get; set; }
	public float MinCm { get; set; }
	public float MaxCm { get; set; }
	public float MinKg { get; set; }
	public float MaxKg { get; set; }
	public int BaseValue { get; set; }
	public float SpawnWeight { get; set; } = 1f;
	public float MinDepth { get; set; }
	public float MaxDepth { get; set; }
	public float FightSpeed { get; set; } = 1f;
	public float FightStamina { get; set; } = 1f;
	public float SurgeChance { get; set; } = 0.2f;
	public float EscapePressure { get; set; } = 1f;
	public string SpriteId { get; set; }
	public string[] PreferredBait { get; set; } = Array.Empty<string>();
	public int RequiredRodTier { get; set; }
	public int RequiredHookTier { get; set; }
}

public sealed class ZoneDef
{
	public string Id { get; set; }
	public string Name { get; set; }
	public string Description { get; set; }
	public float MinDistanceM { get; set; }
	public float MaxDistanceM { get; set; }
	public float WaterTempC { get; set; }
	public int UnlockOrder { get; set; }
	public int RequiredBoatTier { get; set; }
	public float RequiredRangeM { get; set; }
	public string BackgroundId { get; set; }
	public string[] FishIds { get; set; } = Array.Empty<string>();
}

public sealed class ItemDef
{
	public string Id { get; set; }
	public string Name { get; set; }
	public string Description { get; set; }
	public ItemCategory Category { get; set; }
	public int Tier { get; set; }
	public int Price { get; set; }
	public int StackLimit { get; set; } = 99;
	public bool Consumable { get; set; }
	public float CastPower { get; set; }
	public float LineStrength { get; set; }
	public float ReelSpeed { get; set; }
	public float HookPower { get; set; }
	public float BiteBonus { get; set; }
	public float ValueBonus { get; set; }
	public float RarityBonus { get; set; }
	public string IconId { get; set; }
	public string UnlockAfterItemId { get; set; }
}

public sealed class BoatDef
{
	public string Id { get; set; }
	public string Name { get; set; }
	public string Description { get; set; }
	public int Tier { get; set; }
	public int Price { get; set; }
	public float Speed { get; set; }
	public float MaxDepthM { get; set; }
	public float MaxRangeM { get; set; }
	public float GasCapacityL { get; set; }
	public int StorageSlots { get; set; }
	public string SpriteId { get; set; }
	public string UnlockAfterBoatId { get; set; }
}

public sealed class HiredBoatDef
{
	public string Id { get; set; }
	public string Name { get; set; }
	public string Description { get; set; }
	public int Price { get; set; }
	public float TripMinutes { get; set; }
	public int GoldPerTripMin { get; set; }
	public int GoldPerTripMax { get; set; }
	public string RequiredBoatId { get; set; }
	public int UnlockOrder { get; set; }
}

public sealed class ObjectiveDef
{
	public string Id { get; set; }
	public string Title { get; set; }
	public string Description { get; set; }
	public ObjectiveType Type { get; set; }
	public string TargetId { get; set; }
	public int TargetCount { get; set; } = 1;
	public int RewardGold { get; set; }
	public string RewardItemId { get; set; }
	public string UnlockZoneId { get; set; }
	public int SortOrder { get; set; }
	public bool Tutorial { get; set; }
}

public sealed class EconomyDef
{
	public int StartingGold { get; set; } = 25;
	public float SizeValueCurve { get; set; } = 1.15f;
	public float RarityMultipliers { get; set; } // unused placeholder; see array
	public float[] RarityValueMult { get; set; } = { 1f, 1.6f, 2.4f, 3.8f, 6f };
	public float QualityValueMultMin { get; set; } = 0.85f;
	public float QualityValueMultMax { get; set; } = 1.35f;
	public int OfflineCapHours { get; set; } = 8;
	public float ShopSellRatio { get; set; } = 1f;
	public int FirstUpgradePrice { get; set; } = 40;
}

public sealed class TutorialDef
{
	public string GuaranteedFirstFishId { get; set; } = "harbor_minnow";
	public string FirstUpgradeItemId { get; set; } = "spool_braided";
	public string FirstUncommonFishId { get; set; } = "harbor_perch";
	public int GuaranteedUncommonByCatch { get; set; } = 8;
}
