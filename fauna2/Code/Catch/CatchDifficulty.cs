namespace Fauna2;

/// <summary>Maps species rarity and authored tuning into catch minigame parameters.</summary>
public static class CatchDifficulty
{
	public static float For( AnimalDefinition def )
	{
		if ( def is null ) return 0.3f;

		var rarityBase = def.Rarity switch
		{
			AnimalRarity.Common => 0.12f,
			AnimalRarity.Uncommon => 0.28f,
			AnimalRarity.Rare => 0.45f,
			AnimalRarity.Exotic => 0.65f,
			AnimalRarity.Legendary => 0.82f,
			_ => 0.3f,
		};

		return MathF.Max( rarityBase, def.CatchDifficulty ).Clamp( 0.05f, 0.95f );
	}

	public static float GreenZoneWidth( float difficulty ) =>
		(0.34f - difficulty * 0.24f).Clamp( 0.06f, 0.34f );

	public static float BarSpeed( float difficulty ) =>
		(0.5f + difficulty * 1.05f).Clamp( 0.38f, 1.55f );

	/// <summary>Consecutive green-zone hits needed to secure the catch.</summary>
	public static int RequiredHits( AnimalDefinition def )
	{
		if ( def is null ) return 5;

		var hits = def.Rarity switch
		{
			AnimalRarity.Common => 5,
			AnimalRarity.Uncommon => 6,
			AnimalRarity.Rare => 7,
			AnimalRarity.Exotic => 8,
			AnimalRarity.Legendary => 9,
			_ => 5,
		};

		if ( def.CatchDifficulty >= 0.65f )
			hits++;

		return hits;
	}

	public static string RarityLabel( AnimalRarity rarity ) => rarity switch
	{
		AnimalRarity.Common => "Common",
		AnimalRarity.Uncommon => "Uncommon",
		AnimalRarity.Rare => "Rare",
		AnimalRarity.Exotic => "Exotic",
		AnimalRarity.Legendary => "Legendary",
		_ => "Unknown",
	};

	public static int SpawnWeight( AnimalRarity rarity ) => rarity switch
	{
		AnimalRarity.Common => 42,
		AnimalRarity.Uncommon => 26,
		AnimalRarity.Rare => 16,
		AnimalRarity.Exotic => 10,
		AnimalRarity.Legendary => 4,
		_ => 20,
	};
}
