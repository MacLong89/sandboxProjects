namespace Terraingen.UI.Core;

using Terraingen.Buildings;
using Terraingen.Player;

/// <summary>
/// Single source of truth for whether gameplay UI overlays should consume world input.
/// AUDIT NOTE: death is intentionally NOT listed here — see <see cref="Terraingen.Combat.ThornsPlayerActionGate"/>
/// and <see cref="ThornsPlayerLocomotion.EnforceOverlayInputBlock"/> which combine overlay + death.
/// Revert caution: adding IsDead here can break death-camera look if other systems assume look stays on.
/// </summary>
public static class ThornsUiInputGate
{
	public static bool BlocksGameplayInput =>
		ThornsUiManager.BlocksGameplayInput
		|| ThornsMenuPerformance.IsOverlayUiOpen
		|| ThornsPlayerGameplay.Local?.IsAwaitingWorldContainerUi == true;

	public static bool BlocksHotbarInput =>
		!ThornsUiInputContext.AllowsHotbarInput
		|| BlocksGameplayInput;

	public static bool BlocksBuildInput =>
		!ThornsUiInputContext.AllowsBuildInput
		|| ThornsMenuHost.IsOpen
		|| ThornsMenuHost.IsWorldContainerOpen
		|| ThornsMenuHost.IsRadioShopOpen
		|| ThornsMenuHost.IsResearchOpen
		|| ThornsMenuHost.IsCampfireOpen
		|| ThornsMenuHost.IsWorkbenchOpen
		|| ThornsMenuHost.IsVictoryIntroOpen;

	public static bool AllowsHudTick =>
		ThornsUiInputContext.AllowsHudTick
		&& !ThornsUiManager.IsModalOpen
		&& !ThornsUiGameplayState.IsDead;

	public static bool AllowsTransientFeedback =>
		ThornsUiInputContext.AllowsTransientFeedback
		&& !ThornsUiGameplayState.ShouldDeferTransientFeedback;
}
