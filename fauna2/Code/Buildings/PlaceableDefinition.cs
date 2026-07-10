namespace Fauna2;

public enum BuildCategory
{
	Habitats,
	Entrance,
	Paths,
	Decorations,
	Nature,
	Utility,
}

/// <summary>One visual building block of a placeable (a box or sphere part).</summary>
public sealed class VisualPart
{
	[Property] public string Model { get; set; } = "models/dev/box.vmdl";
	[Property] public Vector3 Offset { get; set; }
	[Property] public Vector3 Scale { get; set; } = Vector3.One;
	[Property] public Color Tint { get; set; } = Color.White;
}

/// <summary>
/// Data-driven buildable: habitats, paths, decorations, nature and utility
/// pieces are all .place resources. Visuals are assembled from primitive
/// parts so new content needs zero code or custom models.
/// (Note: resource extensions must be 8 characters or fewer.)
/// </summary>
[AssetType( Name = "Fauna Placeable", Extension = "place", Category = "Fauna" )]
public sealed class PlaceableDefinition : GameResource
{
	[Property] public string DisplayName { get; set; } = "Placeable";
	[Property, TextArea] public string Description { get; set; } = "";
	[Property] public BuildCategory Category { get; set; } = BuildCategory.Decorations;

	[Property] public int Cost { get; set; } = 100;
	[Property] public int UnlockLevel { get; set; } = 1;
	[Property] public int RequiredPrestige { get; set; }

	// ── Gameplay contributions ──────────────────────────────
	/// <summary>Direct guest appeal (zoo beauty).</summary>
	[Property] public float AppealBonus { get; set; }
	/// <summary>Habitat enrichment when placed inside one.</summary>
	[Property] public float EnrichmentValue { get; set; }
	[Property] public float EducationValue { get; set; }
	[Property] public float ComfortValue { get; set; }
	[Property] public string DecorSet { get; set; } = "";
	[Property] public bool IsShelter { get; set; }
	[Property] public bool IsWater { get; set; }
	[Property] public bool IsRestroom { get; set; }
	[Property] public bool IsRestaurant { get; set; }
	[Property] public bool IsShop { get; set; }
	/// <summary>Collectible revenue per minute at full guest coverage (0 = category default).</summary>
	[Property] public float CollectIncomePerMinute { get; set; }
	/// <summary>How many guests this facility can serve (0 = category default).</summary>
	[Property] public int GuestsServed { get; set; }
	/// <summary>Max stored tips before collection (0 = default).</summary>
	[Property] public float MaxStoredRevenue { get; set; }

	// ── Habitat-specific (Category == Habitats) ─────────────
	[Property] public Vector2 HabitatSize { get; set; } = new Vector2( 512, 512 );
	[Property] public Biome HabitatBiome { get; set; } = Biome.Grassland;

	// ── Placement ───────────────────────────────────────────
	/// <summary>Footprint used for bounds checks (non-habitat pieces).</summary>
	[Property] public Vector2 Footprint { get; set; } = GameConstants.StandardBuildingFootprint;
	[Property] public float GridSnap { get; set; } = 64f;
	[Property] public float RotationStep { get; set; } = 45f;

	// ── Visuals ─────────────────────────────────────────────
	[Property] public List<VisualPart> Visuals { get; set; } = new();

	public bool IsHabitat =>
		Category == BuildCategory.Habitats
		|| Defs.ResourceStem( ResourceName ).StartsWith( "habitat_" );

	public bool IsEntrance =>
		Defs.ResourceStem( ResourceName ) == "entrance"
		|| (Category == BuildCategory.Paths && string.Equals( DisplayName, "Zoo Entrance", StringComparison.OrdinalIgnoreCase ));

	/// <summary>Walkable path tiles — not the zoo entrance (which lives in the Paths menu historically).</summary>
	public bool IsPathTile =>
		Category == BuildCategory.Paths && !IsEntrance;

	public bool ProvidesRestroom =>
		IsRestroom || Defs.ResourceStem( ResourceName ) == "restroom";

	public bool ProvidesRestaurant =>
		IsRestaurant || Defs.ResourceStem( ResourceName ).StartsWith( "restaurant" );

	public bool ProvidesShop =>
		IsShop || Defs.ResourceStem( ResourceName ).StartsWith( "shop" );

	public bool ProvidesCollectibleRevenue => ProvidesRestaurant || ProvidesShop;

	public bool IsGuestAmenity => ProvidesRestroom || ProvidesCollectibleRevenue;

	/// <summary>Guest-facing structures (restaurants, shops, restrooms).</summary>
	public bool IsBuilding => Category == BuildCategory.Utility;

	public float EffectiveCollectIncomePerMinute
	{
		get
		{
			if ( CollectIncomePerMinute > 0f )
				return CollectIncomePerMinute;

			if ( ProvidesShop )
				return GameConstants.ShopCollectIncomePerMinute;

			return GameConstants.RestaurantCollectIncomePerMinute;
		}
	}

	public int EffectiveGuestsServed
	{
		get
		{
			if ( GuestsServed > 0 )
				return GuestsServed;

			if ( ProvidesShop )
				return GameConstants.GuestsPerShop;

			return GameConstants.GuestsPerRestaurant;
		}
	}

	public float EffectiveMaxStoredRevenue =>
		MaxStoredRevenue > 0f ? MaxStoredRevenue : GameConstants.RestaurantMaxStored;

	public Vector2 EffectiveFootprint
	{
		get
		{
			if ( IsEntrance )
				return GameConstants.EntranceFootprint;

			var fp = IsHabitat ? HabitatSize : Footprint;
			return EnforceMinimumFootprint( fp, IsBuilding || IsHabitat );
		}
	}

	/// <summary>Clamp utility structures to at least 4×4 build tiles on each axis.</summary>
	public static Vector2 EnforceMinimumFootprint( Vector2 footprint, bool enforceMinimum )
	{
		if ( !enforceMinimum )
			return footprint;

		var tile = GameConstants.TileSize;
		var minTiles = GameConstants.MinBuildingTiles;
		var tilesX = Math.Max( minTiles, (int)MathF.Ceiling( footprint.x / tile ) );
		var tilesY = Math.Max( minTiles, (int)MathF.Ceiling( footprint.y / tile ) );
		return new Vector2( tilesX * tile, tilesY * tile );
	}
}
