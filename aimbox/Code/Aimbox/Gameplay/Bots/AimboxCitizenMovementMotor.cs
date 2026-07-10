namespace Sandbox;

public readonly struct AimboxCitizenMovementInput
{
	public Vector3 WishDirection { get; init; }
	public bool Sprint { get; init; }
	public bool Crouch { get; init; }
	public bool AdsSlowdown { get; init; }
	public bool Jump { get; init; }
	public bool SlideStartRequested { get; init; }
	public float SpeedMultiplier { get; init; }
	public bool UnlimitedSprint { get; init; }
}

public struct AimboxCitizenMovementState
{
	public Vector3 Velocity;
	public Vector3 Position;
	public bool IsSliding;
}

public static class AimboxCitizenMovementMotor
{
	public const float WalkSpeed = 245f;
	public const float SprintSpeed = 510f;
	public const float CrouchSpeed = 145f;
	public const float AdsSpeedMultiplier = 0.72f;
	public const float MinGroundZ = 0f;
	public const float GroundTraceUp = 64f;
	public const float GroundTraceDown = 512f;
	public const float GroundedTraceUp = 0.5f;
	public const float GroundedTraceDistance = 4f;
	public const float GroundSnapTolerance = 1.5f;
	public const float GroundSnapEpsilon = 0.08f;
	public const float MaxStepHeight = 20f;
	public const float MaxWalkableSlopeNormalZ = 0.65f;
	public const float CollisionSkin = 0.75f;
	public const float WallCollisionSkin = 0.35f;
	public const float JumpSpeed = 300f;
	public const float GroundStickVelocity = -8f;
	public const float Gravity = 800f;
	public const float SlideMinStartSpeed = 180f;
	public const float SlideBoostSpeed = 440f;
	public const float SlideMaxSpeed = 580f;
	public const float SlideFriction = 320f;
	public const float SlideEndSpeed = 130f;
	public const float SlideSteering = 0.12f;

	public static bool TryGetGroundHeight( Scene scene, GameObject body, Vector3 position, out float groundZ )
	{
		groundZ = MinGroundZ;
		if ( scene is null || !scene.IsValid() )
			return false;

		var tr = scene.Trace.Ray( position + Vector3.Up * GroundTraceUp, position + Vector3.Down * GroundTraceDown )
			.IgnoreGameObjectHierarchy( body )
			.Run();

		if ( !IsWalkableGroundHit( tr ) )
			return false;

		groundZ = tr.HitPosition.z;
		return true;
	}

	public static Vector3 SnapToGround( Scene scene, GameObject body, Vector3 position )
	{
		return TryGetGroundHeight( scene, body, position, out var groundZ )
			? position.WithZ( groundZ )
			: position.WithZ( MathF.Max( position.z, MinGroundZ ) );
	}

	public static void Tick(
		Scene scene,
		GameObject body,
		ref AimboxCitizenMovementState state,
		in AimboxCitizenMovementInput input,
		Rotation bodyRotation,
		float delta )
	{
		var wish = input.WishDirection.WithZ( 0 );
		if ( wish.Length > 0.01f )
			wish = wish.Normal;

		var grounded = IsGrounded( scene, body, state.Position );
		var jumped = false;

		if ( grounded && input.Jump && CanJump( input ) )
		{
			state.Velocity = state.Velocity.WithZ( JumpSpeed );
			jumped = true;
			grounded = false;
		}

		var horizontalVel = state.Velocity.WithZ( 0 );
		var horizontalSpeed = horizontalVel.Length;

		if ( !state.IsSliding
		     && input.SlideStartRequested
		     && grounded
		     && !input.AdsSlowdown
		     && horizontalSpeed >= SlideMinStartSpeed )
		{
			state.IsSliding = true;
			var slideDir = horizontalSpeed > 1f ? horizontalVel.Normal : bodyRotation.Forward.WithZ( 0 ).Normal;
			var boostedSpeed = MathF.Min( MathF.Max( horizontalSpeed, SlideBoostSpeed ), SlideMaxSpeed );
			state.Velocity = slideDir * boostedSpeed + Vector3.Up * state.Velocity.z;
			horizontalVel = state.Velocity.WithZ( 0 );
			horizontalSpeed = boostedSpeed;
		}

		if ( state.IsSliding && ( !grounded || horizontalSpeed <= SlideEndSpeed ) )
			state.IsSliding = false;

		if ( state.IsSliding && grounded )
		{
			horizontalVel = state.Velocity.WithZ( 0 );
			horizontalSpeed = horizontalVel.Length;
			var slideDir = horizontalSpeed > 1f ? horizontalVel.Normal : bodyRotation.Forward.WithZ( 0 ).Normal;
			var newSpeed = MathF.Max( 0f, horizontalSpeed - SlideFriction * delta );

			if ( wish.Length > 0.01f )
				slideDir = Vector3.Lerp( slideDir, wish, SlideSteering ).Normal;

			state.Velocity = slideDir * newSpeed;
			state.Velocity = state.Velocity.WithZ( GroundStickVelocity );
		}
		else
		{
			var crouching = input.Crouch || state.IsSliding;
			var speed = crouching ? CrouchSpeed : input.Sprint ? SprintSpeed : WalkSpeed;
			speed *= MathF.Max( 0.1f, input.SpeedMultiplier <= 0f ? 1f : input.SpeedMultiplier );
			if ( input.AdsSlowdown )
				speed *= AdsSpeedMultiplier;

			var target = wish * speed;
			var accel = grounded ? AimboxBotTuning.MotorGroundAcceleration : AimboxBotTuning.MotorAirAcceleration;
			var blend = Math.Clamp( delta * accel, 0f, 1f );
			state.Velocity = state.Velocity.LerpTo( new Vector3( target.x, target.y, state.Velocity.z ), blend );

			var planarSpeed = state.Velocity.WithZ( 0 ).Length;
			if ( grounded && state.Velocity.z <= 0f )
				state.Velocity = state.Velocity.WithZ( planarSpeed < 8f ? 0f : GroundStickVelocity );

			if ( !grounded )
				state.Velocity += Vector3.Down * Gravity * delta;
		}

		var previousPosition = state.Position;
		var desiredPosition = previousPosition + state.Velocity * delta;
		state.Position = ResolveMovementCollision( scene, body, previousPosition, desiredPosition );

		var moved = state.Position - previousPosition;
		var wanted = desiredPosition - previousPosition;
		if ( wanted.WithZ( 0 ).Length > 0.01f && moved.WithZ( 0 ).Length < wanted.WithZ( 0 ).Length * 0.85f && delta > 0.0001f )
		{
			var actualVelocity = moved / delta;
			state.Velocity = new Vector3( actualVelocity.x, actualVelocity.y, state.Velocity.z );
		}

		ApplyGroundSnap( scene, body, ref state, grounded, jumped );
	}

	static void ApplyGroundSnap(
		Scene scene,
		GameObject body,
		ref AimboxCitizenMovementState state,
		bool wasGrounded,
		bool jumped )
	{
		if ( jumped || state.Velocity.z > 1f )
			return;

		if ( !TryGetGroundHeight( scene, body, state.Position, out var groundZ ) )
		{
			if ( state.Position.z < MinGroundZ )
			{
				state.Position = state.Position.WithZ( MinGroundZ );
				state.Velocity = state.Velocity.WithZ( 0f );
			}

			return;
		}

		var stepDelta = groundZ - state.Position.z;
		if ( MathF.Abs( stepDelta ) <= GroundSnapEpsilon )
		{
			if ( wasGrounded )
				state.Velocity = state.Velocity.WithZ( 0f );

			return;
		}

		var groundedNow = IsGrounded( scene, body, state.Position );
		if ( groundedNow )
		{
			// Already on walkable ground — only step up onto ledges, never pull down (avoids snap/collision fights).
			if ( stepDelta > GroundSnapEpsilon && stepDelta <= MaxStepHeight )
				state.Position = state.Position.WithZ( groundZ );

			if ( state.Velocity.z <= 0f )
				state.Velocity = state.Velocity.WithZ( 0f );

			return;
		}

		// Landing / falling: snap onto walkable ground within tolerance.
		if ( stepDelta <= MaxStepHeight && stepDelta >= -GroundSnapTolerance )
		{
			state.Position = state.Position.WithZ( groundZ );
			if ( state.Velocity.z <= 0f )
				state.Velocity = state.Velocity.WithZ( 0f );
		}
	}

	public static bool IsGrounded( Scene scene, GameObject body, Vector3 position )
	{
		if ( scene is null || !scene.IsValid() )
			return true;

		var tr = scene.Trace.Ray(
				position + Vector3.Up * GroundedTraceUp,
				position + Vector3.Down * GroundedTraceDistance )
			.IgnoreGameObjectHierarchy( body )
			.Run();
		return IsWalkableGroundHit( tr );
	}

	static bool IsWalkableGroundHit( SceneTraceResult tr ) =>
		tr.Hit && tr.Normal.z >= MaxWalkableSlopeNormalZ;

	static Vector3 ResolveMovementCollision( Scene scene, GameObject body, Vector3 from, Vector3 to )
	{
		if ( scene is null || !scene.IsValid() )
			return to;

		var delta = to - from;
		if ( delta.LengthSquared < 1e-4f )
			return from;

		var capsule = new Capsule( AimboxHitboxes.CitizenCapsuleStart, AimboxHitboxes.CitizenCapsuleEnd, AimboxHitboxes.CitizenRadius );
		var planarDelta = delta.WithZ( 0 );

		// Planar move: lift the sweep slightly so the floor slab doesn't zero out travel every tick.
		if ( planarDelta.LengthSquared > 1e-4f )
		{
			var planarDest = from + planarDelta;
			var traceLift = Vector3.Up * 4f;
			var tr = scene.Trace.Capsule( capsule, from + traceLift, planarDest + traceLift )
				.IgnoreGameObjectHierarchy( body )
				.Run();

			if ( !tr.Hit || tr.Normal.z >= MaxWalkableSlopeNormalZ )
				return new Vector3( planarDest.x, planarDest.y, to.z );

			var wallNormal = tr.Normal.WithZ( 0 );
			if ( wallNormal.LengthSquared > 1e-4f )
				wallNormal = wallNormal.Normal;

			var moved = from + planarDelta * MathF.Max( 0f, tr.Fraction - 0.01f );
			if ( wallNormal.LengthSquared > 1e-4f )
				moved += wallNormal * WallCollisionSkin;

			var remaining = planarDest - moved;
			var slide = remaining - wallNormal * Vector3.Dot( remaining, wallNormal );
			if ( slide.WithZ( 0 ).LengthSquared > 1e-4f )
			{
				var slideTr = scene.Trace.Capsule( capsule, moved + traceLift, moved + traceLift + slide )
					.IgnoreGameObjectHierarchy( body )
					.Run();
				if ( !slideTr.Hit || slideTr.Normal.z >= MaxWalkableSlopeNormalZ )
					moved += slide;
				else
					moved += slide * MathF.Max( 0f, slideTr.Fraction - 0.01f );
			}

			return new Vector3( moved.x, moved.y, to.z );
		}

		var verticalTr = scene.Trace.Capsule( capsule, from, to )
			.IgnoreGameObjectHierarchy( body )
			.Run();

		if ( !verticalTr.Hit )
			return to;

		var safePosition = from + delta * MathF.Max( 0f, verticalTr.Fraction - 0.01f );
		if ( verticalTr.Normal.z < MaxWalkableSlopeNormalZ )
			safePosition += verticalTr.Normal * WallCollisionSkin;

		return safePosition;
	}

	static bool CanJump( in AimboxCitizenMovementInput input ) => !input.AdsSlowdown;

	public static Vector3 SteerAroundObstacles( Scene scene, GameObject body, Vector3 from, Vector3 wishDirection, float probeDistance = 64f ) =>
		SteerAroundObstacles( scene, body, from, wishDirection, probeDistance, Vector3.Zero, 0f );

	public static Vector3 SteerAroundObstacles(
		Scene scene,
		GameObject body,
		Vector3 from,
		Vector3 wishDirection,
		float probeDistance,
		Vector3 steerMemory,
		float delta )
	{
		wishDirection = wishDirection.WithZ( 0 );
		if ( wishDirection.Length <= 0.01f )
			return SmoothSteerMemory( steerMemory, Vector3.Zero, delta );

		wishDirection = wishDirection.Normal;
		var raw = ComputeSteeredDirection( scene, body, from, wishDirection, probeDistance );
		return SmoothSteerMemory( steerMemory, raw, delta );
	}

	static Vector3 ComputeSteeredDirection( Scene scene, GameObject body, Vector3 from, Vector3 wishDirection, float probeDistance )
	{
		if ( !IsBlocked( scene, body, from, wishDirection, probeDistance ) )
			return wishDirection;

		var bestDir = wishDirection;
		var bestScore = ProbeClearance( scene, body, from, wishDirection, probeDistance );

		foreach ( var angle in new[] { 25f, -25f, 40f, -40f, 55f, -55f, 70f, -70f, 90f, -90f, 110f, -110f } )
		{
			var steered = Rotation.FromYaw( angle ) * wishDirection;
			var score = ProbeClearance( scene, body, from, steered, probeDistance );
			if ( score > bestScore )
			{
				bestScore = score;
				bestDir = steered;
			}
		}

		if ( bestScore > probeDistance * 0.2f )
			return bestDir;

		var backward = -wishDirection;
		if ( ProbeClearance( scene, body, from, backward, probeDistance * 0.6f ) > probeDistance * 0.15f )
			return backward;

		return wishDirection * 0.35f;
	}

	static Vector3 SmoothSteerMemory( Vector3 steerMemory, Vector3 raw, float delta )
	{
		if ( raw.Length <= 0.01f )
			return Vector3.Lerp( steerMemory, Vector3.Zero, Math.Clamp( delta * AimboxBotTuning.SteeringSmoothSpeed, 0f, 1f ) );

		if ( steerMemory.Length <= 0.01f )
			return raw;

		var blend = Math.Clamp( delta * AimboxBotTuning.SteeringSmoothSpeed, 0f, 1f );
		return Vector3.Lerp( steerMemory, raw, blend );
	}

	static float ProbeClearance( Scene scene, GameObject body, Vector3 from, Vector3 direction, float distance )
	{
		if ( direction.WithZ( 0 ).Length <= 0.01f )
			return 0f;

		var start = from + Vector3.Up * 32f;
		var end = start + direction.WithZ( 0 ).Normal * distance;
		var tr = scene.Trace.Ray( start, end )
			.IgnoreGameObjectHierarchy( body )
			.Run();
		return tr.Hit ? tr.Distance : distance;
	}

	static bool IsBlocked( Scene scene, GameObject body, Vector3 from, Vector3 direction, float distance )
	{
		var start = from + Vector3.Up * 32f;
		var end = start + direction.WithZ( 0 ).Normal * distance;
		var tr = scene.Trace.Ray( start, end )
			.IgnoreGameObjectHierarchy( body )
			.Run();
		return tr.Hit && tr.Distance < distance * 0.85f;
	}
}
