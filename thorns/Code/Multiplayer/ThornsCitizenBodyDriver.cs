using Sandbox.Citizen;

namespace Sandbox;

/// <summary>
/// Drives <see cref="CitizenAnimationHelper"/> from <see cref="PlayerController"/> velocity,
/// <see cref="ThornsPawnMovement"/> look / yaw, and <see cref="ThornsVitals"/> crouch.
/// Runs on every client so remote players see walk cycles; local owner still hides the Body mesh in <see cref="ThornsPawn"/>.
/// </summary>
[Title( "Thorns — Citizen Body Driver" )]
[Category( "Thorns" )]
[Icon( "directions_run" )]
[Order( 47 )]
public sealed class ThornsCitizenBodyDriver : Component
{
	CitizenAnimationHelper _anim;
	ThornsPawnMovement _movement;
	PlayerController _player;
	ThornsVitals _vitals;
	ThornsWeapon _weapon;
	ThornsHotbarEquipment _hotbar;

	/// <summary>SoundEvent path (<c>.sound</c>) — player-built floors, proc-gen building pieces, and other non-terrain surfaces.</summary>
	[Property] public string FootstepSoundPath { get; set; } = "sounds/footsteps.sound";

	/// <summary>Footsteps on <see cref="ThornsTerrainChunk"/> grass/mesh.</summary>
	[Property] public string FootstepGrassSoundPath { get; set; } = "sounds/footsteps_grass.sound";

	[Property] public float FootstepMinHorizontalSpeed { get; set; } = 38f;

	[Property] public float FootstepDistancePerStep { get; set; } = 125f;

	/// <summary>Minimum seconds between footstep sounds.</summary>
	[Property] public float FootstepMinInterval { get; set; } = 0.14f;

	/// <summary>Fast fade when movement/UI stops so overlapping one-shots do not tail ~0.5s (burst leaves multiple <see cref="SoundHandle"/>s).</summary>
	[Property] public float FootstepStopFadeSeconds { get; set; } = 0.07f;

	[Property] public float FootstepVolume { get; set; } = 0.55f;

	/// <summary>Own footstep spatial mix for the local owner (0 = 2D, 1 = full 3D). Lower values keep FPP steps from imaging behind the camera.</summary>
	[Property] public float FootstepLocalOwnerSpacialBlend { get; set; } = 0.22f;

	[Property] public float FootstepSurfaceOffsetAlongNormal { get; set; } = 2f;

	float _lastWorldYaw;
	Vector3 _lastNetPos;
	bool _hasLastNetPos;

	float _footstepDistanceAccum;

	/// <summary>Game time of last footstep; <c>0</c> = no step yet this run.</summary>
	double _lastFootstepTime;

	SoundHandle _activeFootstep;

	protected override void OnAwake()
	{
		_movement = Components.Get<ThornsPawnMovement>();
		_player = Components.Get<PlayerController>();
		_vitals = Components.Get<ThornsVitals>();
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

		_weapon = Components.Get<ThornsWeapon>();
		_hotbar = Components.Get<ThornsHotbarEquipment>();
		_lastWorldYaw = GameObject.WorldRotation.Angles().yaw;
	}

	public void NotifyJumpAnim()
	{
		if ( _anim.IsValid() )
			_anim.TriggerJump();
	}

	protected override void OnUpdate()
	{
		if ( !_anim.IsValid() )
			return;

		var banditMotor = Components.Get<ThornsBanditMotor>();
		if ( !_movement.IsValid() && banditMotor.IsValid() )
		{
			TickBanditCitizenPresentation();
			return;
		}

		if ( !_movement.IsValid() )
			return;

		var local = ThornsPawn.IsLocalConnectionOwner( this );

		Vector3 vel;
		var grounded = true;
		var dt = MathF.Max( Time.Delta, 0.0001f );

		if ( local && _player.IsValid() )
		{
			vel = _player.Velocity;
			grounded = _player.IsOnGround;
		}
		else
		{
			var pos = GameObject.WorldPosition;
			if ( !_hasLastNetPos )
			{
				_lastNetPos = pos;
				_hasLastNetPos = true;
				vel = _player.IsValid() ? _player.Velocity : Vector3.Zero;
				grounded = !_player.IsValid() || _player.IsOnGround;
			}
			else
			{
				vel = (pos - _lastNetPos) / dt;
				_lastNetPos = pos;
				grounded = !_player.IsValid() || _player.IsOnGround;
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

		var combatId = local && _weapon.IsValid()
			? _weapon.ClientMirrorCombatDefinitionId ?? ""
			: (_hotbar.IsValid() ? _hotbar.ObserversCombatWeaponDefinitionId ?? "" : "");

		_anim.HoldType = MapHoldType( combatId );

		TickFootsteps( vel, grounded, local );
	}

	bool BanditGroundedForFootsteps( Vector3 rootWorld )
	{
		if ( _player.IsValid() && ( !Networking.IsActive || Networking.IsHost ) )
			return _player.IsOnGround;

		var scene = GameObject.Scene;
		if ( scene is null || !scene.IsValid() )
			return true;

		var tr = ThornsTraceUtility.RunRay(
			scene,
			new Ray( rootWorld + Vector3.Up * 28f, Vector3.Down ),
			96f,
			ThornsTraceProfile.FootstepGround,
			GameObject );
		return tr.Hit;
	}

	/// <summary>
	/// <see cref="ThornsBanditMotor"/> NPCs: same Citizen mesh / footsteps as players, fixed rifle hold — no <see cref="ThornsPawnMovement"/>.
	/// </summary>
	void TickBanditCitizenPresentation()
	{
		var dt = MathF.Max( Time.Delta, 0.0001f );
		var pos = GameObject.WorldPosition;
		if ( !_hasLastNetPos )
		{
			_lastNetPos = pos;
			_hasLastNetPos = true;
		}

		// Host simulates <see cref="ThornsBanditMotor"/> in FixedUpdate; proxies do not — replicated CC velocity stays ~0.
		// Drive anim + footsteps from root motion (same idea as remote human pawns).
		var vel = (pos - _lastNetPos) / dt;
		_lastNetPos = pos;

		var grounded = BanditGroundedForFootsteps( pos );

		_anim.WithVelocity( vel );
		_anim.IsGrounded = grounded;
		_anim.DuckLevel = 0f;

		var yawNow = GameObject.WorldRotation.Angles().yaw;
		var yawSpeed = (yawNow - _lastWorldYaw) / Time.Delta;
		_lastWorldYaw = yawNow;
		_anim.MoveRotationSpeed = yawSpeed;

		var viewGo = ThornsCombatAuthority.FindChild( GameObject, "View" );
		Angles aimAngles;
		if ( viewGo.IsValid() )
			aimAngles = viewGo.WorldRotation.Angles();
		else
			aimAngles = new Angles( 0f, yawNow, 0f );

		_anim.AimAngle = Rotation.From( aimAngles );
		_anim.HoldType = CitizenAnimationHelper.HoldTypes.Rifle;

		TickFootsteps( vel, grounded, localOwner: false );
	}

	bool FootstepsPausedByModalUi()
	{
		var shell = Components.Get<ThornsGameShell>();
		if ( shell.IsValid() && shell.Enabled && shell.BlocksGameplayShellOverlay )
			return true;

		var hud = Components.Get<ThornsDebugHudHost>();
		if ( hud.IsValid() && (hud.ShowFullInventory || hud.ShowDebugOverlay || hud.ShowRadioShop) )
			return true;

		return false;
	}

	void StopFootstepTail()
	{
		var h = _activeFootstep;
		_activeFootstep = default;
		if ( !h.IsValid() )
			return;

		var fade = Math.Clamp( FootstepStopFadeSeconds, 0f, 0.35f );
		h.Stop( fade );
	}

	/// <summary>Next one-shot replaces the last — avoids grass + grass stacking when <see cref="FootstepMinInterval"/> is 0 or the burst loop fires twice.</summary>
	void SilenceFootstepBeforeNewOneShot()
	{
		var h = _activeFootstep;
		_activeFootstep = default;
		if ( h is { IsValid: true } )
			h.Stop( 0f );
	}

	void TickFootsteps( Vector3 velocityWorld, bool grounded, bool localOwner )
	{
		if ( !Game.IsPlaying ||
		     (string.IsNullOrWhiteSpace( FootstepSoundPath ) && string.IsNullOrWhiteSpace( FootstepGrassSoundPath )) )
		{
			StopFootstepTail();
			return;
		}

		var hp = Components.Get<ThornsHealth>( FindMode.EnabledInSelf );
		if ( hp.IsValid() && ( !hp.IsAlive || hp.IsDeadState ) )
		{
			StopFootstepTail();
			_footstepDistanceAccum = 0f;
			_lastFootstepTime = 0;
			return;
		}

		// Player pawns only: crouch movement is silent (no footstep one-shots).
		if ( _movement.IsValid() && _vitals.IsValid() && _vitals.ServerCrouching )
		{
			StopFootstepTail();
			_footstepDistanceAccum = 0f;
			_lastFootstepTime = 0;
			return;
		}

		if ( FootstepsPausedByModalUi() )
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

		GameObject surfaceHit = null;
		var emitPos = FallbackFootstepWorldPosition();
		var scene = GameObject.Scene;
		if ( scene is not null && scene.IsValid() )
		{
			var tr = ThornsTraceUtility.RunRay(
				scene,
				new Ray( GameObject.WorldPosition + Vector3.Up * 28f, Vector3.Down ),
				260f,
				ThornsTraceProfile.FootstepGround,
				GameObject );
			if ( tr.Hit && tr.GameObject.IsValid() )
			{
				surfaceHit = tr.GameObject;
				var nudge = MathF.Max( 0.25f, FootstepSurfaceOffsetAlongNormal );
				emitPos = tr.HitPosition + tr.Normal * nudge;
			}
		}

		var clipPath = ResolveFootstepSoundPath( surfaceHit );
		if ( string.IsNullOrWhiteSpace( clipPath ) )
		{
			StopFootstepTail();
			return;
		}

		_footstepDistanceAccum += hSpeed * Time.Delta;
		var bursts = 0;
		while ( _footstepDistanceAccum >= dist && bursts++ < 4 )
		{
			if ( FootstepMinInterval > 0f && _lastFootstepTime > 0.0
			     && ( Time.Now - _lastFootstepTime ) < FootstepMinInterval )
				break;

			_footstepDistanceAccum -= dist;
			_lastFootstepTime = Time.Now;
			SilenceFootstepBeforeNewOneShot();
			var localOffset = ThornsWorldSpatialSfx.WorldEmitToLocalOffset( GameObject, emitPos );
			var emitWorld = ThornsWorldSpatialSfx.LocalOffsetToWorldEmit( GameObject, localOffset );
			var snd = Sound.Play( clipPath.Trim(), emitWorld );
			if ( snd is { IsValid: true } sh )
			{
				ThornsWorldSpatialSfx.BindFollowingEmitter( sh, GameObject, localOffset );
				ApplyFootstepEmitTuning( sh, localOwner, localOffset );
				_activeFootstep = sh;
			}
		}

		if ( _footstepDistanceAccum >= dist )
			_footstepDistanceAccum = 0f;
	}

	Vector3 FallbackFootstepWorldPosition()
	{
		var wp = GameObject.WorldPosition;
		if ( _player.IsValid() )
			wp += Vector3.Down * MathF.Max( 12f, _player.BodyHeight * 0.48f );

		return wp;
	}

	void ApplyFootstepEmitTuning( SoundHandle sh, bool localOwner, Vector3 localOffset )
	{
		if ( localOwner )
		{
			sh.Volume = FootstepVolume;
			sh.SpacialBlend = Math.Clamp( FootstepLocalOwnerSpacialBlend, 0f, 1f );
			return;
		}

		var emitWorld = ThornsWorldSpatialSfx.LocalOffsetToWorldEmit( GameObject, localOffset );
		var distMul = ThornsWorldSpatialSfx.DistanceVolumeMultiplier( emitWorld, ThornsSpatialSfxCategory.FootstepRemote );
		sh.Volume = FootstepVolume * distMul;
		sh.SpacialBlend = 1f;
	}

	/// <summary>
	/// Terrain heightfield → grass clip; placed or procedural building collision → default footstep clip; other surfaces → default.
	/// </summary>
	string ResolveFootstepSoundPath( GameObject surfaceHit )
	{
		var defaultPath = string.IsNullOrWhiteSpace( FootstepSoundPath ) ? FootstepGrassSoundPath : FootstepSoundPath;
		if ( surfaceHit is null || !surfaceHit.IsValid() )
			return string.IsNullOrWhiteSpace( defaultPath ) ? "" : defaultPath.Trim();

		if ( IsFootstepBuildingFloorSurface( surfaceHit ) )
			return (string.IsNullOrWhiteSpace( FootstepSoundPath ) ? defaultPath : FootstepSoundPath).Trim();

		if ( surfaceHit.Components.GetInAncestorsOrSelf<ThornsTerrainChunk>( true ).IsValid()
		     && !string.IsNullOrWhiteSpace( FootstepGrassSoundPath ) )
			return FootstepGrassSoundPath.Trim();

		return string.IsNullOrWhiteSpace( defaultPath ) ? "" : defaultPath.Trim();
	}

	static bool IsFootstepBuildingFloorSurface( GameObject hit )
	{
		if ( !hit.IsValid() )
			return false;

		if ( hit.Components.GetInAncestorsOrSelf<ThornsPlacedStructure>( true ).IsValid() )
			return true;

		for ( var p = hit; p.IsValid(); p = p.Parent )
		{
			foreach ( var tag in p.Tags )
			{
				if ( tag == "thorns_proc_building" )
					return true;
			}
		}

		return false;
	}

	static CitizenAnimationHelper.HoldTypes MapHoldType( string combatId )
	{
		if ( string.IsNullOrEmpty( combatId ) )
			return CitizenAnimationHelper.HoldTypes.None;

		if ( string.Equals( combatId, "shotgun", StringComparison.OrdinalIgnoreCase ) )
			return CitizenAnimationHelper.HoldTypes.Shotgun;

		if ( string.Equals( combatId, "m4", StringComparison.OrdinalIgnoreCase )
		     || string.Equals( combatId, "mp5", StringComparison.OrdinalIgnoreCase )
		     || string.Equals( combatId, "sniper", StringComparison.OrdinalIgnoreCase ) )
			return CitizenAnimationHelper.HoldTypes.Rifle;

		if ( string.Equals( combatId, "m9_bayonet", StringComparison.OrdinalIgnoreCase ) )
			return CitizenAnimationHelper.HoldTypes.Swing;

		return CitizenAnimationHelper.HoldTypes.None;
	}
}
