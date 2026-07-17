namespace SceneLab;

/// <summary>Non-house commercial / industrial kit types — saturated legend colors for clear reading.</summary>
public enum BuildingKind
{
	Skyscraper,
	Office,
	Apartment,
	Factory,
	Warehouse,
}

/// <summary>Canonical colors + labels for the scene HUD and chat legend.</summary>
public static class BuildingLegend
{
	public readonly record struct Entry( BuildingKind Kind, string Label, Color Color, string Hex );

	public static readonly Entry Skyscraper = new( BuildingKind.Skyscraper, "Skyscraper", new Color( 0.86f, 0.20f, 0.90f ), "#DB33E6" );
	public static readonly Entry Office = new( BuildingKind.Office, "Office", new Color( 0.15f, 0.45f, 0.95f ), "#2673F2" );
	public static readonly Entry Apartment = new( BuildingKind.Apartment, "Apartment", new Color( 0.98f, 0.48f, 0.10f ), "#FA7A1A" );
	public static readonly Entry Factory = new( BuildingKind.Factory, "Factory", new Color( 0.95f, 0.85f, 0.12f ), "#F2D91F" );
	public static readonly Entry Warehouse = new( BuildingKind.Warehouse, "Warehouse", new Color( 0.10f, 0.78f, 0.72f ), "#1AC7B8" );

	/// <summary>Suburban houses keep natural kit tints — listed for contrast.</summary>
	public const string HouseLabel = "Suburban house";
	public static readonly Color HouseColor = new( 0.90f, 0.84f, 0.72f );
	public const string HouseHex = "#E6D6B8";

	public static readonly Entry[] Commercial =
	{
		Skyscraper, Office, Apartment, Factory, Warehouse,
	};

	public static Entry Of( BuildingKind kind ) => kind switch
	{
		BuildingKind.Skyscraper => Skyscraper,
		BuildingKind.Office => Office,
		BuildingKind.Apartment => Apartment,
		BuildingKind.Factory => Factory,
		_ => Warehouse,
	};

	public static Color ColorOf( BuildingKind kind ) => Of( kind ).Color;
}
