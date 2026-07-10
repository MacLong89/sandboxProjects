namespace Sandbox;

/// <summary>Distinct minimap + structure tints per <see cref="ThornsProcBuildingType"/> for worldgen tuning.</summary>
public static class ThornsProcBuildingTypeDebugColors
{
	public static bool UseTypeColors { get; set; }

	public static Color MinimapColor( ThornsProcBuildingType type ) =>
		type switch
		{
			ThornsProcBuildingType.Skyscraper => new Color( 0.15f, 0.95f, 1f, 1f ),
			ThornsProcBuildingType.ApartmentTower => new Color( 1f, 0.35f, 0.95f, 1f ),
			ThornsProcBuildingType.OfficeBuilding => new Color( 0.35f, 0.55f, 1f, 1f ),
			ThornsProcBuildingType.Apartment => new Color( 0.95f, 0.75f, 0.35f, 1f ),
			ThornsProcBuildingType.Store => new Color( 0.4f, 1f, 0.45f, 1f ),
			ThornsProcBuildingType.Factory => new Color( 0.85f, 0.45f, 0.2f, 1f ),
			ThornsProcBuildingType.Warehouse => new Color( 0.7f, 0.55f, 0.35f, 1f ),
			ThornsProcBuildingType.House => new Color( 0.9f, 0.82f, 0.55f, 1f ),
			ThornsProcBuildingType.Cabin => new Color( 0.55f, 0.78f, 0.42f, 1f ),
			ThornsProcBuildingType.Barn => new Color( 0.78f, 0.62f, 0.28f, 1f ),
			ThornsProcBuildingType.Ruin => new Color( 0.55f, 0.55f, 0.55f, 1f ),
			ThornsProcBuildingType.MilitaryComplex => new Color( 0.45f, 0.72f, 0.38f, 1f ),
			ThornsProcBuildingType.RadioOutpost => new Color( 1f, 0.92f, 0.25f, 1f ),
			_ => new Color( 1f, 1f, 1f, 1f )
		};

	/// <summary>Multiply tint on wood_* pieces so silhouettes read at distance.</summary>
	public static Color StructureTint( ThornsProcBuildingType type )
	{
		var c = MinimapColor( type );
		return new Color( 0.55f + c.r * 0.45f, 0.55f + c.g * 0.45f, 0.55f + c.b * 0.45f, 1f );
	}

	public static string ShortLabel( ThornsProcBuildingType type ) =>
		type switch
		{
			ThornsProcBuildingType.ApartmentTower => "AptTower",
			ThornsProcBuildingType.OfficeBuilding => "Office",
			ThornsProcBuildingType.MilitaryComplex => "Military",
			ThornsProcBuildingType.RadioOutpost => "Radio",
			_ => type.ToString()
		};
}
