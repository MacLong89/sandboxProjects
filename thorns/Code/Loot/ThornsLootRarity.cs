namespace Sandbox;

/// <summary>Borderlands-style tier — affects weapon damage / fire rate rolls and armor DR rolls.</summary>
public enum ThornsLootRarity : byte
{
	Common = 0,
	Uncommon = 1,
	Rare = 2,
	Epic = 3,
	Legendary = 4
}

public static class ThornsLootRarityExtensions
{
	public static string DisplayName( this ThornsLootRarity r ) =>
		r switch
		{
			ThornsLootRarity.Common => "Common",
			ThornsLootRarity.Uncommon => "Uncommon",
			ThornsLootRarity.Rare => "Rare",
			ThornsLootRarity.Epic => "Epic",
			ThornsLootRarity.Legendary => "Legendary",
			_ => "Common"
		};

	public static string ShortLabel( this ThornsLootRarity r ) =>
		r switch
		{
			ThornsLootRarity.Common => "W",
			ThornsLootRarity.Uncommon => "G",
			ThornsLootRarity.Rare => "B",
			ThornsLootRarity.Epic => "P",
			ThornsLootRarity.Legendary => "O",
			_ => "?"
		};

	public static Color TintApprox( this ThornsLootRarity r ) =>
		r switch
		{
			ThornsLootRarity.Common => new Color( 0.82f, 0.82f, 0.82f, 1f ),
			ThornsLootRarity.Uncommon => new Color( 0.35f, 0.82f, 0.38f, 1f ),
			ThornsLootRarity.Rare => new Color( 0.38f, 0.55f, 0.95f, 1f ),
			ThornsLootRarity.Epic => new Color( 0.72f, 0.42f, 0.92f, 1f ),
			ThornsLootRarity.Legendary => new Color( 0.95f, 0.72f, 0.22f, 1f ),
			_ => Color.White
		};

	/// <summary>Muted panel fill behind weapon/armor HUD icons — same hues as <see cref="TintApprox"/>, darkened for readability.</summary>
	public static Color RarityInventorySlotBackdropTint( this ThornsLootRarity r ) =>
		r switch
		{
			ThornsLootRarity.Common => new Color( 0.15f, 0.15f, 0.16f, 0.92f ),
			ThornsLootRarity.Uncommon => new Color( 0.09f, 0.20f, 0.11f, 0.92f ),
			ThornsLootRarity.Rare => new Color( 0.10f, 0.14f, 0.24f, 0.92f ),
			ThornsLootRarity.Epic => new Color( 0.18f, 0.10f, 0.22f, 0.92f ),
			ThornsLootRarity.Legendary => new Color( 0.22f, 0.15f, 0.06f, 0.92f ),
			_ => new Color( 0.12f, 0.16f, 0.22f, 0.92f )
		};
}
