namespace Sandbox;

/// <summary>Offline / listen-server local hitscan. Replace with host-validated authority for competitive MP.</summary>
public sealed class AimboxLocalCombatAuthority : IAimboxCombatAuthority
{
	const int MaxPelletsPerShot = 12;

	readonly AimboxPelletResult[] _pelletScratch = new AimboxPelletResult[MaxPelletsPerShot];

	public AimboxHitscanShotResult ResolveShot( in AimboxCombatShotRequest request )
	{
		var attacker = request.Attacker;
		var weapon = request.Weapon;
		if ( attacker is null || weapon is null )
			return AimboxHitscanShotResult.Empty;

		var def = weapon.Definition;
		if ( def.IsMelee )
			return ResolveMeleeShot( attacker, weapon, request.AimForward, request.MeleeHeavy );

		var pelletSpread = ResolvePelletSpreadDegrees( weapon, request.AdsHeld );
		if ( !attacker.IsHumanPlayer && request.AdsHeld )
			pelletSpread *= AimboxBotTuning.BotAdsPelletSpreadMultiplier;
		var aimOrigin = attacker.EyePosition;
		var aimForward = request.AimForward.Normal;
		var pelletCount = Math.Clamp( def.Pellets, 1, MaxPelletsPerShot );

		for ( var i = 0; i < pelletCount; i++ )
		{
			var direction = AimboxHitscanSpread.ApplyPelletSpread( aimForward, pelletSpread, i, def.Id );
			_pelletScratch[i] = TracePellet( attacker, def, aimOrigin, direction );
		}

		return new AimboxHitscanShotResult( _pelletScratch[..pelletCount] );
	}

	public void SpawnTracers(
		in AimboxCombatShotRequest request,
		AimboxHitscanShotResult shot,
		AimboxCombatPresentationContext presentation )
	{
		var attacker = request.Attacker;
		var weapon = request.Weapon;
		if ( attacker is null || weapon is null || weapon.Definition.IsMelee || shot.Pellets.Count <= 0 )
			return;

		foreach ( var pellet in shot.Pellets )
		{
			var tracerOrigin = ResolveTracerAimOrigin( attacker, weapon, pellet.Direction, presentation );
			AimboxCombatTracerService.SpawnLocalShot(
				attacker,
				weapon,
				tracerOrigin,
				pellet.Direction,
				pellet.TracerEnd,
				presentation.ViewModelRenderer,
				presentation.ViewModelRoot,
				presentation.Camera,
				presentation.ViewModelController?.AttachmentMount?.SuppressorRenderer );
		}
	}

	static Vector3 ResolveTracerAimOrigin(
		IAimboxCombatActor attacker,
		AimboxWeaponRuntime weapon,
		Vector3 aimDirection,
		AimboxCombatPresentationContext presentation )
	{
		if ( attacker.ShowThirdPersonBody || attacker is AimboxBotController )
		{
			if ( AimboxCombatMuzzleResolve.TryResolveThirdPersonMuzzleWorld( attacker, weapon.Definition.Id, aimDirection, out var muzzle ) )
				return muzzle;

			return AimboxCombatMuzzleResolve.ResolveThirdPersonTracerFallback( attacker, aimDirection );
		}

		if ( attacker is AimboxPlayerController player )
		{
			return AimboxCombatMuzzleResolve.ResolvePlayerTracerOrigin(
				player,
				aimDirection,
				weapon.Definition.Id,
				presentation.ViewModelRenderer,
				presentation.ViewModelRoot,
				presentation.Camera,
				presentation.ViewModelController?.AttachmentMount?.SuppressorRenderer );
		}

		return attacker.EyePosition;
	}

	public void ApplyDamage( IAimboxCombatActor attacker, AimboxWeaponId weaponId, in AimboxHitscanShotResult shot, bool meleeHeavy )
	{
		if ( attacker is null || shot.Pellets.Count <= 0 )
			return;

		foreach ( var pellet in shot.Pellets )
		{
			if ( !pellet.Hit )
				continue;

			if ( pellet.HitActor is not null )
			{
				if ( meleeHeavy )
					AimboxGameplaySfx.PlayMeleeContact( attacker, true );
				AimboxGame.Instance.Damage.ApplyDamage(
					attacker,
					pellet.HitActor,
					weaponId,
					pellet.Damage,
					pellet.Headshot,
					pellet.Distance );
				continue;
			}

			if ( pellet.HitDummy is not null )
			{
				if ( meleeHeavy )
					AimboxGameplaySfx.PlayMeleeContact( attacker, true );

				var aimMode = AimboxAimModeRules.IsAimMode( AimboxGame.Instance?.Match.Mode ?? default );
				if ( attacker.IsHumanPlayer && attacker is AimboxPlayerController playerAttacker )
				{
					var scaled = pellet.Damage * playerAttacker.CombatDamageMultiplier;
					if ( !aimMode )
					{
						AimboxGame.Instance.WeaponProgression.RecordWeaponDamage(
							playerAttacker.Data,
							weaponId,
							(int)MathF.Round( scaled ) );
					}

					pellet.HitDummy.TakeDamage( playerAttacker, weaponId, scaled, pellet.Headshot );
				}
				else
				{
					pellet.HitDummy.TakeDamage( null, weaponId, pellet.Damage, pellet.Headshot );
				}
			}
		}
	}

	static AimboxHitscanShotResult ResolveMeleeShot(
		IAimboxCombatActor attacker,
		AimboxWeaponRuntime weapon,
		Vector3 aimForward,
		bool heavy )
	{
		var def = weapon.Definition;
		aimForward = aimForward.Normal;
		var end = attacker.EyePosition + aimForward * def.Range;

		var tr = attacker.Scene.Trace.Ray( attacker.EyePosition, end )
			.IgnoreGameObjectHierarchy( attacker.GameObject )
			.Run();

		var tracerEnd = tr.Hit ? tr.HitPosition : end;
		if ( !tr.Hit )
			return new AimboxHitscanShotResult( [new AimboxPelletResult( aimForward, tracerEnd, null, null, false, 0f, def.Range )] );

		var distance = attacker.EyePosition.Distance( tr.HitPosition );
		var damage = def.Damage * (heavy ? 1.35f : 1f);

		var hitActor = AimboxCombatTargetResolve.FindCombatActor( tr.GameObject );
		if ( hitActor is not null && hitActor != attacker && !hitActor.IsTeammate( attacker ) )
		{
			var headshot = AimboxHitboxes.IsHeadshot( tr.HitPosition, hitActor.WorldPosition, hitActor.IsCrouching );
			var finalDamage = headshot ? damage * def.HeadshotMultiplier : damage;
			return new AimboxHitscanShotResult( [new AimboxPelletResult( aimForward, tracerEnd, hitActor, null, headshot, finalDamage, distance )] );
		}

		var hitDummy = AimboxCombatTargetResolve.FindDummy( tr.GameObject );
		if ( hitDummy is not null && hitDummy.IsAlive )
		{
			var headshot = AimboxHitboxes.IsHeadshot( tr.HitPosition, hitDummy.WorldPosition );
			var finalDamage = headshot ? damage * def.HeadshotMultiplier : damage;
			return new AimboxHitscanShotResult( [new AimboxPelletResult( aimForward, tracerEnd, null, hitDummy, headshot, finalDamage, distance )] );
		}

		return new AimboxHitscanShotResult( [new AimboxPelletResult( aimForward, tracerEnd, null, null, false, 0f, distance )] );
	}

	static float ResolvePelletSpreadDegrees( AimboxWeaponRuntime weapon, bool ads )
	{
		var def = weapon.Definition;
		var spread = def.PelletSpreadDegrees > 0f ? def.PelletSpreadDegrees : weapon.EffectiveSpread;
		if ( ads )
			spread *= def.AdsSpreadMultiplier;

		return spread;
	}

	static AimboxPelletResult TracePellet(
		IAimboxCombatActor attacker,
		AimboxWeaponDefinition def,
		Vector3 aimOrigin,
		Vector3 direction )
	{
		direction = direction.Normal;
		var end = aimOrigin + direction * def.Range;

		var tr = attacker.Scene.Trace.Ray( aimOrigin, end )
			.IgnoreGameObjectHierarchy( attacker.GameObject )
			.Run();

		var tracerEnd = tr.Hit ? tr.HitPosition : end;
		if ( !tr.Hit )
			return new AimboxPelletResult( direction, tracerEnd, null, null, false, 0f, def.Range );

		var distance = aimOrigin.Distance( tr.HitPosition );
		var damage = def.DamageAtDistance( distance );

		var hitActor = AimboxCombatTargetResolve.FindCombatActor( tr.GameObject );
		if ( hitActor is not null && hitActor != attacker && !hitActor.IsTeammate( attacker ) )
		{
			var headshot = AimboxHitboxes.IsHeadshot( tr.HitPosition, hitActor.WorldPosition, hitActor.IsCrouching );
			var finalDamage = headshot ? damage * def.HeadshotMultiplier : damage;
			return new AimboxPelletResult( direction, tracerEnd, hitActor, null, headshot, finalDamage, distance );
		}

		var hitDummy = AimboxCombatTargetResolve.FindDummy( tr.GameObject );
		if ( hitDummy is not null && hitDummy.IsAlive )
		{
			var headshot = AimboxHitboxes.IsHeadshot( tr.HitPosition, hitDummy.WorldPosition );
			var finalDamage = headshot ? damage * def.HeadshotMultiplier : damage;
			return new AimboxPelletResult( direction, tracerEnd, null, hitDummy, headshot, finalDamage, distance );
		}

		return new AimboxPelletResult( direction, tracerEnd, null, null, false, 0f, distance );
	}
}

/// <summary>Deterministic pellet spread helpers shared by authority + future replay systems.</summary>
public static class AimboxHitscanSpread
{
	public static Vector3 ApplyPelletSpread( Vector3 forward, float halfAngleDegrees, int pelletIndex, AimboxWeaponId weaponId )
	{
		forward = forward.Normal;
		if ( halfAngleDegrees <= 0.0005f )
			return forward;

		var radius = MathF.Sqrt( SpreadUnit( weaponId, pelletIndex, 5 ) ) * halfAngleDegrees;
		var theta = SpreadUnit( weaponId, pelletIndex, 11 ) * MathF.PI * 2f;
		return Rotation.From( MathF.Sin( theta ) * radius, MathF.Cos( theta ) * radius, 0f ) * forward;
	}

	static float SpreadUnit( AimboxWeaponId weaponId, int pelletIndex, int salt )
	{
		unchecked
		{
			var hash = (uint)((int)weaponId * 73856093) ^ (uint)(pelletIndex * 19349663) ^ (uint)(salt * 83492791);
			hash ^= hash >> 16;
			hash *= 2246822519u;
			hash ^= hash >> 13;
			hash *= 3266489917u;
			hash ^= hash >> 16;
			return (hash & 0x00ffffff) / 16777215f;
		}
	}
}

/// <summary>Resolves combat targets on hit colliders (supports child bones/body).</summary>
public static class AimboxCombatTargetResolve
{
	public static IAimboxCombatActor FindCombatActor( GameObject hitObject ) =>
		AimboxCombatActorRegistry.FindFromGameObject( hitObject );

	public static AimboxPlayerController FindPlayer( GameObject hitObject ) =>
		hitObject.IsValid()
			? hitObject.Components.Get<AimboxPlayerController>( FindMode.EverythingInSelfAndParent )
			: null;

	public static AimboxDummyTarget FindDummy( GameObject hitObject ) =>
		hitObject.IsValid()
			? hitObject.Components.Get<AimboxDummyTarget>( FindMode.EverythingInSelfAndParent )
			: null;
}

/// <summary>Default presentation gate — blocks fire during deploy/reload/melee anim.</summary>
public sealed class AimboxDefaultPresentationGate : IAimboxWeaponPresentationGate
{
	public static AimboxDefaultPresentationGate Instance { get; } = new();

	public bool AllowsCombatFire( IAimboxCombatActor owner, AimboxViewModelController viewModel )
	{
		if ( owner is not AimboxPlayerController player || player.IsProxy )
			return false;

		if ( AimboxGame.Instance?.IsCombatLocked == true )
			return false;

		if ( owner.IsSprintMoving && owner.CurrentWeapon is { Definition.IsMelee: false } )
			return false;

		var animator = viewModel?.Animator;
		if ( animator is null || !animator.IsValid() )
			return true;

		return animator.PresentationAllowsCombatFire;
	}

	public bool AllowsAds( IAimboxCombatActor owner, AimboxViewModelController viewModel )
	{
		if ( owner is not AimboxPlayerController player || player.IsProxy )
			return false;

		var animator = viewModel?.Animator;
		if ( animator is null || !animator.IsValid() )
			return true;

		return animator.PresentationAllowsAds;
	}
}
