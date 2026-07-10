namespace Sandbox;

/// <summary>
/// Sole placeable furniture sizing: <see cref="ThornsPlaceableFurnitureCatalog.GetWorldSizeInches"/> is the final world size (gallery WYSIWYG = proc scatter / kits).
/// </summary>
public static class ThornsPlaceableFurnitureScale
{
	/// <summary>Bump after editing sizing so proc props refresh on host load.</summary>
	public const int CatalogRevision = 41;

	/// <summary>Height trim (inches) before mesh scale is computed — visible on all catalog-sized props.</summary>
	public const float ProcInteriorHeightTrimInches = 5f;

	/// <summary>Extra trim on inspector-tuned mesh local scale Z (desk/bed/cabinet overrides).</summary>
	public const float ProcInteriorTunedLocalScaleDownZ = 5f;

	public static Vector3 ApplyProcInteriorLocalScaleAdjust( Vector3 localScale ) =>
		new Vector3(
			localScale.x,
			localScale.y,
			MathF.Max( 0.01f, localScale.z - ProcInteriorTunedLocalScaleDownZ ) );

	/// <summary>Desk scale is exported at final mesh local scale; bed/cabinet still get Z trim.</summary>
	public static Vector3 ApplyProcInteriorTunedLocalScaleIfNeeded( string structureDefId, Vector3 tunedLocalScale )
	{
		if ( string.Equals( structureDefId, "desk", StringComparison.OrdinalIgnoreCase )
		     || string.Equals( structureDefId, "military_supply", StringComparison.OrdinalIgnoreCase )
		     || string.Equals( structureDefId, "bunk", StringComparison.OrdinalIgnoreCase ) )
			return tunedLocalScale;

		return ApplyProcInteriorLocalScaleAdjust( tunedLocalScale );
	}

	public static Vector3 EffectiveWorldSizeInches( Vector3 catalogWorldSizeInches, string structureDefId = null )
	{
		_ = structureDefId;
		return new Vector3(
			MathF.Max( 1f, catalogWorldSizeInches.x ),
			MathF.Max( 1f, catalogWorldSizeInches.y ),
			MathF.Max( 1f, catalogWorldSizeInches.z - ProcInteriorHeightTrimInches ) );
	}

	public static Vector3 EffectiveWorldSizeInches( in ThornsPlaceableFurnitureCatalog.Entry entry ) =>
		EffectiveWorldSizeInches( entry.WorldSizeInches, entry.StructureDefId );

	/// <summary>Mesh local scale so render bounds match catalog world size (X width, Y depth, Z height).</summary>
	public static Vector3 ComputeLocalScale( Model model, Vector3 worldSizeInches, string structureDefId = null )
	{
		var want = EffectiveWorldSizeInches( worldSizeInches, structureDefId );
		want = new Vector3(
			MathF.Max( 1f, want.x ),
			MathF.Max( 1f, want.y ),
			MathF.Max( 1f, want.z ) );

		if ( !model.IsValid() || model.IsError )
			return want / 24f;

		var size = model.Bounds.Size;
		if ( size.LengthSquared < 1e-8f )
			size = new Vector3( 24f, 24f, 32f );

		return new Vector3(
			want.x / MathF.Max( size.x, 1e-4f ),
			want.y / MathF.Max( size.y, 1e-4f ),
			want.z / MathF.Max( size.z, 1e-4f ) );
	}

	public static Vector3 ComputeLocalScale( in ThornsPlaceableFurnitureCatalog.Entry entry, Model model ) =>
		ComputeLocalScale( model, entry.WorldSizeInches, entry.StructureDefId );

	public static Vector3 ComputeLocalScale( string structureDefId )
	{
		if ( !ThornsPlaceableFurnitureCatalog.TryGet( structureDefId, out var entry ) )
			return Vector3.One;

		var model = Model.Load( entry.ModelPath );
		return ComputeLocalScale( in entry, model );
	}

	/// <summary>Half-extent on XY for placement overlap (from effective world size).</summary>
	public static float PlanarRadius( in ThornsPlaceableFurnitureCatalog.Entry entry )
	{
		var w = ThornsPlaceableFurnitureCatalog.GetWorldSizeInches( entry.StructureDefId );
		return MathF.Max( w.x, w.y ) * 0.5f + 4f;
	}

	public static void ApplyWorldScale( GameObject root, Vector3 desiredWorldScale )
	{
		if ( !root.IsValid() )
			return;

		var parent = root.Parent;
		if ( !parent.IsValid() )
		{
			root.LocalScale = desiredWorldScale;
			return;
		}

		var pw = parent.WorldScale;
		root.LocalScale = new Vector3(
			desiredWorldScale.x / MathF.Max( MathF.Abs( pw.x ), 1e-6f ),
			desiredWorldScale.y / MathF.Max( MathF.Abs( pw.y ), 1e-6f ),
			desiredWorldScale.z / MathF.Max( MathF.Abs( pw.z ), 1e-6f ) );
	}

	/// <summary>Approximate world-axis extents after scale (model bounds × world scale).</summary>
	public static Vector3 EstimateWorldExtents( Model model, Vector3 meshLocalScale, GameObject host )
	{
		if ( !model.IsValid() || model.IsError )
			return Vector3.Zero;

		var bounds = model.Bounds.Size;
		var ws = host.IsValid() ? host.WorldScale : meshLocalScale;
		return new Vector3( bounds.x * ws.x, bounds.y * ws.y, bounds.z * ws.z );
	}

	public static void LogProbe( string context, params string[] structureDefIds )
	{
		foreach ( var id in structureDefIds )
		{
			if ( !ThornsPlaceableFurnitureCatalog.TryGet( id, out var entry ) )
				continue;

			var model = Model.Load( entry.ModelPath );
			var w = ThornsPlaceableFurnitureCatalog.GetWorldSizeInches( id );
			var scale = ComputeLocalScale( model, w, id );
			var effective = EffectiveWorldSizeInches( w, id );
			Log.Info(
				$"[Thorns] Furniture size ({context}) id={id} rev={CatalogRevision} "
				+ $"catalogIn={w.x:F0},{w.y:F0},{w.z:F0} effectiveIn={effective.x:F0},{effective.y:F0},{effective.z:F0} "
				+ $"meshLocalScale={scale} modelBounds={model.Bounds.Size}" );
		}
	}
}
