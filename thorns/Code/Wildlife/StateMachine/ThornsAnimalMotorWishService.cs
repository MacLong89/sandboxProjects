namespace Sandbox;

/// <summary>Motor wish application — states/brain delegate locomotion output here.</summary>
public static class ThornsAnimalMotorWishService
{
	public static void SyncForState(
		ThornsAnimalBrainContext ctx,
		ThornsWildlifeBrain brain,
		ThornsWildlifeMotor motor,
		Vector3 selfFlat,
		ThornsWildlifeAiState stateId )
	{
		if ( !brain.IsValid() || motor is null || !motor.IsValid() )
			return;

		var id = ctx.Identity ?? brain.Components.Get<ThornsWildlifeIdentity>();
		var def = ctx.Definition ?? id.Definition;
		var spdMul = ctx.SpeedMultiplier > 0.01f ? ctx.SpeedMultiplier : id.GetEffectiveSpeedMultiplier();

		switch ( stateId )
		{
			case ThornsWildlifeAiState.Idle:
			case ThornsWildlifeAiState.Stay:
			case ThornsWildlifeAiState.Alert:
				motor.HostSetWishPlanarVelocity( Vector3.Zero );
				break;

			case ThornsWildlifeAiState.Flee:
				brain.ApplyFleeMotorWish( ctx, motor, def, selfFlat, spdMul );
				break;

			case ThornsWildlifeAiState.Attack:
			case ThornsWildlifeAiState.Hunt:
			case ThornsWildlifeAiState.Chase:
			case ThornsWildlifeAiState.HuntForOwner:
				brain.ApplyFocusChaseMotorWish( ctx, motor, id, def, selfFlat, spdMul );
				break;

			case ThornsWildlifeAiState.Stalk:
				brain.ApplyStalkMotorWish( ctx, motor, id, def, selfFlat, spdMul );
				break;

			case ThornsWildlifeAiState.Follow:
			case ThornsWildlifeAiState.GuardOwner:
				if ( id.HostIsTamed && id.TameFollowOwnerSync && id.TameRiderConnectionId == Guid.Empty
				     && brain.HostTryResolveTameOwnerInternal( id, out var followOwner ) && followOwner.IsValid() )
					brain.ApplyFollowMotorWish( ctx, motor, id, def, selfFlat, followOwner, spdMul );
				else
					motor.HostSetWishPlanarVelocity( Vector3.Zero );

				break;

			case ThornsWildlifeAiState.FollowLeader:
				if ( ctx.LeaderAnimal.IsValid() )
				{
					var follow = ThornsAnimalFollowTarget.ForLeader( ctx.LeaderAnimal, 220f );
					brain.ApplyFollowTargetMotorWish( ctx, motor, id, def, selfFlat, follow, spdMul );
				}
				else
					motor.HostSetWishPlanarVelocity( Vector3.Zero );

				break;

			case ThornsWildlifeAiState.ReturnToLeash:
			case ThornsWildlifeAiState.Leashed:
				brain.ApplyReturnToLeashMotorWish( ctx, motor, id, def, selfFlat, spdMul );
				break;

			case ThornsWildlifeAiState.GuardArea:
			case ThornsWildlifeAiState.Patrol:
			case ThornsWildlifeAiState.Wander:
				brain.ApplyWanderMotorWish( ctx, motor, id, def, selfFlat, spdMul );
				break;

			default:
				motor.HostSetWishPlanarVelocity( Vector3.Zero );
				break;
		}
	}
}
