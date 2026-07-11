namespace Sandbox;

/// <summary>Facade kept for <see cref="AimboxGame"/> wiring. Implementation lives in Gunplay module.</summary>
public sealed class AimboxHitscanSystem
{
	readonly AimboxLocalCombatAuthority _authority = new();

	public AimboxHitscanShotResult Fire(
		IAimboxCombatActor attacker,
		AimboxWeaponRuntime weapon,
		bool ads,
		Vector3 aimForward ) =>
		_authority.ResolveShot( new AimboxCombatShotRequest(
			attacker,
			weapon,
			aimForward,
			ads,
			attacker is not null && attacker.GetMovementVelocity().WithZ( 0f ).Length > 55f,
			attacker?.IsCrouching ?? false,
			false ) );

	public void SpawnTracersForShot(
		IAimboxCombatActor attacker,
		AimboxWeaponRuntime weapon,
		AimboxHitscanShotResult shot,
		SkinnedModelRenderer viewModelRenderer,
		GameObject viewModelRoot,
		CameraComponent camera ) =>
		_authority.SpawnTracers(
			new AimboxCombatShotRequest( attacker, weapon, attacker?.AimForward ?? Vector3.Forward, false, false, false, false ),
			shot,
			new AimboxCombatPresentationContext( viewModelRenderer, viewModelRoot, camera, null ) );
}

public sealed class AimboxDamageSystem
{
	public void ApplyDamage( IAimboxCombatActor attacker, IAimboxCombatActor target, AimboxWeaponId weapon, float damage, bool headshot, float distance, bool allowSelfDamage = false )
	{
		if ( attacker is null || target is null )
			return;

		var mode = AimboxGame.Instance?.Match.Mode ?? default;
		if ( AimboxAimModeRules.IsAimMode( mode ) && attacker.IsHumanPlayer && target.IsHumanPlayer )
			return;

		if ( attacker == target )
		{
			if ( !allowSelfDamage )
				return;
		}
		else if ( attacker.IsTeammate( target ) )
		{
			return;
		}

		if ( attacker.IsHumanPlayer && attacker is AimboxPlayerController playerAttacker )
		{
			var scaled = damage * playerAttacker.CombatDamageMultiplier;
			var weaponData = playerAttacker.Data.GetWeapon( weapon );
			AimboxGame.Instance.WeaponProgression.RecordWeaponDamage( playerAttacker.Data, weapon, (int)MathF.Round( scaled ) );
			target.TakeDamage( attacker, weapon, scaled, headshot, distance );
			return;
		}

		target.TakeDamage( attacker, weapon, damage, headshot, distance );
	}
}
