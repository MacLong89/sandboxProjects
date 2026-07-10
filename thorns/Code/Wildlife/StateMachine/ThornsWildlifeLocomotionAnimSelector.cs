namespace Sandbox;

/// <summary>Locomotion clip selection — one clip per tick from the active AI state + planar speed.</summary>
public static class ThornsWildlifeLocomotionAnimSelector
{
	/// <summary>Host: publish synced locomotion ordinal after motor move (single presentation authority).</summary>
	public static void HostSyncLocomotionPresentation(
		GameObject go,
		ThornsWildlifeAnimSync sync,
		ThornsWildlifeAiState aiState,
		float planarSpeed,
		bool isDead,
		float idleCutoff = 14f,
		float runCutoff = 175f )
	{
		if ( !Networking.IsHost || go is null || !go.IsValid() || !sync.IsValid() )
			return;

		var anim = ResolveForAiState( aiState, planarSpeed, isDead, idleCutoff, runCutoff );
		var ordinal = (int)anim;
		if ( sync.LocomotionAnimOrdinal != ordinal )
			sync.HostSetLocomotionAnim( anim );

		sync.HostSetLocomotionPlanarSpeed( planarSpeed );
	}

	/// <summary>Maps the single active AI state + speed to idle / walk / run (melee uses <see cref="ThornsWildlifeAnimSync.MeleeStrikeSerial"/>).</summary>
	public static ThornsAnimalLocomotionAnim ResolveForAiState(
		ThornsWildlifeAiState aiState,
		float planarSpeed,
		bool isDead,
		float idleCutoff,
		float runCutoff )
	{
		if ( isDead )
			return ThornsAnimalLocomotionAnim.Death;

		var moving = !ThornsWildlifeLocomotionAnimUtil.IsStationary( planarSpeed, idleCutoff );

		switch ( aiState )
		{
			case ThornsWildlifeAiState.Idle:
			case ThornsWildlifeAiState.Stay:
			case ThornsWildlifeAiState.Alert:
			case ThornsWildlifeAiState.Mounted:
			case ThornsWildlifeAiState.Dead:
				return ThornsAnimalLocomotionAnim.Idle;

			case ThornsWildlifeAiState.Attack:
				return moving ? ThornsAnimalLocomotionAnim.Walk : ThornsAnimalLocomotionAnim.Idle;

			case ThornsWildlifeAiState.Flee:
			case ThornsWildlifeAiState.Hunt:
			case ThornsWildlifeAiState.Chase:
			case ThornsWildlifeAiState.HuntForOwner:
				return moving ? ThornsAnimalLocomotionAnim.Run : ThornsAnimalLocomotionAnim.Idle;

			case ThornsWildlifeAiState.Stalk:
				return moving ? ThornsAnimalLocomotionAnim.Walk : ThornsAnimalLocomotionAnim.Idle;

			case ThornsWildlifeAiState.Follow:
			case ThornsWildlifeAiState.GuardOwner:
				if ( !moving )
					return ThornsAnimalLocomotionAnim.Idle;

				return ThornsAnimalLocomotionAnim.Run;

			case ThornsWildlifeAiState.FollowLeader:
			case ThornsWildlifeAiState.Wander:
			case ThornsWildlifeAiState.Patrol:
			case ThornsWildlifeAiState.GuardArea:
			case ThornsWildlifeAiState.ReturnToLeash:
			case ThornsWildlifeAiState.Leashed:
				if ( !moving )
					return ThornsAnimalLocomotionAnim.Idle;

				return planarSpeed >= runCutoff ? ThornsAnimalLocomotionAnim.Run : ThornsAnimalLocomotionAnim.Walk;

			default:
				if ( !moving )
					return ThornsAnimalLocomotionAnim.Idle;

				return planarSpeed >= runCutoff ? ThornsAnimalLocomotionAnim.Run : ThornsAnimalLocomotionAnim.Walk;
		}
	}

	public static ThornsAnimalLocomotionAnim Resolve(
		GameObject go,
		float positionDeltaPlanarSpeed,
		bool isDead,
		bool inAttackLock,
		float idleCutoff,
		float runCutoff )
	{
		if ( isDead )
			return ThornsAnimalLocomotionAnim.Death;

		if ( inAttackLock )
			return ThornsAnimalLocomotionAnim.Attack;

		var sync = go.Components.Get<ThornsWildlifeAnimSync>();
		if ( sync.IsValid() && Enum.IsDefined( typeof(ThornsAnimalLocomotionAnim), sync.LocomotionAnimOrdinal ) )
			return (ThornsAnimalLocomotionAnim)sync.LocomotionAnimOrdinal;

		var aiState = sync.IsValid()
			? (ThornsWildlifeAiState)sync.AiStateOrdinal
			: ThornsWildlifeAiState.Wander;
		var speed = ThornsWildlifeLocomotionAnimUtil.SampleEffectivePlanarSpeed( go, positionDeltaPlanarSpeed );
		return ResolveForAiState( aiState, speed, isDead: false, idleCutoff, runCutoff );
	}

	public static string PickSequenceName(
		ThornsAnimalLocomotionAnim anim,
		string idle,
		string walk,
		string run,
		string attack,
		string death )
	{
		return anim switch
		{
			ThornsAnimalLocomotionAnim.Death => death,
			ThornsAnimalLocomotionAnim.Attack => attack,
			ThornsAnimalLocomotionAnim.Run => run,
			ThornsAnimalLocomotionAnim.Walk => walk,
			_ => idle,
		};
	}
}
