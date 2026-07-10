namespace Sandbox;

/// <summary>Shared building-piece dimensions — 100-wide cell, 5-thick slabs/walls.</summary>
public static class ThornsBuildingModule
{
	/// <summary>Plan XY grid + vertical storey module (foundation cell / wall height).</summary>
	public const float Cell = 100f;

	public const float FloorThickness = 5f;

	public const float WallHeight = 100f;

	public const float WallThickness = 5f;

	/// <summary>One full storey: floor slab + wall band (<see cref="WallHeight"/> + <see cref="FloorThickness"/>).</summary>
	public static float StoryHeightWorld => WallHeight + FloorThickness;

	/// <summary>Perimeter wall run along a cell edge — extra <see cref="WallThickness"/> so panels overlap at convex corners.</summary>
	public static float ProcPerimeterWallRunWorld => Cell + WallThickness;

	/// <summary>Standoff past the exterior wall face — trims sit in front of the shell for readable depth.</summary>
	public const float ProcPerimeterTrimFaceGapWorld = 12f;

	/// <summary>Corner pillar width/depth (floor-textured posts at footprint corners).</summary>
	public const float ProcPerimeterTrimCornerSizeWorld = 24f;

	/// <summary>Seam post width along a wall run at panel joints.</summary>
	public const float ProcPerimeterTrimSeamRunWorld = 16f;

	/// <summary>Seam post depth protruding past the wall plane.</summary>
	public const float ProcPerimeterTrimSeamDepthWorld = 14f;

	/// <summary>Horizontal band trim vertical thickness (base, floor lines, roofline).</summary>
	public const float ProcPerimeterBandTrimHeightWorld = 10f;

	/// <summary>Legacy alias — corner XY size.</summary>
	public static float ProcPerimeterTrimDepthWorld => ProcPerimeterTrimCornerSizeWorld;

	/// <summary>Legacy alias — seam run.</summary>
	public static float ProcPerimeterTrimRunWorld => ProcPerimeterTrimSeamRunWorld;

	/// <summary>Full exterior shell height and centre Z — one continuous column per corner/seam.</summary>
	public static void ProcPerimeterShellFullExtent( int stories, out float centerLocalZ, out float spanWorld )
	{
		var perStorySpan = ProcPerimeterWallSpanWorld( stories );
		if ( stories <= 1 )
		{
			centerLocalZ = ProcPerimeterWallCenterLocalZ( 0, 1 );
			spanWorld = perStorySpan;
			return;
		}

		var zBottom = ProcPerimeterWallCenterLocalZ( 0, stories ) - perStorySpan * 0.5f;
		var zTop = ProcPerimeterWallCenterLocalZ( stories - 1, stories ) + perStorySpan * 0.5f;
		spanWorld = zTop - zBottom;
		centerLocalZ = ( zBottom + zTop ) * 0.5f;
	}

	/// <summary>
	/// Local position for a corner trim centre — <paramref name="wallIntersectX"/>/<paramref name="wallIntersectY"/>
	/// are where perimeter wall centerlines meet (same XY as <c>SpawnPiece</c> wall anchors). The post wraps the
	/// outer shell corner: half wall thickness to the exterior face, then half the trim depth outward.
	/// </summary>
	public static Vector3 ProcPerimeterCornerTrimLocalPosition(
		float wallIntersectX,
		float wallIntersectY,
		float centerLocalZ,
		int outwardSideA,
		int outwardSideB )
	{
		var halfWall = WallThickness * 0.5f;
		// Half trim depth only — centre sits on the exterior shell corner (wall centerline + half thickness).
		var push = halfWall;
		var px = wallIntersectX;
		var py = wallIntersectY;
		ApplyOutwardPush( outwardSideA, ref px, ref py, push );
		ApplyOutwardPush( outwardSideB, ref px, ref py, push );
		return new Vector3( px, py, centerLocalZ );
	}

	/// <summary>
	/// Panel seam between cells <paramref name="alongIndex"/> and <paramref name="alongIndex"/>+1 on an edge run.
	/// Skips the first and last joints — footprint corners use <see cref="ProcPerimeterCornerTrimLocalPosition"/> instead.
	/// </summary>
	public static bool IsInteriorPerimeterPanelSeam( int alongIndex, int alongCount ) =>
		alongIndex > 0 && alongIndex + 1 < alongCount - 1;

	static void ApplyOutwardPush( int side, ref float px, ref float py, float push )
	{
		switch ( side )
		{
			case 0: py -= push; break;
			case 2: py += push; break;
			case 3: px -= push; break;
			case 1: px += push; break;
		}
	}

	/// <summary>Local centre Z for horizontal band trims — storey joints and roofline (no ground/base band — blocks doorways).</summary>
	public static int CollectPerimeterBandTrimCenterZ( int stories, Span<float> into )
	{
		if ( stories < 1 || into.Length < 1 )
			return 0;

		var span = ProcPerimeterWallSpanWorld( stories );
		var h = ProcPerimeterBandTrimHeightWorld;
		var n = 0;

		for ( var s = 1; s < stories; s++ )
		{
			if ( n >= into.Length )
				break;
			into[n++] = s * StoryHeightWorld;
		}

		if ( n < into.Length )
			into[n++] = ProcPerimeterWallCenterLocalZ( stories - 1, stories ) + span * 0.5f - h * 0.5f;

		return n;
	}

	/// <summary>
	/// Horizontal band on an exterior face — flush with shell like corner posts.
	/// <paramref name="alongLocal"/> runs along the wall (X for south/north, Y for east/west);
	/// <paramref name="edgeLocal"/> is the wall plane coordinate (Y for south/north, X for east/west).
	/// </summary>
	public static Vector3 ProcPerimeterBandTrimLocalPosition(
		float alongLocal,
		float edgeLocal,
		float bandCenterZ,
		int side )
	{
		var push = WallThickness * 0.5f;
		return side switch
		{
			0 => new Vector3( alongLocal, edgeLocal - push, bandCenterZ ),
			2 => new Vector3( alongLocal, edgeLocal + push, bandCenterZ ),
			3 => new Vector3( edgeLocal - push, alongLocal, bandCenterZ ),
			1 => new Vector3( edgeLocal + push, alongLocal, bandCenterZ ),
			_ => new Vector3( alongLocal, edgeLocal, bandCenterZ )
		};
	}

	/// <summary>Local position for a seam trim on an exterior face (south/north/east/west).</summary>
	public static Vector3 ProcPerimeterSeamTrimLocalPosition(
		float alongLocal,
		float edgeLocal,
		float centerLocalZ,
		int side )
	{
		var half = WallThickness * 0.5f;
		var halfTrim = ProcPerimeterTrimSeamDepthWorld * 0.5f;
		var push = half + halfTrim + ProcPerimeterTrimFaceGapWorld;
		return side switch
		{
			0 => new Vector3( alongLocal, edgeLocal - push, centerLocalZ ),
			2 => new Vector3( alongLocal, edgeLocal + push, centerLocalZ ),
			3 => new Vector3( edgeLocal - push, alongLocal, centerLocalZ ),
			1 => new Vector3( edgeLocal + push, alongLocal, centerLocalZ ),
			_ => new Vector3( alongLocal, edgeLocal, centerLocalZ )
		};
	}

	/// <summary>
	/// Procedural perimeter wall vertical span. Multi-storey walls overlap half a floor slab into the next band
	/// so there is no see-through gap between exterior wall segments at floor lines.
	/// </summary>
	public static float ProcPerimeterWallSpanWorld( int stories ) =>
		stories <= 1 ? WallHeight : WallHeight + FloorThickness;

	/// <summary>Local Z centre for a procedural perimeter wall on storey <paramref name="storyIndex"/>.</summary>
	public static float ProcPerimeterWallCenterLocalZ( int storyIndex, int stories )
	{
		if ( stories <= 1 )
			return FloorThickness * 0.5f + WallHeight * 0.5f;

		var span = ProcPerimeterWallSpanWorld( stories );
		var sh = StoryHeightWorld;
		var bottom = storyIndex * sh + (storyIndex == 0 ? FloorThickness * 0.5f : -FloorThickness * 0.5f );
		return bottom + span * 0.5f;
	}

	/// <summary>Window aperture (opening) for frame/glass approximation.</summary>
	public const float WindowHoleSize = 36f;

	/// <summary>Door aperture (opening) width × height in world units.</summary>
	public const float DoorWidth = 60f;

	/// <summary>Tall opening — top aligns with window hole top; bottom stays at slab/wall foot.</summary>
	public const float DoorHeight = 100f;

	/// <summary>
	/// Full-size door slab shifted along local −X (inward from the +X jamb face) so the outer frame depth reads as a recess.
	/// </summary>
	public const float DoorPanelDepthInset = 2.85f;

	/// <summary>Local Z of door-frame opening foot — matches <see cref="ThornsBuildingVisuals"/> doorframe hole bottom.</summary>
	public static float DoorFrameHoleBottomLocalZ => -WallHeight * 0.5f;

	/// <summary>Bottom hinge corner on the inward jamb (local +Y is width across the opening).</summary>
	public static Vector3 DoorPanelHingeLocal =>
		new( -DoorPanelDepthInset, -DoorWidth * 0.5f, DoorFrameHoleBottomLocalZ );

	/// <summary>Panel pivot is mesh centre; offset from hinge places the full-size slab in the opening.</summary>
	public static Vector3 DoorPanelOffsetFromHinge =>
		new( 0f, DoorWidth * 0.5f, DoorHeight * 0.5f );

	/// <summary>Placeholder box mesh extent per axis — legacy <c>models/dev/box.vmdl</c> and <see cref="ThornsBuildingVisuals.UnitCubeModel"/> match this so <see cref="ScaleBoxToWorldAxes"/> stays correct.</summary>
	public static float DevReferenceSize { get; set; } = 50f;

	public static Vector3 ScaleBoxToWorldAxes( float sizeX, float sizeY, float sizeZ )
	{
		var r = DevReferenceSize;
		return new Vector3( sizeX / r, sizeY / r, sizeZ / r );
	}
}
