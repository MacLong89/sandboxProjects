namespace Sandbox;

/// <summary>
/// Velocity-driven idle / walk / run selection for sequence-based wildlife (<see cref="ThornsWildlifeElkAnimDriver"/>).
/// Uses position delta, <see cref="CharacterController.Velocity"/>, host motor wish, and <see cref="ThornsWildlifeAnimSync.LocomotionPlanarSpeed"/> so stationary units idle even when AI state is Wander.
/// </summary>
public static class ThornsWildlifeLocomotionAnimUtil
{
	public static float SampleEffectivePlanarSpeed( GameObject go, float positionDeltaPlanarSpeed )
	{
		var ccSpeed = 0f;
		var cc = go.Components.Get<CharacterController>();
		if ( cc.IsValid() )
			ccSpeed = cc.Velocity.WithZ( 0 ).Length;

		var wishSpeed = 0f;
		var motor = go.Components.Get<ThornsWildlifeMotor>();
		if ( motor.IsValid() )
			wishSpeed = motor.HostDebugWishPlanarLength;

		var local = MathF.Max( positionDeltaPlanarSpeed, MathF.Max( ccSpeed, wishSpeed ) );

		var sync = go.Components.Get<ThornsWildlifeAnimSync>();
		if ( sync.IsValid() && sync.LocomotionPlanarSpeed > 0.01f )
			return MathF.Max( local, sync.LocomotionPlanarSpeed );

		return local;
	}

	public static bool IsStationary( float effectivePlanarSpeed, float idleCutoff ) =>
		effectivePlanarSpeed < idleCutoff;

	public static string PickWanderClip(
		float effectivePlanarSpeed,
		string idle,
		string walk,
		string run,
		float idleCutoff,
		float runCutoff )
	{
		if ( IsStationary( effectivePlanarSpeed, idleCutoff ) )
			return idle;

		if ( effectivePlanarSpeed >= runCutoff )
			return run;

		return walk;
	}

	public static string PickChaseClip(
		float effectivePlanarSpeed,
		string idle,
		string run,
		float idleCutoff ) =>
		IsStationary( effectivePlanarSpeed, idleCutoff ) ? idle : run;
}
