namespace Terraingen.World;

using Terraingen.Buildings;
using Terraingen.Physics;

/// <summary>Oriented XY exclusion footprints for procedural world scatter (buildings, boulders, trees, minerals).</summary>
public readonly struct ThornsWorldScatterFootprint
{
	public readonly float CenterX;
	public readonly float CenterY;
	public readonly float HalfW;
	public readonly float HalfD;
	public readonly float YawRad;
	public readonly float Margin;

	public ThornsWorldScatterFootprint( float centerX, float centerY, float halfW, float halfD, float yawRad, float margin )
	{
		CenterX = centerX;
		CenterY = centerY;
		HalfW = halfW;
		HalfD = halfD;
		YawRad = yawRad;
		Margin = margin;
	}
}

public static class ThornsWorldScatterFootprintRegistry
{
	[SkipHotload]
	static readonly List<ThornsWorldScatterFootprint> Footprints = new();

	public static int Count => Footprints.Count;

	public static void Clear() => Footprints.Clear();

	public static bool WouldBuildingFootprintOverlapRegistered(
		Vector3 worldPosition,
		Rotation worldRotation,
		int widthCells,
		int depthCells )
	{
		var fp = CreateBuildingPlacementFootprint( worldPosition, worldRotation, widthCells, depthCells );
		return WouldOverlapAnyRegistered( fp );
	}

	public static bool WouldBuildingPlacementsOverlap(
		Vector3 positionA,
		Rotation rotationA,
		int widthCellsA,
		int depthCellsA,
		Vector3 positionB,
		Rotation rotationB,
		int widthCellsB,
		int depthCellsB )
	{
		var a = CreateBuildingPlacementFootprint( positionA, rotationA, widthCellsA, depthCellsA );
		var b = CreateBuildingPlacementFootprint( positionB, rotationB, widthCellsB, depthCellsB );
		return FootprintsOverlap( a, b );
	}

	public static bool WouldBuildingFootprintsOverlap(
		Vector3 positionA,
		Rotation rotationA,
		int widthCellsA,
		int depthCellsA,
		Vector3 positionB,
		Rotation rotationB,
		int widthCellsB,
		int depthCellsB )
	{
		var a = CreateBuildingFootprint( positionA, rotationA, widthCellsA, depthCellsA );
		var b = CreateBuildingFootprint( positionB, rotationB, widthCellsB, depthCellsB );
		return FootprintsOverlap( a, b );
	}

	/// <summary>Tight exterior OBB for building-vs-building placement (no scatter clearance).</summary>
	public static ThornsWorldScatterFootprint CreateBuildingPlacementFootprint(
		Vector3 worldPosition,
		Rotation worldRotation,
		int widthCells,
		int depthCells )
	{
		var halfW = ThornsProcBuildingFootprintCatalog.ExteriorWidthInches( widthCells ) * 0.5f;
		var halfD = ThornsProcBuildingFootprintCatalog.ExteriorDepthInches( depthCells ) * 0.5f;
		var yawRad = worldRotation.Angles().yaw * MathF.PI / 180f;
		return new ThornsWorldScatterFootprint(
			worldPosition.x,
			worldPosition.y,
			halfW,
			halfD,
			yawRad,
			ThornsBuildingModule.BuildingPlacementOverlapMarginInches );
	}

	public static ThornsWorldScatterFootprint CreateBuildingFootprint(
		Vector3 worldPosition,
		Rotation worldRotation,
		int widthCells,
		int depthCells,
		float extraMarginInches = 0f )
	{
		var halfW = ThornsProcBuildingFootprintCatalog.ExteriorWidthInches( widthCells ) * 0.5f + 10f;
		var halfD = ThornsProcBuildingFootprintCatalog.ExteriorDepthInches( depthCells ) * 0.5f + 10f;
		var yawRad = worldRotation.Angles().yaw * MathF.PI / 180f;
		return new ThornsWorldScatterFootprint(
			worldPosition.x,
			worldPosition.y,
			halfW,
			halfD,
			yawRad,
			ThornsBuildingModule.ProcTownScatterExclusionMargin + extraMarginInches );
	}

	static bool WouldOverlapAnyRegistered( ThornsWorldScatterFootprint candidate )
	{
		for ( var i = 0; i < Footprints.Count; i++ )
		{
			if ( FootprintsOverlap( candidate, Footprints[i] ) )
				return true;
		}

		return false;
	}

	public static void RegisterBuilding( Vector3 worldPosition, Rotation worldRotation ) =>
		RegisterBuilding(
			worldPosition,
			worldRotation,
			ThornsProcBuildingInterior.GridCells,
			ThornsProcBuildingInterior.GridCells );

	public static void RegisterBuilding(
		Vector3 worldPosition,
		Rotation worldRotation,
		int widthCells,
		int depthCells )
	{
		var fp = CreateBuildingFootprint( worldPosition, worldRotation, widthCells, depthCells );
		Register( fp.CenterX, fp.CenterY, fp.HalfW, fp.HalfD, fp.YawRad, fp.Margin );
	}

	public static void RegisterBoulder( Vector3 worldPosition, float yawDegrees, Model model, float uniformScale, float extraMarginInches = 40f )
	{
		if ( !TryModelHorizontalHalfExtents( model, uniformScale, out var halfW, out var halfD ) )
			return;

		Register(
			worldPosition.x,
			worldPosition.y,
			halfW,
			halfD,
			yawDegrees * MathF.PI / 180f,
			extraMarginInches );
	}

	public static void RegisterTree( Vector3 worldPosition, float yawDegrees, Model model, float uniformScale, float extraMarginInches = 28f )
	{
		if ( !TryModelHorizontalHalfExtents( model, uniformScale, out var halfW, out var halfD ) )
			return;

		var trunkHalf = MathF.Max( MathF.Min( halfW, halfD ) * 0.42f, 52f );
		Register(
			worldPosition.x,
			worldPosition.y,
			trunkHalf,
			trunkHalf,
			yawDegrees * MathF.PI / 180f,
			extraMarginInches );
	}

	public static void RegisterMineral( Vector3 worldPosition, float yawDegrees, Model model, float uniformScale, float hullScale = 0.74f, float extraMarginInches = 20f )
	{
		if ( !TryModelHorizontalHalfExtents( model, uniformScale * MathF.Max( hullScale, 0.05f ), out var halfW, out var halfD ) )
			return;

		Register(
			worldPosition.x,
			worldPosition.y,
			halfW,
			halfD,
			yawDegrees * MathF.PI / 180f,
			extraMarginInches );
	}

	public static void Register(
		float centerX,
		float centerY,
		float halfW,
		float halfD,
		float yawRad,
		float margin )
	{
		if ( halfW <= 0f || halfD <= 0f )
			return;

		Footprints.Add( new ThornsWorldScatterFootprint( centerX, centerY, halfW, halfD, yawRad, margin ) );
	}

	public static bool ContainsWorldPoint( float worldX, float worldY, float extraMarginInches = 0f )
	{
		if ( Footprints.Count == 0 )
			return false;

		for ( var i = 0; i < Footprints.Count; i++ )
		{
			if ( PointInsideFootprint( worldX, worldY, Footprints[i], extraMarginInches ) )
				return true;
		}

		return false;
	}

	public static bool WouldBoulderOverlap( float worldX, float worldY, float yawDegrees, Model model, float uniformScale, float extraMarginInches = 40f )
	{
		if ( !TryModelHorizontalHalfExtents( model, uniformScale, out var halfW, out var halfD ) )
			return false;

		return WouldOverlap(
			worldX,
			worldY,
			halfW,
			halfD,
			yawDegrees * MathF.PI / 180f,
			extraMarginInches );
	}

	public static bool WouldTreeOverlap( float worldX, float worldY, float yawDegrees, Model model, float uniformScale, float extraMarginInches = 28f )
	{
		if ( !TryModelHorizontalHalfExtents( model, uniformScale, out var halfW, out var halfD ) )
			return false;

		var trunkHalf = MathF.Max( MathF.Min( halfW, halfD ) * 0.42f, 52f );
		return WouldOverlap( worldX, worldY, trunkHalf, trunkHalf, yawDegrees * MathF.PI / 180f, extraMarginInches );
	}

	public static bool WouldMineralOverlap( float worldX, float worldY, float yawDegrees, Model model, float uniformScale, float hullScale = 0.74f, float extraMarginInches = 20f )
	{
		if ( !TryModelHorizontalHalfExtents( model, uniformScale * MathF.Max( hullScale, 0.05f ), out var halfW, out var halfD ) )
			return false;

		return WouldOverlap(
			worldX,
			worldY,
			halfW,
			halfD,
			yawDegrees * MathF.PI / 180f,
			extraMarginInches );
	}

	public static bool WouldOverlap(
		float centerX,
		float centerY,
		float halfW,
		float halfD,
		float yawRad,
		float margin )
	{
		if ( halfW <= 0f || halfD <= 0f )
			return false;

		var candidate = new ThornsWorldScatterFootprint( centerX, centerY, halfW, halfD, yawRad, margin );
		for ( var i = 0; i < Footprints.Count; i++ )
		{
			if ( FootprintsOverlap( candidate, Footprints[i] ) )
				return true;
		}

		return false;
	}

	static bool TryModelHorizontalHalfExtents( Model model, float uniformScale, out float halfW, out float halfD )
	{
		halfW = 0f;
		halfD = 0f;

		if ( !model.IsValid() )
			return false;

		var bounds = model.RenderBounds.Size.LengthSquared > 1e-12f
			? model.RenderBounds
			: TerraingenAnchoredPhysics.GetTightModelBounds( model );
		if ( bounds.Size.LengthSquared < 1e-12f )
			return false;

		uniformScale = MathF.Max( uniformScale, 0.01f );
		halfW = MathF.Max( bounds.Size.x * uniformScale * 0.5f, 8f );
		halfD = MathF.Max( bounds.Size.y * uniformScale * 0.5f, 8f );
		return true;
	}

	static bool PointInsideFootprint( float worldX, float worldY, ThornsWorldScatterFootprint fp, float extraMarginInches )
	{
		var dx = worldX - fp.CenterX;
		var dy = worldY - fp.CenterY;
		var c = MathF.Cos( -fp.YawRad );
		var s = MathF.Sin( -fp.YawRad );
		var bx = dx * c - dy * s;
		var by = dx * s + dy * c;
		var margin = fp.Margin + extraMarginInches;
		return MathF.Abs( bx ) <= fp.HalfW + margin && MathF.Abs( by ) <= fp.HalfD + margin;
	}

	static bool FootprintsOverlap( ThornsWorldScatterFootprint a, ThornsWorldScatterFootprint b )
	{
		Span<Vector2> cornersA = stackalloc Vector2[4];
		Span<Vector2> cornersB = stackalloc Vector2[4];
		BuildObbCorners( a, cornersA );
		BuildObbCorners( b, cornersB );

		Span<Vector2> axes = stackalloc Vector2[4];
		axes[0] = EdgeNormal( cornersA[0], cornersA[1] );
		axes[1] = EdgeNormal( cornersA[1], cornersA[2] );
		axes[2] = EdgeNormal( cornersB[0], cornersB[1] );
		axes[3] = EdgeNormal( cornersB[1], cornersB[2] );

		for ( var i = 0; i < axes.Length; i++ )
		{
			var axis = axes[i];
			if ( axis.LengthSquared < 1e-8f )
				continue;

			ProjectCorners( cornersA, axis, out var minA, out var maxA );
			ProjectCorners( cornersB, axis, out var minB, out var maxB );
			if ( maxA < minB || maxB < minA )
				return false;
		}

		return true;
	}

	static void BuildObbCorners( ThornsWorldScatterFootprint fp, Span<Vector2> corners )
	{
		var c = MathF.Cos( fp.YawRad );
		var s = MathF.Sin( fp.YawRad );
		var hw = fp.HalfW + fp.Margin;
		var hd = fp.HalfD + fp.Margin;
		var cx = fp.CenterX;
		var cy = fp.CenterY;

		corners[0] = Offset( cx, cy, -hw, -hd, c, s );
		corners[1] = Offset( cx, cy, hw, -hd, c, s );
		corners[2] = Offset( cx, cy, hw, hd, c, s );
		corners[3] = Offset( cx, cy, -hw, hd, c, s );
	}

	static Vector2 Offset( float cx, float cy, float lx, float ly, float c, float s ) =>
		new( cx + lx * c - ly * s, cy + lx * s + ly * c );

	static Vector2 EdgeNormal( Vector2 a, Vector2 b )
	{
		var edge = b - a;
		if ( edge.LengthSquared < 1e-8f )
			return Vector2.Zero;

		var normal = new Vector2( -edge.y, edge.x );
		return normal.Normal;
	}

	static void ProjectCorners( ReadOnlySpan<Vector2> corners, Vector2 axis, out float min, out float max )
	{
		min = max = Vector2.Dot( corners[0], axis );
		for ( var i = 1; i < corners.Length; i++ )
		{
			var p = Vector2.Dot( corners[i], axis );
			min = MathF.Min( min, p );
			max = MathF.Max( max, p );
		}
	}
}
