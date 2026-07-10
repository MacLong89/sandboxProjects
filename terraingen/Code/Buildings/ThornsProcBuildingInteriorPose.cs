namespace Terraingen.Buildings;

/// <summary>Building-local furniture pose (ported from thorns <c>ThornsProcBuildingInteriorSample</c>).</summary>
public static class ThornsProcBuildingInteriorPose
{
	public readonly record struct SpawnContext(
		GameObject BuildingRoot,
		int WidthCells,
		int DepthCells,
		int Story,
		int GridX,
		int GridY );

	public static float InteriorFloorWalkLocalZ( int storyIndex ) =>
		ThornsProcBuildingInterior.InteriorFloorWalkLocalZ( storyIndex );

	public static void SnapBuildingLocal(
		GameObject buildingRoot,
		in SpawnContext ctx,
		string structureDefId,
		Rotation worldRotation,
		ref Vector3 buildingLocalPosition )
	{
		if ( buildingRoot is null || !buildingRoot.IsValid() || ctx.Story < 0 )
			return;

		buildingLocalPosition = new Vector3(
			buildingLocalPosition.x,
			buildingLocalPosition.y,
			InteriorFloorWalkLocalZ( ctx.Story ) );

		var worldPosition = buildingRoot.WorldPosition + buildingRoot.WorldRotation * buildingLocalPosition;
		ThornsPlaceableFurniturePresentation.AlignPlacementPivotOnSurface(
			structureDefId,
			ref worldPosition,
			worldRotation,
			ctx.Story,
			ctx.GridX,
			ctx.GridY,
			ctx.WidthCells,
			ctx.DepthCells );
		buildingLocalPosition = buildingRoot.WorldRotation.Inverse * ( worldPosition - buildingRoot.WorldPosition );
	}

	public static bool TryComputeScriptedBuildingLocal(
		in SpawnContext ctx,
		string structureDefId,
		Rotation worldRotation,
		out Vector3 buildingLocal,
		out Rotation buildingLocalRotation )
	{
		buildingLocal = default;
		buildingLocalRotation = Rotation.Identity;

		var buildingRoot = ctx.BuildingRoot;
		if ( buildingRoot is null || !buildingRoot.IsValid() )
			return false;

		if ( ctx.Story < 0 || ctx.GridX < 0 || ctx.GridY < 0 || ctx.WidthCells < 1 || ctx.DepthCells < 1 )
			return false;

		if ( !ThornsProcBuildingInterior.TryGridCellCenterLocal(
			     ctx.GridX,
			     ctx.GridY,
			     ctx.WidthCells,
			     ctx.DepthCells,
			     out var center ) )
			return false;

		var hasTune = ThornsPlaceableFurnitureCatalog.TryGetInteriorPlacementTune(
			structureDefId,
			ctx.Story,
			ctx.GridX,
			ctx.GridY,
			ctx.WidthCells,
			ctx.DepthCells,
			out var placementTune );
		var offsetBuildingLocal = hasTune
			? placementTune.OffsetFromCellCenterBuildingLocal
			: ThornsPlaceableFurnitureCatalog.GetInteriorPlacementLocalOffsetInches( structureDefId );

		buildingLocal = ThornsPlaceableFurnitureCatalog.BuildInteriorFurnitureLocalPosition(
			ctx.Story,
			center.x,
			center.y,
			offsetBuildingLocal );

		float buildingYawDeg;
		if ( hasTune )
			buildingYawDeg = placementTune.BuildingLocalYawDegrees;
		else if ( ThornsInteriorFurnitureCanonicalSlots.TryGetScriptedCornerYaw(
			          structureDefId,
			          ctx.Story,
			          ctx.GridX,
			          ctx.GridY,
			          ctx.WidthCells,
			          ctx.DepthCells,
			          out var cornerYawDeg ) )
			buildingYawDeg = cornerYawDeg;
		else if ( ThornsProcBuildingInterior.TryGetInteriorWallDeskYaw(
			          ctx.WidthCells,
			          ctx.DepthCells,
			          ctx.Story,
			          ctx.GridX,
			          ctx.GridY,
			          out var wallYawDeg ) )
			buildingYawDeg = wallYawDeg;
		else
			buildingYawDeg = ( buildingRoot.WorldRotation.Inverse * worldRotation ).Angles().yaw;

		buildingLocalRotation = Rotation.FromYaw( buildingYawDeg );

		SnapBuildingLocal(
			buildingRoot,
			in ctx,
			structureDefId,
			buildingRoot.WorldRotation * buildingLocalRotation,
			ref buildingLocal );
		return true;
	}

	public static void SeatOnBuilding(
		GameObject furnitureObject,
		GameObject buildingRoot,
		in SpawnContext ctx,
		string structureDefId,
		Rotation worldRotation,
		Vector3 fallbackWorldPosition )
	{
		if ( furnitureObject is null || !furnitureObject.IsValid()
		     || buildingRoot is null || !buildingRoot.IsValid() )
			return;

		if ( furnitureObject.Parent != buildingRoot )
			furnitureObject.Parent = buildingRoot;

		if ( !TryComputeScriptedBuildingLocal(
			     in ctx,
			     structureDefId,
			     worldRotation,
			     out var buildingLocal,
			     out var buildingLocalRotation ) )
		{
			buildingLocal = buildingRoot.WorldRotation.Inverse * ( fallbackWorldPosition - buildingRoot.WorldPosition );
			buildingLocalRotation = buildingRoot.WorldRotation.Inverse * worldRotation;
			buildingLocalRotation = Rotation.FromYaw( buildingLocalRotation.Angles().yaw );
			if ( ctx.Story >= 0 )
				SnapBuildingLocal(
					buildingRoot,
					in ctx,
					structureDefId,
					buildingRoot.WorldRotation * buildingLocalRotation,
					ref buildingLocal );
		}

		furnitureObject.LocalPosition = buildingLocal;
		furnitureObject.LocalRotation = buildingLocalRotation;
	}
}
