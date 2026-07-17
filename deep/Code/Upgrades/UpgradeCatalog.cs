namespace Deep;

public static class UpgradeCatalog
{
	public static IReadOnlyList<UpgradeDefinition> All { get; } =
	[
		new()
		{
			Id = "oxygen_tank", DisplayName = "Oxygen Tank",
			Description = "+12s max oxygen per level",
			Category = UpgradeCategory.Oxygen, MaxLevel = 10,
			BasePrice = 35f, GrowthRate = 1.4f, EffectPerLevel = 12f
		},
		new()
		{
			Id = "fins", DisplayName = "Swim Fins",
			Description = "+2.5 swim / ascent / descent speed",
			Category = UpgradeCategory.Movement, MaxLevel = 8,
			BasePrice = 40f, GrowthRate = 1.42f, EffectPerLevel = 2.5f
		},
		new()
		{
			Id = "dive_bag", DisplayName = "Dive Bag",
			Description = "+2 haul capacity per level",
			Category = UpgradeCategory.Haul, MaxLevel = 8,
			BasePrice = 45f, GrowthRate = 1.48f, EffectPerLevel = 2f
		},
		new()
		{
			Id = "pressure_suit", DisplayName = "Pressure Suit",
			Description = "+30m safe depth per level",
			Category = UpgradeCategory.Pressure, MaxLevel = 10,
			BasePrice = 55f, GrowthRate = 1.5f, EffectPerLevel = 30f
		},
		new()
		{
			Id = "hull_health", DisplayName = "Reinforced Suit",
			Description = "+20 max health per level",
			Category = UpgradeCategory.Health, MaxLevel = 6,
			BasePrice = 50f, GrowthRate = 1.46f, EffectPerLevel = 20f
		},
		new()
		{
			Id = "floodlight", DisplayName = "Floodlight",
			Description = "+12% visibility / brightness per level",
			Category = UpgradeCategory.Lighting, MaxLevel = 5,
			BasePrice = 0f, ShellBasePrice = 12f, GrowthRate = 1.35f, EffectPerLevel = 0.12f
		},
		new()
		{
			Id = "sonar_range", DisplayName = "Sonar Range",
			Description = "+6m scanner / drone range per level",
			Category = UpgradeCategory.Scanning, MaxLevel = 5,
			BasePrice = 0f, ShellBasePrice = 14f, GrowthRate = 1.38f, EffectPerLevel = 6f
		},
	];

	public static UpgradeDefinition Get( string id ) =>
		All.FirstOrDefault( u => u.Id == id );

	public static float CostForLevel( UpgradeDefinition def, int currentLevel )
	{
		if ( def is null ) return float.MaxValue;
		var basePrice = def.ShellBasePrice > 0f ? def.ShellBasePrice : def.BasePrice;
		return basePrice * MathF.Pow( def.GrowthRate, Math.Max( 0, currentLevel ) );
	}

	public static bool UsesShells( UpgradeDefinition def ) =>
		def is not null && def.ShellBasePrice > 0f;
}
