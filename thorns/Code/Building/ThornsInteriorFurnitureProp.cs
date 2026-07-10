namespace Sandbox;

/// <summary>
/// Proc interior furniture — reapplies <see cref="ThornsPlaceableFurniturePresentation"/> on start
/// when the catalog revision changes. <see cref="WorldSizeInches"/> is synced from host so joiners
/// match gallery/catalog sizing even if transform scale replication is late.
/// </summary>
[Title( "Thorns — Interior Furniture Prop" )]
[Category( "Thorns/World" )]
public sealed class ThornsInteriorFurnitureProp : Component
{
	[Sync( SyncFlags.FromHost )] public string StructureDefId { get; set; } = "";

	/// <summary>Authoritative world size (inches) from host scatter — X width, Y depth, Z height.</summary>
	[Sync( SyncFlags.FromHost )] public Vector3 WorldSizeInches { get; set; }

	/// <summary>Storey used for floor snap (host scatter). -1 = infer from local Z (legacy).</summary>
	[Sync( SyncFlags.FromHost )] public int InteriorStoryIndex { get; set; } = -1;

	[Sync( SyncFlags.FromHost )] public int SpawnGridX { get; set; } = -1;

	[Sync( SyncFlags.FromHost )] public int SpawnGridY { get; set; } = -1;

	[Sync( SyncFlags.FromHost )] public int SpawnWidthCells { get; set; }

	[Sync( SyncFlags.FromHost )] public int SpawnDepthCells { get; set; }

	int _appliedCatalogRevision = -1;

	protected override void OnStart()
	{
		if ( !Game.IsPlaying )
			return;

		TryApplyCatalogPresentationIfStale( force: true );
	}

	void TryApplyCatalogPresentationIfStale( bool force )
	{
		if ( !force && _appliedCatalogRevision == ThornsPlaceableFurniturePresentation.CatalogRevision )
			return;

		ApplyCatalogPresentation();
	}

	public void TryInferStructureDefIdFromName()
	{
		if ( !string.IsNullOrWhiteSpace( StructureDefId ) )
			return;

		var name = GameObject?.Name ?? "";
		const string prefix = "Furniture_";
		if ( name.StartsWith( prefix, StringComparison.OrdinalIgnoreCase ) )
			StructureDefId = name[prefix.Length..];
	}

	public void ApplyCatalogPresentation()
	{
		if ( !GameObject.IsValid() )
			return;

		TryInferStructureDefIdFromName();
		if ( !ThornsPlaceableFurniturePresentation.TryGetEntry( StructureDefId, out var entry ) )
			return;

		var catalogSize = ThornsPlaceableFurnitureCatalog.GetWorldSizeInches( StructureDefId );
		var worldSize = WorldSizeInches.LengthSquared >= 1f ? WorldSizeInches : catalogSize;

		if ( !Networking.IsActive || Networking.IsHost )
			WorldSizeInches = worldSize;

		var sized = entry with { WorldSizeInches = worldSize };

		ThornsPlaceableFurniturePresentation.Apply( GameObject, in sized, honorEntryWorldSize: true );
		ThornsModelMaterialUvScale.ApplyToHierarchy( GameObject, includeChildren: false );

		var buildingRoot = FindProcBuildingRoot( GameObject );
		if ( buildingRoot.IsValid() )
		{
			var layout = ThornsProcBuildingLayoutHost.TryGet( buildingRoot );
			var doorSide = layout?.DoorSide ?? -1;
			var doorIndex = layout?.DoorIndex ?? 0;
			var spawnCtx = new ThornsFloorplanFurnitureTuneItem.SpawnContext(
				buildingRoot,
				SpawnWidthCells > 0 ? SpawnWidthCells : layout?.WidthCells ?? 0,
				SpawnDepthCells > 0 ? SpawnDepthCells : layout?.DepthCells ?? 0,
				doorSide,
				doorIndex,
				story: InteriorStoryIndex,
				gridX: SpawnGridX,
				gridY: SpawnGridY );

			ThornsProcBuildingInteriorSample.SeatProcInteriorFurnitureOnBuilding(
				GameObject,
				buildingRoot,
				in spawnCtx,
				in sized,
				GameObject.WorldRotation,
				GameObject.WorldPosition );
		}

		_appliedCatalogRevision = ThornsPlaceableFurniturePresentation.CatalogRevision;
	}

	public static GameObject FindProcBuildingRoot( GameObject from )
	{
		for ( var p = from; p is not null && p.IsValid(); p = p.Parent )
		{
			if ( p.Tags.Has( "thorns_proc_building" ) )
				return p;
		}

		return null;
	}
}
