namespace Sandbox;

/// <summary>One resource row consumed when placing (inventory item ids must exist in <see cref="ThornsItemRegistry"/>).</summary>
public sealed record ThornsStructureCost( string ItemId, int Quantity );

/// <summary>Authoritative structure tuning for server placement + future upgrades/damage.</summary>
public sealed record ThornsStructureDefinition(
	string Id,
	string DisplayName,
	/// <summary>If non-empty, player must have ≥1 and one is removed when placing (in addition to <see cref="ResourceCosts"/>).</summary>
	string RequiredPlacementItemId,
	ThornsStructureCost[] ResourceCosts,
	float MaxHealth,
	ThornsPlacementKind PlacementKind,
	/// <summary>Horizontal footprint for overlap tests (no heavy physics).</summary>
	float FootprintRadius,
	/// <summary>Placeholder for wood/stone/metal tier linkage.</summary>
	int UpgradeTierPlaceholder = 0,
	ThornsBuildingSnapKind SnapKind = ThornsBuildingSnapKind.None );

/// <summary>Static registry — costs, placement mode, health caps (THORNS_EVERYTHING_DOCUMENT §12).</summary>
public static class ThornsBuildingDefinitions
{
	/// <summary>Plan footprint per cell (Z = vertical module); aliases <see cref="ThornsBuildingModule.Cell"/>.</summary>
	public static float GridCellSize => ThornsBuildingModule.Cell;

	public const float MaxPlacementDistance = 800f;

	/// <summary>
	/// Horizontal overlap radius for kit placeables (world units) — <b>not</b> scaled by
	/// <see cref="ThornsBuildingVisuals.PlaceableStructureWorldScale"/> (that multiplier is mesh-only; using it here
	/// made overlap spheres ~2200u and rejected valid adjacent foundations).
	/// </summary>
	public const float PortableKitPlaceableFootprintRadius = 22f;

	/// <summary>Crafted kit placeables — no <see cref="ThornsStructureCost"/> on place (materials paid at craft time).</summary>
	public static readonly ThornsStructureDefinition StorageChestStructureDefinition = new(
		Id: "storage_chest",
		DisplayName: "Storage Chest",
		RequiredPlacementItemId: "storage_chest_kit",
		ResourceCosts: Array.Empty<ThornsStructureCost>(),
		MaxHealth: 400f,
		PlacementKind: ThornsPlacementKind.Free,
		FootprintRadius: PortableKitPlaceableFootprintRadius );

	public static readonly ThornsStructureDefinition CampfireStructureDefinition = new(
		Id: "campfire",
		DisplayName: "Campfire",
		RequiredPlacementItemId: "campfire_kit",
		ResourceCosts: Array.Empty<ThornsStructureCost>(),
		MaxHealth: 250f,
		PlacementKind: ThornsPlacementKind.Free,
		FootprintRadius: PortableKitPlaceableFootprintRadius );

	public static readonly ThornsStructureDefinition WorkbenchStructureDefinition = new(
		Id: "workbench",
		DisplayName: "Workbench",
		RequiredPlacementItemId: "workbench_kit",
		ResourceCosts: Array.Empty<ThornsStructureCost>(),
		MaxHealth: 320f,
		PlacementKind: ThornsPlacementKind.Free,
		FootprintRadius: PortableKitPlaceableFootprintRadius );

	public static readonly ThornsStructureDefinition BedStructureDefinition = new(
		Id: "bed",
		DisplayName: "Bed",
		RequiredPlacementItemId: "bed_kit",
		ResourceCosts: Array.Empty<ThornsStructureCost>(),
		MaxHealth: 200f,
		PlacementKind: ThornsPlacementKind.Free,
		FootprintRadius: PortableKitPlaceableFootprintRadius );

	static readonly Dictionary<string, ThornsStructureDefinition> _byId = new()
	{
		["wood_foundation"] = new ThornsStructureDefinition(
			Id: "wood_foundation",
			DisplayName: "Wood Foundation",
			RequiredPlacementItemId: "",
			ResourceCosts: new[] { new ThornsStructureCost( "wood", 20 ) },
			MaxHealth: 500f,
			PlacementKind: ThornsPlacementKind.Grid,
			FootprintRadius: 48f,
			UpgradeTierPlaceholder: 0,
			SnapKind: ThornsBuildingSnapKind.Foundation ),

		["wood_wall"] = new ThornsStructureDefinition(
			Id: "wood_wall",
			DisplayName: "Wood Wall",
			RequiredPlacementItemId: "",
			ResourceCosts: new[] { new ThornsStructureCost( "wood", 15 ) },
			MaxHealth: 350f,
			PlacementKind: ThornsPlacementKind.Grid,
			FootprintRadius: 32f,
			UpgradeTierPlaceholder: 0,
			SnapKind: ThornsBuildingSnapKind.Wall ),

		["wood_window"] = new ThornsStructureDefinition(
			Id: "wood_window",
			DisplayName: "Wood Window",
			RequiredPlacementItemId: "",
			ResourceCosts: new[] { new ThornsStructureCost( "wood", 12 ) },
			MaxHealth: 280f,
			PlacementKind: ThornsPlacementKind.Grid,
			FootprintRadius: 32f,
			UpgradeTierPlaceholder: 0,
			SnapKind: ThornsBuildingSnapKind.Window ),

		["wood_ramp"] = new ThornsStructureDefinition(
			Id: "wood_ramp",
			DisplayName: "Wood Ramp",
			RequiredPlacementItemId: "",
			ResourceCosts: new[] { new ThornsStructureCost( "wood", 18 ) },
			MaxHealth: 320f,
			PlacementKind: ThornsPlacementKind.Grid,
			FootprintRadius: 32f,
			UpgradeTierPlaceholder: 0,
			SnapKind: ThornsBuildingSnapKind.Ramp ),

		["wood_doorframe"] = new ThornsStructureDefinition(
			Id: "wood_doorframe",
			DisplayName: "Wood Door Frame",
			RequiredPlacementItemId: "",
			ResourceCosts: new[] { new ThornsStructureCost( "wood", 12 ) },
			MaxHealth: 300f,
			PlacementKind: ThornsPlacementKind.Grid,
			FootprintRadius: 32f,
			UpgradeTierPlaceholder: 0,
			SnapKind: ThornsBuildingSnapKind.DoorFrame ),

		["wood_door"] = new ThornsStructureDefinition(
			Id: "wood_door",
			DisplayName: "Wood Door",
			RequiredPlacementItemId: "",
			ResourceCosts: new[] { new ThornsStructureCost( "wood", 20 ) },
			MaxHealth: 250f,
			PlacementKind: ThornsPlacementKind.Grid,
			FootprintRadius: 28f,
			UpgradeTierPlaceholder: 0,
			SnapKind: ThornsBuildingSnapKind.DoorPanel ),

		["storage_chest"] = StorageChestStructureDefinition,
		["campfire"] = CampfireStructureDefinition,
		["workbench"] = WorkbenchStructureDefinition,
		["bed"] = BedStructureDefinition,

		["base_core"] = new ThornsStructureDefinition(
			Id: "base_core",
			DisplayName: "Base Core",
			RequiredPlacementItemId: "",
			ResourceCosts: new[]
			{
				new ThornsStructureCost( "wood", 100 ),
				new ThornsStructureCost( "stone", 50 ),
				new ThornsStructureCost( "metal", 20 )
			},
			MaxHealth: 2000f,
			PlacementKind: ThornsPlacementKind.Free,
			FootprintRadius: 36f )
	};

	static readonly string[] _allIds = _byId.Keys.OrderBy( x => x ).ToArray();

	public static IReadOnlyList<string> AllStructureIds => _allIds;

	static void EnsurePortableKitStructureDefinitionsOnEveryLookup()
	{
		EnsureCanonicalStructureRow( StorageChestStructureDefinition );
		EnsureCanonicalStructureRow( CampfireStructureDefinition );
		EnsureCanonicalStructureRow( WorkbenchStructureDefinition );
		EnsureCanonicalStructureRow( BedStructureDefinition );

		foreach ( var entry in ThornsPlaceableFurnitureCatalog.All )
		{
			if ( !entry.AllowPlayerKitPlacement )
				continue;

			if ( !_byId.ContainsKey( entry.StructureDefId ) )
			{
				_byId[entry.StructureDefId] = new ThornsStructureDefinition(
					Id: entry.StructureDefId,
					DisplayName: ThornsPlaceableFurnitureCatalog.FormatDisplayName( entry.StructureDefId ),
					RequiredPlacementItemId: entry.KitItemId,
					ResourceCosts: Array.Empty<ThornsStructureCost>(),
					MaxHealth: 280f,
					PlacementKind: ThornsPlacementKind.Free,
					FootprintRadius: PlaceableFootprintRadiusForEntry( entry ) );
			}
			else
			{
				EnsureCanonicalStructureRow( _byId[entry.StructureDefId] );
			}
		}
	}

	static float PlaceableFootprintRadiusForEntry( in ThornsPlaceableFurnitureCatalog.Entry entry ) =>
		MathF.Max( PortableKitPlaceableFootprintRadius, ThornsPlaceableFurnitureScale.PlanarRadius( in entry ) );

	static void EnsureCanonicalStructureRow( ThornsStructureDefinition canonical )
	{
		if ( !_byId.TryGetValue( canonical.Id, out var existing ) || !ReferenceEquals( existing, canonical ) )
			_byId[canonical.Id] = canonical;
	}

	public static bool TryGet( string structureId, out ThornsStructureDefinition def )
	{
		EnsurePortableKitStructureDefinitionsOnEveryLookup();
		return _byId.TryGetValue( structureId, out def );
	}

	/// <summary>Hotbar kit structures that share relaxed mutual overlap rules.</summary>
	public static bool IsPortableKitPlaceableId( string structureDefId ) =>
		ThornsPlaceableFurnitureCatalog.IsPortableKitStructureId( structureDefId );

	/// <summary>Placement costs for crafted kits — only the hotbar kit item, never duplicate craft mats.</summary>
	public static ReadOnlySpan<ThornsStructureCost> PlacementResourceCosts( ThornsStructureDefinition def )
	{
		if ( def is null )
			return ReadOnlySpan<ThornsStructureCost>.Empty;

		return IsPortableKitPlaceableId( def.Id ) && !string.IsNullOrEmpty( def.RequiredPlacementItemId )
			? ReadOnlySpan<ThornsStructureCost>.Empty
			: def.ResourceCosts;
	}

	/// <summary>Wood/stone/metal tier path for grid pieces (matches host <see cref="ThornsBuildingController"/> upgrade rules).</summary>
	public static bool SupportsMaterialTierUpgrade( string structureDefId ) =>
		structureDefId is "wood_foundation" or "wood_wall" or "wood_window" or "wood_doorframe" or "wood_ramp" or "wood_door";

	/// <summary>Wood component of <paramref name="def"/> for upgrade pricing; minimum 8 matches host upgrade math.</summary>
	public static int HostWoodBaselineForUpgradeCost( ThornsStructureDefinition def )
	{
		var wood = 0;
		foreach ( var c in def.ResourceCosts )
		{
			if ( string.Equals( c.ItemId, "wood", StringComparison.OrdinalIgnoreCase ) )
				wood += c.Quantity;
		}

		return Math.Max( wood, 8 );
	}

	/// <summary>Consumable for upgrading from <paramref name="fromTier"/> (0=wood, 1=stone) to the next tier.</summary>
	public static (string ItemId, int Quantity) GetUpgradeCostForTier( string structureDefId, int fromTier )
	{
		if ( !SupportsMaterialTierUpgrade( structureDefId ) || !TryGet( structureDefId, out var def ) )
			return ("", 0);

		var b = HostWoodBaselineForUpgradeCost( def );
		return fromTier switch
		{
			0 => ("stone", b * 2),
			1 => ("metal", b * 2),
			_ => ("", 0)
		};
	}

	public static Vector3 SnapGrid( Vector3 worldPosition )
	{
		var g = GridCellSize;
		return new Vector3(
			MathF.Round( worldPosition.x / g ) * g,
			MathF.Round( worldPosition.y / g ) * g,
			worldPosition.z );
	}

	public static (int CellX, int CellY) GridCellFromWorld( Vector3 worldPosition )
	{
		var g = GridCellSize;
		return ((int)MathF.Round( worldPosition.x / g ), (int)MathF.Round( worldPosition.y / g ));
	}
}
