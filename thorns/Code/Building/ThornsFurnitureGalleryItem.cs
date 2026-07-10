namespace Sandbox;

/// <summary>
/// One catalog furniture piece in <see cref="ThornsFurnitureScaleGallery"/>.
/// Edit <see cref="WorldSizeInches"/> in the inspector (width X, depth Y, height Z) — same path as proc scatter / player kits.
/// </summary>
[Title( "Thorns — Furniture Gallery Item" )]
[Category( "Thorns/Dev" )]
[Icon( "chair" )]
public sealed class ThornsFurnitureGalleryItem : Component
{
	[Property] public string StructureDefId { get; set; } = "";

	/// <summary>When true, size is pulled from <see cref="ThornsPlaceableFurnitureCatalog"/> on apply (uncheck to tune manually).</summary>
	[Property] public bool UseCatalogWorldSize { get; set; } = true;

	/// <summary>Target world size in inches (X width, Y depth, Z height). Mirrors catalog when <see cref="UseCatalogWorldSize"/>.</summary>
	[Property] public Vector3 WorldSizeInches { get; set; }

	[Property, ReadOnly] public Vector3 AppliedLocalScale { get; private set; }

	Vector3 _lastWorldSizeInches;
	string _lastStructureDefId;
	bool _lastUseCatalogWorldSize;
	bool _hasAppliedOnce;
	int _appliedCatalogRevision = -1;

	protected override void OnStart()
	{
		if ( !Game.IsPlaying )
			return;

		ApplyPresentation();
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

		if ( !catalogRevisionChanged && !modeChanged && !idChanged && !sizeChanged )
			return;

		if ( UseCatalogWorldSize && ( catalogRevisionChanged || modeChanged || idChanged ) )
			PullInspectorWorldSizeFromCatalog();

		ApplyPresentation();
		RememberInspectorState();
	}

	/// <summary>Apply using a catalog row directly (used when the gallery spawns props).</summary>
	public void ApplyFromCatalog( in ThornsPlaceableFurnitureCatalog.Entry catalogEntry )
	{
		StructureDefId = catalogEntry.StructureDefId;
		UseCatalogWorldSize = true;
		PullInspectorWorldSizeFromCatalog();
		ApplyPresentation( in catalogEntry );
		RememberInspectorState();
	}

	/// <summary>Updates inspector fields from the catalog (edit mode safe).</summary>
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

	public void ApplyPresentation()
	{
		if ( !ThornsPlaceableFurnitureCatalog.TryGet( StructureDefId, out var entry ) )
		{
			Log.Warning( $"[Thorns FurnitureGallery] Unknown id '{StructureDefId}'" );
			return;
		}

		ApplyPresentation( in entry );
	}

	public void ApplyPresentation( in ThornsPlaceableFurnitureCatalog.Entry catalogEntry )
	{
		if ( string.IsNullOrWhiteSpace( catalogEntry.StructureDefId ) )
			return;

		StructureDefId = catalogEntry.StructureDefId;

		var catalogSize = ThornsPlaceableFurnitureCatalog.GetWorldSizeInches( catalogEntry.StructureDefId );
		var worldSize = UseCatalogWorldSize
			? catalogSize
			: WorldSizeInches.LengthSquared >= 1f
				? WorldSizeInches
				: catalogSize;

		var sizedEntry = catalogEntry with { WorldSizeInches = worldSize };

		ThornsPlaceableFurniturePresentation.Apply( GameObject, in sizedEntry, honorEntryWorldSize: !UseCatalogWorldSize );

		var pos = GameObject.WorldPosition;
		var rot = GameObject.WorldRotation;
		ThornsPlaceableFurniturePresentation.AlignPlacementPivotOnSurface( in sizedEntry, ref pos, rot );
		GameObject.WorldPosition = pos;

		AppliedLocalScale = GameObject.LocalScale;
		if ( UseCatalogWorldSize )
			WorldSizeInches = worldSize;

		GameObject.Name =
			$"Gallery_{StructureDefId} {worldSize.x:F0}x{worldSize.y:F0}x{worldSize.z:F0}in";

		_appliedCatalogRevision = ThornsPlaceableFurniturePresentation.CatalogRevision;
		_hasAppliedOnce = true;
	}

	void DetectManualSizeOverride()
	{
		if ( !UseCatalogWorldSize || string.IsNullOrWhiteSpace( StructureDefId ) )
			return;

		var catalogSize = ThornsPlaceableFurnitureCatalog.GetWorldSizeInches( StructureDefId );
		if ( ( WorldSizeInches - catalogSize ).LengthSquared <= 0.25f )
			return;

		UseCatalogWorldSize = false;
		Log.Info(
			$"[Thorns FurnitureGallery] Manual size on '{StructureDefId}' ({WorldSizeInches.x:F0}x{WorldSizeInches.y:F0}x{WorldSizeInches.z:F0}in) — " +
			"copy values into ThornsPlaceableFurnitureCatalog.GetWorldSizeInches, bump CatalogRevision, then host a new world." );
	}

	void RememberInspectorState()
	{
		_lastStructureDefId = StructureDefId;
		_lastWorldSizeInches = WorldSizeInches;
		_lastUseCatalogWorldSize = UseCatalogWorldSize;
	}
}
