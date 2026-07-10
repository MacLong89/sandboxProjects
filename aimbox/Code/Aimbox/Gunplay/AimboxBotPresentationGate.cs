namespace Sandbox;

/// <summary>Presentation gate for bots — no viewmodel deploy/reload blocking.</summary>
public sealed class AimboxBotPresentationGate : IAimboxWeaponPresentationGate
{
	public static AimboxBotPresentationGate Instance { get; } = new();

	public bool AllowsCombatFire( IAimboxCombatActor owner, AimboxViewModelController viewModel ) =>
		owner is not null && owner.IsAlive && AimboxGame.Instance?.IsCombatLocked != true;

	public bool AllowsAds( IAimboxCombatActor owner, AimboxViewModelController viewModel ) =>
		owner is not null && owner.IsAlive;
}
