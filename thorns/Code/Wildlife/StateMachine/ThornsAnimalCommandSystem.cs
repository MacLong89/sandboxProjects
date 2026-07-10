namespace Sandbox;

/// <summary>Applies owner commands to tamed animals.</summary>
public static class ThornsAnimalCommandSystem
{
	public static bool TryApplyCommand(
		ThornsWildlifeBrain brain,
		ThornsAnimalCommandKind command,
		GameObject ownerRoot = null,
		Vector3? guardCenter = null,
		IReadOnlyList<Vector3> patrolPoints = null )
	{
		if ( !Networking.IsHost || !brain.IsValid() )
			return false;

		var id = brain.Components.Get<ThornsWildlifeIdentity>();
		if ( !id.IsValid() || !id.HostIsTamed )
			return false;

		var ctx = brain.StateMachineContext;
		if ( ctx is null )
			return false;

		switch ( command )
		{
			case ThornsAnimalCommandKind.Follow:
				id.TameFollowOwnerSync = true;
				ctx.BehaviorMode = ThornsAnimalBehaviorMode.Defensive;
				return brain.StateMachine.TryTransition( ctx, ThornsWildlifeAiState.Follow, "cmd:Follow" );

			case ThornsAnimalCommandKind.Stay:
				id.TameFollowOwnerSync = false;
				ctx.HomePosition = brain.GameObject.WorldPosition.WithZ( 0 );
				brain.HostResetPassiveLeashAnchorToCurrentPosition();
				return brain.StateMachine.TryTransition( ctx, ThornsWildlifeAiState.Stay, "cmd:Stay" );

			case ThornsAnimalCommandKind.GuardOwner:
				id.TameFollowOwnerSync = true;
				ctx.BehaviorMode = ThornsAnimalBehaviorMode.Aggressive;
				ctx.OwnerPlayer = ownerRoot;
				return brain.StateMachine.TryTransition( ctx, ThornsWildlifeAiState.GuardOwner, "cmd:GuardOwner" );

			case ThornsAnimalCommandKind.GuardArea:
				id.TameFollowOwnerSync = false;
				ctx.HomePosition = guardCenter ?? brain.GameObject.WorldPosition.WithZ( 0 );
				ctx.BehaviorMode = ThornsAnimalBehaviorMode.Aggressive;
				return brain.StateMachine.TryTransition( ctx, ThornsWildlifeAiState.GuardArea, "cmd:GuardArea" );

			case ThornsAnimalCommandKind.Patrol:
				id.TameFollowOwnerSync = false;
				ctx.PatrolPoints.Clear();
				if ( patrolPoints is not null )
				{
					for ( var i = 0; i < patrolPoints.Count; i++ )
						ctx.PatrolPoints.Add( patrolPoints[i].WithZ( 0 ) );
				}

				if ( ctx.PatrolPoints.Count == 0 )
					ctx.PatrolPoints.Add( ctx.HomePosition );

				return brain.StateMachine.TryTransition( ctx, ThornsWildlifeAiState.Patrol, "cmd:Patrol" );

			case ThornsAnimalCommandKind.Passive:
				ctx.BehaviorMode = ThornsAnimalBehaviorMode.Passive;
				return true;

			case ThornsAnimalCommandKind.Defensive:
				ctx.BehaviorMode = ThornsAnimalBehaviorMode.Defensive;
				return true;

			case ThornsAnimalCommandKind.Aggressive:
				ctx.BehaviorMode = ThornsAnimalBehaviorMode.Aggressive;
				return true;

			case ThornsAnimalCommandKind.Hunt:
				ctx.BehaviorMode = ThornsAnimalBehaviorMode.Aggressive;
				return brain.StateMachine.TryTransition( ctx, ThornsWildlifeAiState.HuntForOwner, "cmd:Hunt" );

			default:
				return false;
		}
	}
}
