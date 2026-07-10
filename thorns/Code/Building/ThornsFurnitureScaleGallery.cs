namespace Sandbox;

/// <summary>
/// Dev layout: one of each placeable vmdl on a flat floor. Each child has <see cref="ThornsFurnitureGalleryItem"/>
/// so you can tune <c>WorldSizeInches</c> in the inspector without world-gen or proc buildings.
/// Open <c>scenes/thorns_furniture_gallery.scene</c> and press Play (or host that scene).
/// </summary>
[Title( "Thorns — Furniture Scale Gallery" )]
[Category( "Thorns/Dev" )]
[Icon( "grid_view" )]
public sealed class ThornsFurnitureScaleGallery : Component
{
	[Property] public Vector3 GridOrigin { get; set; } = new( 0f, 0f, 64f );

	[Property] public float ColumnSpacing { get; set; } = 480f;

	[Property] public float RowSpacing { get; set; } = 480f;

	[Property] public int Columns { get; set; } = 6;

	[Property] public bool IncludePlayerOnlyKits { get; set; }

	[Property] public bool RebuildOnStart { get; set; } = true;

	/// <summary>Re-spawn gallery props when <see cref="ThornsPlaceableFurnitureScale.CatalogRevision"/> changes.</summary>
	[Property] public bool RebuildWhenCatalogRevisionChanges { get; set; } = true;

	[Property, ReadOnly] public int LastAppliedCatalogRevision { get; private set; } = -1;

	[Property, Group( "Floor" )] public string FloorModelPath { get; set; } = "models/dev/box.vmdl";

	[Property, Group( "Floor" )] public Vector3 FloorScale { get; set; } = new( 120f, 120f, 4f );

	GameObject _floor;
	GameObject _spawn;
	GameObject _itemsRoot;

	/// <summary>World Z of the walkable top of <see cref="_floor"/> (same as <see cref="GridOrigin"/>.z).</summary>
	float FloorSurfaceWorldZ => GridOrigin.z;

	protected override void OnAwake()
	{
		if ( !Game.IsPlaying )
			return;

		EnsureFloor();
	}

	protected override void OnStart()
	{
		if ( !Game.IsPlaying )
			return;

		EnsureFloor();
		LogCatalogProbe();

		if ( RebuildOnStart )
			RebuildGallery();
	}

	protected override void OnValidate()
	{
		if ( !Game.IsPlaying || !GameObject.IsValid() )
			return;

		EnsureFloor();
	}

	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying || !RebuildWhenCatalogRevisionChanges )
			return;

		var rev = ThornsPlaceableFurniturePresentation.CatalogRevision;
		if ( rev == LastAppliedCatalogRevision )
			return;

		LastAppliedCatalogRevision = rev;
		SyncAllGalleryItemsFromCatalog();
	}

	public void RebuildGallery()
	{
		if ( !GameObject.IsValid() )
			return;

		EnsureFloor();
		_itemsRoot?.Destroy();
		_itemsRoot = new GameObject( true, "FurnitureGallery_Items" );
		_itemsRoot.Parent = GameObject;
		_itemsRoot.WorldPosition = GridOrigin;

		var columns = Math.Max( 1, Columns );
		var col = 0;
		var row = 0;

		foreach ( var raw in ThornsPlaceableFurnitureCatalog.All )
		{
			if ( raw.ExcludeFromProcInteriorScatter && !IncludePlayerOnlyKits )
				continue;

			if ( string.IsNullOrEmpty( raw.ModelPath ) )
				continue;

			if ( !ThornsPlaceableFurnitureCatalog.TryCreateSizedEntry( raw.StructureDefId, out var entry ) )
				continue;

			var offset = new Vector3(
				(col - (columns - 1) * 0.5f) * ColumnSpacing,
				row * RowSpacing,
				0f );

			var piece = new GameObject( true, $"Gallery_{entry.StructureDefId}" );
			piece.Parent = _itemsRoot;
			piece.WorldPosition = new Vector3(
				GridOrigin.x + offset.x,
				GridOrigin.y + offset.y,
				FloorSurfaceWorldZ );
			piece.WorldRotation = Rotation.FromYaw( entry.InteriorDecorYawOffsetDegrees );

			var item = piece.Components.Create<ThornsFurnitureGalleryItem>();
			item.ApplyFromCatalog( in entry );

			col++;
			if ( col >= columns )
			{
				col = 0;
				row++;
			}
		}

		LastAppliedCatalogRevision = ThornsPlaceableFurniturePresentation.CatalogRevision;

		Log.Info(
			$"[Thorns FurnitureGallery] Rebuilt {ThornsPlaceableFurnitureCatalog.All.Length} catalog entries at {GridOrigin} (cols={columns}, spacing={ColumnSpacing}x{RowSpacing})." );
	}

	void EnsureFloor()
	{
		if ( !_floor.IsValid() )
			CreateFloor();

		if ( !_spawn.IsValid() )
			CreateSpawnPoint();
	}

	void CreateFloor()
	{
		var model = Model.Load( FloorModelPath );
		if ( !model.IsValid() || model.IsError )
			model = ThornsAnchoredWorldPhysics.DevBoxCollisionModel;

		var planar = new Vector3(
			MathF.Max( 8f, FloorScale.x ),
			MathF.Max( 8f, FloorScale.y ),
			MathF.Max( 2f, FloorScale.z ) );

		_floor = new GameObject( true, "FurnitureGallery_Floor" );
		_floor.Parent = GameObject;
		_floor.LocalScale = planar;

		// Align mesh/collision top to <see cref="GridOrigin"/> Z (walk surface).
		var bb = model.Bounds;
		var topLocalZ = bb.Maxs.z;
		if ( topLocalZ < 1e-4f )
			topLocalZ = bb.Size.z * 0.5f + bb.Mins.z;
		_floor.WorldPosition = new Vector3(
			GridOrigin.x,
			GridOrigin.y,
			GridOrigin.z - topLocalZ * planar.z );

		var mr = _floor.Components.Create<ModelRenderer>();
		mr.Model = model;
		mr.Tint = new Color( 0.35f, 0.38f, 0.32f );

		ThornsAnchoredWorldPhysics.EnsureAnchoredBoxPhysics( _floor, model );
		ThornsCollisionTags.EnsureWorldSolidTriplet( _floor );
		_floor.Tags.Add( ThornsCollisionTags.FurnitureGalleryFloor );
	}

	void CreateSpawnPoint()
	{
		_spawn = new GameObject( true, "FurnitureGallery_Spawn" );
		_spawn.Parent = GameObject;
		_spawn.WorldPosition = GridOrigin + new Vector3( 0f, 0f, 72f );
		_spawn.Components.Create<SpawnPoint>();
	}

	/// <summary>Refresh sizes on existing <see cref="ThornsFurnitureGalleryItem"/> children without destroying the grid.</summary>
	public void SyncAllGalleryItemsFromCatalog()
	{
		foreach ( var item in Scene.GetAllComponents<ThornsFurnitureGalleryItem>() )
		{
			if ( item is null || !item.IsValid() )
				continue;

			if ( Game.IsPlaying )
			{
				if ( item.UseCatalogWorldSize )
					item.PullInspectorWorldSizeFromCatalog();
				item.ApplyPresentation();
			}
			else
			{
				item.PullInspectorWorldSizeFromCatalog();
			}
		}
	}

	static void LogCatalogProbe() =>
		ThornsPlaceableFurniturePresentation.LogCatalogScaleProbe( "furniture-gallery", "chair", "couch", "desk" );
}
