using System;

namespace Sandbox;

/// <summary>
/// Source-style automatic step-up: trace-guided detection of small curbs / stair rises / door saddles, then raise the pawn root
/// before <see cref="CharacterController.Move"/> so horizontal motion + ground snapping stay authoritative on the networked root.
/// </summary>
public static class ThornsCharacterStepMove
{
	public enum FailReason
	{
		None = 0,
		NotGrounded,
		AirborneRising,
		PlanarSpeedLow,
		NoPlanarDelta,
		/// <summary>No forward floor rise found within search (capsule may skim the lip with a clear <see cref="CharacterController.TraceDirection"/>).</summary>
		NoForwardFloorRise,
		/// <summary>Deprecated path — kept for saved scenes; use <see cref="NoForwardFloorRise"/> / <see cref="LedgeTooLow"/>.</summary>
		NotBlocked,
		/// <summary>Hit normal behaves like a floor/wedge ahead — let slope code handle it.</summary>
		HitIsFloorLike,
		/// <summary>Velocity not pressing into the obstacle in the horizontal plane.</summary>
		NotMovingIntoObstacle,
		GroundTraceMissed,
		LedgeTraceMissed,
		LedgeTooLow,
		LedgeTooHigh,
		LedgeTopNotWalkable,
		ForwardBlockedAfterStep,
		HeadroomBlocked,
		Internal,
	}

	public readonly struct PlanResult
	{
		public readonly bool Success;
		public readonly float StepHeightWorld;
		public readonly FailReason FailReason;

		public PlanResult( bool success, float stepHeightWorld, FailReason failReason )
		{
			Success = success;
			StepHeightWorld = stepHeightWorld;
			FailReason = failReason;
		}
	}

	/// <summary>
	/// Plans a vertical offset (world Z, feet datum) without mutating transforms. Call from the owning client’s
	/// <c>OnFixedUpdate</c> after velocity integration, before <see cref="CharacterController.Move"/>.
	/// </summary>
	public static PlanResult TryPlanStepUp(
		GameObject pawnRoot,
		CharacterController cc,
		Scene scene,
		Vector3 velocityWorld,
		float deltaTime,
		float maxStepHeight,
		float stepSearchDistance,
		float maxWalkableSlopeDegrees,
		float minPlanarSpeed,
		float upwardVelocityAirThreshold,
		float headroomProbeAboveFeet,
		float headroomTraceDistance,
		bool drawDebugTraces )
	{
		if ( pawnRoot is null || !pawnRoot.IsValid() || cc is null || !cc.IsValid() || scene is null || !scene.IsValid() )
			return new PlanResult( false, 0f, FailReason.Internal );

		DebugOverlaySystem dbgOv = null;
		if ( drawDebugTraces )
			dbgOv = scene.GetSystem<DebugOverlaySystem>();

		if ( !cc.IsOnGround )
			return new PlanResult( false, 0f, FailReason.NotGrounded );

		if ( velocityWorld.z > upwardVelocityAirThreshold )
			return new PlanResult( false, 0f, FailReason.AirborneRising );

		var planarVel = new Vector3( velocityWorld.x, velocityWorld.y, 0f );
		var planarSpeed = planarVel.Length;
		if ( planarSpeed < minPlanarSpeed )
			return new PlanResult( false, 0f, FailReason.PlanarSpeedLow );

		var planarDelta = planarVel * deltaTime;
		var planarDist = planarDelta.Length;
		if ( planarDist < 1e-5f )
			return new PlanResult( false, 0f, FailReason.NoPlanarDelta );

		var dir = planarDelta / planarDist;
		// Tiny per-tick motion (low FPS / heavy friction) still needs a minimum forward probe so door sills register.
		var probeLen = MathF.Max( planarDist, MathF.Max( 2.75f, cc.Radius * 0.22f ) );
		var planarProbe = dir * probeLen;

		var feet = pawnRoot.WorldPosition;
		var halfH = cc.Height * 0.5f;
		var hullCenter = feet + Vector3.Up * halfH;
		var extents = new Vector3( cc.Radius * 0.96f, cc.Radius * 0.96f, halfH * 0.96f );
		var stepLimit = MathF.Min( maxStepHeight, cc.Height * 0.20f );

		// Debug-only: show what the controller thinks about horizontal motion (often clears over a low saddle).
		if ( drawDebugTraces )
		{
			var dbgBlock = cc.TraceDirection( planarProbe );
			dbgOv?.Trace( dbgBlock, Time.Delta * 2.5f, false );
		}

		// Door saddles / thin sills: horizontal traces often read "clear" while ground ahead is higher — compare Z samples first.
		return TryResolveStepViaForwardFloorRise(
			pawnRoot,
			cc,
			scene,
			feet,
			hullCenter,
			extents,
			dir,
			planarProbe,
			probeLen,
			stepSearchDistance,
			stepLimit,
			maxWalkableSlopeDegrees,
			headroomProbeAboveFeet,
			headroomTraceDistance,
			drawDebugTraces,
			dbgOv );
	}

	/// <summary>
	/// Samples ground under the feet vs ground ahead in the move direction (several distances). If a walkable step ≤
	/// <paramref name="maxStepHeight"/> clears forward + headroom when raised, returns success.
	/// </summary>
	static PlanResult TryResolveStepViaForwardFloorRise(
		GameObject pawnRoot,
		CharacterController cc,
		Scene scene,
		Vector3 feet,
		Vector3 hullCenter,
		Vector3 extents,
		Vector3 dir,
		Vector3 planarProbe,
		float probeLen,
		float stepSearchDistance,
		float maxStepHeight,
		float maxWalkableSlopeDegrees,
		float headroomProbeAboveFeet,
		float headroomTraceDistance,
		bool drawDebugTraces,
		DebugOverlaySystem dbgOv )
	{
		var groundTr = ThornsTraceUtility.RunRay(
			scene,
			new Ray( feet + Vector3.Up * 6f, Vector3.Down ),
			56f,
			ThornsTraceProfile.MovementProbe,
			pawnRoot );

		if ( drawDebugTraces && dbgOv is not null )
			dbgOv.Trace( groundTr, Time.Delta * 2.5f, false );

		if ( !groundTr.Hit )
			return new PlanResult( false, 0f, FailReason.GroundTraceMissed );

		var groundZ = groundTr.HitPosition.z;
		var groundIsTerrain = IsTerrainHit( groundTr );

		// Reach far enough forward that the down trace hits interior slab past the sill; also try nearer samples for shallow lips.
		var forwardBase = MathF.Min(
			stepSearchDistance,
			MathF.Max( probeLen + cc.Radius * 0.55f, cc.Radius * 1.9f ) );

		var lateral = new Vector3( -dir.y, dir.x, 0f );
		if ( lateral.LengthSquared > 1e-6f )
			lateral = lateral.Normal * (cc.Radius * 0.42f );
		else
			lateral = Vector3.Zero;

		PlanResult lastFail = new PlanResult( false, 0f, FailReason.LedgeTraceMissed );

		float[] sampleFracs = { 1f, 0.62f, 0.38f };
		for ( var s = 0; s < sampleFracs.Length; s++ )
		{
			var dist = MathF.Max( cc.Radius * 0.95f, forwardBase * sampleFracs[s] );
			dist = MathF.Min( dist, stepSearchDistance );

			// Several XY samples so a forward probe over a workbench/crate does not pick the prop's top as the floor height.
			SceneTraceResult downTr = default;
			var bestZ = float.PositiveInfinity;
			var haveHit = false;
			for ( var o = -1; o <= 1; o++ )
			{
				var lateralOff = o == 0 ? Vector3.Zero : lateral * o;
				var probeTop = feet + new Vector3( dir.x, dir.y, 0f ) * dist + lateralOff + Vector3.Up * (maxStepHeight + 3f );
				var tr = ThornsTraceUtility.RunRay(
					scene,
					new Ray( probeTop, Vector3.Down ),
					maxStepHeight * 2.35f + 10f,
					ThornsTraceProfile.MovementProbe,
					pawnRoot );

				if ( drawDebugTraces && dbgOv is not null )
					dbgOv.Trace( tr, Time.Delta * 2.5f, false );

				if ( !tr.Hit )
					continue;

				if ( tr.HitPosition.z < bestZ )
				{
					bestZ = tr.HitPosition.z;
					downTr = tr;
					haveHit = true;
				}
			}

			if ( !haveHit )
			{
				lastFail = new PlanResult( false, 0f, FailReason.LedgeTraceMissed );
				continue;
			}

			var dh = downTr.HitPosition.z - groundZ;
			if ( groundIsTerrain && IsTerrainHit( downTr ) )
			{
				lastFail = new PlanResult( false, dh, FailReason.HitIsFloorLike );
				continue;
			}
			// Ignore sub-inch terrain noise / mesh lips — stepping on those wedges the capsule and feels like a stuck WASD axis.
			if ( dh <= 2.5f )
			{
				lastFail = new PlanResult( false, dh, FailReason.LedgeTooLow );
				continue;
			}

			if ( dh > maxStepHeight + 0.25f )
			{
				lastFail = new PlanResult( false, dh, FailReason.LedgeTooHigh );
				continue;
			}

			var topSlopeAngle = Vector3.GetAngle( downTr.Normal, Vector3.Up );
			if ( topSlopeAngle > maxWalkableSlopeDegrees + 0.5f )
			{
				lastFail = new PlanResult( false, dh, FailReason.LedgeTopNotWalkable );
				continue;
			}

			var raise = Vector3.Up * dh;
			// Hull clearance — same physics/hitbox intent as <see cref="ThornsTraceProfile.MovementProbe"/> rays.
			var raisedTr = scene.Trace.Box( extents, hullCenter + raise, hullCenter + raise + planarProbe * 1.03f )
				.UsePhysicsWorld( true )
				.UseHitboxes( true )
				.IgnoreGameObjectHierarchy( pawnRoot )
				.Run();

			if ( drawDebugTraces && dbgOv is not null )
				dbgOv.Trace( raisedTr, Time.Delta * 2.5f, false );

			if ( raisedTr.Hit && MoveFraction( hullCenter + raise, hullCenter + raise + planarProbe * 1.03f, raisedTr ) < 0.94f )
			{
				lastFail = new PlanResult( false, dh, FailReason.ForwardBlockedAfterStep );
				continue;
			}

			var headStart = feet + raise + new Vector3( dir.x, dir.y, 0f ) * (cc.Radius * 0.75f)
				+ Vector3.Up * MathF.Max( 8f, headroomProbeAboveFeet );
			var headTr = ThornsTraceUtility.RunRay(
				scene,
				new Ray( headStart, Vector3.Up ),
				headroomTraceDistance,
				ThornsTraceProfile.MovementProbe,
				pawnRoot );

			if ( drawDebugTraces && dbgOv is not null )
				dbgOv.Trace( headTr, Time.Delta * 2.5f, false );

			if ( headTr.Hit )
			{
				lastFail = new PlanResult( false, dh, FailReason.HeadroomBlocked );
				continue;
			}

			return new PlanResult( true, dh, FailReason.None );
		}

		return lastFail;
	}

	static bool IsTerrainHit( in SceneTraceResult tr )
	{
		var go = tr.GameObject;
		if ( !go.IsValid() )
			return false;

		return go.Components.GetInAncestorsOrSelf<ThornsTerrainChunk>( true ).IsValid()
		       || go.Components.GetInAncestorsOrSelf<Terrain>( true ).IsValid();
	}

	/// <summary>Plan + raise <paramref name="pawnRoot"/> when the plan succeeds (single-tick vertical snap).</summary>
	public static bool AttemptStepUp(
		GameObject pawnRoot,
		CharacterController cc,
		Scene scene,
		Vector3 velocityWorld,
		float deltaTime,
		float maxStepHeight,
		float stepSearchDistance,
		float maxWalkableSlopeDegrees,
		float minPlanarSpeed,
		float upwardVelocityAirThreshold,
		float headroomProbeAboveFeet,
		float headroomTraceDistance,
		bool drawDebugTraces,
		out PlanResult plan )
	{
		plan = TryPlanStepUp(
			pawnRoot,
			cc,
			scene,
			velocityWorld,
			deltaTime,
			maxStepHeight,
			stepSearchDistance,
			maxWalkableSlopeDegrees,
			minPlanarSpeed,
			upwardVelocityAirThreshold,
			headroomProbeAboveFeet,
			headroomTraceDistance,
			drawDebugTraces );

		if ( !plan.Success )
			return false;

		pawnRoot.WorldPosition += Vector3.Up * plan.StepHeightWorld;
		return true;
	}

	/// <inheritdoc cref="AttemptStepUp"/>
	public static bool TryStepMove(
		GameObject pawnRoot,
		CharacterController cc,
		Scene scene,
		Vector3 velocityWorld,
		float deltaTime,
		float maxStepHeight,
		float stepSearchDistance,
		float maxWalkableSlopeDegrees,
		float minPlanarSpeed,
		float upwardVelocityAirThreshold,
		float headroomProbeAboveFeet,
		float headroomTraceDistance,
		bool drawDebugTraces,
		out PlanResult plan ) =>
		AttemptStepUp(
			pawnRoot,
			cc,
			scene,
			velocityWorld,
			deltaTime,
			maxStepHeight,
			stepSearchDistance,
			maxWalkableSlopeDegrees,
			minPlanarSpeed,
			upwardVelocityAirThreshold,
			headroomProbeAboveFeet,
			headroomTraceDistance,
			drawDebugTraces,
			out plan );

	static float MoveFraction( Vector3 from, Vector3 to, SceneTraceResult tr )
	{
		var delta = to - from;
		var len = delta.Length;
		if ( len < 1e-5f )
			return 1f;
		if ( !tr.Hit )
			return 1f;
		var traveled = Vector3.Dot( tr.HitPosition - from, delta.Normal );
		return Math.Clamp( traveled / len, 0f, 1f );
	}
}
