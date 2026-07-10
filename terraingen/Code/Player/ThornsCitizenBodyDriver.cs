namespace Terraingen.Player;

using Sandbox.Citizen;
using Terraingen.AI;
using Terraingen.Combat;

/// <summary>Drives Citizen animation + footsteps for bandit NPCs and remote player pawns.</summary>
[Title( "Thorns Citizen Body Driver" )]
[Category( "Thorns/Player" )]
public sealed class ThornsCitizenBodyDriver : Component
{
	CitizenAnimationHelper _anim;
	Vector3 _lastNetPos;
	bool _hasLastNetPos;
	float _lastWorldYaw;

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

		_lastWorldYaw = GameObject.WorldRotation.Angles().yaw;

		if ( !Components.Get<PlayerController>( FindMode.EverythingInSelf ).IsValid() )
			_ = Components.Get<ThornsNpcFootstepAudio>() ?? Components.Create<ThornsNpcFootstepAudio>();
	}

	protected override void OnUpdate()
	{
		if ( !_anim.IsValid() )
			return;

		if ( Components.Get<ThornsBanditMotor>().IsValid() )
			TickBanditCitizenPresentation();
		else if ( Components.Get<ThornsPlayerGameplay>().IsValid() || Components.Get<PlayerController>().IsValid() )
			TickPlayerCitizenPresentation();
	}

	void TickBanditCitizenPresentation()
	{
		var dt = MathF.Max( Time.Delta, 0.0001f );
		var pos = GameObject.WorldPosition;
		if ( !_hasLastNetPos )
		{
			_lastNetPos = pos;
			_hasLastNetPos = true;
		}

		var vel = (pos - _lastNetPos) / dt;
		_lastNetPos = pos;

		_anim.WithVelocity( vel );
		_anim.IsGrounded = BanditGroundedForFootsteps( pos );
		_anim.DuckLevel = 0f;

		var yawNow = GameObject.WorldRotation.Angles().yaw;
		var yawSpeed = (yawNow - _lastWorldYaw) / dt;
		_lastWorldYaw = yawNow;
		_anim.MoveRotationSpeed = yawSpeed;

		var aim = ResolveEyeRotation();
		_anim.AimAngle = aim;
		_anim.HoldType = CitizenAnimationHelper.HoldTypes.Rifle;
	}

	void TickPlayerCitizenPresentation()
	{
		var dt = MathF.Max( Time.Delta, 0.0001f );
		var controller = Components.Get<PlayerController>( FindMode.EverythingInSelf );
		var vel = controller.IsValid() ? controller.Velocity : Vector3.Zero;
		if ( vel.LengthSquared < 1f )
		{
			var pos = GameObject.WorldPosition;
			if ( !_hasLastNetPos )
			{
				_lastNetPos = pos;
				_hasLastNetPos = true;
			}

			vel = (pos - _lastNetPos) / dt;
			_lastNetPos = pos;
		}
		else if ( _hasLastNetPos )
		{
			_lastNetPos = GameObject.WorldPosition;
		}

		_anim.WithVelocity( vel );
		_anim.IsGrounded = BanditGroundedForFootsteps( GameObject.WorldPosition );
		_anim.DuckLevel = ResolvePlayerDuckLevel( controller );

		var yawNow = GameObject.WorldRotation.Angles().yaw;
		var yawDelta = (yawNow - _lastWorldYaw).NormalizeDegrees();
		_anim.MoveRotationSpeed = MathX.Lerp( _anim.MoveRotationSpeed, yawDelta / dt, Math.Clamp( dt * 8f, 0f, 1f ) );
		_lastWorldYaw = yawNow;
		_anim.AimAngle = ResolveEyeRotation();
		_anim.HoldType = CitizenAnimationHelper.HoldTypes.Rifle;
	}

	Rotation ResolveEyeRotation()
	{
		if ( ThornsLocalPlayer.TryGetAuthoritativeEye( GameObject, out _, out var eyeRot ) )
			return eyeRot;

		var view = ThornsBanditUtil.FindChild( GameObject, "View" );
		if ( view.IsValid() )
			return view.WorldRotation;

		return GameObject.WorldRotation;
	}

	static float ResolvePlayerDuckLevel( PlayerController controller )
	{
		if ( !controller.IsValid() )
			return 0f;

		var cc = controller.Components.Get<CharacterController>( FindMode.EverythingInSelf );
		if ( cc.IsValid() && cc.Height < ThornsPlayerFirstPersonRig.DefaultBodyHeight - 8f )
			return 1f;

		return 0f;
	}

	bool BanditGroundedForFootsteps( Vector3 rootWorld )
	{
		var scene = GameObject.Scene;
		if ( scene is null || !scene.IsValid() )
			return true;

		var tr = ThornsBanditTraceUtil.RunRay(
			scene,
			new Ray( rootWorld + Vector3.Up * 28f, Vector3.Down ),
			96f,
			ThornsBanditTraceUtil.GroundProfile,
			GameObject );
		return tr.Hit;
	}
}
