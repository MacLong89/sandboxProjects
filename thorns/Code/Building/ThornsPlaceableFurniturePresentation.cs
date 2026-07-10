namespace Sandbox;

/// <summary>
/// Placeable furniture visuals: model, collision, UV. Sizing is <see cref="ThornsPlaceableFurnitureScale"/> only.
/// Gallery, player kits, build ghost, and proc interior scatter all call <see cref="Apply"/> — same sizes as <see cref="ThornsPlaceableFurnitureCatalog.GetWorldSizeInches"/>.
/// </summary>
public static class ThornsPlaceableFurniturePresentation
{
	public static int CatalogRevision => ThornsPlaceableFurnitureScale.CatalogRevision;

	public static bool TryGetEntry( string structureDefId, out ThornsPlaceableFurnitureCatalog.Entry entry ) =>
		ThornsPlaceableFurnitureCatalog.TryGet( structureDefId, out entry );

	public static Model LoadModel( in ThornsPlaceableFurnitureCatalog.Entry entry ) =>
		ThornsBuildingVisuals.PlaceableFurnitureModel( entry.StructureDefId );

	/// <summary>Chest / campfire use dedicated visuals; everything else in the catalog uses <see cref="Apply"/>.</summary>
	public static bool UsesCatalogPresentation( string structureDefId ) =>
		TryGetEntry( structureDefId, out _ )
		&& structureDefId is not ("storage_chest" or "campfire");

	/// <summary>Applies catalog world size, mesh, collision, and UV on <paramref name="root"/>.</summary>
	/// <param name="honorEntryWorldSize">Gallery manual override only — game/scatter always use <see cref="ThornsPlaceableFurnitureCatalog.GetWorldSizeInches"/>.</param>
	public static void Apply( GameObject root, in ThornsPlaceableFurnitureCatalog.Entry entry, bool honorEntryWorldSize = false )
	{
		if ( !root.IsValid() )
			return;

		var catalogSize = ThornsPlaceableFurnitureCatalog.GetWorldSizeInches( entry.StructureDefId );
		var worldSize = honorEntryWorldSize && entry.WorldSizeInches.LengthSquared >= 1f
			? entry.WorldSizeInches
			: catalogSize;

		var sized = entry with { WorldSizeInches = worldSize };

		var model = LoadModel( in sized );
		if ( !model.IsValid() || model.IsError )
		{
			Log.Warning( $"[Thorns] PlaceableFurniture: model failed '{entry.ModelPath}'" );
			return;
		}

		var scale = ResolvePresentationLocalScale( in sized, model, sized.WorldSizeInches );
		ThornsPlaceableFurnitureScale.ApplyWorldScale( root, scale );

		var mr = root.Components.Get<ModelRenderer>( FindMode.EnabledInSelf );
		if ( !mr.IsValid() )
			mr = root.Components.Create<ModelRenderer>();

		mr.Enabled = true;
		mr.Model = model;
		mr.Tint = Color.White;
		ThornsModelMaterialUvScale.ApplyForPlaceableFurniture( mr, model, sized.ModelPath, scale );
		ThornsModelMaterialUvScale.EnsureFixupOnHierarchy( root, includeChildren: true );

		ThornsAnchoredWorldPhysics.EnsureAnchoredBoxPhysicsMatchVisualMesh( root, model );
	}

	/// <summary>Mesh local scale used by <see cref="Apply"/> and floor alignment (includes tuned overrides).</summary>
	public static Vector3 ResolvePresentationLocalScale(
		in ThornsPlaceableFurnitureCatalog.Entry entry,
		Model model,
		Vector3 worldSizeInches )
	{
		if ( ThornsPlaceableFurnitureCatalog.TryGetInteriorPlacementLocalScale(
			     entry.StructureDefId,
			     out var tunedScale ) )
			return ThornsPlaceableFurnitureScale.ApplyProcInteriorTunedLocalScaleIfNeeded(
				entry.StructureDefId,
				tunedScale );

		return ThornsPlaceableFurnitureScale.ComputeLocalScale(
			model,
			worldSizeInches,
			entry.StructureDefId );
	}

	public static void Apply( GameObject root, string structureDefId )
	{
		if ( !TryGetEntry( structureDefId, out var entry ) )
			return;

		Apply( root, in entry );
	}

	public static void AlignPlacementPivotOnSurface(
		string structureDefId,
		ref Vector3 worldPosition,
		Rotation worldRotation )
	{
		if ( !TryGetEntry( structureDefId, out var entry ) )
			return;

		AlignPlacementPivotOnSurface( in entry, ref worldPosition, worldRotation );
	}

	public static void AlignPlacementPivotOnSurface(
		in ThornsPlaceableFurnitureCatalog.Entry entry,
		ref Vector3 worldPosition,
		Rotation worldRotation )
	{
		var model = LoadModel( in entry );
		if ( !model.IsValid() || model.IsError )
			return;

		var worldSize = entry.WorldSizeInches.LengthSquared >= 1f
			? entry.WorldSizeInches
			: ThornsPlaceableFurnitureCatalog.GetWorldSizeInches( entry.StructureDefId );
		var scale = ResolvePresentationLocalScale( in entry, model, worldSize );
		worldPosition = ThornsFoliageScatter.AlignPivotWorldPositionMeshBottomOnGround(
			worldPosition,
			model,
			scale,
			worldRotation );
	}

	public static float PlacementPlanarHalfExtent( in ThornsPlaceableFurnitureCatalog.Entry entry ) =>
		ThornsPlaceableFurnitureScale.PlanarRadius( in entry );

	public static void LogCatalogScaleProbe( string context, params string[] structureDefIds ) =>
		ThornsPlaceableFurnitureScale.LogProbe( context, structureDefIds );

	// Legacy names used by a few call sites.
	public static Vector3 GetLocalScale( in ThornsPlaceableFurnitureCatalog.Entry entry, Model model, float extraMul = 1f )
	{
		var sized = entry with
		{
			WorldSizeInches = ThornsPlaceableFurnitureCatalog.GetWorldSizeInches( entry.StructureDefId )
		};
		var scale = ThornsPlaceableFurnitureScale.ComputeLocalScale(
			model,
			sized.WorldSizeInches,
			sized.StructureDefId );
		if ( MathF.Abs( extraMul - 1f ) < 1e-4f )
			return scale;

		return scale * extraMul;
	}

	public static Vector3 GetLocalScale( string structureDefId, float extraMul = 1f )
	{
		if ( !TryGetEntry( structureDefId, out var entry ) )
			return Vector3.One;

		var model = LoadModel( in entry );
		return GetLocalScale( in entry, model, extraMul );
	}

	public static bool UsesUnifiedPresentation( string structureDefId ) => UsesCatalogPresentation( structureDefId );
}
