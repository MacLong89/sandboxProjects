namespace FinalOutpost;

public struct TakeoverMoveState
{
	public Vector3 Velocity;
	public Vector3 Position;
}

/// <summary>Aimbox <c>AimboxCitizenMovementMotor</c> speeds with outpost terrain + building collision.</summary>
public static class TakeoverMovement
{
	public const float WalkSpeed = 245f;
	public const float SprintSpeed = 510f;
	public const float CrouchSpeed = 145f;
	public const float AdsSpeedMul = 0.72f;
	public const float JumpSpeed = 300f;
	public const float Gravity = 900f;
	public const float EyeStand = 64f;
	public const float EyeCrouch = 42f;

	/// <summary>Tighter probe used when the normal resolve makes no progress (squeeze past corners).</summary>
	const float StuckAgentRadius = 2f;

	public static void Tick(
		ref TakeoverMoveState state,
		Vector3 wishDir,
		bool sprint,
		bool crouch,
		bool ads,
		bool jump,
		float dt )
	{
		wishDir = wishDir.WithZ( 0f );
		if ( wishDir.Length > 0.01f )
			wishDir = wishDir.Normal;

		var grounded = IsGrounded( state.Position );
		if ( grounded && jump && !ads )
		{
			state.Velocity = state.Velocity.WithZ( JumpSpeed );
			grounded = false;
		}

		var speed = crouch ? CrouchSpeed : sprint ? SprintSpeed : WalkSpeed;
		if ( ads ) speed *= AdsSpeedMul;

		var target = wishDir * speed;
		var blend = Math.Clamp( dt * (grounded ? 12f : 4f), 0f, 1f );
		state.Velocity = state.Velocity.LerpTo( new Vector3( target.x, target.y, state.Velocity.z ), blend );

		if ( grounded && state.Velocity.z <= 0f )
			state.Velocity = state.Velocity.WithZ( 0f );
		else
			state.Velocity += Vector3.Down * Gravity * dt;

		IntegrateWithBuildingCollision( ref state, dt, wishDir );
		SnapToTerrain( ref state, grounded );
		ClampInsideCourtyard( ref state );
		EscapeIfEmbedded( ref state, wishDir );
	}

	/// <summary>Same grid blockers recruits use — HQ, defenses, walls, placed buildings.</summary>
	static void IntegrateWithBuildingCollision( ref TakeoverMoveState state, float dt, Vector3 wishDir )
	{
		var from = state.Position.WithZ( 0f );

		// Already inside solid — escape before resolving the step, or ResolveMove returns `from` forever.
		if ( BuildingCollision.BlocksUnit( from ) )
		{
			if ( BuildingCollision.TryEscape( from, out var clear ) )
			{
				from = clear.WithZ( 0f );
				state.Position = from.WithZ( state.Position.z );
			}
		}

		var desired = from + state.Velocity.WithZ( 0f ) * dt;
		var resolvedXy = BuildingCollision.ResolveMove( from, desired );

		// If the normal probe made no progress, retry with a tighter radius so corners don't hard-pin us.
		var movedProbe = (resolvedXy - from).WithZ( 0f );
		var intended = (desired - from).WithZ( 0f );
		if ( intended.LengthSquared > 0.01f && movedProbe.LengthSquared < intended.LengthSquared * 0.04f )
		{
			var tight = BuildingCollision.ResolveMove( from, desired, StuckAgentRadius );
			if ( (tight - from).LengthSquared > movedProbe.LengthSquared )
				resolvedXy = tight;
		}

		state.Position = resolvedXy.WithZ( state.Position.z + state.Velocity.z * dt );

		if ( dt <= 1e-5f )
			return;

		var moved = (state.Position - from.WithZ( state.Position.z )).WithZ( 0f );
		if ( intended.LengthSquared < 0.0001f )
			return;

		// Keep planar velocity — zeroing it made blocked frames feel like a hard freeze.
		if ( moved.LengthSquared < intended.LengthSquared * 0.04f )
		{
			// Prefer sliding along the wish / free axis instead of killing input.
			if ( wishDir.LengthSquared > 0.01f )
			{
				var slide = BuildingCollision.ResolveMove(
					from,
					from + wishDir * MathF.Max( intended.Length, WalkSpeed * dt ),
					StuckAgentRadius );
				var slideMove = (slide - from).WithZ( 0f );
				if ( slideMove.LengthSquared > 0.01f )
				{
					state.Position = slide.WithZ( state.Position.z );
					state.Velocity = new Vector3( slideMove.x / dt, slideMove.y / dt, state.Velocity.z );
					return;
				}
			}

			// Soft damp only — next frame can still try wish / escape.
			state.Velocity = new Vector3( state.Velocity.x * 0.35f, state.Velocity.y * 0.35f, state.Velocity.z );
		}
		else
		{
			state.Velocity = new Vector3( moved.x / dt, moved.y / dt, state.Velocity.z );
		}
	}

	public static void EscapeIfEmbedded( ref TakeoverMoveState state ) =>
		EscapeIfEmbedded( ref state, Vector3.Zero );

	public static void EscapeIfEmbedded( ref TakeoverMoveState state, Vector3 preferDir )
	{
		if ( !BuildingCollision.BlocksUnit( state.Position ) )
			return;

		if ( BuildingCollision.TryEscape( state.Position, out var clear ) )
		{
			state.Position = clear.WithZ( state.Position.z );
			return;
		}

		// Last resort: walk preferDir / outward from origin across several cell steps.
		preferDir = preferDir.WithZ( 0f );
		if ( preferDir.LengthSquared < 0.01f )
		{
			var flat = state.Position.WithZ( 0f );
			preferDir = flat.LengthSquared > 1f ? -flat.Normal : Vector3.Forward;
		}
		else
		{
			preferDir = preferDir.Normal;
		}

		var pos = state.Position.WithZ( 0f );
		var step = GameConstants.CellSize * 0.35f;
		for ( var i = 1; i <= 8; i++ )
		{
			var candidate = pos + preferDir * (step * i);
			if ( !BuildingCollision.BlocksUnit( candidate, StuckAgentRadius ) )
			{
				state.Position = candidate.WithZ( state.Position.z );
				return;
			}

			var perp = new Vector3( -preferDir.y, preferDir.x, 0f );
			foreach ( var side in new[] { perp, -perp } )
			{
				var sideCandidate = pos + (preferDir * 0.5f + side).Normal * (step * i);
				if ( !BuildingCollision.BlocksUnit( sideCandidate, StuckAgentRadius ) )
				{
					state.Position = sideCandidate.WithZ( state.Position.z );
					return;
				}
			}
		}
	}

	public static bool IsGrounded( Vector3 pos )
	{
		var ground = OutpostTerrain.SampleHeight( pos.x, pos.y );
		return pos.z <= ground + 4f;
	}

	public static void SnapToTerrain( ref TakeoverMoveState state, bool wasGrounded )
	{
		var ground = OutpostTerrain.SampleHeight( state.Position.x, state.Position.y );
		if ( state.Velocity.z <= 0f && state.Position.z <= ground + 6f )
		{
			state.Position = state.Position.WithZ( ground );
			if ( wasGrounded || state.Velocity.z < 0f )
				state.Velocity = state.Velocity.WithZ( 0f );
		}
	}

	/// <summary>
	/// Keep the pawn inside the square courtyard (not a circle). Circular clamp was shoving
	/// corner / wall fights back into blocked wall-path cells.
	/// </summary>
	public static void ClampInsideArena( ref TakeoverMoveState state ) =>
		ClampInsideCourtyard( ref state );

	static void ClampInsideCourtyard( ref TakeoverMoveState state )
	{
		// Stay inside the perimeter wall path cells (outer ring).
		var half = GameConstants.ArenaHalf - GameConstants.CellSize * 0.6f;
		var x = Math.Clamp( state.Position.x, -half, half );
		var y = Math.Clamp( state.Position.y, -half, half );
		state.Position = new Vector3( x, y, state.Position.z );
	}
}
