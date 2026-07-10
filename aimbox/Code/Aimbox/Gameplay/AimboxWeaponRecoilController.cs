namespace Sandbox;

/// <summary>Spring-smoothed view kick applied to the local player camera angles.</summary>
public sealed class AimboxWeaponRecoilController
{
	public const float SpringStiffness = 220f;
	public const float SpringDamping = 12f;
	/// <summary>Fraction of each kick applied instantly so recoil is felt on the same frame as the shot.</summary>
	public const float ImmediateKickFraction = 0.55f;

	float _pitchTarget;
	float _yawTarget;
	float _pitchDisplay;
	float _yawDisplay;
	float _pitchVel;
	float _yawVel;
	float _pitchApplied;
	float _yawApplied;

	public float PitchTarget => _pitchTarget;
	public float YawTarget => _yawTarget;
	public float PitchDisplay => _pitchDisplay;
	public float YawDisplay => _yawDisplay;
	public bool IsActive =>
		MathF.Abs( _pitchTarget ) > 1e-3f
		|| MathF.Abs( _yawTarget ) > 1e-3f
		|| MathF.Abs( _pitchDisplay ) > 1e-3f
		|| MathF.Abs( _yawDisplay ) > 1e-3f
		|| MathF.Abs( _pitchVel ) > 1e-3f
		|| MathF.Abs( _yawVel ) > 1e-3f;

	public void Reset()
	{
		_pitchTarget = 0f;
		_yawTarget = 0f;
		_pitchDisplay = 0f;
		_yawDisplay = 0f;
		_pitchVel = 0f;
		_yawVel = 0f;
		_pitchApplied = 0f;
		_yawApplied = 0f;
	}

	public void ApplyKick( float pitchDegreesUp, float yawDegreesRight )
	{
		if ( MathF.Abs( pitchDegreesUp ) < 1e-5f && MathF.Abs( yawDegreesRight ) < 1e-5f )
			return;

		_pitchTarget += pitchDegreesUp;
		_yawTarget += yawDegreesRight;

		var immPitch = pitchDegreesUp * ImmediateKickFraction;
		var immYaw = yawDegreesRight * ImmediateKickFraction;
		_pitchDisplay += immPitch;
		_yawDisplay += immYaw;
	}

	public bool Integrate( ref float pitch, ref Rotation worldRotation )
	{
		var hasTarget = MathF.Abs( _pitchTarget - _pitchDisplay ) > 1e-4f
		                || MathF.Abs( _yawTarget - _yawDisplay ) > 1e-4f;
		var hasMotion = MathF.Abs( _pitchVel ) > 1e-4f || MathF.Abs( _yawVel ) > 1e-4f;
		if ( !hasTarget && !hasMotion )
			return false;

		var dt = Math.Clamp( Time.Delta, 0.001f, 0.05f );
		StepSpring( _pitchTarget, ref _pitchDisplay, ref _pitchVel, SpringStiffness, SpringDamping, dt );
		StepSpring( _yawTarget, ref _yawDisplay, ref _yawVel, SpringStiffness, SpringDamping, dt );

		var pitchDelta = _pitchDisplay - _pitchApplied;
		var yawDelta = _yawDisplay - _yawApplied;
		if ( MathF.Abs( pitchDelta ) < 1e-5f && MathF.Abs( yawDelta ) < 1e-5f )
			return false;

		_pitchApplied = _pitchDisplay;
		_yawApplied = _yawDisplay;

		_pitchTarget -= pitchDelta;
		_yawTarget -= yawDelta;

		var pitchBefore = pitch;
		pitch = Math.Clamp( pitch - pitchDelta, -85f, 85f );
		var angles = worldRotation.Angles();
		angles.yaw += yawDelta;
		worldRotation = Rotation.FromYaw( angles.yaw );

		AimboxRecoilDebug.RecordIntegrate( pitchBefore, pitch, pitchDelta, yawDelta, this );
		return true;
	}

	static void StepSpring( float target, ref float display, ref float vel, float stiffness, float damping, float dt )
	{
		var error = target - display;
		vel += (error * stiffness - vel * damping) * dt;
		display += vel * dt;
	}
}

/// <summary>Rate-limited recoil diagnostics for console tuning.</summary>
public static class AimboxRecoilDebug
{
	public static bool Enabled { get; set; } = false;

	static TimeSince _integrateLogCooldown;

	public static void LogShot( AimboxWeaponId weapon, float kickPitch, float kickYaw, float pitch, float yaw, AimboxWeaponRecoilController recoil )
	{
		if ( !Enabled )
			return;

		Log.Info( $"[Aimbox Recoil] shot weapon={weapon} kickPitch={kickPitch:F2} kickYaw={kickYaw:F2} pitch={pitch:F2} yaw={yaw:F2} target=({recoil.PitchTarget:F2},{recoil.YawTarget:F2}) display=({recoil.PitchDisplay:F2},{recoil.YawDisplay:F2})" );
	}

	public static void LogSolve( AimboxWeaponId weapon, int patternRow, Vector2 step, float patternScale, float visualScale, float kickPitch, float kickYaw )
	{
		if ( !Enabled )
			return;

		Log.Info( $"[Aimbox Recoil] solve weapon={weapon} row={patternRow} step=({step.x:F2},{step.y:F2}) scale={patternScale:F3} visual={visualScale:F1} kick=({kickPitch:F2},{kickYaw:F2})" );
	}

	public static void LogSolveSkip( string reason )
	{
		if ( !Enabled )
			return;

		Log.Info( $"[Aimbox Recoil] solve-skip reason={reason}" );
	}

	public static void RecordIntegrate( float pitchBefore, float pitchAfter, float pitchDelta, float yawDelta, AimboxWeaponRecoilController recoil )
	{
		if ( !Enabled || _integrateLogCooldown < 0.35f )
			return;

		_integrateLogCooldown = 0f;
		Log.Info( $"[Aimbox Recoil] integrate pitch {pitchBefore:F2}->{pitchAfter:F2} delta=({pitchDelta:F2},{yawDelta:F2}) target=({recoil.PitchTarget:F2},{recoil.YawTarget:F2}) display=({recoil.PitchDisplay:F2},{recoil.YawDisplay:F2})" );
	}
}
