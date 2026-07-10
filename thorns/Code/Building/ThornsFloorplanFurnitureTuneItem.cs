using System.Text;

namespace Sandbox;

/// <summary>
/// Live-tuning handle for proc interior furniture in <see cref="ThornsInteriorFurnitureFloorplanTest"/>.
/// Parent under the building root and edit <see cref="GameObject.LocalPosition"/> / <see cref="GameObject.LocalRotation"/>
/// (building-local) plus <see cref="WorldSizeInches"/> in the inspector while playing.
/// </summary>
[Title( "Thorns — Floorplan Furniture Tune Item" )]
[Category( "Thorns/Dev" )]
[Icon( "chair" )]
public sealed class ThornsFloorplanFurnitureTuneItem : Component
{
	public readonly struct SpawnContext
	{
		public GameObject BuildingRoot { get; init; }
		public int WidthCells { get; init; }
		public int DepthCells { get; init; }
		public int DoorSide { get; init; }
		public int DoorIndex { get; init; }
		public int Story { get; init; }
		public int GridX { get; init; }
		public int GridY { get; init; }

		public SpawnContext(
			GameObject buildingRoot,
			int widthCells,
			int depthCells,
			int doorSide,
			int doorIndex,
			int story = -1,
			int gridX = -1,
			int gridY = -1 )
		{
			BuildingRoot = buildingRoot;
			WidthCells = widthCells;
			DepthCells = depthCells;
			DoorSide = doorSide;
			DoorIndex = doorIndex;
			Story = story;
			GridX = gridX;
			GridY = gridY;
		}
	}

	[Property] public GameObject BuildingRoot { get; set; }

	[Property] public string StructureDefId { get; set; } = "";

	[Property] public int Story { get; set; } = -1;

	[Property] public int GridX { get; set; } = -1;

	[Property] public int GridY { get; set; } = -1;

	[Property] public int DoorSide { get; set; } = -1;

	[Property] public int DoorIndex { get; set; }

	/// <summary>Building-root local position (inches). Mirrors <see cref="GameObject.LocalPosition"/> when parented.</summary>
	[Property] public Vector3 BuildingLocalPosition { get; set; }

	/// <summary>Building-root local yaw (degrees).</summary>
	[Property] public float BuildingLocalYawDegrees { get; set; }

	[Property] public bool UseCatalogWorldSize { get; set; } = true;

	[Property] public Vector3 WorldSizeInches { get; set; }

	[Property] public float InteriorDecorYawOffsetDegrees { get; set; }

	[Property, ReadOnly] public Vector3 AppliedLocalScale { get; private set; }

	[Property, ReadOnly] public Vector3 PlacementOffsetFromCellCenterBuildingLocal { get; private set; }

	Vector3 _lastLocalPosition;
	float _lastLocalYawDegrees;
	Vector3 _lastWorldSizeInches;
	bool _lastUseCatalogWorldSize;
	string _lastStructureDefId;
	bool _hasAppliedOnce;
	bool _pivotFloorAligned;
	int _appliedCatalogRevision = -1;

	public void InitializeFromSpawn( in SpawnContext ctx, string structureDefId )
	{
		StructureDefId = structureDefId;
		BuildingRoot = ctx.BuildingRoot;
		Story = ctx.Story;
		GridX = ctx.GridX;
		GridY = ctx.GridY;
		DoorSide = ctx.DoorSide;
		DoorIndex = ctx.DoorIndex;

		if ( !GameObject.IsValid() || !BuildingRoot.IsValid() )
			return;

		BuildingLocalPosition = BuildingRoot.WorldRotation.Inverse * (GameObject.WorldPosition - BuildingRoot.WorldPosition);
		BuildingLocalYawDegrees = (BuildingRoot.WorldRotation.Inverse * GameObject.WorldRotation).Angles().yaw;
		if ( Story >= 0
		     && GridX >= 0
		     && GridY >= 0
		     && ThornsPlaceableFurnitureCatalog.TryGetInteriorPlacementTune(
			     structureDefId,
			     Story,
			     GridX,
			     GridY,
			     GetWidthCellsGuess(),
			     GetDepthCellsGuess(),
			     out var spawnTune ) )
		{
			BuildingLocalYawDegrees = spawnTune.BuildingLocalYawDegrees;
			if ( ThornsProcBuildingInteriorSample.TryGridCellCenterLocalPublic(
				     GridX,
				     GridY,
				     GetWidthCellsGuess(),
				     GetDepthCellsGuess(),
				     ThornsBuildingModule.Cell,
				     out var lx,
				     out var ly ) )
			{
				BuildingLocalPosition = ThornsPlaceableFurnitureCatalog.BuildInteriorFurnitureLocalPosition(
					Story,
					lx,
					ly,
					spawnTune.OffsetFromCellCenterBuildingLocal );
			}
		}

		if ( ThornsPlaceableFurnitureCatalog.TryGet( structureDefId, out var entry ) )
			InteriorDecorYawOffsetDegrees = entry.InteriorDecorYawOffsetDegrees;

		PullInspectorWorldSizeFromCatalog();
		GameObject.SetParent( BuildingRoot, true );
		GameObject.LocalPosition = BuildingLocalPosition;
		GameObject.LocalRotation = Rotation.FromYaw( BuildingLocalYawDegrees );

		ApplyPresentation( reseatPivotOnFloor: true );
		RememberInspectorState();
	}

	protected override void OnStart()
	{
		if ( !Game.IsPlaying || _hasAppliedOnce )
			return;

		ApplyPresentation( reseatPivotOnFloor: true );
		RememberInspectorState();
	}

	protected override void OnValidate()
	{
		if ( !GameObject.IsValid() )
			return;

		if ( !Game.IsPlaying )
		{
			PullInspectorWorldSizeFromCatalog();
			return;
		}

		DetectManualSizeOverride();
		if ( UseCatalogWorldSize )
			PullInspectorWorldSizeFromCatalog();

		SyncInspectorFromTransform();
		ApplyPresentation();
		RememberInspectorState();
	}

	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying || !_hasAppliedOnce )
			return;

		DetectManualSizeOverride();

		var catalogRevision = ThornsPlaceableFurniturePresentation.CatalogRevision;
		var catalogRevisionChanged = UseCatalogWorldSize && catalogRevision != _appliedCatalogRevision;
		var modeChanged = UseCatalogWorldSize != _lastUseCatalogWorldSize;
		var idChanged = !string.Equals( _lastStructureDefId, StructureDefId, StringComparison.OrdinalIgnoreCase );

		const float eps = 0.5f;
		var sizeChanged = ( WorldSizeInches - _lastWorldSizeInches ).LengthSquared > eps * eps;
		var moved = ( GameObject.LocalPosition - _lastLocalPosition ).LengthSquared > eps * eps
		            || MathF.Abs( GameObject.LocalRotation.Angles().yaw - _lastLocalYawDegrees ) > eps;

		if ( !catalogRevisionChanged && !modeChanged && !idChanged && !sizeChanged && !moved )
			return;

		if ( moved )
		{
			SyncInspectorFromTransform();
			_pivotFloorAligned = true;
		}

		if ( UseCatalogWorldSize && ( catalogRevisionChanged || modeChanged || idChanged ) )
			PullInspectorWorldSizeFromCatalog();

		var reseatPivot = sizeChanged || idChanged || catalogRevisionChanged || !_pivotFloorAligned;
		if ( reseatPivot || catalogRevisionChanged )
			ApplyPresentation( reseatPivot );

		RememberInspectorState();
	}

	void SyncInspectorFromTransform()
	{
		BuildingLocalPosition = GameObject.LocalPosition;
		BuildingLocalYawDegrees = GameObject.LocalRotation.Angles().yaw;
	}

	public void PullInspectorWorldSizeFromCatalog()
	{
		if ( string.IsNullOrWhiteSpace( StructureDefId ) )
			return;

		WorldSizeInches = ThornsPlaceableFurnitureCatalog.GetWorldSizeInches( StructureDefId );
	}

	[Button( "Reset Size From Catalog" )]
	public void ResetSizeFromCatalog()
	{
		UseCatalogWorldSize = true;
		PullInspectorWorldSizeFromCatalog();
		if ( Game.IsPlaying )
			ApplyPresentation();
	}

	public void ApplyPresentation( bool reseatPivotOnFloor = true )
	{
		if ( !ThornsPlaceableFurnitureCatalog.TryGet( StructureDefId, out var entry ) )
		{
			Log.Warning( $"[Thorns FloorplanTune] Unknown id '{StructureDefId}'" );
			return;
		}

		if ( !BuildingRoot.IsValid() )
			return;

		if ( !GameObject.Parent.IsValid() || GameObject.Parent != BuildingRoot )
			GameObject.SetParent( BuildingRoot, true );

		var catalogSize = ThornsPlaceableFurnitureCatalog.GetWorldSizeInches( StructureDefId );
		var worldSize = UseCatalogWorldSize
			? catalogSize
			: WorldSizeInches.LengthSquared >= 1f
				? WorldSizeInches
				: catalogSize;

		var sizedEntry = entry with
		{
			WorldSizeInches = worldSize,
			InteriorDecorYawOffsetDegrees = InteriorDecorYawOffsetDegrees
		};

		if ( Story >= 0
		     && GridX >= 0
		     && GridY >= 0
		     && ThornsPlaceableFurnitureCatalog.TryGetInteriorPlacementTune(
			     StructureDefId,
			     Story,
			     GridX,
			     GridY,
			     GetWidthCellsGuess(),
			     GetDepthCellsGuess(),
			     out var tune ) )
		{
			BuildingLocalYawDegrees = tune.BuildingLocalYawDegrees;
			if ( ThornsProcBuildingInteriorSample.TryGridCellCenterLocalPublic(
				     GridX,
				     GridY,
				     GetWidthCellsGuess(),
				     GetDepthCellsGuess(),
				     ThornsBuildingModule.Cell,
				     out var lx,
				     out var ly ) )
			{
				BuildingLocalPosition = ThornsPlaceableFurnitureCatalog.BuildInteriorFurnitureLocalPosition(
					Story,
					lx,
					ly,
					tune.OffsetFromCellCenterBuildingLocal );
			}
		}

		GameObject.LocalPosition = BuildingLocalPosition;
		GameObject.LocalRotation = Rotation.FromYaw( BuildingLocalYawDegrees );

		ThornsPlaceableFurniturePresentation.Apply( GameObject, in sizedEntry, honorEntryWorldSize: !UseCatalogWorldSize );

		if ( reseatPivotOnFloor )
		{
			var worldRot = GameObject.WorldRotation;
			var worldPos = GameObject.WorldPosition;
			if ( Story >= 0 )
				ThornsProcBuildingInteriorSample.SnapInteriorFurniturePivot(
					BuildingRoot,
					Story,
					in sizedEntry,
					worldRot,
					ref worldPos );
			else
				ThornsPlaceableFurniturePresentation.AlignPlacementPivotOnSurface( in sizedEntry, ref worldPos, worldRot );

			GameObject.WorldPosition = worldPos;
			_pivotFloorAligned = true;
		}

		BuildingLocalPosition = GameObject.LocalPosition;
		BuildingLocalYawDegrees = GameObject.LocalRotation.Angles().yaw;
		AppliedLocalScale = GameObject.LocalScale;
		if ( UseCatalogWorldSize )
			WorldSizeInches = worldSize;

		UpdatePlacementOffsetReadonly( worldSize );
		UpdateObjectName( worldSize );

		_appliedCatalogRevision = ThornsPlaceableFurniturePresentation.CatalogRevision;
		_hasAppliedOnce = true;
	}

	void UpdatePlacementOffsetReadonly( Vector3 worldSize )
	{
		PlacementOffsetFromCellCenterBuildingLocal = Vector3.Zero;
		if ( GridX < 0 || GridY < 0 || !BuildingRoot.IsValid() )
			return;

		if ( !ThornsProcBuildingInteriorSample.TryGridCellCenterLocalPublic(
			     GridX,
			     GridY,
			     Math.Max( 1, GetWidthCellsGuess() ),
			     Math.Max( 1, GetDepthCellsGuess() ),
			     ThornsBuildingModule.Cell,
			     out var lx,
			     out var ly ) )
			return;

		var floorZ = Story >= 0
			? ThornsProcBuildingInteriorSample.InteriorFloorWalkLocalZForStory( Story )
			: BuildingLocalPosition.z;
		var anchor = new Vector3( lx, ly, floorZ );
		PlacementOffsetFromCellCenterBuildingLocal = BuildingLocalPosition - anchor;
		_ = worldSize;
	}

	int GetWidthCellsGuess() =>
		BuildingRoot.IsValid() && ThornsProcBuildingLayoutHost.TryGet( BuildingRoot ) is { } layout
			? layout.WidthCells
			: ThornsInteriorFurnitureFloorplanAscii.TestWidthCells;

	int GetDepthCellsGuess() =>
		BuildingRoot.IsValid() && ThornsProcBuildingLayoutHost.TryGet( BuildingRoot ) is { } layout
			? layout.DepthCells
			: ThornsInteriorFurnitureFloorplanAscii.TestDepthCells;

	void UpdateObjectName( Vector3 worldSize )
	{
		var cell = GridX >= 0 && GridY >= 0 ? $" ({GridX},{GridY})" : "";
		var story = Story >= 0 ? $" F{Story}" : "";
		GameObject.Name =
			$"Tune_{StructureDefId}{story}{cell} {worldSize.x:F0}x{worldSize.y:F0}x{worldSize.z:F0}in";
	}

	void DetectManualSizeOverride()
	{
		if ( !UseCatalogWorldSize || string.IsNullOrWhiteSpace( StructureDefId ) )
			return;

		var catalogSize = ThornsPlaceableFurnitureCatalog.GetWorldSizeInches( StructureDefId );
		if ( ( WorldSizeInches - catalogSize ).LengthSquared <= 0.25f )
			return;

		UseCatalogWorldSize = false;
	}

	void RememberInspectorState()
	{
		_lastStructureDefId = StructureDefId;
		_lastWorldSizeInches = WorldSizeInches;
		_lastUseCatalogWorldSize = UseCatalogWorldSize;
		_lastLocalPosition = GameObject.LocalPosition;
		_lastLocalYawDegrees = GameObject.LocalRotation.Angles().yaw;
	}

	public string BuildExportSnippet( string buildingLabel = "" )
	{
		var sb = new StringBuilder();
		var label = string.IsNullOrWhiteSpace( buildingLabel ) ? StructureDefId : buildingLabel;
		sb.AppendLine( $"// --- {label} story={Story} grid=({GridX},{GridY}) doorSide={DoorSide} ---" );
		sb.AppendLine(
			$"\"{StructureDefId}\" => Inches( {WorldSizeInches.x:F1}f, {WorldSizeInches.y:F1}f, {WorldSizeInches.z:F1}f )," );

		var off = PlacementOffsetFromCellCenterBuildingLocal;
		if ( off.LengthSquared >= 0.01f )
		{
			sb.AppendLine(
				$"GetInteriorPlacementLocalOffsetInches( \"{StructureDefId}\" ) => new Vector3( {off.x:F1}f, {off.y:F1}f, {off.z:F1}f )," );
		}

		sb.AppendLine( $"// building-local pose: pos=({BuildingLocalPosition.x:F1}, {BuildingLocalPosition.y:F1}, {BuildingLocalPosition.z:F1}) yaw={BuildingLocalYawDegrees:F1}" );
		sb.AppendLine( $"// catalog InteriorDecorYawOffsetDegrees: {InteriorDecorYawOffsetDegrees:F1}" );

		if ( string.Equals( StructureDefId, "retail", StringComparison.OrdinalIgnoreCase ) )
		{
			sb.AppendLine( "// retail fixed building-local pose (ThornsPlaceableFurnitureCatalog):" );
			sb.AppendLine( $"//   RetailBuildingLocalX = {ThornsPlaceableFurnitureCatalog.RetailBuildingLocalX:F1}f;" );
			sb.AppendLine( $"//   RetailBuildingLocalY = {ThornsPlaceableFurnitureCatalog.RetailBuildingLocalY:F1}f;" );
			sb.AppendLine( $"//   RetailBuildingLocalYawDegrees = {ThornsPlaceableFurnitureCatalog.RetailBuildingLocalYawDegrees:F1}f;" );
		}

		return sb.ToString();
	}
}
