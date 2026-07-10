namespace Sandbox;

public interface IAimboxCombatAuthority
{
	AimboxHitscanShotResult ResolveShot( in AimboxCombatShotRequest request );

	void SpawnTracers(
		in AimboxCombatShotRequest request,
		AimboxHitscanShotResult shot,
		AimboxCombatPresentationContext presentation );

	void ApplyDamage( IAimboxCombatActor attacker, AimboxWeaponId weaponId, in AimboxHitscanShotResult shot, bool meleeHeavy );
}

public interface IAimboxWeaponPresentationGate
{
	bool AllowsCombatFire( IAimboxCombatActor owner, AimboxViewModelController viewModel );
	bool AllowsAds( IAimboxCombatActor owner, AimboxViewModelController viewModel );
}

public readonly record struct AimboxCombatPresentationContext(
	SkinnedModelRenderer ViewModelRenderer,
	GameObject ViewModelRoot,
	CameraComponent Camera,
	AimboxViewModelController ViewModelController );
