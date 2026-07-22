namespace PawnShop;

public enum ItemCategory
{
	Jewelry,
	Watches,
	Electronics,
	Instruments,
	Tools,
	Sports,
	Collectibles,
	Art,
	Antiques,
	Gaming,
	Cameras,
	Appliances,
	Memorabilia,
}

public enum Authenticity
{
	Unknown,
	Genuine,
	Counterfeit,
	Replica,
	Altered,
}

public enum Condition
{
	Broken,
	Poor,
	Fair,
	Good,
	Excellent,
	Mint,
}

public enum Rarity
{
	Common,
	Uncommon,
	Rare,
	VeryRare,
	Legendary,
}

public enum LegalStatus
{
	Clean,
	Suspicious,
	Stolen,
}

/// <summary>Where an item instance currently lives.</summary>
public enum ItemLocation
{
	CustomerOwned,
	Backroom,
	PawnStorage,
	RepairBench,
	OnDisplay,
	Sold,
	Confiscated,
	Scrapped,
}

public enum InspectTool
{
	Eyes,
	Magnifier,
	ElectronicsTester,
	MetalTester,
	GemTester,
	UvLight,
	Database,
}

public static class ItemEnumInfo
{
	public static string Label( this ItemCategory c ) => c switch
	{
		ItemCategory.Jewelry => "Jewelry",
		ItemCategory.Watches => "Watches",
		ItemCategory.Electronics => "Electronics",
		ItemCategory.Instruments => "Instruments",
		ItemCategory.Tools => "Tools",
		ItemCategory.Sports => "Sports Gear",
		ItemCategory.Collectibles => "Collectibles",
		ItemCategory.Art => "Art",
		ItemCategory.Antiques => "Antiques",
		ItemCategory.Gaming => "Gaming",
		ItemCategory.Cameras => "Cameras",
		ItemCategory.Appliances => "Appliances",
		ItemCategory.Memorabilia => "Memorabilia",
		_ => c.ToString(),
	};

	public static string Icon( this ItemCategory c ) => c switch
	{
		ItemCategory.Jewelry => "diamond",
		ItemCategory.Watches => "watch",
		ItemCategory.Electronics => "devices",
		ItemCategory.Instruments => "music_note",
		ItemCategory.Tools => "construction",
		ItemCategory.Sports => "sports_basketball",
		ItemCategory.Collectibles => "toys",
		ItemCategory.Art => "palette",
		ItemCategory.Antiques => "chair_alt",
		ItemCategory.Gaming => "sports_esports",
		ItemCategory.Cameras => "photo_camera",
		ItemCategory.Appliances => "kitchen",
		ItemCategory.Memorabilia => "military_tech",
		_ => "inventory_2",
	};

	public static float ConditionMult( this Condition c ) => c switch
	{
		Condition.Broken => 0.30f,
		Condition.Poor => 0.55f,
		Condition.Fair => 0.75f,
		Condition.Good => 1.00f,
		Condition.Excellent => 1.20f,
		Condition.Mint => 1.45f,
		_ => 1f,
	};

	public static float RarityMult( this Rarity r ) => r switch
	{
		Rarity.Common => 1.0f,
		Rarity.Uncommon => 1.35f,
		Rarity.Rare => 2.0f,
		Rarity.VeryRare => 3.2f,
		Rarity.Legendary => 5.5f,
		_ => 1f,
	};

	public static Color RarityColor( this Rarity r ) => r switch
	{
		Rarity.Common => new Color( 0.75f, 0.78f, 0.80f ),
		Rarity.Uncommon => new Color( 0.35f, 0.85f, 0.45f ),
		Rarity.Rare => new Color( 0.30f, 0.62f, 1.00f ),
		Rarity.VeryRare => new Color( 0.75f, 0.42f, 1.00f ),
		Rarity.Legendary => new Color( 1.00f, 0.72f, 0.15f ),
		_ => Color.White,
	};

	public static string Label( this Condition c ) => c.ToString();

	public static string Label( this Rarity r ) => r switch
	{
		Rarity.VeryRare => "Very Rare",
		_ => r.ToString(),
	};
}
