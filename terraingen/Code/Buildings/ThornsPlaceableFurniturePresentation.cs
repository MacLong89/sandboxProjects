namespace Terraingen.Buildings;

using Terraingen;
using Terraingen.Physics;
using Terraingen.Rendering;

/// <summary>Catalog furniture visuals and scale (matches thorns <c>ThornsPlaceableFurniturePresentation</c>).</summary>
public static class ThornsPlaceableFurniturePresentation
{
	public static Model LoadModel( string structureDefId ) =>
		ThornsPlaceableModels.LoadPlaceableModel( ThornsPlaceableFurnitureCatalog.GetModelPath( structureDefId ) );

	public static void Apply( GameObject root, string structureDefId ) =>
		Apply( root, structureDefId, storyIndex: -1, gridX: -1, gridY: -1 );

	public static void Apply(
		GameObject root,
		string structureDefId,
		int storyIndex,
		int gridX,
		int gridY,
		int widthCells = ThornsProcBuildingInterior.GridCells,
		int depthCells = ThornsProcBuildingInterior.GridCells )
	{
		if ( !root.IsValid() || string.IsNullOrWhiteSpace( structureDefId ) )
			return;

		var worldSize = ThornsPlaceableFurnitureCatalog.GetWorldSizeInches( structureDefId );
		var modelPath = ThornsPlaceableFurnitureCatalog.GetModelPath( structureDefId );
		var model = LoadModel( structureDefId );
		if ( !ThornsModelResourceLoad.IsUsable( model ) )
		{
			ThornsFurnitureMaterialDebug.Write(
				$"Apply ABORT id={structureDefId} modelPath={modelPath} mounted={ThornsModelResourceLoad.MountedVmdlExists( modelPath )} "
				+ $"modelValid={model is not null && model.IsValid} modelError={model is not null && model.IsError}" );
			Log.Warning( $"[Thorns Furniture] Model failed for '{structureDefId}'." );
			return;
		}

		var scale = ResolvePresentationLocalScale(
			structureDefId,
			model,
			worldSize,
			storyIndex,
			gridX,
			gridY,
			widthCells,
			depthCells );
		ThornsPlaceableFurnitureScale.ApplyWorldScale( root, scale );

		var mr = root.Components.Get<ModelRenderer>() ?? root.Components.Create<ModelRenderer>();
		mr.Enabled = true;
		mr.Model = model;
		mr.Tint = Color.White;
		ThornsWorldShadowUtil.EnableWorldShadows( mr );

		ThornsModelMaterialUvScale.ApplyForPlaceableFurniture( mr, model, modelPath, scale );
		ThornsModelMaterialUvScale.EnsureFixupOnHierarchy( root, includeChildren: true );

		ThornsFurnitureMaterialDebug.LogSpawn(
			"Apply",
			structureDefId,
			modelPath,
			model,
			root,
			usedFallbackBox: false,
			mr );

		TerraingenAnchoredPhysics.EnsureFurnitureCollider( root, model );
	}

	public static Vector3 ResolvePresentationLocalScale( string structureDefId, Model model, Vector3 worldSizeInches ) =>
		ResolvePresentationLocalScale(
			structureDefId,
			model,
			worldSizeInches,
			storyIndex: -1,
			gridX: -1,
			gridY: -1 );

	public static Vector3 ResolvePresentationLocalScale(
		string structureDefId,
		Model model,
		Vector3 worldSizeInches,
		int storyIndex,
		int gridX,
		int gridY,
		int widthCells = ThornsProcBuildingInterior.GridCells,
		int depthCells = ThornsProcBuildingInterior.GridCells )
	{
		if ( storyIndex >= 0
		     && gridX >= 0
		     && gridY >= 0
		     && ThornsPlaceableFurnitureCatalog.TryGetInteriorPlacementTune(
			     structureDefId,
			     storyIndex,
			     gridX,
			     gridY,
			     widthCells,
			     depthCells,
			     out var tune )
		     && tune.LocalScaleOverride.HasValue )
		{
			return ThornsPlaceableFurnitureScale.ApplyProcInteriorTunedLocalScaleIfNeeded(
				structureDefId,
				tune.LocalScaleOverride.Value );
		}

		if ( ThornsPlaceableFurnitureCatalog.TryGetInteriorPlacementLocalScale( structureDefId, out var tunedScale ) )
			return ThornsPlaceableFurnitureScale.ApplyProcInteriorTunedLocalScaleIfNeeded( structureDefId, tunedScale );

		return ThornsPlaceableFurnitureScale.ComputeLocalScale( model, worldSizeInches, structureDefId );
	}

	public static void AlignPlacementPivotOnSurface(
		string structureDefId,
		ref Vector3 worldPosition,
		Rotation worldRotation ) =>
		AlignPlacementPivotOnSurface(
			structureDefId,
			ref worldPosition,
			worldRotation,
			storyIndex: -1,
			gridX: -1,
			gridY: -1 );

	public static void AlignPlacementPivotOnSurface(
		string structureDefId,
		ref Vector3 worldPosition,
		Rotation worldRotation,
		int storyIndex,
		int gridX,
		int gridY,
		int widthCells = ThornsProcBuildingInterior.GridCells,
		int depthCells = ThornsProcBuildingInterior.GridCells )
	{
		var model = LoadModel( structureDefId );
		if ( !ThornsModelResourceLoad.IsUsable( model ) )
			return;

		var worldSize = ThornsPlaceableFurnitureCatalog.GetWorldSizeInches( structureDefId );
		var scale = ResolvePresentationLocalScale(
			structureDefId,
			model,
			worldSize,
			storyIndex,
			gridX,
			gridY,
			widthCells,
			depthCells );
		worldPosition = ThornsFurniturePivotAlign.AlignPivotMeshBottomOnGround(
			worldPosition,
			model,
			scale,
			worldRotation );
	}
}
