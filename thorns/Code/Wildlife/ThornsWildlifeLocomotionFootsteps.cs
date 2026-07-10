namespace Sandbox;

/// <summary>
/// Quadruped / creature walk-run footfalls at the creature root — all peers use replicated world motion + distance falloff
/// (<see cref="ThornsSpatialSfxCategory.FootstepRemote"/>), same tuning idea as <see cref="ThornsCitizenBodyDriver"/> remote steps.
/// </summary>
[Title( "Thorns — Wildlife locomotion audio" )]
[Category( "Thorns/Wildlife" )]
[Icon( "footprint" )]
[Order( 18 )]
public sealed class ThornsWildlifeLocomotionFootsteps : Component
{
	[Property] public string FootstepSoundPath { get; set; } = "sounds/footsteps_grass.sound";

	[Property] public float MinHorizontalSpeed { get; set; } = 18f;

	[Property] public float DistanceUnitsPerStep { get; set; } = 118f;

	[Property] public float MinStepIntervalSeconds { get; set; } = 0.11f;

	[Property] public float FootstepVolume { get; set; } = 0.42f;

	Vector3 _lastWorldPos;
	bool _hasLastWorldPos;
	float _distanceAccum;
	double _lastStepTime;

	protected override void OnStart()
	{
		_lastWorldPos = GameObject.WorldPosition;
		_hasLastWorldPos = true;
	}

	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying || string.IsNullOrWhiteSpace( FootstepSoundPath ) )
			return;

		if ( ShouldSkipFootstepsForLod() )
			return;

		var hp = Components.Get<ThornsHealth>( FindMode.EnabledInSelf );
		if ( hp.IsValid() && ( !hp.IsAlive || hp.IsDeadState ) )
		{
			_distanceAccum = 0f;
			_lastStepTime = 0;
			return;
		}

		var dt = MathF.Max( Time.Delta, 0.0001f );
		var pos = GameObject.WorldPosition;
		if ( !_hasLastWorldPos )
		{
			_lastWorldPos = pos;
			_hasLastWorldPos = true;
			return;
		}

		var vel = (pos - _lastWorldPos) / dt;
		_lastWorldPos = pos;

		var hSpeed = vel.WithZ( 0f ).Length;
		if ( hSpeed < MinHorizontalSpeed || !WildlifeGroundedApprox( pos ) )
		{
			_distanceAccum = 0f;
			return;
		}

		var dist = MathF.Max( 24f, DistanceUnitsPerStep );
		_distanceAccum += hSpeed * Time.Delta;

		while ( _distanceAccum >= dist )
		{
			if ( MinStepIntervalSeconds > 0f && _lastStepTime > 0.0
			     && ( Time.Now - _lastStepTime ) < MinStepIntervalSeconds )
				break;

			_distanceAccum -= dist;
			_lastStepTime = Time.Now;

			var emit = pos + Vector3.Down * 8f;
			var path = FootstepSoundPath.Trim();
			var localOffset = ThornsWorldSpatialSfx.WorldEmitToLocalOffset( GameObject, emit );
			_ = ThornsWorldSpatialSfx.PlayWorldOneShotFollowing(
				GameObject,
				localOffset,
				path,
				ThornsSpatialSfxCategory.FootstepRemote,
				FootstepVolume );
		}

		if ( _distanceAccum >= dist )
			_distanceAccum = 0f;
	}

	bool ShouldSkipFootstepsForLod()
	{
		var director = ThornsWildlifeDirector.Instance;
		if ( director is null || !director.IsValid() )
			return false;

		var distSq = director.HostNearestPlayerDistSq( GameObject.WorldPosition.WithZ( 0 ) );
		return ThornsWildlifeLOD.ComputeTier( distSq ) == ThornsWildlifeLodTier.Dormant;
	}

	bool WildlifeGroundedApprox( Vector3 rootWorld )
	{
		var scene = GameObject.Scene;
		if ( scene is null || !scene.IsValid() )
			return true;

		var tr = ThornsTraceUtility.RunRay(
			scene,
			new Ray( rootWorld + Vector3.Up * 36f, Vector3.Down ),
			140f,
			ThornsTraceProfile.FootstepGround,
			GameObject );
		return tr.Hit;
	}
}
