using Sandbox.Citizen;

namespace Sandbox;

/// <summary>
/// Drives <see cref="CitizenAnimationHelper"/> from <see cref="CharacterController"/> velocity,
/// <see cref="YaPawnMovement"/> look / yaw, and <see cref="YaVitalsStub"/> crouch (replicated).
/// Runs on every client so remote players see walk cycles; local owner still hides the Body mesh in <see cref="YaPawn"/>.
/// </summary>
[Title( "Thorns — Citizen Body Driver" )]
[Category( "Thorns" )]
[Icon( "directions_run" )]
[Order( 47 )]
public sealed class YaCitizenBodyDriver : Component
{
	CitizenAnimationHelper _anim;
	YaPawnMovement _movement;
	CharacterController _controller;
	YaVitalsStub _vitals;
	YaWeapon _weapon;
	YaHotbarEquipment _hotbar;

	/// <summary>SoundEvent path ( <c>.sound</c> ); raw .mp3 will not resolve — use a SoundEvent that references your imported clip.</summary>
	[Property] public string FootstepSoundPath { get; set; } = "sounds/footsteps.sound";

	[Property] public float FootstepMinHorizontalSpeed { get; set; } = 38f;

	[Property] public float FootstepDistancePerStep { get; set; } = 125f;

	/// <summary>Minimum seconds between footstep sounds; adds space so rapid distance triggers do not stack into a gallop. <c>0</c> = no limit.</summary>
	[Property] public float FootstepMinInterval { get; set; } = 0.14f;

	float _lastWorldYaw;
	Vector3 _lastNetPos;
	bool _hasLastNetPos;
	float _footstepDistanceAccum;
	/// <summary>Game time of last footstep; <c>0</c> means no step yet this “run” (first step is not delayed).</summary>
	double _lastFootstepTime;

	/// <summary>Latest footstep instance — stopped when walking ends so clip tail does not continue after you stop.</summary>
	SoundHandle _lastFootstepSound;

	protected override void OnAwake()
	{
		_movement = Components.Get<YaPawnMovement>();
		_controller = Components.Get<CharacterController>();
		_vitals = Components.Get<YaVitalsStub>();
	}

	protected override void OnStart()
	{
		foreach ( var c in GameObject.Components.GetAll<CitizenAnimationHelper>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( c.IsValid() )
			{
				_anim = c;
				break;
			}
		}

		_weapon = Components.Get<YaWeapon>();
		_hotbar = Components.Get<YaHotbarEquipment>();
		_lastWorldYaw = GameObject.WorldRotation.Angles().yaw;
	}

	public void NotifyJumpAnim()
	{
		if ( _anim.IsValid() )
			_anim.TriggerJump();
	}

	protected override void OnUpdate()
	{
		if ( !_anim.IsValid() || !_movement.IsValid() )
			return;

		var local = YaPawn.IsLocalConnectionOwner( this );

		Vector3 vel;
		var grounded = true;
		var dt = MathF.Max( Time.Delta, 0.0001f );

		if ( local && _controller.IsValid() )
		{
			vel = _controller.Velocity;
			grounded = _controller.IsOnGround;
		}
		else
		{
			var pos = GameObject.WorldPosition;
			if ( !_hasLastNetPos )
			{
				_lastNetPos = pos;
				_hasLastNetPos = true;
				vel = _controller.IsValid() ? _controller.Velocity : Vector3.Zero;
				grounded = !_controller.IsValid() || _controller.IsOnGround;
			}
			else
			{
				vel = (pos - _lastNetPos) / dt;
				_lastNetPos = pos;
				grounded = !_controller.IsValid() || _controller.IsOnGround;
			}
		}

		_anim.WithVelocity( vel );
		_anim.IsGrounded = grounded;

		if ( _vitals.IsValid() )
			_anim.DuckLevel = _vitals.ServerCrouching ? 1f : 0f;

		var yawNow = GameObject.WorldRotation.Angles().yaw;
		var yawSpeed = (yawNow - _lastWorldYaw) / Time.Delta;
		_lastWorldYaw = yawNow;
		_anim.MoveRotationSpeed = yawSpeed;

		Angles aimAngles;
		if ( local )
			aimAngles = _movement.LookAngles;
		else
			aimAngles = new Angles( 0f, yawNow, 0f );

		_anim.AimAngle = Rotation.From( aimAngles );

		var combatId = "";
		var aloneMech = Components.Get<YaAloneMechanics>( FindMode.EnabledInSelf );
		if ( !local
		     && aloneMech.IsValid()
		     && aloneMech.MimicPresentationActive
		     && !string.IsNullOrWhiteSpace( aloneMech.MimicMirrorCombatId ) )
		{
			// Other clients: match TP gun pose. Local Alone keeps real (melee) hold — own body is hidden in FP.
			combatId = aloneMech.MimicMirrorCombatId;
		}
		else if ( local && _weapon.IsValid() )
			combatId = _weapon.ClientMirrorCombatDefinitionId ?? "";
		else if ( _hotbar.IsValid() )
			combatId = _hotbar.ObserversCombatWeaponDefinitionId ?? "";

		_anim.HoldType = MapHoldType( combatId );

		TickFootsteps( vel, grounded );
	}

	void StopFootstepTail()
	{
		var h = _lastFootstepSound;
		_lastFootstepSound = null;
		if ( h is { IsValid: true, IsPlaying: true } )
			h.Stop( 0f );
	}

	void TickFootsteps( Vector3 velocityWorld, bool grounded )
	{
		if ( !Game.IsPlaying || string.IsNullOrWhiteSpace( FootstepSoundPath ) )
		{
			StopFootstepTail();
			return;
		}

		var hp = Components.Get<YaPlayerHealth>( FindMode.EnabledInSelf );
		if ( hp.IsValid() && ( !hp.IsAlive || hp.IsDeadState ) )
		{
			StopFootstepTail();
			_footstepDistanceAccum = 0f;
			_lastFootstepTime = 0;
			return;
		}

		if ( !YaRoundGate.MayMoveAndLook() )
		{
			StopFootstepTail();
			_footstepDistanceAccum = 0f;
			_lastFootstepTime = 0;
			return;
		}

		var hSpeed = velocityWorld.WithZ( 0f ).Length;
		if ( !grounded || hSpeed < FootstepMinHorizontalSpeed )
		{
			StopFootstepTail();
			_footstepDistanceAccum = 0f;
			_lastFootstepTime = 0;
			return;
		}

		var dist = FootstepDistancePerStep;
		if ( dist < 8f )
			dist = 8f;

		_footstepDistanceAccum += hSpeed * Time.Delta;
		var bursts = 0;
		while ( _footstepDistanceAccum >= dist && bursts++ < 4 )
		{
			if ( FootstepMinInterval > 0f && _lastFootstepTime > 0.0
			     && ( Time.Now - _lastFootstepTime ) < FootstepMinInterval )
				break;

			_footstepDistanceAccum -= dist;
			_lastFootstepTime = Time.Now;
			var pos = GameObject.WorldPosition + Vector3.Down * 8f;
			var snd = Sound.Play( FootstepSoundPath.Trim(), pos );
			if ( snd is { IsValid: true } sh )
			{
				var roleCmp = Components.Get<YaPlayerRoleComponent>( FindMode.EnabledInSelf );
				var role = roleCmp.IsValid() ? roleCmp.Role : YaPlayerRole.Unassigned;
				sh.Volume = role == YaPlayerRole.Alone ? 0.08f : 0.55f;
				_lastFootstepSound = sh;
			}
		}
		if ( _footstepDistanceAccum >= dist )
			_footstepDistanceAccum = 0f;
	}

	static CitizenAnimationHelper.HoldTypes MapHoldType( string combatId )
	{
		if ( string.IsNullOrEmpty( combatId ) )
			return CitizenAnimationHelper.HoldTypes.None;

		if ( string.Equals( combatId, "shotgun", StringComparison.OrdinalIgnoreCase ) )
			return CitizenAnimationHelper.HoldTypes.Shotgun;

		if ( string.Equals( combatId, "m4", StringComparison.OrdinalIgnoreCase )
		     || string.Equals( combatId, "mp5", StringComparison.OrdinalIgnoreCase )
		     || string.Equals( combatId, "rifle", StringComparison.OrdinalIgnoreCase )
		     || string.Equals( combatId, "sniper", StringComparison.OrdinalIgnoreCase ) )
			return CitizenAnimationHelper.HoldTypes.Rifle;

		if ( string.Equals( combatId, "m9_bayonet", StringComparison.OrdinalIgnoreCase ) )
			return CitizenAnimationHelper.HoldTypes.Swing;

		return CitizenAnimationHelper.HoldTypes.None;
	}
}
