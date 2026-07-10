namespace Terraingen.Player;

using Sandbox;

/// <summary>
/// Terraingen human locomotion speeds — aligned with working thorns multiplayer
/// (<c>ThornsTerraingenParity.PlayerWalkSpeed</c> / sprint multiplier).
/// </summary>
public static class ThornsPlayerMovementDefaults
{
	public const float WalkSpeed = 320f;

	/// <summary>Standing sprint vs walk (matches thorns terraingen parity).</summary>
	public const float SprintSpeedMultiplier = 1.75f;

	public static void Apply( PlayerController controller, float walkSpeedMultiplier = 1f, float sprintSpeedMultiplier = SprintSpeedMultiplier )
	{
		if ( controller is null || !controller.IsValid() )
			return;

		var walkMul = Math.Max( 0.01f, walkSpeedMultiplier );
		var sprintMul = Math.Max( 1f, sprintSpeedMultiplier );
		var walk = WalkSpeed * walkMul;
		controller.WalkSpeed = walk;
		controller.RunSpeed = walk * sprintMul;
	}

	/// <summary>Planar speed² threshold for sprint locomotion / sprint-fire gate (aimbox parity).</summary>
	public const float SprintMovingSpeedSquared = 400f;

	public static bool ResolveSprintHeld( GameObject pawn )
	{
		if ( pawn is null || !pawn.IsValid() )
			return false;

		var gameplay = pawn.Components.Get<ThornsPlayerGameplay>();
		if ( !gameplay.IsValid() || !gameplay.CanSprint )
			return false;

		if ( ThornsLocalPlayer.IsLocallyControlledPawn( pawn ) )
			return Input.Down( "Run" ) && !Input.Down( "Duck" );

		return gameplay.HostReportedSprintHeld;
	}

	public static bool IsSprintMoving( GameObject pawn ) =>
		ResolveSprintHeld( pawn ) && TryGetPlanarSpeedSquared( pawn, out var speedSq ) && speedSq > SprintMovingSpeedSquared;

	static bool TryGetPlanarSpeedSquared( GameObject pawn, out float speedSq )
	{
		speedSq = 0f;
		var controller = pawn.Components.Get<PlayerController>( FindMode.EverythingInSelf );
		if ( !controller.IsValid() )
			return false;

		speedSq = controller.Velocity.WithZ( 0f ).LengthSquared;
		return true;
	}

	public static bool TryResolvePresentationLocomotion(
		GameObject pawn,
		out bool crouching,
		out Angles eyeAngles,
		out Vector3 planarVelocity,
		out bool grounded,
		out float runSpeed )
	{
		crouching = false;
		eyeAngles = Angles.Zero;
		planarVelocity = Vector3.Zero;
		grounded = true;
		runSpeed = WalkSpeed;

		if ( pawn is null || !pawn.IsValid() )
			return false;

		var controller = pawn.Components.Get<PlayerController>( FindMode.EverythingInSelf );
		if ( !controller.IsValid() )
			return false;

		crouching = Input.Down( "Duck" );
		var cc = pawn.Components.Get<CharacterController>( FindMode.EverythingInSelf );
		if ( cc.IsValid() && cc.Height < ThornsPlayerFirstPersonRig.DefaultBodyHeight - 8f )
			crouching = true;

		eyeAngles = controller.EyeAngles;
		planarVelocity = controller.Velocity.WithZ( 0f );
		grounded = !cc.IsValid() || cc.IsOnGround;
		var sprintHeld = ResolveSprintHeld( pawn );
		runSpeed = sprintHeld ? controller.RunSpeed : controller.WalkSpeed;
		return true;
	}
}
