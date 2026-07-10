namespace Terraingen.Animals;

// Measured vs configured locomotion speed (host diagnostics).
public sealed partial class ThornsAnimalBrain
{
	Vector3 _speedSamplePos;
	double _speedSampleAt;
	float _speedSampleMeasured;
	TimeUntil _nextSpeedLogAt;

	internal float MeasuredPlanarSpeed => _speedSampleMeasured;

	internal bool IsActiveLocomotionSample =>
		IsRunningLocomotion
		|| (ShouldTickTamedFollow() && HasMoveIntent);

	internal void UpdateSpeedDiagnostics( float delta )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || IsDead || IsMounted )
			return;

		var pos = GameObject.WorldPosition.WithZ( 0f );
		var now = Time.Now;

		if ( !IsActiveLocomotionSample || !HasMoveIntent )
		{
			_speedSamplePos = pos;
			_speedSampleAt = now;
			_speedSampleMeasured = 0f;
			return;
		}

		if ( _speedSampleAt <= 0d )
		{
			_speedSamplePos = pos;
			_speedSampleAt = now;
			return;
		}

		var elapsed = (float)(now - _speedSampleAt);
		if ( elapsed >= 0.35f )
		{
			_speedSampleMeasured = _speedSamplePos.Distance( pos ) / MathF.Max( elapsed, 0.001f );
			_speedSamplePos = pos;
			_speedSampleAt = now;

			if ( ThornsAnimalDebug.SpeedLog && _nextSpeedLogAt )
			{
				_nextSpeedLogAt = ThornsAnimalDebug.SpeedLogIntervalSeconds;
				ThornsAnimalDebug.LogSpeedSample( this );
			}
		}
	}

	internal string BuildSpeedDebugLine()
	{
		var speciesName = _species?.DisplayName ?? $"species:{SpeciesId}";
		var target = GetTargetMoveSpeed();
		var ramped = _rampedMoveSpeed;
		var measured = _speedSampleMeasured;
		var ratio = ramped > 1f ? measured / ramped : 0f;
		var sprint = ResolveRunningSpeedMultiplier();
		var global = ThornsAnimalDebug.ResolveGlobalSpeedMultiplier();
		var follow = AiState == ThornsAnimalState.Wander && IsTamedFollowSprinting ? " follow" : "";
		var accel = _species?.SprintAccelSeconds ?? 1f;
		var decel = _species?.SprintDecelSeconds ?? 0.9f;
		return
			$"{speciesName} {AiState}{follow} LOD={LodTier} " +
			$"base={_species?.BaseSpeed:F0} roll={_spawnSpeed:F0} sprint×{sprint:F2} global×{global:F2} " +
			$"accel={accel:F2}s decel={decel:F2}s target={target:F0} ramp={ramped:F0} meas={measured:F0} ({ratio:P0} of ramp) rep={ReplicatedMoveSpeed:F0}";
	}
}
