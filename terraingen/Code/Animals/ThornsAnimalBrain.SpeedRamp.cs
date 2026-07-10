namespace Terraingen.Animals;

using Terraingen.Multiplayer;

public sealed partial class ThornsAnimalBrain
{
	float _rampedMoveSpeed;

	internal float RampedMoveSpeed => _rampedMoveSpeed;

	internal float GetTargetMoveSpeed()
	{
		var global = ThornsAnimalDebug.ResolveGlobalSpeedMultiplier();

		if ( AiState == ThornsAnimalState.Mounted )
		{
			var mounted = _mountedWishDir.WithZ( 0f ).Length > 0.05f ? ResolveMountedMoveSpeed( ResolveMountedRider() ) : 0f;
			return mounted * global;
		}

		if ( !HasMoveIntent )
			return 0f;

		if ( IsRunningLocomotion )
			return _spawnSpeed * ResolveRunningSpeedMultiplier() * global;

		if ( AiState == ThornsAnimalState.Wander )
			return _spawnSpeed * WanderSpeedFraction * global;

		return 0f;
	}

	internal float GetMoveSpeed() => _rampedMoveSpeed;

	internal float ResolveMaxSprintSpeed()
		=> _spawnSpeed * ResolveRunningSpeedMultiplier() * ThornsAnimalDebug.ResolveGlobalSpeedMultiplier();

	internal void ResetMoveSpeedRamp() => _rampedMoveSpeed = 0f;

	internal void TickMoveSpeedRamp( float delta )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || IsDead )
			return;

		delta = MathF.Max( 0f, delta );
		var target = GetTargetMoveSpeed();

		if ( IsMounted )
		{
			_rampedMoveSpeed = target;
			return;
		}

		if ( delta <= 0f )
			return;

		if ( MathF.Abs( _rampedMoveSpeed - target ) <= 1f )
		{
			_rampedMoveSpeed = target;
			return;
		}

		var maxSprint = ResolveMaxSprintSpeed();
		var accel = _species?.ResolveSprintAcceleration( maxSprint ) ?? (maxSprint / 1f);
		var decel = _species?.ResolveSprintDeceleration( maxSprint ) ?? (maxSprint / 0.9f);

		if ( _rampedMoveSpeed < target )
			_rampedMoveSpeed = MathF.Min( target, _rampedMoveSpeed + accel * delta );
		else
			_rampedMoveSpeed = MathF.Max( target, _rampedMoveSpeed - decel * delta );
	}
}
