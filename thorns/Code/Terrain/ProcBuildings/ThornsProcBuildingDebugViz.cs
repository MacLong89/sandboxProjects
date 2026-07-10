namespace Sandbox;

/// <summary>
/// Debug helpers for proc-building validation. Call from editor/console while a layout host is selected.
/// </summary>
public static class ThornsProcBuildingDebugViz
{
	public static bool Enabled { get; set; }

	public static void LogValidationReport( ThornsProcBuildingLayoutHost host )
	{
		var layout = host?.Layout;
		if ( layout is null )
		{
			Log.Warning( "[Thorns ProcBuilding] No layout on host." );
			return;
		}

		var report = ThornsProcBuildingValidation.Validate( layout, layout.InteriorWalls );
		var id = layout.Identity;
		var idLine = id is not null
			? $" type={id.Type} district={id.District} ruin={id.IsRuinVariant}"
			: "";
		Log.Info( $"[Thorns ProcBuilding] ruleset v{ThornsProcBuildingRules.RulesetVersion}{idLine} {report.Summary}" );
		if ( report.FailedHardRules.Count > 0 )
			Log.Warning( $"[Thorns ProcBuilding] hard fails: {string.Join( ", ", report.FailedHardRules )}" );

		Log.Info(
			$"[Thorns ProcBuilding] analysis: reachable={report.Analysis.ReachableWalkableCells}/{report.Analysis.TotalWalkableCells} "
			+ $"rooms={report.Analysis.RoomRegionCount} tiny={report.Analysis.TinyRoomCount} huge={report.Analysis.HugeRoomCount} "
			+ $"deadEnds={report.Analysis.DeadEndCellCount} maxSight={report.Analysis.MaxGroundSightlineCells}" );

		if ( layout.Identity?.Type is { } t
		     && ThornsProcTileBlueprintLibrary.TryGet( t, out var bp ) )
		{
			var mismatches = ThornsProcTileBlueprintValidator.FindRampOpeningMismatches( layout, bp );
			if ( mismatches.Count > 0 )
				Log.Warning( $"[Thorns ProcBuilding] ramp/opening mismatches: {mismatches.Count} (see debug draw)." );
		}

		if ( layout.Stories >= 4 && TryCountIsolatedRoofCells( layout, out var isolated ) && isolated > 0 )
			Log.Warning( $"[Thorns ProcBuilding] isolated roof walkables: {isolated} (not reachable from ground door)." );

		var rampIssues = ThornsProcBuildingRampValidation.CollectIssues( layout );
		if ( rampIssues.Count > 0 )
		{
			Log.Warning( $"[Thorns ProcBuilding] ramp validation issues: {rampIssues.Count} (use DrawRampTraversalDebug)." );
			for ( var i = 0; i < Math.Min( rampIssues.Count, 6 ); i++ )
			{
				var issue = rampIssues[i];
				Log.Warning( $"  story={issue.Story} ({issue.X},{issue.Y}) {issue.Detail}" );
			}
		}
	}

	/// <summary>Runs unreachable + ramp/opening + isolated-roof overlays.</summary>
	public static void DrawBuildingTraversalIssues(
		Scene scene,
		GameObject buildingRoot,
		float durationSeconds = 8f )
	{
		DrawRampTraversalDebug( scene, buildingRoot, durationSeconds );
		DrawUnreachableCells( scene, buildingRoot, durationSeconds );
		DrawRampOpeningMismatches( scene, buildingRoot, durationSeconds );
		DrawIsolatedRoofCells( scene, buildingRoot, durationSeconds );
	}

	/// <summary>
	/// Ramp tiles (yellow), direction arrows, shaft/headroom cells, validation conflicts (red),
	/// and vertical links between stairwells (cyan).
	/// </summary>
	public static void DrawRampTraversalDebug( Scene scene, GameObject buildingRoot, float durationSeconds = 8f )
	{
		if ( scene is null || buildingRoot is null || !buildingRoot.IsValid() )
			return;

		var layout = ThornsProcBuildingLayoutHost.TryGet( buildingRoot );
		if ( layout is null || layout.Stories <= 1 )
			return;

		var dbg = scene.GetSystem<DebugOverlaySystem>();
		if ( dbg is null )
			return;

		var hostTr = buildingRoot.Transform.World;
		var cell = ThornsBuildingModule.Cell;
		var storyH = ThornsBuildingModule.StoryHeightWorld;

		var shaftScratch = new List<(int x, int y)>( 4 );
		var headroomScratch = new List<(int x, int y)>( 4 );

		for ( var s = 0; s < layout.Stories; s++ )
		{
			foreach ( var ramp in layout.GetRampsOnStory( s ) )
			{
				var z = s * storyH + 48f;
				var anchor = hostTr.PointToWorld( new Vector3( layout.GridAxisLocalX( ramp.X ), layout.GridAxisLocalY( ramp.Y ), z ) );
				var spawn = hostTr.PointToWorld( ThornsProcBuildingRampGeometry.GetRampSpawnLocalPosition( layout, ramp, z ) );
				var ar = cell * 0.22f;
				dbg.Line( anchor - Vector3.Up * ar, anchor + Vector3.Up * ar, Color.Gray, durationSeconds, default, false );
				dbg.Line( anchor - Vector3.Left * ar, anchor + Vector3.Right * ar, Color.Gray, durationSeconds, default, false );
				var r = cell * 0.42f;
				dbg.Line( spawn - Vector3.Up * r, spawn + Vector3.Up * r, Color.Yellow, durationSeconds, default, false );
				dbg.Line( spawn - Vector3.Left * r, spawn + Vector3.Right * r, Color.Yellow, durationSeconds, default, false );

				ThornsProcTileRampHeadroom.GetRiseDelta( ramp.Direction, out var riseDx, out var riseDy );
				if ( ramp.Direction == ThornsProcRampDirection.None )
					ThornsProcBuildingRampGeometry.GetRiseDirection( layout, ramp, out riseDx, out riseDy );

				var arrowEnd = anchor + hostTr.PointToWorld( new Vector3( riseDx * cell * 0.65f, riseDy * cell * 0.65f, 0f ) )
				                 - hostTr.PointToWorld( Vector3.Zero );
				dbg.Line( anchor, anchor + arrowEnd, Color.White, durationSeconds, default, false );
				dbg.Line( anchor, spawn, Color.Yellow.WithAlpha( 0.35f ), durationSeconds, default, false );

				ThornsProcBuildingRampGeometry.CollectShaftCells( layout, ramp, shaftScratch );
				foreach ( var (sx, sy) in shaftScratch )
				{
					var shaftWorld = hostTr.PointToWorld( new Vector3( layout.GridAxisLocalX( sx ), layout.GridAxisLocalY( sy ), z ) );
					var sr = cell * 0.28f;
					dbg.Line( shaftWorld - Vector3.Up * sr, shaftWorld + Vector3.Up * sr, Color.Magenta, durationSeconds, default, false );
				}

				if ( s + 1 < layout.Stories )
				{
					ThornsProcTileRampHeadroom.CollectHeadroomCells( s, ramp.X, ramp.Y, ramp.Direction, headroomScratch );
					var uz = ( s + 1 ) * storyH + 52f;
					foreach ( var (hx, hy) in headroomScratch )
					{
						var headWorld = hostTr.PointToWorld( new Vector3( layout.GridAxisLocalX( hx ), layout.GridAxisLocalY( hy ), uz ) );
						var hr = cell * 0.32f;
						dbg.Line( headWorld - Vector3.Up * hr, headWorld + Vector3.Up * hr, Color.Green, durationSeconds, default, false );
					}

					foreach ( var (ux, uy) in ThornsProcBuildingRampTraversal.EnumerateUpperEntryCellsForRamp( layout, ramp ) )
					{
						var upperWorld = hostTr.PointToWorld( new Vector3( layout.GridAxisLocalX( ux ), layout.GridAxisLocalY( uy ), uz ) );
						dbg.Line( anchor, upperWorld, Color.Cyan, durationSeconds, default, false );
					}
				}
			}
		}

		foreach ( var issue in ThornsProcBuildingRampValidation.CollectIssues( layout ) )
		{
			var iz = issue.Story * storyH + 72f;
			var world = hostTr.PointToWorld( new Vector3( layout.GridAxisLocalX( issue.X ), layout.GridAxisLocalY( issue.Y ), iz ) );
			var rr = cell * 0.45f;
			dbg.Line( world - Vector3.Up * rr, world + Vector3.Up * rr, Color.Red, durationSeconds, default, false );
			dbg.Line( world - Vector3.Left * rr, world + Vector3.Right * rr, Color.Red, durationSeconds, default, false );
		}
	}

	static bool TryCountIsolatedRoofCells( ThornsProcBuildingLayout layout, out int isolatedCount )
	{
		isolatedCount = 0;
		if ( !ThornsProcBuildingInteriorSample.TryGetDoorInteriorCell(
			     layout.DoorSide, layout.DoorIndex, layout.WidthCells, layout.DepthCells, out var sx, out var sy ) )
			return false;

		var stride = layout.WidthCells * layout.DepthCells;
		var visited = new bool[layout.Stories * stride];
		FloodWalkable( layout, visited, stride, sx, sy );

		var top = layout.Stories - 1;
		for ( var x = 0; x < layout.WidthCells; x++ )
		for ( var y = 0; y < layout.DepthCells; y++ )
		{
			if ( !layout.HasWalkableFloorAt( top, x, y ) )
				continue;

			var idx = top * stride + y * layout.WidthCells + x;
			if ( !visited[idx] )
				isolatedCount++;
		}

		return true;
	}

	/// <summary>Highlights top-storey walkables that never connect to the ground entrance (cyan).</summary>
	public static void DrawIsolatedRoofCells( Scene scene, GameObject buildingRoot, float durationSeconds = 8f )
	{
		if ( scene is null || buildingRoot is null || !buildingRoot.IsValid )
			return;

		var layout = ThornsProcBuildingLayoutHost.TryGet( buildingRoot );
		if ( layout is null || layout.Stories < 2 )
			return;

		if ( !TryCountIsolatedRoofCells( layout, out var _ ) )
			return;

		if ( !ThornsProcBuildingInteriorSample.TryGetDoorInteriorCell(
			     layout.DoorSide, layout.DoorIndex, layout.WidthCells, layout.DepthCells, out var sx, out var sy ) )
			return;

		var stride = layout.WidthCells * layout.DepthCells;
		var visited = new bool[layout.Stories * stride];
		FloodWalkable( layout, visited, stride, sx, sy );

		var dbg = scene.GetSystem<DebugOverlaySystem>();
		if ( dbg is null )
			return;

		var top = layout.Stories - 1;
		var hostTr = buildingRoot.Transform.World;
		var cell = ThornsBuildingModule.Cell;
		for ( var x = 0; x < layout.WidthCells; x++ )
		for ( var y = 0; y < layout.DepthCells; y++ )
		{
			if ( !layout.HasWalkableFloorAt( top, x, y ) )
				continue;

			var idx = top * stride + y * layout.WidthCells + x;
			if ( visited[idx] )
				continue;

			var local = new Vector3(
				layout.GridAxisLocalX( x ),
				layout.GridAxisLocalY( y ),
				top * ThornsBuildingModule.StoryHeightWorld + 80f );
			var world = hostTr.PointToWorld( local );
			var r = cell * 0.38f;
			dbg.Line( world - Vector3.Up * r, world + Vector3.Up * r, Color.Cyan, durationSeconds, default, false );
			dbg.Line( world - Vector3.Left * r, world + Vector3.Right * r, Color.Cyan, durationSeconds, default, false );
		}
	}

	static void FloodWalkable(
		ThornsProcBuildingLayout layout,
		bool[] visited,
		int stride,
		int startX,
		int startY )
	{
		var walls = layout.InteriorWalls;
		var q = new Queue<(int s, int x, int y)>();
		void Visit( int s, int x, int y )
		{
			var idx = s * stride + y * layout.WidthCells + x;
			if ( visited[idx] || !layout.HasWalkableFloorAt( s, x, y ) )
				return;

			visited[idx] = true;
			q.Enqueue( (s, x, y) );
		}

		Visit( 0, startX, startY );
		while ( q.Count > 0 )
		{
			var (s, x, y) = q.Dequeue();
			TryStep( s, x - 1, y, x, y );
			TryStep( s, x + 1, y, x, y );
			TryStep( s, x, y - 1, x, y );
			TryStep( s, x, y + 1, x, y );

			if ( s < layout.Stories - 1 )
			{
				foreach ( var ramp in layout.GetRampsOnStory( s ) )
				{
					if ( !ThornsProcBuildingRampTraversal.IsOnRampLanding( layout, s, x, y, ramp ) )
						continue;

					foreach ( var (tx, ty) in ThornsProcBuildingRampTraversal.EnumerateUpperEntryCellsForRamp( layout, ramp ) )
						Visit( s + 1, tx, ty );
				}
			}
		}

		void TryStep( int s, int tx, int ty, int fx, int fy )
		{
			if ( tx < 0 || tx >= layout.WidthCells || ty < 0 || ty >= layout.DepthCells )
				return;

			if ( !layout.HasWalkableFloorAt( s, tx, ty ) )
				return;

			if ( tx == fx + 1 && walls.HasInteriorWallEast( s, fx, fy ) )
				return;

			if ( tx == fx - 1 && walls.HasInteriorWallEast( s, tx, ty ) )
				return;

			if ( ty == fy + 1 && walls.HasInteriorWallNorth( s, fx, fy ) )
				return;

			if ( ty == fy - 1 && walls.HasInteriorWallNorth( s, fx, ty ) )
				return;

			Visit( s, tx, ty );
		}
	}

	/// <summary>Highlights blueprint ramp cells missing an opening directly above.</summary>
	public static void DrawRampOpeningMismatches(
		Scene scene,
		GameObject buildingRoot,
		float durationSeconds = 8f )
	{
		if ( scene is null || buildingRoot is null || !buildingRoot.IsValid )
			return;

		var layout = ThornsProcBuildingLayoutHost.TryGet( buildingRoot );
		if ( layout?.Identity?.Type is not { } type
		     || !ThornsProcTileBlueprintLibrary.TryGet( type, out var bp ) )
			return;

		var mismatches = ThornsProcTileBlueprintValidator.FindRampOpeningMismatches( layout, bp );
		if ( mismatches.Count == 0 )
			return;

		var dbg = scene.GetSystem<DebugOverlaySystem>();
		if ( dbg is null )
			return;

		var hostTr = buildingRoot.Transform.World;
		var cell = ThornsBuildingModule.Cell;
		foreach ( var (s, x, y) in mismatches )
		{
			var local = new Vector3(
				layout.GridAxisLocalX( x ),
				layout.GridAxisLocalY( y ),
				s * ThornsBuildingModule.StoryHeightWorld + 60f );
			var world = hostTr.PointToWorld( local );
			var r = cell * 0.4f;
			dbg.Line( world - Vector3.Up * r, world + Vector3.Up * r, Color.Orange, durationSeconds, default, false );
			dbg.Line( world - Vector3.Left * r, world + Vector3.Right * r, Color.Orange, durationSeconds, default, false );
		}
	}

	/// <summary>Draws unreachable walkable cells in red for a few seconds (requires <see cref="DebugOverlaySystem"/>).</summary>
	public static void DrawUnreachableCells( Scene scene, GameObject buildingRoot, float durationSeconds = 8f )
	{
		if ( scene is null || buildingRoot is null || !buildingRoot.IsValid )
			return;

		var layout = ThornsProcBuildingLayoutHost.TryGet( buildingRoot );
		if ( layout is null )
			return;

		var walls = layout.InteriorWalls;
		if ( !ThornsProcBuildingInteriorSample.TryGetDoorInteriorCell(
			     layout.DoorSide, layout.DoorIndex, layout.WidthCells, layout.DepthCells, out var sx, out var sy ) )
			return;

		var stride = layout.WidthCells * layout.DepthCells;
		var visited = new bool[layout.Stories * stride];
		var q = new Queue<(int s, int x, int y)>();
		Visit( 0, sx, sy );

		while ( q.Count > 0 )
		{
			var (s, x, y) = q.Dequeue();
			TryVisit( s, x - 1, y, x, y );
			TryVisit( s, x + 1, y, x, y );
			TryVisit( s, x, y - 1, x, y );
			TryVisit( s, x, y + 1, x, y );

			if ( s < layout.Stories - 1 )
			{
				foreach ( var ramp in layout.GetRampsOnStory( s ) )
				{
					if ( !ThornsProcBuildingRampTraversal.IsOnRampLanding( layout, s, x, y, ramp ) )
						continue;

					foreach ( var (tx, ty) in ThornsProcBuildingRampTraversal.EnumerateUpperEntryCellsForRamp( layout, ramp ) )
						Visit( s + 1, tx, ty );
				}
			}
		}

		var cell = ThornsBuildingModule.Cell;
		var hostTr = buildingRoot.Transform.World;
		var dbg = scene.GetSystem<DebugOverlaySystem>();
		if ( dbg is null )
			return;

		for ( var s = 0; s < layout.Stories; s++ )
		for ( var x = 0; x < layout.WidthCells; x++ )
		for ( var y = 0; y < layout.DepthCells; y++ )
		{
			if ( !layout.HasWalkableFloorAt( s, x, y ) )
				continue;

			var idx = s * stride + y * layout.WidthCells + x;
			if ( visited[idx] )
				continue;

			var local = new Vector3( layout.GridAxisLocalX( x ), layout.GridAxisLocalY( y ), s * ThornsBuildingModule.StoryHeightWorld + 40f );
			var world = hostTr.PointToWorld( local );
			var r = cell * 0.35f;
			dbg.Line( world - Vector3.Up * r, world + Vector3.Up * r, Color.Red, durationSeconds, default, false );
			dbg.Line( world - Vector3.Left * r, world + Vector3.Right * r, Color.Red, durationSeconds, default, false );
		}

		void Visit( int s, int x, int y )
		{
			var idx = s * stride + y * layout.WidthCells + x;
			if ( visited[idx] )
				return;

			if ( !layout.HasWalkableFloorAt( s, x, y ) )
				return;

			visited[idx] = true;
			q.Enqueue( (s, x, y) );
		}

		void TryVisit( int s, int fx, int fy, int tx, int ty )
		{
			if ( tx < 0 || tx >= layout.WidthCells || ty < 0 || ty >= layout.DepthCells )
				return;

			if ( !layout.HasWalkableFloorAt( s, tx, ty ) )
				return;

			if ( tx == fx + 1 && walls.HasInteriorWallEast( s, fx, fy ) )
				return;

			if ( tx == fx - 1 && walls.HasInteriorWallEast( s, tx, ty ) )
				return;

			if ( ty == fy + 1 && walls.HasInteriorWallNorth( s, fx, fy ) )
				return;

			if ( ty == fy - 1 && walls.HasInteriorWallNorth( s, fx, ty ) )
				return;

			Visit( s, tx, ty );
		}
	}
}
