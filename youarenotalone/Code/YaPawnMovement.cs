namespace Sandbox;

/// <summary>
/// First-pass walk + look for the pawn. Runs only for the connection that owns the pawn root (<see cref="YaPawn.IsLocalConnectionOwner"/>).
/// Uses <see cref="CharacterController"/> so movement replicates with the owned networked root.
/// </summary>
[Title( "Thorns — Pawn Movement" )]
[Category( "Thorns" )]
[Icon( "directions_walk" )]
[Order( 46 )]
public sealed class YaPawnMovement : Component
{
	[Property] public float WalkSpeed { get; set; } = 320f;
	/// <summary>Initial upward punch — lower for a softer pop (arc duration is mostly <see cref="Gravity"/>).</summary>
	[Property] public float JumpStrength { get; set; } = 940f;

	/// <summary>Vertical impulse when role is Alone (hunters use <see cref="JumpStrength"/>).</summary>
	[Property] public float AloneJumpStrength { get; set; } = 1320f;
	/// <summary>Higher = snappier falls; lower = longer hang / slower up–down cycle (softer, less “pogo”).</summary>
	[Property] public float Gravity { get; set; } = 380f;
	/// <summary>Airborne gravity when role is Alone (hunters use <see cref="Gravity"/>).</summary>
	[Property] public float AloneGravity { get; set; } = 380f;
	[Property] public float GroundFriction { get; set; } = 8f;

	/// <summary>While Alone + on ground, skip <see cref="GroundFriction"/> for this long after a Q dash so friction does not erase the punch over the next ticks.</summary>
	[Property] public float AloneDashGroundFrictionBypassSeconds { get; set; } = 0.28f;

	/// <summary>Scales planar wish velocity while airborne (lower = less mid-air strafe, more committed jumps).</summary>
	[Property] public float AirborneMoveMultiplier { get; set; } = 0.32f;

	/// <summary>Extra reach beyond the capsule radius when testing wall cling (Alone + hold jump).</summary>
	[Property] public float AloneWallClingReach { get; set; } = 14f;

	/// <summary>Surfaces with |normal.z| above this are treated as floors/ceilings, not cling walls.</summary>
	[Property] public float AloneWallClingMaxSurfaceZ { get; set; } = 0.55f;

	/// <summary>Standing capsule height (server-driven crouch scales this via <see cref="YaVitalsStub"/>).</summary>
	[Property] public float StandingHeight { get; set; } = 72f;

	CharacterController _controller;
	Angles _look;
	float _lastAppliedHeight = -1f;

	double _aloneDashFrictionBypassEndRealtime;

	/// <summary>
	/// Pending view punch (degrees) merged into <see cref="_look"/> smoothly in <see cref="OnUpdate"/> — avoids one-frame teleport.
	/// </summary>
	float _recoilPitchPending;
	float _recoilYawPending;

	public Angles LookAngles => _look;

	/// <summary>Higher = kick reaches most of its travel in fewer frames (~12–28 feels like a punch; lower = softer).</summary>
	[Property] public float WeaponRecoilSmoothRate { get; set; } = 20f;

	/// <summary>
	/// Local owner only: queues visual kick from authoritative fire (THORNS §3 — does not affect server hit direction).
	/// Positive pitch drives the barrel-up / crosshair-rise direction for this pawn’s pitch convention.
	/// </summary>
	public void OwnerApplyMomentaryWeaponRecoil( float pitchDegreesKickUp, float yawDegreesRight )
	{
		if ( !YaPawn.IsLocalConnectionOwner( this ) )
			return;

		_recoilPitchPending += pitchDegreesKickUp;
		_recoilYawPending += yawDegreesRight;
	}

	void IntegrateSmoothWeaponRecoil()
	{
		if ( MathF.Abs( _recoilPitchPending ) < 1e-5f && MathF.Abs( _recoilYawPending ) < 1e-5f )
			return;

		var k = Math.Max( 0.01f, WeaponRecoilSmoothRate );
		var t = 1f - MathF.Exp( -k * Time.Delta );
		var dPitch = _recoilPitchPending * t;
		var dYaw = _recoilYawPending * t;
		_look.pitch += dPitch;
		_look.yaw += dYaw;
		_recoilPitchPending -= dPitch;
		_recoilYawPending -= dYaw;

		if ( MathF.Abs( _recoilPitchPending ) < 1e-4f )
			_recoilPitchPending = 0f;
		if ( MathF.Abs( _recoilYawPending ) < 1e-4f )
			_recoilYawPending = 0f;
	}

	/// <summary>Host: zero controller velocity after authoritative teleport (respawn) to reduce interpolation mismatch.</summary>
	public void HostApplyRespawnSnap()
	{
		if ( !Networking.IsHost || !_controller.IsValid() )
			return;

		_controller.Velocity = Vector3.Zero;
	}

	protected override void OnAwake()
	{
		_controller = Components.GetOrCreate<CharacterController>();
		_controller.UseCollisionRules = true;
		_controller.Height = StandingHeight;
		// Slimmer capsule so players can pass through tighter doorway geometry.
		_controller.Radius = 20f;
		_controller.StepHeight = 18f;
	}

	protected override void OnUpdate()
	{
		if ( !YaPawn.IsLocalConnectionOwner( this ) )
			return;

		if ( !YaRoundGate.MayMoveAndLook() )
			return;

		var health = Components.Get<YaPlayerHealth>();
		if ( health.IsValid() && health.IsDeadState )
			return;

		if ( YaRoundSpectator.LocalMidRoundJoinFreeze( GameObject ) )
			return;

		var aloneMech = Components.Get<YaAloneMechanics>( FindMode.EnabledInSelf );
		if ( aloneMech.IsValid() )
			aloneMech.TryTeleportFromMovementInput();

		IntegrateSmoothWeaponRecoil();

		var look = Input.AnalogLook;
		look.pitch = -look.pitch;
		_look += look;
		_look.pitch = Math.Clamp( _look.pitch, -89f, 89f );
		_look.roll = 0f;

		GameObject.WorldRotation = Rotation.From( 0f, _look.yaw, 0f );
	}

	protected override void OnFixedUpdate()
	{
		if ( !YaPawn.IsLocalConnectionOwner( this ) )
			return;

		if ( !YaRoundGate.MayMoveAndLook() )
			return;

		var health = Components.Get<YaPlayerHealth>();
		if ( health.IsValid() && health.IsDeadState )
			return;

		if ( YaRoundSpectator.LocalMidRoundJoinFreeze( GameObject ) )
			return;

		var wish = WorldRotation * Input.AnalogMove.WithZ( 0f );
		if ( wish.Length > 1f )
			wish = wish.Normal;

		var vitals = Components.Get<YaVitalsStub>();
		var speedMul = vitals.IsValid() ? vitals.GetMoveSpeedMultiplier() : 1f;
		var roleMove = Components.Get<YaPlayerRoleComponent>( FindMode.EnabledInSelf );
		var mut = YaWeeklyMutatorSystem.Instance;
		if ( mut is { IsValid: true } && roleMove.IsValid() )
		{
			if ( roleMove.Role == YaPlayerRole.Alone )
				speedMul *= mut.AloneMoveSpeedMul;
			else if ( roleMove.Role == YaPlayerRole.NotAlone )
				speedMul *= mut.HunterMoveSpeedMul;
		}

		var flow = YaGameStateSystem.Instance;
		if ( roleMove is { IsValid: true, Role: YaPlayerRole.NotAlone }
		     && flow is { IsValid: true }
		     && flow.ParanoiaDebuffSecondsRemaining > 0.02f )
			speedMul *= 0.5f;
		wish *= WalkSpeed * speedMul;

		var targetH = StandingHeight;
		if ( vitals.IsValid() )
			targetH = StandingHeight * vitals.GetCrouchHeightMultiplier();
		if ( MathF.Abs( targetH - _lastAppliedHeight ) > 0.001f )
		{
			_controller.Height = targetH;
			_lastAppliedHeight = targetH;
		}

		var aloneGroundFrictionBypass = roleMove is { IsValid: true, Role: YaPlayerRole.Alone }
		                                && Time.Now < _aloneDashFrictionBypassEndRealtime;

		if ( _controller.IsOnGround )
		{
			_controller.Accelerate( wish );
			if ( !aloneGroundFrictionBypass )
				_controller.ApplyFriction( GroundFriction );
		}
		else
		{
			var airMul = Math.Clamp( AirborneMoveMultiplier, 0f, 1f );
			var aloneWallCling = roleMove is { IsValid: true, Role: YaPlayerRole.Alone }
			                     && Input.Down( "jump" )
			                     && TryProbeAloneWallCling( wish, out _ );

			_controller.Accelerate( wish * airMul );
			if ( aloneWallCling )
			{
				var vel = _controller.Velocity;
				_controller.Velocity = vel.WithZ( 0f );
			}
			else
			{
				var gravity = roleMove is { IsValid: true, Role: YaPlayerRole.Alone }
					? AloneGravity
					: Gravity;
				_controller.Accelerate( Vector3.Down * gravity );
			}
		}

		if ( Input.Pressed( "jump" ) && _controller.IsOnGround )
		{
			var roleCmp = Components.Get<YaPlayerRoleComponent>( FindMode.EnabledInSelf );
			var jumpImpulse = roleCmp.IsValid() && roleCmp.Role == YaPlayerRole.Alone
				? AloneJumpStrength
				: JumpStrength;
			_controller.Punch( Vector3.Up * jumpImpulse );
			var citizenDriver = Components.Get<YaCitizenBodyDriver>();
			if ( citizenDriver.IsValid() )
				citizenDriver.NotifyJumpAnim();
		}

		// Alone Q dash — after walk/friction this tick; host consumes authoritative pending, clients consume replicated impulse.
		var aloneMechDash = Components.Get<YaAloneMechanics>( FindMode.EnabledInSelf );
		var dashApplied = false;
		if ( aloneMechDash.IsValid() && _controller.IsValid() )
		{
			if ( Networking.IsHost && aloneMechDash.HostConsumePendingAloneDashImpulse( out var hostDash ) )
			{
				_controller.Punch( hostDash );
				dashApplied = true;
			}
			else if ( !Networking.IsHost && aloneMechDash.TryConsumeOwnerClientDashImpulse( out var clientDash ) )
			{
				_controller.Punch( clientDash );
				dashApplied = true;
			}
		}

		if ( dashApplied && roleMove is { IsValid: true, Role: YaPlayerRole.Alone } )
		{
			_aloneDashFrictionBypassEndRealtime = Time.Now
				+ Math.Max( 0.05f, AloneDashGroundFrictionBypassSeconds );
		}

		_controller.Move();
	}

	/// <summary>Alone: true when a vertical surface is within cling range (used while holding jump in air).</summary>
	bool TryProbeAloneWallCling( Vector3 wishPlanar, out Vector3 wallNormal )
	{
		wallNormal = default;
		if ( !_controller.IsValid() || Scene is null || !Scene.IsValid() )
			return false;

		var reach = Math.Max( 8f, _controller.Radius + AloneWallClingReach );
		var feet = GameObject.WorldPosition;
		ReadOnlySpan<float> heights = stackalloc float[] { 24f, 44f, 62f };

		var fwd = GameObject.WorldRotation.Forward.WithZ( 0f );
		fwd = fwd.LengthSquared > 0.0001f ? fwd.Normal : Vector3.Forward;
		var right = Vector3.Cross( Vector3.Up, fwd ).Normal;

		Span<Vector3> dirs = stackalloc Vector3[6];
		var dirCount = 0;
		dirs[dirCount++] = fwd;
		dirs[dirCount++] = -fwd;
		dirs[dirCount++] = right;
		dirs[dirCount++] = -right;

		var wishFlat = wishPlanar.WithZ( 0f );
		if ( wishFlat.LengthSquared > 0.02f )
		{
			var w = wishFlat.Normal;
			dirs[dirCount++] = w;
			dirs[dirCount++] = -w;
		}

		for ( var di = 0; di < dirCount; di++ )
		{
			var dir = dirs[di];
			if ( dir.LengthSquared < 0.0001f )
				continue;

			foreach ( var h in heights )
			{
				if ( !ProbeWallClingRay( feet + Vector3.Up * h, dir, reach, out wallNormal ) )
					continue;

				return true;
			}
		}

		return false;
	}

	bool ProbeWallClingRay( Vector3 start, Vector3 dir, float reach, out Vector3 wallNormal )
	{
		wallNormal = default;
		dir = dir.WithZ( 0f );
		if ( dir.LengthSquared < 0.0001f )
			return false;

		dir = dir.Normal;
		var tr = Scene.Trace.Ray( start, start + dir * reach )
			.UseHitPosition( true )
			.UsePhysicsWorld( true )
			.IgnoreGameObjectHierarchy( GameObject )
			.Run();

		if ( !tr.Hit )
			return false;

		if ( tr.Distance > reach + 2f )
			return false;

		var n = tr.Normal;
		if ( MathF.Abs( n.z ) > AloneWallClingMaxSurfaceZ )
			return false;

		wallNormal = n;
		return true;
	}
}
