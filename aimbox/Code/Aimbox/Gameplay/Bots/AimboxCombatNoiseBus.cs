namespace Sandbox;

public readonly record struct AimboxGunfireNoiseEvent(
	Vector3 Origin,
	AimboxTeam Team,
	float Loudness,
	float Time );

public readonly record struct AimboxMovementNoiseEvent(
	Vector3 Origin,
	AimboxTeam Team,
	float Loudness,
	float Time );

public static class AimboxCombatNoiseBus
{
	static readonly List<AimboxGunfireNoiseEvent> _gunfire = [];
	static readonly List<AimboxMovementNoiseEvent> _movement = [];
	const float MaxEventAge = 3f;

	public static void EmitGunfire( IAimboxCombatActor shooter, AimboxWeaponDefinition weapon, float loudnessMultiplier = 1f )
	{
		if ( shooter is null || weapon is null )
			return;

		loudnessMultiplier = MathF.Max( 0f, loudnessMultiplier );
		if ( loudnessMultiplier <= 0.001f )
			return;

		var loudness = weapon.Id switch
		{
			AimboxWeaponId.Usp => AimboxBotTuning.HearingRadiusPistol,
			AimboxWeaponId.M700 => AimboxBotTuning.HearingRadiusRifle * 1.15f,
			AimboxWeaponId.SpaghelliM4 => AimboxBotTuning.HearingRadiusRifle * 1.1f,
			_ => AimboxBotTuning.HearingRadiusRifle
		} * loudnessMultiplier;

		_gunfire.Add( new AimboxGunfireNoiseEvent( shooter.EyePosition, shooter.Team, loudness, Time.Now ) );
		PruneOldEvents();
	}

	public static void EmitMovement( IAimboxCombatActor mover, float loudness, float loudnessMultiplier = 1f )
	{
		if ( mover is null || loudness <= 0f )
			return;

		loudnessMultiplier = MathF.Max( 0f, loudnessMultiplier );
		if ( loudnessMultiplier <= 0.001f )
			return;

		_movement.Add( new AimboxMovementNoiseEvent(
			mover.WorldPosition,
			mover.Team,
			loudness * loudnessMultiplier,
			Time.Now ) );
		PruneOldEvents();
	}

	public static bool TryGetLoudestGunfireHeard( IAimboxCombatActor listener, out AimboxGunfireNoiseEvent heard )
	{
		heard = default;
		PruneOldEvents();

		var bestScore = 0f;
		foreach ( var noise in _gunfire )
		{
			if ( Time.Now - noise.Time > MaxEventAge )
				continue;

			var distance = listener.EyePosition.Distance( noise.Origin );
			if ( distance > noise.Loudness )
				continue;

			var score = 1f - distance / noise.Loudness;
			if ( score <= bestScore )
				continue;

			bestScore = score;
			heard = noise;
		}

		return bestScore > 0f;
	}

	public static bool TryGetLoudestHeard( IAimboxCombatActor listener, out AimboxGunfireNoiseEvent heard )
	{
		heard = default;
		PruneOldEvents();

		var bestScore = 0f;
		foreach ( var noise in _gunfire )
		{
			if ( Time.Now - noise.Time > MaxEventAge )
				continue;

			var distance = listener.EyePosition.Distance( noise.Origin );
			if ( distance > noise.Loudness )
				continue;

			var score = 1f - distance / noise.Loudness;
			if ( score <= bestScore )
				continue;

			bestScore = score;
			heard = noise;
		}

		if ( bestScore > 0f )
			return true;

		AimboxMovementNoiseEvent movementHeard = default;
		foreach ( var noise in _movement )
		{
			if ( Time.Now - noise.Time > MaxEventAge )
				continue;

			var distance = listener.EyePosition.Distance( noise.Origin );
			if ( distance > noise.Loudness )
				continue;

			var score = 1f - distance / noise.Loudness;
			if ( score <= bestScore )
				continue;

			bestScore = score;
			movementHeard = noise;
		}

		if ( bestScore <= 0f )
			return false;

		heard = new AimboxGunfireNoiseEvent( movementHeard.Origin, movementHeard.Team, movementHeard.Loudness, movementHeard.Time );
		return true;
	}

	static void PruneOldEvents()
	{
		for ( var i = _gunfire.Count - 1; i >= 0; i-- )
		{
			if ( Time.Now - _gunfire[i].Time > MaxEventAge )
				_gunfire.RemoveAt( i );
		}

		for ( var i = _movement.Count - 1; i >= 0; i-- )
		{
			if ( Time.Now - _movement[i].Time > MaxEventAge )
				_movement.RemoveAt( i );
		}
	}
}
