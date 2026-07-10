namespace Sandbox;

/// <summary>
/// Per-archetype loot crate kind weights when an interior crate spawns (all kinds possible everywhere).
/// Order matches <see cref="ThornsLootGenerator.InteriorCrateKindOrder"/>.
/// </summary>
public static class ThornsProcBuildingLootAffinity
{
	public const int CrateKindCount = 8;

	/// <summary>Chance each occupied floor rolls at least one interior crate (0–1).</summary>
	public const float DefaultFloorSpawnChance = 0.30f;

	// Index = (int)<see cref="ThornsProcBuildingType"/> — Med, Wpn, Arm, Prov, Mil, Ind, Salv, Hunt.
	static readonly float[][] _weightsByType =
	{
		new float[] { 16f, 10f, 12f, 20f, 8f, 10f, 10f, 14f },   // House
		new float[] { 14f, 8f, 6f, 16f, 6f, 12f, 18f, 20f },    // Ruin
		new float[] { 7f, 10f, 10f, 12f, 14f, 26f, 16f, 5f },   // Warehouse
		new float[] { 6f, 18f, 16f, 5f, 28f, 10f, 12f, 5f },    // MilitaryComplex
		new float[] { 18f, 8f, 6f, 22f, 5f, 8f, 10f, 23f },    // Cabin
		new float[] { 18f, 10f, 12f, 28f, 5f, 7f, 10f, 10f },  // Store
		new float[] { 16f, 12f, 14f, 18f, 8f, 10f, 12f, 10f }, // Apartment
		new float[] { 5f, 12f, 10f, 8f, 14f, 26f, 20f, 5f },    // Factory
		new float[] { 15f, 6f, 5f, 25f, 5f, 12f, 10f, 22f },   // Barn
		new float[] { 8f, 20f, 14f, 8f, 22f, 10f, 12f, 6f },   // RadioOutpost
		new float[] { 10f, 16f, 18f, 10f, 14f, 10f, 14f, 8f },  // ApartmentTower
		new float[] { 10f, 18f, 18f, 10f, 16f, 8f, 14f, 6f },   // Skyscraper
		new float[] { 12f, 14f, 16f, 12f, 14f, 10f, 14f, 8f }   // OfficeBuilding
	};

	static ReadOnlySpan<float> WeightsFor( ThornsProcBuildingType type )
	{
		var i = (int)type;
		if ( i >= 0 && i < _weightsByType.Length )
			return _weightsByType[i];

		return _weightsByType[(int)ThornsProcBuildingType.House];
	}

	public static ThornsLootCrateKind PickCrateKind( ThornsProcBuildingType type, Random rng )
	{
		var w = WeightsFor( type );
		var sum = 0f;
		for ( var i = 0; i < w.Length; i++ )
			sum += w[i];

		var t = (float)rng.NextDouble() * sum;
		var acc = 0f;
		for ( var i = 0; i < w.Length; i++ )
		{
			acc += w[i];
			if ( t <= acc )
				return ThornsLootGenerator.InteriorCrateKindOrder[i];
		}

		return ThornsLootGenerator.InteriorCrateKindOrder[ThornsLootGenerator.InteriorCrateKindOrder.Length - 1];
	}

	/// <summary>Normalized % per crate kind for UI / docs (sums to ~100).</summary>
	public static void GetCrateKindPercentages( ThornsProcBuildingType type, Span<float> percentages )
	{
		var w = WeightsFor( type );
		var n = Math.Min( percentages.Length, w.Length );
		var sum = 0f;
		for ( var i = 0; i < n; i++ )
			sum += w[i];

		if ( sum <= 0.001f )
		{
			for ( var i = 0; i < n; i++ )
				percentages[i] = 0f;
			return;
		}

		for ( var i = 0; i < n; i++ )
			percentages[i] = w[i] / sum * 100f;
	}

	/// <summary>Facade materials this archetype usually rolls (see <see cref="ThornsProcBuildingMaterialPalette"/>).</summary>
	public static string DescribeTypicalFacadeSlugs( ThornsProcBuildingType type ) =>
		type switch
		{
			ThornsProcBuildingType.Cabin => "barn_wood, light_wood_siding, wood",
			ThornsProcBuildingType.Barn => "barn_wood, wood, sheet_metal",
			ThornsProcBuildingType.House => "light_wood_siding, wood, brick, stucco",
			ThornsProcBuildingType.Ruin => "barn_wood, brick, stone_brick, cobblestone_brick",
			ThornsProcBuildingType.Store => "brick, stucco, light_wood_siding, concrete",
			ThornsProcBuildingType.Apartment => "brick, stucco, concrete, stone_brick",
			ThornsProcBuildingType.Warehouse => "sheet_metal, concrete, concrete_dark",
			ThornsProcBuildingType.Factory => "sheet_metal, concrete, concrete_dark, metal",
			ThornsProcBuildingType.MilitaryComplex => "concrete_dark, sheet_metal, metal",
			ThornsProcBuildingType.RadioOutpost => "metal, sheet_metal, concrete_dark",
			ThornsProcBuildingType.OfficeBuilding => "glass_panes_light/dark, concrete, stone_brick",
			ThornsProcBuildingType.ApartmentTower => "concrete, glass_panes_light/dark, stucco, metal",
			ThornsProcBuildingType.Skyscraper => "glass_panes_light/dark, concrete, metal",
			_ => "wood, stone, brick"
		};
}
