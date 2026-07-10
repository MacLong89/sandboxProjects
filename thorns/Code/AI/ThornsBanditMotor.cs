namespace Sandbox;

/// <summary>
/// Host-only locomotion for NPC bandits — does not run input-driven <see cref="ThornsPawnMovement"/>.
/// </summary>
[Title( "Thorns — Bandit Motor" )]
[Category( "Thorns/AI" )]
[Icon( "directions_walk" )]
public sealed class ThornsBanditMotor : Component
{
	[Property] public float Gravity { get; set; } = 800f;
	[Property] public float GroundFriction { get; set; } = 8f;

	/// <summary>Low step — avoids treating other humanoid capsules as stairs when <c>creature</c> capsules collide.</summary>
	public const float DefaultStepHeight = 8f;

	/// <summary>Match player/wildlife terrain tolerance so NPCs do not lose grounded movement on ordinary hills.</summary>
	[Property] public float GroundAngleDegrees { get; set; } = 86f;

	CharacterController _controller;
	Vector3 _wishWorldHorizontal;

	protected override void OnAwake()
	{
		_controller = Components.GetOrCreate<CharacterController>();
		_controller.UseCollisionRules = true;
		_controller.Height = 72f;
		_controller.Radius = 20f;
		_controller.StepHeight = DefaultStepHeight;
		_controller.GroundAngle = Math.Clamp( GroundAngleDegrees, 0f, 90f );
	}

	protected override void OnStart()
	{
		if ( !GameObject.Tags.Has( ThornsTraceLayers.Creature ) )
			GameObject.Tags.Add( ThornsTraceLayers.Creature );
	}

	/// <summary>Planar wish velocity in world units/sec (zero to brake).</summary>
	public void HostSetWishWorld( Vector3 planarWishVelocityWorld )
	{
		if ( !Networking.IsHost )
			return;

		_wishWorldHorizontal = planarWishVelocityWorld.WithZ( 0f );
	}

	protected override void OnFixedUpdate()
	{
		var authoritative = !Networking.IsActive || Networking.IsHost;
		if ( !authoritative || !_controller.IsValid() )
			return;

		_controller.GroundAngle = Math.Clamp( GroundAngleDegrees, 0f, 90f );

		var hp = Components.Get<ThornsHealth>();
		if ( hp.IsValid() && ( !hp.IsAlive || hp.IsDeadState ) )
			return;

		var wish = _wishWorldHorizontal;
		var brain = Components.Get<ThornsBanditBrain>();
		if ( brain.IsValid() && brain.HostTryGetBanditPeerSeparationWish( out var peerSep ) )
			wish += peerSep;

		if ( _controller.IsOnGround )
		{
			_controller.Accelerate( wish );
			_controller.ApplyFriction( GroundFriction );
		}
		else
		{
			_controller.Accelerate( wish );
			_controller.Accelerate( Vector3.Down * Gravity );
		}

		_controller.Move();
	}
}
