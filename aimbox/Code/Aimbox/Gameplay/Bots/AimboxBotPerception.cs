namespace Sandbox;

public sealed class AimboxBotPerception
{
	public IAimboxCombatActor Target { get; private set; }
	public IAimboxCombatActor RememberedTarget { get; private set; }
	public Vector3 LastKnownPosition { get; private set; }
	public TimeSince LastSeenTime { get; private set; } = 999f;
	public TimeSince LastLosTime { get; private set; } = 999f;
	public bool HasRecentMemory => LastSeenTime < AimboxBotTuning.MemoryKeepSeconds;
	public bool MemoryExpired => LastSeenTime >= AimboxBotTuning.MemoryForgetSeconds;
	public bool RemembersThreat => RememberedTarget is { IsAlive: true } && !MemoryExpired;

	TimeSince _nextLosCheck;
	TimeSince _nextCloseCheck;

	public void Tick( AimboxBotController bot )
	{
		if ( bot is null || !bot.IsAlive )
		{
			ClearTarget();
			return;
		}

		if ( RememberedTarget is { IsAlive: false } )
			RememberedTarget = null;

		if ( _nextCloseCheck >= AimboxBotTuning.LosCheckCloseInterval )
		{
			_nextCloseCheck = 0;
			UpdateProximityAwareness( bot );
		}

		if ( _nextLosCheck >= AimboxBotTuning.LosCheckInterval )
		{
			_nextLosCheck = 0;
			UpdateLineOfSight( bot );
		}

		if ( Target is not null )
			RefreshLastKnownPosition( Target );

		if ( Target is null && AimboxCombatNoiseBus.TryGetLoudestGunfireHeard( bot, out var noise ) )
		{
			LastKnownPosition = noise.Origin;
			LastSeenTime = 0;
		}
	}

	public void ClearTarget()
	{
		Target = null;
		RememberedTarget = null;
		LastKnownPosition = Vector3.Zero;
		LastSeenTime = 999f;
		LastLosTime = 999f;
	}

	void UpdateProximityAwareness( AimboxBotController bot )
	{
		IAimboxCombatActor closest = null;
		var closestDistance = float.MaxValue;

		foreach ( var candidate in AimboxCombatActorRegistry.GetAll( bot.Scene ) )
		{
			if ( candidate == bot || !candidate.IsAlive || candidate.IsTeammate( bot ) )
				continue;

			var distance = bot.WorldPosition.Distance( candidate.WorldPosition );
			if ( distance > AimboxBotTuning.CloseNoticeDistance || distance >= closestDistance )
				continue;

			if ( !HasLineOfSight( bot, candidate ) )
				continue;

			closestDistance = distance;
			closest = candidate;
		}

		if ( closest is null )
			return;

		AcquireTarget( closest );
	}

	void UpdateLineOfSight( AimboxBotController bot )
	{
		IAimboxCombatActor best = null;
		var bestScore = 0f;

		foreach ( var candidate in AimboxCombatActorRegistry.GetAll( bot.Scene ) )
		{
			if ( candidate == bot || !candidate.IsAlive || candidate.IsTeammate( bot ) )
				continue;

			if ( !TryCanSee( bot, candidate, out var score ) )
				continue;

			if ( score <= bestScore )
				continue;

			bestScore = score;
			best = candidate;
		}

		if ( best is not null )
		{
			AcquireTarget( best );
			return;
		}

		if ( Target is not null )
			LoseLineOfSight();
	}

	void AcquireTarget( IAimboxCombatActor target )
	{
		Target = target;
		RememberedTarget = target;
		RefreshLastKnownPosition( target );
		LastSeenTime = 0;
		LastLosTime = 0;
	}

	void LoseLineOfSight()
	{
		if ( Target is null )
			return;

		RememberedTarget = Target;
		RefreshLastKnownPosition( Target );
		Target = null;
	}

	void RefreshLastKnownPosition( IAimboxCombatActor target )
	{
		LastKnownPosition = target.WorldPosition + Vector3.Up * 40f;
	}

	bool TryCanSee( IAimboxCombatActor observer, IAimboxCombatActor target, out float score )
	{
		score = 0f;
		var toTarget = target.EyePosition - observer.EyePosition;
		var distance = toTarget.Length;
		if ( distance <= 1f || distance > AimboxBotTuning.SightRange )
			return false;

		if ( distance > AimboxBotTuning.CloseNoticeDistance )
		{
			var forward = observer.AimForward;
			var angle = Vector3.GetAngle( forward, toTarget );
			if ( angle > AimboxBotTuning.SightFovDegrees * 0.5f )
				return false;
		}

		if ( !HasLineOfSight( observer, target ) )
			return false;

		score = 1f - distance / AimboxBotTuning.SightRange;
		return true;
	}

	static bool HasLineOfSight( IAimboxCombatActor observer, IAimboxCombatActor target )
	{
		var tracePoints = new[]
		{
			target.EyePosition,
			target.WorldPosition + Vector3.Up * 48f
		};

		foreach ( var point in tracePoints )
		{
			var tr = observer.Scene.Trace.Ray( observer.EyePosition, point )
				.IgnoreGameObjectHierarchy( observer.GameObject )
				.Run();

			if ( !tr.Hit )
				return true;

			var hitActor = AimboxCombatTargetResolve.FindCombatActor( tr.GameObject );
			if ( hitActor == target )
				return true;
		}

		return false;
	}

	public bool TargetHasLineOfSight( AimboxBotController bot )
	{
		if ( Target is null || !Target.IsAlive )
			return false;

		return HasLineOfSight( bot, Target );
	}
}
