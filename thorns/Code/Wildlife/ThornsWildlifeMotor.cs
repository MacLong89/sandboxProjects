namespace Sandbox;

/// <summary>Host locomotion + facing smoothing (movement intent separate from mesh).</summary>
[Title( "Thorns — Wildlife motor" )]
[Category( "Thorns/Wildlife" )]
[Icon( "directions_run" )]
[Order( 13 )]
public sealed class ThornsWildlifeMotor : Component
{
	/// <summary>Wildlife at or below this HP fraction cannot move (easier tame UX). Host-only.</summary>
	public const float TamingStunHealthFraction = 0.10f;

	/// <summary>
	/// Baseline <see cref="CharacterController"/> before <see cref="HostApplyCapsuleScaleMul"/> / <see cref="HostMultiplyCapsuleDimensions"/>.
	/// Slightly wider/taller than the old 22×64 defaults so aggressive fauna + ridden tames overlap less deeply and jam <see cref="CharacterController.Move"/> less often.
	/// </summary>
	public const float DefaultCapsuleHeight = 72f;
	public const float DefaultCapsuleRadius = 28f;
	/// <summary>Low step — avoids treating other fauna capsules as stairs when <c>creature</c> capsules collide.</summary>
	public const float DefaultStepHeight = 8f;

	[Property] public float Gravity { get; set; } = 800f;
	[Property] public float GroundFriction { get; set; } = 9f;

	/// <summary>When planar wish exceeds (human sprint × <see cref="ThornsWildlifeVsPlayerBalance.WildlifeChaseVelocitySnapWishVsHumanSprint"/>), snap planar <see cref="CharacterController.Velocity"/> to wish — CC <see cref="CharacterController.Accelerate"/> + friction cannot sustain top-tier chase wishes without snap.</summary>
	[Property] public bool SnapPlanarVelocityToWishWhenChasing { get; set; } = true;

	[Property] public float TurnYawSmoothRate { get; set; } = 14f;

	/// <summary>While a player is riding, yaw follows velocity more slowly so A/D strafe does not snap facing.</summary>
	[Property] public float MountTurnYawSmoothRate { get; set; } = 4.25f;

	/// <summary>Ridden tame: exponential blend per second from RPC steer toward motor wish (higher = snappier).</summary>
	[Property] public float MountPlanarWishSmoothRate { get; set; } = 6.5f;

	/// <summary>Walkable ground slope (degrees) for wildlife CC — stock CC default is ~45°.</summary>
	[Property] public float GroundAngleDegrees { get; set; } = 45f;

	[Property] public float TerrainFollowSmoothSpeed { get; set; } = 260f;
	[Property] public float TerrainFollowHardSnapDistance { get; set; } = 80f;
	[Property, Category( "Thorns/Wildlife/Terrain" )] public bool EnableTerrainPlanarMotor { get; set; } = true;
	[Property, Category( "Thorns/Wildlife/Terrain" )] public float TerrainSurfaceOffset { get; set; } = 0.15f;
	[Property, Category( "Thorns/Wildlife/Terrain" )] public float TerrainPlanarMotorMaxGroundDelta { get; set; } = 64f;
	[Property, Category( "Thorns/Wildlife/Terrain" )] public float TerrainPlanarMotorMaxVerticalStep { get; set; } = 24f;
	[Property, Category( "Thorns/Wildlife/Terrain" )] public float TerrainPlanarMotorNonTerrainProbeUp { get; set; } = 16f;
	[Property, Category( "Thorns/Wildlife/Terrain" )] public float TerrainPlanarMotorNonTerrainProbeDown { get; set; } = 112f;

	/// <summary>Host: upward <see cref="CharacterController.Punch"/> when the mounted owner presses jump (tame CC carries rider).</summary>
	[Property] public float MountRiderJumpStrength { get; set; } = 720f;

	CharacterController _controller;
	Vector3 _wishPlanar;
	float _yawDeg;

	Vector3 _mountPlanarWishSmoothed;
	bool _mountWishSmoothedPrimed;
	double _nextMountJumpAllowedHostTime;

	protected override void OnAwake()
	{
		_controller = Components.GetOrCreate<CharacterController>();
		_controller.UseCollisionRules = true;
		// High chase uses velocity snap; keep accel strong for wander / air so low wishes still converge quickly.
		_controller.Acceleration = Math.Max( _controller.Acceleration, 320f );
		_controller.Height = DefaultCapsuleHeight;
		_controller.Radius = DefaultCapsuleRadius;
		_controller.StepHeight = DefaultStepHeight;
		_controller.GroundAngle = Math.Clamp( GroundAngleDegrees, 0f, 90f );
	}

	/// <summary>Host spawn tuning — e.g. <see cref="ThornsWildlifeSpawn.PantherVisualAndHitboxScaleMul"/> for larger mesh + hitscan analytic capsule.</summary>
	public void HostApplyCapsuleScaleMul( float uniformMul )
	{
		if ( !_controller.IsValid() || uniformMul <= 0.001f )
			return;

		_controller.Height = DefaultCapsuleHeight * uniformMul;
		_controller.Radius = DefaultCapsuleRadius * uniformMul;
	}

	/// <summary>Multiplies current capsule extents (e.g. boss wildlife after species-specific <see cref="HostApplyCapsuleScaleMul"/>).</summary>
	public void HostMultiplyCapsuleDimensions( float uniformMul )
	{
		if ( !_controller.IsValid() || uniformMul <= 0.001f )
			return;

		_controller.Height *= uniformMul;
		_controller.Radius *= uniformMul;
	}

	public void HostSetWishPlanarVelocity( Vector3 planarWorld )
	{
		if ( !Networking.IsHost )
			return;

		_wishPlanar = planarWorld.WithZ( 0f );
	}

	/// <summary>Host-only — clears planar wish and velocity (taming stun, hard stops).</summary>
	public void HostHaltPlanarLocomotion()
	{
		if ( !Networking.IsHost || !_controller.IsValid() )
			return;

		_wishPlanar = Vector3.Zero;
		_mountPlanarWishSmoothed = Vector3.Zero;
		_mountWishSmoothedPrimed = false;
		var vel = _controller.Velocity;
		_controller.Velocity = _controller.IsOnGround ? Vector3.Zero : vel.WithZ( vel.z );
	}

	/// <summary>Host-only — ridden tame jumps; rider pawn CC is disabled while mounted.</summary>
	public bool HostTryApplyRiderJumpImpulse()
	{
		if ( !Networking.IsHost || !_controller.IsValid() )
			return false;

		var now = Time.Now;
		if ( now < _nextMountJumpAllowedHostTime )
			return false;

		var hp = Components.Get<ThornsHealth>();
		if ( hp.IsValid() && ( !hp.IsAlive || hp.IsDeadState ) )
			return false;

		if ( !_controller.IsOnGround )
			return false;

		_nextMountJumpAllowedHostTime = now + 0.22;
		_controller.Punch( Vector3.Up * MountRiderJumpStrength );
		return true;
	}

	/// <summary>Host-only read for diagnostics (<see cref="ThornsWildlifeBrain.LogChaseDiagnostics"/>).</summary>
	public float HostDebugWishPlanarLength => _wishPlanar.Length;

	/// <summary>Host-only read for diagnostics.</summary>
	public float HostDebugPlanarVelocityLength =>
		_controller.IsValid() ? _controller.Velocity.WithZ( 0f ).Length : 0f;

	/// <summary>Host-only snap used by tame summon — clears wish + controller velocity.</summary>
	public void HostTeleportToWorldPosition( Vector3 worldPosition )
	{
		if ( !Networking.IsHost )
			return;

		_wishPlanar = Vector3.Zero;
		_mountPlanarWishSmoothed = Vector3.Zero;
		_mountWishSmoothedPrimed = false;
		GameObject.WorldPosition = worldPosition;
		if ( _controller.IsValid() )
			_controller.Velocity = Vector3.Zero;
	}

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost || !_controller.IsValid() )
			return;

		_controller.GroundAngle = Math.Clamp( GroundAngleDegrees, 0f, 90f );

		var hp = Components.Get<ThornsHealth>();
		if ( hp.IsValid() && ( !hp.IsAlive || hp.IsDeadState ) )
		{
			HostHaltPlanarLocomotion();
			Components.Get<ThornsWildlifeAnimSync>()?.HostSetLocomotionPlanarSpeed( 0f );
			return;
		}

		var idEarly = Components.Get<ThornsWildlifeIdentity>();
		var brain = Components.Get<ThornsWildlifeBrain>();
		if ( IsInTamingStun( hp, idEarly ) )
		{
			brain?.HostApplyTamingStunHold( this );
			Components.Get<ThornsWildlifeAnimSync>()?.HostSetLocomotionPlanarSpeed( 0f );
			if ( !_controller.IsOnGround )
			{
				_controller.Accelerate( Vector3.Down * Gravity );
				_controller.Move();
			}

			return;
		}

		if ( idEarly.IsValid() && idEarly.HostIsTamed )
			ThornsWildlifeMountHost.HostDetachTameWildlifeIfUnderPawnHierarchy( idEarly );

		brain?.HostSyncMotorWishForPhysicsStep( this );

		var peerSep = Vector3.Zero;
		if ( brain.IsValid() && !brain.HostShouldSuppressPeerSeparationForMotor() )
			brain.HostGetPeerSeparationForMotor( out peerSep, out _ );
		if ( peerSep.LengthSquared >= 4f )
			_wishPlanar += peerSep;

		HostClampPlanarWishToSpeciesMax( idEarly );

		var isRidden = idEarly.IsValid() && idEarly.HostIsTamed && idEarly.Definition.AllowPlayerMount
		              && idEarly.TameRiderConnectionId != Guid.Empty;
		if ( isRidden )
		{
			var rawWish = _wishPlanar;
			if ( !_mountWishSmoothedPrimed )
			{
				_mountPlanarWishSmoothed = rawWish;
				_mountWishSmoothedPrimed = true;
			}
			else
			{
				var rate = Math.Max( 0.01f, MountPlanarWishSmoothRate );
				var blend = 1f - MathF.Exp( -rate * Time.Delta );
				_mountPlanarWishSmoothed = Vector3.Lerp( _mountPlanarWishSmoothed, rawWish, blend );
			}

			_wishPlanar = _mountPlanarWishSmoothed;
		}
		else
		{
			_mountWishSmoothedPrimed = false;
		}

		var wish = _wishPlanar;

		HostIntegratePlanarLocomotion( wish );

		var animSync = Components.Get<ThornsWildlifeAnimSync>();
		if ( animSync.IsValid() )
		{
			var planarSpeed = MathF.Max( _controller.Velocity.WithZ( 0f ).Length, _wishPlanar.Length );
			var aiState = brain.IsValid()
				? brain.State
				: (ThornsWildlifeAiState)animSync.AiStateOrdinal;
			ThornsWildlifeLocomotionAnimSelector.HostSyncLocomotionPresentation(
				GameObject,
				animSync,
				aiState,
				planarSpeed,
				isDead: false );
		}

		var vel = _controller.Velocity.WithZ( 0f );
		if ( vel.LengthSquared > 4f )
		{
			var targetYaw = MathF.Atan2( vel.y, vel.x ).RadianToDegree();
			var yawRate = isRidden ? Math.Max( 0.01f, MountTurnYawSmoothRate ) : TurnYawSmoothRate;
			var k = 1f - MathF.Exp( -yawRate * Time.Delta );
			var diff = targetYaw - _yawDeg;
			while ( diff > 180f )
				diff -= 360f;
			while ( diff < -180f )
				diff += 360f;
			_yawDeg += diff * k;
			GameObject.WorldRotation = Rotation.FromYaw( _yawDeg );
		}
	}

	void HostClampPlanarWishToSpeciesMax( ThornsWildlifeIdentity id )
	{
		if ( !id.IsValid() )
			return;

		var max = id.Definition.ChaseSpeed * id.GetEffectiveSpeedMultiplier() * 1.06f;
		var len = _wishPlanar.Length;
		if ( len <= max || len <= 1e-4f )
			return;

		_wishPlanar *= max / len;
	}

	/// <summary>
	/// Ground locomotion assigns planar velocity from the AI wish, then <see cref="CharacterController.Move"/> resolves
	/// fauna-vs-fauna collision (see <c>creature</c> × <c>creature</c> in Collision.config). Do not resync velocity to wish
	/// after Move — that caused runaway tangential sprint. Airborne: gravity only.
	/// </summary>
	void HostIntegratePlanarLocomotion( Vector3 wish )
	{
		if ( !_controller.IsValid() )
			return;

		var planarWish = wish.WithZ( 0f );

		if ( HostIsGroundedForPlanarLocomotion() )
		{
			if ( planarWish.LengthSquared > 1f )
			{
				var vz = _controller.Velocity.z;
				_controller.Velocity = new Vector3( planarWish.x, planarWish.y, vz );
			}
			else
			{
				_controller.ApplyFriction( GroundFriction );
				var vel = _controller.Velocity;
				_controller.Velocity = new Vector3( 0f, 0f, vel.z );
				HostTryApplyTerrainGroundFollowOnly();
			}

			_controller.Move();
			HostTryCorrectFloatingAboveTerrain();
			HostTryResolveWildlifePeerDepenetration();
			return;
		}

		_controller.Accelerate( Vector3.Down * Gravity );
		_controller.Move();
		HostTryResolveWildlifePeerDepenetration();
	}

	void HostTryResolveWildlifePeerDepenetration()
	{
		var brain = Components.Get<ThornsWildlifeBrain>();
		if ( !brain.IsValid() )
			return;

		var step = brain.HostComputeWildlifePeerDepenetrationStep();
		if ( step.LengthSquared < 0.01f )
			return;

		GameObject.WorldPosition += step.WithZ( 0 );
	}

	bool HostIsGroundedForPlanarLocomotion()
	{
		if ( !_controller.IsValid() )
			return false;

		if ( _controller.IsOnGround && _controller.Velocity.z <= 48f )
			return true;

		if ( _controller.Velocity.z > 48f )
			return false;

		var scene = GameObject.Scene;
		if ( scene is null || !scene.IsValid() )
			return _controller.IsOnGround;

		if ( !TrySampleTerrainGround( scene, GameObject.WorldPosition, out var ground ) )
			return _controller.IsOnGround;

		var delta = GameObject.WorldPosition.z - ground.z;
		return delta >= -2.5f && delta <= MathF.Max( 4f, TerrainPlanarMotorMaxGroundDelta );
	}

	void HostTryApplyTerrainGroundFollowOnly()
	{
		if ( !EnableTerrainPlanarMotor || !_controller.IsValid() )
			return;

		var scene = GameObject.Scene;
		if ( scene is null || !scene.IsValid() )
			return;

		if ( !TrySampleTerrainGround( scene, GameObject.WorldPosition, out var currentGround ) )
			return;

		var currentDelta = GameObject.WorldPosition.z - currentGround.z;
		if ( currentDelta < -2.5f || currentDelta > MathF.Max( 4f, TerrainPlanarMotorMaxGroundDelta ) )
			return;

		if ( HostIsStandingOnNonTerrainSurface( scene, currentGround.z ) )
			return;

		HostApplyTerrainGroundFollow( currentGround.z );
	}

	/// <summary>
	/// CC can treat other fauna hulls as ground and drift upward; pull feet back to terrain when airborne in a sane band.
	/// </summary>
	void HostTryCorrectFloatingAboveTerrain()
	{
		if ( !_controller.IsValid() )
			return;

		if ( _controller.IsOnGround && _controller.Velocity.z <= 24f )
			return;

		var scene = GameObject.Scene;
		if ( scene is null || !scene.IsValid() )
			return;

		var pos = GameObject.WorldPosition;
		if ( !ThornsTerrainGeometry.TrySnapWorldPositionToTerrainGround( scene, pos, 96f, 2048f, out var groundPos ) )
			return;

		var floatAbove = pos.z - groundPos.z;
		if ( floatAbove < 3f || floatAbove > 160f )
			return;

		if ( floatAbove >= MathF.Max( 8f, TerrainFollowHardSnapDistance ) )
			GameObject.WorldPosition = pos.WithZ( groundPos.z );
		else
		{
			var maxStep = MathF.Max( 1f, TerrainFollowSmoothSpeed ) * Time.Delta;
			GameObject.WorldPosition = pos.WithZ( MathF.Max( groundPos.z, pos.z - maxStep ) );
		}

		if ( _controller.Velocity.z > 0f )
			_controller.Velocity = _controller.Velocity.WithZ( 0f );
	}

	bool HostApplyTerrainGroundFollow( float terrainZ )
	{
		var targetZ = terrainZ + MathF.Max( 0f, TerrainSurfaceOffset );
		var pos = GameObject.WorldPosition;
		var dz = targetZ - pos.z;
		if ( MathF.Abs( dz ) < 0.03f )
			return true;

		if ( MathF.Abs( dz ) >= MathF.Max( 8f, TerrainFollowHardSnapDistance ) )
			GameObject.WorldPosition = pos.WithZ( targetZ );
		else
		{
			var maxStep = MathF.Max( 1f, TerrainFollowSmoothSpeed ) * Time.Delta;
			GameObject.WorldPosition = pos.WithZ( pos.z + Math.Clamp( dz, -maxStep, maxStep ) );
		}

		if ( _controller.Velocity.z > 0f )
			_controller.Velocity = _controller.Velocity.WithZ( 0f );
		return true;
	}

	static bool TrySampleTerrainGround( Scene scene, Vector3 at, out Vector3 ground )
	{
		if ( ThornsTerraingenTerrainQueries.TrySampleGroundWorld( scene, at.x, at.y, 0f, out ground ) )
			return true;

		return ThornsTerrainGeometry.TrySnapWorldPositionToTerrainGround(
			scene,
			at,
			startLiftZ: 512f,
			segmentLength: 2048f,
			out ground );
	}

	bool HostIsStandingOnNonTerrainSurface( Scene scene, float terrainZ )
	{
		var feet = GameObject.WorldPosition;
		var tr = ThornsTraceUtility.RunRay(
			scene,
			new Ray( feet + Vector3.Up * MathF.Max( 2f, TerrainPlanarMotorNonTerrainProbeUp ), Vector3.Down ),
			MathF.Max( 8f, TerrainPlanarMotorNonTerrainProbeDown ),
			ThornsTraceProfile.MovementProbe,
			GameObject );

		if ( !tr.Hit )
			return false;
		if ( IsTerrainLikeHit( tr ) || IsMovementPassthroughHit( tr ) )
			return false;

		return tr.HitPosition.z > terrainZ + 2f;
	}

	static bool IsTerrainLikeHit( in SceneTraceResult tr )
	{
		var go = tr.GameObject;
		if ( !go.IsValid() )
			return false;

		return go.Components.GetInAncestorsOrSelf<Terrain>( true ).IsValid()
		       || go.Components.GetInAncestorsOrSelf<ThornsTerrainChunk>( true ).IsValid();
	}

	static bool IsMovementPassthroughHit( in SceneTraceResult tr )
	{
		var go = tr.GameObject;
		if ( !go.IsValid() )
			return false;

		return go.Tags.Has( ThornsCollisionTags.ResourceNode )
		       || go.Tags.Has( ThornsCollisionTags.WildlifeHull )
		       || go.Tags.Has( "creature" )
		       || go.Tags.Has( "player" );
	}

	static bool HostShouldTamingStun( ThornsHealth hp, ThornsWildlifeIdentity id ) => IsInTamingStun( hp, id );

	/// <summary>Wildlife at or below <see cref="TamingStunHealthFraction"/> HP — host + client (health sync).</summary>
	public static bool IsInTamingStun( GameObject go )
	{
		if ( go is null || !go.IsValid() )
			return false;

		return IsInTamingStun( go.Components.Get<ThornsHealth>(), go.Components.Get<ThornsWildlifeIdentity>() );
	}

	public static bool IsInTamingStun( ThornsHealth hp, ThornsWildlifeIdentity id )
	{
		if ( !hp.IsValid() || hp.MaxHealth <= 0.001f || !hp.IsAlive || hp.IsDeadState )
			return false;
		if ( !id.IsValid() || id.HostIsTamed )
			return false;
		return hp.CurrentHealth <= hp.MaxHealth * TamingStunHealthFraction;
	}
}
