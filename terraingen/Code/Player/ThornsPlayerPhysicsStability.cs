namespace Terraingen.Player;

using Sandbox;

/// <summary>
/// Tunes stock <see cref="PlayerController"/> rigidbody/collider response so walking into
/// anchored solids (trees, walls, fences) does not oscillate violently.
/// </summary>
public static class ThornsPlayerPhysicsStability
{
	const float ObstacleProbeSkin = 6f;
	const float MinPlanarSpeedSquared = 25f;

	/// <summary>Must clear procedural ramp treads (~11" rise) with margin.</summary>
	public const float PlayerStepHeight = 14f;

	public static void Apply( PlayerController controller )
	{
		if ( controller is null || !controller.IsValid() )
			return;

		var body = controller.Body;
		if ( body.IsValid() )
		{
			body.Locking = new PhysicsLock { Pitch = true, Yaw = true, Roll = true };
			body.AngularDamping = MathF.Max( body.AngularDamping, 8f );
		}

		ZeroElasticity( controller.BodyCollider );
		ZeroElasticity( controller.FeetCollider );

		var cc = controller.Components.Get<CharacterController>( FindMode.EverythingInSelf );
		if ( cc.IsValid() )
		{
			cc.StepHeight = PlayerStepHeight;
			cc.UseCollisionRules = true;
		}
	}

	/// <summary>Strip planar velocity/wish into blocking solids before the physics step.</summary>
	public static void StabilizeAgainstObstacles( GameObject pawn, PlayerController controller )
	{
		if ( pawn is null || !pawn.IsValid() || controller is null || !controller.IsValid() )
			return;

		if ( !controller.UseInputControls || !ThornsLocalPlayer.IsLocallyControlledPawn( pawn ) )
			return;

		var scene = pawn.Scene;
		if ( scene is null || !scene.IsValid() )
			return;

		var body = controller.Body;
		if ( !body.IsValid() )
			return;

		var radius = controller.BodyRadius > 0.5f
			? controller.BodyRadius
			: ThornsPlayerFirstPersonRig.DefaultBodyRadius;

		var planarVel = body.Velocity.WithZ( 0f );
		var wish = controller.WishVelocity.WithZ( 0f );
		var moveDir = wish.LengthSquared > planarVel.LengthSquared * 0.25f
			? wish
			: planarVel;

		if ( moveDir.LengthSquared < MinPlanarSpeedSquared )
			return;

		moveDir = moveDir.Normal;
		if ( !TryProbeBlockingSolid( scene, pawn, moveDir, radius, out var hitNormal ) )
			return;

		var normal = hitNormal.WithZ( 0f );
		if ( normal.LengthSquared < 0.01f )
			return;

		normal = normal.Normal;

		var velInto = Vector3.Dot( planarVel, normal );
		if ( velInto < 0f )
			body.Velocity -= normal * velInto;

		var wishInto = Vector3.Dot( wish, normal );
		if ( wishInto < 0f )
			controller.WishVelocity = wish - normal * wishInto;
	}

	static bool TryProbeBlockingSolid(
		Scene scene,
		GameObject pawn,
		Vector3 moveDir,
		float radius,
		out Vector3 hitNormal )
	{
		hitNormal = default;

		var feet = pawn.WorldPosition;
		var probeHeight = Math.Clamp( ThornsPlayerFirstPersonRig.DefaultBodyHeight * 0.35f, 20f, 36f );
		var start = feet + Vector3.Up * probeHeight;
		var end = start + moveDir * (radius + ObstacleProbeSkin);

		var trace = scene.Trace
			.Sphere( radius * 0.92f, start, end )
			.IgnoreGameObjectHierarchy( pawn )
			.Run();

		if ( !trace.Hit || !IsBlockingSolid( trace.GameObject ) )
			return false;

		// Let CharacterController step over stairs/thresholds; only strip velocity into true walls.
		var stepHeight = ResolveStepHeight( pawn );
		if ( trace.HitPosition.z - feet.z <= stepHeight + 3f )
			return false;

		hitNormal = trace.Normal;
		return true;
	}

	static float ResolveStepHeight( GameObject pawn )
	{
		var cc = pawn.Components.Get<CharacterController>( FindMode.EverythingInSelf );
		return cc.IsValid() && cc.StepHeight > 0.5f ? cc.StepHeight : PlayerStepHeight;
	}

	static bool IsBlockingSolid( GameObject go )
	{
		if ( go is null || !go.IsValid() )
			return false;

		if ( go.Tags.Has( "trigger" ) )
			return false;

		return go.Tags.Has( "solid" )
		       || go.Tags.Has( "world" )
		       || go.Tags.Has( "thorns_structure" )
		       || go.Tags.Has( "tree" );
	}

	static void ZeroElasticity( Collider collider )
	{
		if ( !collider.IsValid() )
			return;

		collider.Elasticity = 0f;
	}
}
