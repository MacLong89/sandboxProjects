using System.Collections.Generic;

namespace Sandbox;

/// <summary>Strict ramp rules: no overlaps, headroom, landings, and vertical access per stairwell.</summary>
public static class ThornsProcBuildingRampValidation
{
	public sealed class RampIssue
	{
		public ThornsProcBuildingRules.RuleId Rule { get; init; }
		public int Story { get; init; }
		public int X { get; init; }
		public int Y { get; init; }
		public string Detail { get; init; } = "";
	}

	public static IReadOnlyList<RampIssue> CollectIssues( ThornsProcBuildingLayout layout )
	{
		var issues = new List<RampIssue>( 16 );
		if ( layout is null || layout.Stories <= 1 )
			return issues;

		for ( var s = 0; s < layout.Stories; s++ )
		{
			var seen = new HashSet<(int x, int y)>();
			foreach ( var ramp in layout.GetRampsOnStory( s ) )
			{
				if ( !layout.IsCellOccupied( s, ramp.X, ramp.Y ) )
				{
					issues.Add( new()
					{
						Rule = ThornsProcBuildingRules.RuleId.LowerFloorHasRampWhenMultiStory,
						Story = s,
						X = ramp.X,
						Y = ramp.Y,
						Detail = "ramp cell not occupied"
					} );
					continue;
				}

				if ( !seen.Add( (ramp.X, ramp.Y) ) )
				{
					issues.Add( new()
					{
						Rule = ThornsProcBuildingRules.RuleId.UpperFloorHasShaftAboveRamp,
						Story = s,
						X = ramp.X,
						Y = ramp.Y,
						Detail = "duplicate ramp anchor on story"
					} );
				}

				if ( ramp.Direction == ThornsProcRampDirection.None )
				{
					issues.Add( new()
					{
						Rule = ThornsProcBuildingRules.RuleId.LowerFloorHasRampWhenMultiStory,
						Story = s,
						X = ramp.X,
						Y = ramp.Y,
						Detail = "ramp direction unset"
					} );
					continue;
				}

				if ( !ThornsProcTileRampHeadroom.HasRequiredOpenings( layout, s, ramp.X, ramp.Y, ramp.Direction ) )
				{
					issues.Add( new()
					{
						Rule = ThornsProcBuildingRules.RuleId.UpperFloorHasShaftAboveRamp,
						Story = s,
						X = ramp.X,
						Y = ramp.Y,
						Detail = "missing two-tile headroom above ramp"
					} );
				}

				if ( !ThornsProcBuildingRampTraversal.HasWalkableLandingExit( layout, s, ramp ) )
				{
					issues.Add( new()
					{
						Rule = ThornsProcBuildingRules.RuleId.VerticalAccessReachable,
						Story = s,
						X = ramp.X,
						Y = ramp.Y,
						Detail = "ramp landing blocked"
					} );
				}

				CollectShaftOverlapIssues( layout, s, ramp, issues );
			}
		}

		CollectVerticalRampStackIssues( layout, issues );

		for ( var s = 0; s < layout.Stories - 1; s++ )
		{
			if ( layout.GetRampCountOnStory( s ) == 0 )
			{
				issues.Add( new()
				{
					Rule = ThornsProcBuildingRules.RuleId.LowerFloorHasRampWhenMultiStory,
					Story = s,
					Detail = "story has no ramps"
				} );
				continue;
			}

			var hasUpperEntry = false;
			foreach ( var ramp in layout.GetRampsOnStory( s ) )
			{
				foreach ( var _ in ThornsProcBuildingRampTraversal.EnumerateUpperEntryCellsForRamp( layout, ramp ) )
				{
					hasUpperEntry = true;
					break;
				}

				if ( hasUpperEntry )
					break;
			}

			if ( !hasUpperEntry )
			{
				issues.Add( new()
				{
					Rule = ThornsProcBuildingRules.RuleId.VerticalAccessReachable,
					Story = s,
					Detail = "no walkable upper entry from any ramp"
				} );
			}
		}

		return issues;
	}

	static void CollectVerticalRampStackIssues( ThornsProcBuildingLayout layout, List<RampIssue> issues )
	{
		for ( var s = 0; s < layout.Stories - 1; s++ )
		{
			foreach ( var lower in layout.GetRampsOnStory( s ) )
			{
				ThornsProcTileRampHeadroom.GetRiseDelta( lower.Direction, out var riseDx, out var riseDy );
				var headX = lower.X + riseDx;
				var headY = lower.Y + riseDy;

				foreach ( var upper in layout.GetRampsOnStory( s + 1 ) )
				{
					if ( upper.X == lower.X && upper.Y == lower.Y )
					{
						issues.Add( new RampIssue
						{
							Rule = ThornsProcBuildingRules.RuleId.RampNotStackedOnShaft,
							Story = s + 1,
							X = upper.X,
							Y = upper.Y,
							Detail = "ramp stacked on lower ramp cell"
						} );
					}

					if ( upper.X == headX && upper.Y == headY )
					{
						issues.Add( new RampIssue
						{
							Rule = ThornsProcBuildingRules.RuleId.RampNotStackedOnShaft,
							Story = s + 1,
							X = upper.X,
							Y = upper.Y,
							Detail = "ramp on lower ramp headroom cell"
						} );
					}
				}
			}
		}
	}

	static void CollectShaftOverlapIssues(
		ThornsProcBuildingLayout layout,
		int story,
		ThornsProcRampSpec ramp,
		List<RampIssue> issues )
	{
		var shaft = new List<(int x, int y)>( 4 );
		ThornsProcBuildingRampGeometry.CollectShaftCells( layout, ramp, shaft );
		foreach ( var other in layout.GetRampsOnStory( story ) )
		{
			if ( other.X == ramp.X && other.Y == ramp.Y )
				continue;

			var otherShaft = new List<(int x, int y)>( 4 );
			ThornsProcBuildingRampGeometry.CollectShaftCells( layout, other, otherShaft );
			foreach ( var cell in shaft )
			{
				var overlaps = false;
				for ( var k = 0; k < otherShaft.Count; k++ )
				{
					if ( otherShaft[k] != cell )
						continue;

					overlaps = true;
					break;
				}

				if ( !overlaps )
					continue;

				issues.Add( new()
				{
					Rule = ThornsProcBuildingRules.RuleId.UpperFloorHasShaftAboveRamp,
					Story = story,
					X = cell.x,
					Y = cell.y,
					Detail = $"shaft overlap between ramps ({ramp.X},{ramp.Y}) and ({other.X},{other.Y})"
				} );
			}
		}
	}

	public static bool PassesStrictRules( ThornsProcBuildingLayout layout, List<ThornsProcBuildingRules.RuleId> failedOut = null )
	{
		var issues = CollectIssues( layout );
		if ( issues.Count == 0 )
			return true;

		if ( failedOut is null )
			return false;

		foreach ( var issue in issues )
			if ( !failedOut.Contains( issue.Rule ) )
				failedOut.Add( issue.Rule );

		return false;
	}
}
