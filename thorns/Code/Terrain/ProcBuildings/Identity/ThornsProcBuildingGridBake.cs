namespace Sandbox;

/// <summary>Occupancy grid helpers + finalize ramps/doors/walls/validation.</summary>
public static class ThornsProcBuildingGridBake
{
	public static bool[] NewOccupancy( int stories, int w, int d ) => new bool[stories * w * d];

	public static void FillRect( bool[] occ, int stories, int w, int d, int x0, int y0, int x1, int y1 )
	{
		for ( var s = 0; s < stories; s++ )
		for ( var x = x0; x <= x1; x++ )
		for ( var y = y0; y <= y1; y++ )
			Set( occ, stories, w, d, s, x, y, true );
	}

	public static void Set( bool[] occ, int stories, int w, int d, int s, int x, int y, bool v )
	{
		if ( s < 0 || s >= stories || x < 0 || x >= w || y < 0 || y >= d )
			return;

		occ[s * w * d + y * w + x] = v;
	}

	public static bool Get( bool[] occ, int stories, int w, int d, int s, int x, int y ) =>
		s >= 0 && s < stories && x >= 0 && x < w && y >= 0 && y < d && occ[s * w * d + y * w + x];

	/// <summary>Interior wall between (x,y) and (x+1,y).</summary>
	public static void WallEast( ThornsProcBuildingWallPlan plan, int story, int x, int y ) =>
		plan.SetInteriorWallEast( story, x, y, true );

	/// <summary>Interior wall between (x,y) and (x,y+1).</summary>
	public static void WallNorth( ThornsProcBuildingWallPlan plan, int story, int x, int y ) =>
		plan.SetInteriorWallNorth( story, x, y, true );

	public static bool TryBakeAndValidate(
		bool[] occupied,
		int w,
		int d,
		int stories,
		Random rnd,
		ThornsProcBuildingIdentityMeta identity,
		out ThornsProcBuildingLayout layout )
	{
		layout = null;
		var rampX = new int[stories];
		var rampY = new int[stories];
		var draft = ThornsProcBuildingLayout.CreateDraft( w, d, stories, occupied );
		draft.Identity = identity;

		if ( stories > 1 && !draft.TryAssignRampCorners( rnd ) )
			return false;

		draft.EnsureRampLandingCellsOccupied();
		if ( !draft.TryPickDoor( rnd ) )
			return false;

		draft.InteriorWalls = ThornsProcBuildingWallPlan.Generate( draft, rnd, draft.DoorSide, draft.DoorIndex );
		var report = ThornsProcBuildingValidation.Validate( draft, draft.InteriorWalls );
		if ( !report.Passed )
			return false;

		layout = draft;
		return true;
	}
}
