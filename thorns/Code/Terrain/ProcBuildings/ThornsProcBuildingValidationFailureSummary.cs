using System.Collections.Generic;

namespace Sandbox;

/// <summary>Human-readable primary failure for settlement / compile logging.</summary>
public readonly struct ThornsProcBuildingValidationFailureSummary
{
	public ThornsProcBuildingRules.RuleCategory Category { get; init; }
	public ThornsProcBuildingRules.RuleId Rule { get; init; }
	public string Reason { get; init; }
	public int Story { get; init; }
	public int CellX { get; init; }
	public int CellY { get; init; }

	public bool HasCell => CellX >= 0 && CellY >= 0;

	public static ThornsProcBuildingValidationFailureSummary FromReport(
		ThornsProcBuildingLayout layout,
		ThornsProcBuildingValidationReport report,
		IReadOnlyList<ThornsProcBuildingRampValidation.RampIssue> rampIssues )
	{
		if ( rampIssues is { Count: > 0 } )
		{
			var ri = rampIssues[0];
			return new()
			{
				Category = ThornsProcBuildingRules.RuleCategory.MultiFloor,
				Rule = ri.Rule,
				Reason = ri.Detail,
				Story = ri.Story,
				CellX = ri.X,
				CellY = ri.Y
			};
		}

		if ( report?.FailedHardRules is { Count: > 0 } rules )
		{
			var rule = rules[0];
			return new()
			{
				Category = ThornsProcBuildingRules.CategoryFor( rule ),
				Rule = rule,
				Reason = rule.ToString(),
				Story = -1,
				CellX = -1,
				CellY = -1
			};
		}

		return new()
		{
			Category = ThornsProcBuildingRules.RuleCategory.Structural,
			Rule = ThornsProcBuildingRules.RuleId.EveryWalkableReachableFromDoor,
			Reason = report?.Summary ?? "UnknownValidationFailure",
			Story = layout?.Stories > 0 ? 0 : -1,
			CellX = -1,
			CellY = -1
		};
	}

	public string FormatForLog( ThornsProcBuildingType type ) =>
		HasCell
			? $"Type={type} Reason={Reason} Rule={Rule} Story={Story} Cell=({CellX},{CellY})"
			: $"Type={type} Reason={Reason} Rule={Rule}";
}
