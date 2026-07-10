namespace Sandbox;

/// <summary>Single strict validation entry for world-gen and <see cref="ThornsProcBuildingCompilePolicy.Strict"/> compiles.</summary>
public static class ThornsProcBuildingStrictValidation
{
	public static bool TryValidate(
		ThornsProcBuildingLayout layout,
		ThornsProcTileBlueprint blueprint,
		out ThornsProcBuildingValidationReport report,
		out ThornsProcBuildingValidationFailureSummary failure )
	{
		report = null;
		failure = default;

		if ( layout is null )
		{
			failure = new ThornsProcBuildingValidationFailureSummary
			{
				Category = ThornsProcBuildingRules.RuleCategory.Structural,
				Rule = ThornsProcBuildingRules.RuleId.NoFloatingUpperCells,
				Reason = "LayoutNull"
			};
			return false;
		}

		var walls = layout.InteriorWalls;
		var doorSide = layout.DoorSide;
		var doorIndex = layout.DoorIndex;

		if ( !ThornsProcBuildingConnectivity.IsFullyReachableFromDoor( layout, walls, doorSide, doorIndex ) )
		{
			report = ThornsProcBuildingValidation.Validate( layout, walls );
			var rampIssues = ThornsProcBuildingRampValidation.CollectIssues( layout );
			failure = ThornsProcBuildingValidationFailureSummary.FromReport( layout, report, rampIssues );
			if ( string.IsNullOrEmpty( failure.Reason ) || failure.Reason == failure.Rule.ToString() )
				failure = failure with { Reason = "UnreachableWalkableOrVerticalAccess" };
			return false;
		}

		report = ThornsProcBuildingValidation.Validate( layout, walls );
		var rampList = ThornsProcBuildingRampValidation.CollectIssues( layout );

		if ( !report.Passed )
		{
			failure = ThornsProcBuildingValidationFailureSummary.FromReport( layout, report, rampList );
			return false;
		}

		if ( rampList.Count > 0 )
		{
			failure = ThornsProcBuildingValidationFailureSummary.FromReport( layout, report, rampList );
			return false;
		}

		if ( layout.Stories >= 2 && HasUnreachableRoofWalkables( layout, walls, doorSide, doorIndex ) )
		{
			failure = new ThornsProcBuildingValidationFailureSummary
			{
				Category = ThornsProcBuildingRules.RuleCategory.Connectivity,
				Rule = ThornsProcBuildingRules.RuleId.NoIsolatedFloors,
				Reason = "UnreachableRoofWalkable",
				Story = layout.Stories - 1
			};
			return false;
		}

		if ( blueprint is not null
		     && !ThornsProcTileBlueprintValidator.ValidateRampOpenings( layout, blueprint, out var rampErr ) )
		{
			failure = new ThornsProcBuildingValidationFailureSummary
			{
				Category = ThornsProcBuildingRules.RuleCategory.MultiFloor,
				Rule = ThornsProcBuildingRules.RuleId.UpperFloorHasShaftAboveRamp,
				Reason = rampErr ?? "BlueprintRampOpeningMismatch"
			};
			return false;
		}

		return true;
	}

	static bool HasUnreachableRoofWalkables(
		ThornsProcBuildingLayout layout,
		ThornsProcBuildingWallPlan walls,
		int doorSide,
		int doorIndex )
	{
		if ( layout.Stories < 2 )
			return false;

		if ( !ThornsProcBuildingInteriorSample.TryGetDoorInteriorCell(
			     doorSide, doorIndex, layout.WidthCells, layout.DepthCells, out var sx, out var sy ) )
			return true;

		var stride = layout.WidthCells * layout.DepthCells;
		var visited = new bool[layout.Stories * stride];
		FloodReachable( layout, walls, visited, stride, sx, sy );

		var top = layout.Stories - 1;
		for ( var x = 0; x < layout.WidthCells; x++ )
		for ( var y = 0; y < layout.DepthCells; y++ )
		{
			if ( !layout.HasWalkableFloorAt( top, x, y ) )
				continue;

			if ( !visited[top * stride + y * layout.WidthCells + x] )
				return true;
		}

		return false;
	}

	static void FloodReachable(
		ThornsProcBuildingLayout layout,
		ThornsProcBuildingWallPlan walls,
		bool[] visited,
		int stride,
		int startX,
		int startY )
	{
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

			if ( s >= layout.Stories - 1 )
				continue;

			foreach ( var ramp in layout.GetRampsOnStory( s ) )
			{
				if ( !ThornsProcBuildingRampTraversal.IsOnRampLanding( layout, s, x, y, ramp ) )
					continue;

				foreach ( var (tx, ty) in ThornsProcBuildingRampTraversal.EnumerateUpperEntryCellsForRamp( layout, ramp ) )
					Visit( s + 1, tx, ty );
			}
		}

		void TryStep( int s, int tx, int ty, int fx, int fy )
		{
			if ( tx < 0 || tx >= layout.WidthCells || ty < 0 || ty >= layout.DepthCells )
				return;

			if ( !layout.HasWalkableFloorAt( s, tx, ty ) )
				return;

			if ( walls is not null )
			{
				if ( tx == fx + 1 && walls.HasInteriorWallEast( s, fx, fy ) )
					return;

				if ( tx == fx - 1 && walls.HasInteriorWallEast( s, tx, ty ) )
					return;

				if ( ty == fy + 1 && walls.HasInteriorWallNorth( s, fx, fy ) )
					return;

				if ( ty == fy - 1 && walls.HasInteriorWallNorth( s, fx, ty ) )
					return;
			}

			Visit( s, tx, ty );
		}
	}
}
