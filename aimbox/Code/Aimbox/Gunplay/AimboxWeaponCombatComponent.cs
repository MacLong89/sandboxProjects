namespace Sandbox;

/// <summary>
/// Owns recoil state + fire pipeline for one combat pawn. Attach alongside player or bot controller.
/// </summary>
[Title( "Aimbox Weapon Combat" )]
[Category( "Aimbox/Gunplay" )]
public sealed class AimboxWeaponCombatComponent : Component
{
	public IAimboxCombatAuthority CombatAuthority { get; set; } = new AimboxLocalCombatAuthority();
	public IAimboxWeaponPresentationGate PresentationGate { get; set; } = AimboxDefaultPresentationGate.Instance;

	readonly AimboxRecoilSessionState _recoil = new();

	IAimboxCombatActor _actor;
	bool _isProxyPlayer;

	protected override void OnAwake()
	{
		_actor = Components.Get<AimboxPlayerController>() as IAimboxCombatActor
		         ?? Components.Get<AimboxBotController>() as IAimboxCombatActor;

		if ( Components.Get<AimboxBotController>() is not null )
			PresentationGate = AimboxBotPresentationGate.Instance;

		_isProxyPlayer = Components.Get<AimboxPlayerController>() is { IsProxy: true };
	}

	public void ResetRecoilState() => _recoil.Reset();

	public void IntegrateRecoil( ref float pitch, ref Rotation worldRotation )
	{
		if ( _isProxyPlayer )
			return;

		var yaw = worldRotation;
		if ( _recoil.Controller.Integrate( ref pitch, ref yaw ) )
			worldRotation = yaw;
	}

	public bool TryFire(
		AimboxWeaponRuntime weapon,
		bool wantsAds,
		bool moving,
		bool crouching,
		bool meleeHeavy,
		AimboxViewModelController viewModel,
		CameraComponent camera,
		out AimboxHitscanShotResult shot )
	{
		shot = AimboxHitscanShotResult.Empty;

		if ( _actor is null || _isProxyPlayer || !_actor.IsAlive )
			return false;

		if ( !PresentationGate.AllowsCombatFire( _actor, viewModel ) )
			return false;

		if ( !weapon.TryConsumeShot() )
			return false;

		if ( _actor.IsHumanPlayer && _actor is AimboxPlayerController player )
			player.Data.ShotsFired++;

		PlayFireSfx( weapon, meleeHeavy );

		var fireDirection = ResolveFireDirection( weapon, wantsAds, moving, crouching, out var kickPitch, out var kickYaw );
		ApplyRecoilPresentation( weapon, kickPitch, kickYaw, viewModel );

		if ( AimboxNetworkCombat.UseHostAuthority && !Networking.IsHost && _actor is AimboxPlayerController networkedAttacker )
		{
			AimboxNetworkCombat.RequestPlayerFire(
				networkedAttacker,
				weapon,
				fireDirection,
				wantsAds,
				moving,
				crouching,
				meleeHeavy );
			return true;
		}

		var request = new AimboxCombatShotRequest(
			_actor,
			weapon,
			fireDirection,
			wantsAds,
			moving,
			crouching,
			meleeHeavy );

		shot = CombatAuthority.ResolveShot( in request );
		CombatAuthority.SpawnTracers(
			in request,
			shot,
			new AimboxCombatPresentationContext(
				viewModel?.WeaponSkin,
				viewModel?.ViewModelRoot,
				camera,
				viewModel ) );

		if ( shot.AnyHit && _actor.IsHumanPlayer && _actor is AimboxPlayerController human )
			human.Data.ShotsHit++;

		CombatAuthority.ApplyDamage( _actor, weapon.Definition.Id, in shot, meleeHeavy );

		if ( shot.TotalDamage > 0f )
			_actor.RegisterCombatHitFeedback( shot.TotalDamage, shot.AnyHeadshot );

		if ( !weapon.Definition.IsMelee && !weapon.Definition.IsBow )
			AimboxCombatNoiseBus.EmitGunfire( _actor, weapon.Definition, weapon.NoiseLoudnessMultiplier );

		return true;
	}

	Vector3 ResolveFireDirection(
		AimboxWeaponRuntime weapon,
		bool wantsAds,
		bool moving,
		bool crouching,
		out float kickPitch,
		out float kickYaw )
	{
		kickPitch = 0f;
		kickYaw = 0f;

		if ( weapon.Definition.IsMelee )
			return _actor.AimForward;

		var direction = AimboxWeaponRecoilSolve.SolveFireDirection(
			_actor.AimForward,
			weapon.Definition,
			ref _recoil.LastShotTime,
			ref _recoil.PatternIndex,
			ref _recoil.SprayOrdinal,
			Time.Now,
			wantsAds,
			moving,
			crouching,
			out kickPitch,
			out kickYaw );

		kickPitch *= weapon.EffectiveRecoilKickMultiplier;
		kickYaw *= weapon.EffectiveRecoilKickMultiplier;
		return direction;
	}

	void ApplyRecoilPresentation( AimboxWeaponRuntime weapon, float kickPitch, float kickYaw, AimboxViewModelController viewModel )
	{
		if ( weapon.Definition.IsMelee )
			return;

		_recoil.Controller.ApplyKick( kickPitch, kickYaw );
		viewModel?.ApplyViewKick( kickPitch, kickYaw );

		if ( _actor is AimboxPlayerController player )
		{
			AimboxRecoilDebug.LogShot(
				weapon.Definition.Id,
				kickPitch,
				kickYaw,
				player.GetCombatPitch(),
				player.WorldRotation.Angles().yaw,
				_recoil.Controller );
		}
	}

	void PlayFireSfx( AimboxWeaponRuntime weapon, bool meleeHeavy )
	{
		if ( weapon.Definition.IsMelee )
			AimboxGameplaySfx.PlayMeleeSwing( _actor, meleeHeavy );
		else
			AimboxGameplaySfx.PlayFire( _actor, weapon.Definition, weapon.NoiseLoudnessMultiplier );
	}
}
