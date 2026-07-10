namespace Terraingen.Buildings;

/// <summary>
/// Placeable furniture sizing — <see cref="ThornsPlaceableFurnitureCatalog.GetWorldSizeInches"/> is the target world size (matches thorns).
/// </summary>
public static class ThornsPlaceableFurnitureScale
{
	public const int CatalogRevision = 44;

	public const float ProcInteriorHeightTrimInches = 5f;

	public const float ProcInteriorTunedLocalScaleDownZ = 5f;

	public static Vector3 ApplyProcInteriorLocalScaleAdjust( Vector3 localScale ) =>
		new(
			localScale.x,
			localScale.y,
			MathF.Max( 0.01f, localScale.z - ProcInteriorTunedLocalScaleDownZ ) );

	public static Vector3 ApplyProcInteriorTunedLocalScaleIfNeeded( string structureDefId, Vector3 tunedLocalScale )
	{
		if ( string.Equals( structureDefId, "desk", StringComparison.OrdinalIgnoreCase )
		     || string.Equals( structureDefId, "military_supply", StringComparison.OrdinalIgnoreCase )
		     )
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

	/// <summary>Sets mesh world scale (compensates for parent <see cref="GameObject.WorldScale"/>).</summary>
	public static void ApplyWorldScale( GameObject root, Vector3 desiredWorldScale ) =>
		ApplyLocalScale( root, desiredWorldScale );

	public static void ApplyLocalScale( GameObject root, Vector3 desiredLocalScale )
	{
		if ( !root.IsValid() )
			return;

		var parent = root.Parent;
		if ( !parent.IsValid() )
		{
			root.LocalScale = desiredLocalScale;
			return;
		}

		var pw = parent.WorldScale;
		root.LocalScale = new Vector3(
			desiredLocalScale.x / MathF.Max( MathF.Abs( pw.x ), 1e-6f ),
			desiredLocalScale.y / MathF.Max( MathF.Abs( pw.y ), 1e-6f ),
			desiredLocalScale.z / MathF.Max( MathF.Abs( pw.z ), 1e-6f ) );
	}
}
