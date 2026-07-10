namespace Sandbox;

/// <summary>
/// Procedural building generation entry points.
/// Identity-driven layouts: <see cref="ThornsProcBuildingIdentityGenerator"/>.
/// Legacy random layouts: <see cref="ThornsProcBuildingLayout.Generate"/>.
/// </summary>
public static class ThornsProcBuildingGenerator
{
	public static IReadOnlyList<ThornsProcBuildingRules.GenerationStage> Pipeline { get; } =
	[
		ThornsProcBuildingRules.GenerationStage.FootprintSilhouette, // tile blueprint layers
		ThornsProcBuildingRules.GenerationStage.UpperFloorsAndSupport,
		ThornsProcBuildingRules.GenerationStage.RampAssignment,
		ThornsProcBuildingRules.GenerationStage.DoorSelection,
		ThornsProcBuildingRules.GenerationStage.InteriorWallPlan,
		ThornsProcBuildingRules.GenerationStage.HardValidation,
		ThornsProcBuildingRules.GenerationStage.SoftQualityScore,
		ThornsProcBuildingRules.GenerationStage.PropPlacement,
		ThornsProcBuildingRules.GenerationStage.PostPropValidation
	];

	public static int ActiveRulesetVersion =>
		ThornsProcBuildingRules.RulesetVersion * 100
		+ ThornsProcBuildingIdentityRegistry.IdentityRulesetVersion * 10
		+ ThornsProcBuildingMaterialAffinity.AffinityRulesetVersion;
}
