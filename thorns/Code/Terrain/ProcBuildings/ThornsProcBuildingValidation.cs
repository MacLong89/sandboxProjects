namespace Sandbox;

/// <summary>Hard-rule validation passes for procedural building layouts.</summary>
public sealed class ThornsProcBuildingValidationReport
{
	public bool Passed { get; init; }
	public IReadOnlyList<ThornsProcBuildingRules.RuleId> FailedHardRules { get; init; } = Array.Empty<ThornsProcBuildingRules.RuleId>();
	public ThornsProcBuildingAnalysis Analysis { get; init; }
	public int QualityScore { get; init; }
	public string Summary { get; init; } = "";
}

public static class ThornsProcBuildingValidation
{
	public static ThornsProcBuildingValidationReport Validate(
		ThornsProcBuildingLayout layout,
		ThornsProcBuildingWallPlan walls,
		int exteriorWindowCount = 0,
		int exteriorWallCount = 0 )
	{
		var failed = new List<ThornsProcBuildingRules.RuleId>( 8 );
		var doorSide = layout.DoorSide;
		var doorIndex = layout.DoorIndex;

		if ( !ThornsProcBuildingInteriorSample.TryGetDoorInteriorCell(
			     doorSide, doorIndex, layout.WidthCells, layout.DepthCells, out var dx, out var dy ) )
			failed.Add( ThornsProcBuildingRules.RuleId.DoorOpensOntoWalkableFloor );
		else if ( !layout.HasWalkableFloorAt( 0, dx, dy ) )
			failed.Add( ThornsProcBuildingRules.RuleId.DoorOpensOntoWalkableFloor );

		if ( !ThornsProcBuildingInteriorSample.IsEnterableDoorPlacement( doorSide, doorIndex, layout ) )
			failed.Add( ThornsProcBuildingRules.RuleId.DoorNotOnRampShaft );

		if ( !ValidateStructuralSupport( layout ) )
		{
			failed.Add( ThornsProcBuildingRules.RuleId.NoFloatingUpperCells );
			failed.Add( ThornsProcBuildingRules.RuleId.UpperFloorSupportedByBelow );
		}

		if ( layout.Stories > 1 && !ValidateMultiFloorRamps( layout, failed ) )
		{
			if ( !failed.Contains( ThornsProcBuildingRules.RuleId.LowerFloorHasRampWhenMultiStory ) )
				failed.Add( ThornsProcBuildingRules.RuleId.LowerFloorHasRampWhenMultiStory );
			if ( !failed.Contains( ThornsProcBuildingRules.RuleId.UpperFloorHasShaftAboveRamp ) )
				failed.Add( ThornsProcBuildingRules.RuleId.UpperFloorHasShaftAboveRamp );
		}

		if ( !ThornsProcBuildingConnectivity.IsFullyReachableFromDoor( layout, walls, doorSide, doorIndex ) )
		{
			failed.Add( ThornsProcBuildingRules.RuleId.EveryWalkableReachableFromDoor );
			failed.Add( ThornsProcBuildingRules.RuleId.InteriorWallsPreserveConnectivity );
			failed.Add( ThornsProcBuildingRules.RuleId.NoFullySealedRooms );
		}

		if ( layout.Stories > 1 && !ThornsProcBuildingConnectivity.IsFullyReachableFromDoor( layout, walls, doorSide, doorIndex ) )
			failed.Add( ThornsProcBuildingRules.RuleId.VerticalAccessReachable );

		if ( exteriorWallCount > 0 )
		{
			var windowRatio = exteriorWindowCount / (float)exteriorWallCount;
			if ( windowRatio > ThornsProcBuildingRules.ExteriorWindowChanceMaxHard )
				failed.Add( ThornsProcBuildingRules.RuleId.WindowsOnlyOnExterior );
		}

		var analysis = ThornsProcBuildingAnalysis.Analyze( layout, walls, doorSide, doorIndex );
		if ( analysis.UnreachableWalkableCells > 0 && !failed.Contains( ThornsProcBuildingRules.RuleId.EveryWalkableReachableFromDoor ) )
			failed.Add( ThornsProcBuildingRules.RuleId.NoIsolatedFloors );

		var quality = ThornsProcBuildingQuality.Score( layout, walls, analysis );
		var passed = failed.Count == 0;

		return new ThornsProcBuildingValidationReport
		{
			Passed = passed,
			FailedHardRules = failed.Distinct().ToList(),
			Analysis = analysis,
			QualityScore = quality,
			Summary = passed
				? $"OK — walkable={analysis.TotalWalkableCells} rooms={analysis.RoomRegionCount} quality={quality}"
				: $"FAIL ({failed.Count} hard) — {string.Join( ", ", failed )}"
		};
	}

	static bool ValidateStructuralSupport( ThornsProcBuildingLayout layout )
	{
		for ( var s = 1; s < layout.Stories; s++ )
		for ( var x = 0; x < layout.WidthCells; x++ )
		for ( var y = 0; y < layout.DepthCells; y++ )
		{
			if ( !layout.IsCellOccupied( s, x, y ) )
				continue;

			if ( !layout.IsCellOccupied( s - 1, x, y ) )
				return false;
		}

		return true;
	}

	static bool ValidateMultiFloorRamps( ThornsProcBuildingLayout layout, List<ThornsProcBuildingRules.RuleId> failed ) =>
		layout.Stories <= 1 || ThornsProcBuildingRampValidation.PassesStrictRules( layout, failed );

	/// <summary>Post interior-prop scatter — props must not sit on blocked cells.</summary>
	public static bool ValidatePropCell(
		ThornsProcBuildingLayout layout,
		int gridX,
		int gridY,
		int stories,
		ThornsProcBuildingInteriorSample.InteriorPlacementHints hints )
	{
		if ( gridX < 0 || gridX >= layout.WidthCells || gridY < 0 || gridY >= layout.DepthCells )
			return false;

		if ( !layout.HasWalkableFloorAt( 0, gridX, gridY ) )
			return false;

		if ( stories > 1 )
		{
			for ( var rs = 0; rs < stories - 1; rs++ )
			{
				if ( layout.IsShaftCellForRampAtStory( rs, gridX, gridY ) )
					return false;
			}
		}

		if ( hints.DoorSide >= 0
		     && ThornsProcBuildingInteriorSample.TryGetDoorInteriorCell(
			     hints.DoorSide, hints.DoorIndex, layout.WidthCells, layout.DepthCells, out var dx, out var dy )
		     && dx == gridX && dy == gridY )
			return false;

		return true;
	}
}
