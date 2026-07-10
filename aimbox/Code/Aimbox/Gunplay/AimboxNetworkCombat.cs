namespace Sandbox;

/// <summary>Host-validated player-vs-player combat for listen-server multiplayer.</summary>
public static class AimboxNetworkCombat
{
	public static bool UseHostAuthority => Networking.IsActive;

	public static bool ShouldApplyPlayerDamage =>
		!UseHostAuthority || Networking.IsHost;

	public static void RequestPlayerFire(
		AimboxPlayerController attacker,
		AimboxWeaponRuntime weapon,
		Vector3 aimForward,
		bool adsHeld,
		bool moving,
		bool crouched,
		bool meleeHeavy )
	{
		if ( attacker is null || weapon is null || AimboxGame.Instance is null )
			return;

		if ( !UseHostAuthority || Networking.IsHost )
			return;

		AimboxGame.Instance.RpcRequestPlayerFire(
			attacker.AccountId,
			weapon.Definition.Id,
			attacker.EyePosition,
			aimForward.Normal,
			adsHeld,
			moving,
			crouched,
			meleeHeavy );
	}

	public static void DispatchPlayerKill(
		IAimboxCombatActor attacker,
		AimboxPlayerController victim,
		AimboxWeaponId weapon,
		bool headshot,
		float distance )
	{
		if ( attacker is null || victim is null || AimboxGame.Instance is null || !UseHostAuthority )
			return;

		if ( !Networking.IsHost )
			return;

		AimboxGame.Instance.RpcBroadcastPlayerKill(
			attacker.CombatId,
			victim.AccountId,
			weapon,
			headshot,
			distance );
	}
}
