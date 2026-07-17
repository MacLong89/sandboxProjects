namespace SceneLab;

/// <summary>
/// City metric system keyed off suburban house size (the reference that already feels right).
/// Everything else — roads, lots, commercial — is a multiple of these constants.
/// Values are compile-time constants for attributes / defaults.
/// </summary>
public static class CityScale
{
	// --- House reference (matches current good-feeling house kits) ---

	/// <summary>Typical street-facing house width (Craftsman-class).</summary>
	public const float HouseFront = 560f;

	/// <summary>Typical front-to-back house depth.</summary>
	public const float HouseDepth = 360f;

	/// <summary>Typical single-story interior height.</summary>
	public const float HouseStory = 180f;

	/// <summary>Door clear — tied to body (player BodyRadius 16).</summary>
	public const float DoorW = 56f;
	public const float DoorH = 104f;

	// --- Residential plots ---

	/// <summary>Center-to-center lot pitch along a street (house + side yards).</summary>
	public const float LotPitch = HouseFront * 1.40f; // 784

	/// <summary>Extra yard pad beyond footprint when sizing grass.</summary>
	public const float LotSidePad = HouseFront * 0.28f;

	/// <summary>Yard depth band outside the road corridor (setback + house + backyard).</summary>
	public const float YardBand = HouseDepth * 1.55f; // ~558

	// --- Roads (scaled to house frontage / driveway language) ---

	public const float RoadWidth = HouseFront * 0.40f;       // 224 — two lanes
	public const float SidewalkWidth = HouseFront * 0.10f;   // 56
	public const float EmbankmentWidth = HouseFront * 0.26f; // 146
	public const float DriveWidth = HouseFront * 0.14f;      // ~78

	/// <summary>Full street half-length (each direction from origin).</summary>
	public const float StreetHalfLen = LotPitch * 6.5f; // ~5k

	/// <summary>Distance between N–S cross streets.</summary>
	public const float CrossPeriod = LotPitch * 3.0f; // ~2352

	// --- Commercial (relative to a house lot) ---

	public const float CommercialPitch = HouseFront * 1.55f; // ~868 centers

	public const float SkyscraperFront = HouseFront * 0.55f;
	public const float SkyscraperDepth = HouseDepth * 0.55f;
	public const float SkyscraperStory = HouseStory * 0.72f;

	public const float OfficeFront = HouseFront * 0.95f;
	public const float OfficeDepth = HouseDepth * 0.70f;
	public const float OfficeStory = HouseStory * 0.62f;

	public const float ApartmentFront = HouseFront * 1.10f;
	public const float ApartmentDepth = HouseDepth * 0.65f;
	public const float ApartmentStory = HouseStory * 0.55f;

	public const float FactoryFront = HouseFront * 1.45f;
	public const float FactoryDepth = HouseDepth * 1.05f;
	public const float FactoryStory = HouseStory * 0.85f;

	public const float WarehouseFront = HouseFront * 1.35f;
	public const float WarehouseDepth = HouseDepth * 0.90f;
	public const float WarehouseStory = HouseStory * 0.72f;
}
