namespace Terraingen.TerrainGen;

using Terraingen.Buildings;
using Terraingen.Combat;

/// <summary>Shared flags for stripped-down playtest scenes (lighting test, bandit test, etc.).</summary>
public static class ThornsMinimalTestSceneBootstrap
{
	public static bool IsActive =>
		ThornsLightingTestSceneBootstrap.IsActive
		|| ThornsBanditCombatTestScene.IsActive
		|| ThornsBowTestScene.IsActive
		|| ThornsBanditTestSceneBootstrap.IsActive
		|| ThornsSettlementTestSceneBootstrap.IsActive;
}
