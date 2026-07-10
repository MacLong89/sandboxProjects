namespace Terraingen.Animals;

using Terraingen.AI;

public sealed partial class ThornsAnimalBrain
{
	internal double NextMovementTickRealtime;
	internal double NextBehaviorTickRealtime;

	internal bool HostRequiresActiveSimulation =>
		IsAwaitingTame
		|| ShouldTickTamedFollow()
		|| AiState is ThornsAnimalState.Chase or ThornsAnimalState.Attack or ThornsAnimalState.Flee;

	/// <summary>Ambient skirmish bootstrap — predators hunt immediately even when far from players (sleeping LOD).</summary>
	internal void HostKickstartHunt( GameObject prey )
	{
		if ( IsDead || IsTamed || IsAwaitingTame || !prey.IsValid() )
			return;

		BeginHunt( prey, alertPack: _species?.HuntsInGroups == true );
	}

	internal bool HostShouldDetectWhileSleeping( IReadOnlyList<ThornsAnimalBrain> animals )
	{
		if ( IsDead || IsTamed || IsAwaitingTame || _species is null )
			return false;

		if ( HostRequiresActiveSimulation )
			return true;

		if ( _species.BehaviorType is not (ThornsAnimalBehaviorType.Predator or ThornsAnimalBehaviorType.Prey or ThornsAnimalBehaviorType.Mixed) )
			return false;

		var range = _species.DetectionRange;
		var rangeSq = range * range;
		var origin = GameObject.WorldPosition.WithZ( 0f );

		for ( var i = 0; i < animals.Count; i++ )
		{
			var other = animals[i];
			if ( other == this || !other.IsValid() || other.IsDead || other.IsTamed )
				continue;

			if ( !IsDetectableAnimal( other ) )
				continue;

			if ( origin.DistanceSquared( other.GameObject.WorldPosition.WithZ( 0f ) ) <= rangeSq )
				return true;
		}

		foreach ( var bandit in ThornsBanditPopulation.HostBrainsReadOnly )
		{
			if ( !bandit.IsValid() || bandit.IsDead || !IsDetectableBandit( bandit ) )
				continue;

			if ( origin.DistanceSquared( bandit.GameObject.WorldPosition.WithZ( 0f ) ) <= rangeSq )
				return true;
		}

		return false;
	}

	internal void HostTickSimulation( float delta )
	{
		if ( ThornsAnimalDebug.Enabled )
			DrawDebugOverlay();

		if ( IsDead )
			return;

		if ( _species is null && !ThornsAnimalSpeciesRegistry.TryGet( SpeciesId, out _species ) )
			return;

		if ( IsMounted )
		{
			HostTickMountSimulation();
			UpdateReplicatedMoveSpeed();
			return;
		}

		if ( LodTier == ThornsNpcLodTier.Sleeping && !HostRequiresActiveSimulation )
		{
			UpdateReplicatedMoveSpeed();
			return;
		}

		var now = Time.Now;
		var throttleMovement = !HostRequiresActiveSimulation;
		var moveInterval = LodTier == ThornsNpcLodTier.Reduced ? 0.032 : 0.016;
		var behaviorInterval = 0.016 * ThornsNpcLod.TickIntervalScale( LodTier );

		TickMoveSpeedRamp( delta );

		var runBehaviorNow = now >= NextBehaviorTickRealtime
		                     || (HostRequiresActiveSimulation
		                         && AiState is ThornsAnimalState.Chase or ThornsAnimalState.Attack or ThornsAnimalState.Flee);
		if ( runBehaviorNow )
		{
			if ( now >= NextBehaviorTickRealtime )
				NextBehaviorTickRealtime = now + behaviorInterval;

			TickStateMachine();
		}

		if ( !throttleMovement || now >= NextMovementTickRealtime )
		{
			if ( throttleMovement )
				NextMovementTickRealtime = now + moveInterval;

			EnsureMotor();
			_motor.Tick( delta );
		}

		if ( !runBehaviorNow && now >= NextBehaviorTickRealtime )
		{
			NextBehaviorTickRealtime = now + behaviorInterval;
			TickStateMachine();
		}

		UpdateSpeedDiagnostics( delta );
		UpdateReplicatedMoveSpeed();
	}

	void HostTickMountSimulation() => TickMountedMovement();
}
