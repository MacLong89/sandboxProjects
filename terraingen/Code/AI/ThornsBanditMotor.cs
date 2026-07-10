namespace Terraingen.AI;

using Terraingen.Animals;
using Terraingen.Combat;
using Terraingen.Multiplayer;
using Terraingen.TerrainGen;

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

	// Match player/wildlife terrain tolerance so NPCs do not lose grounded movement on ordinary hills.
	[Property] public float GroundAngleDegrees { get; set; } = 86f;

	CharacterController _controller;
	ThornsBanditHealth _health;
	ThornsBanditBrain _brain;
	Vector3 _wishWorldHorizontal;

	protected override void OnAwake()
	{
		_controller = Components.GetOrCreate<CharacterController>();
		_health = Components.Get<ThornsBanditHealth>();
		_brain = Components.Get<ThornsBanditBrain>();
		_controller.UseCollisionRules = true;
		_controller.Height = 72f;
		_controller.Radius = 20f;
		_controller.StepHeight = DefaultStepHeight;
		_controller.GroundAngle = Math.Clamp( GroundAngleDegrees, 0f, 90f );
	}

	protected override void OnStart()
	{
		if ( !GameObject.Tags.Has( "bandit" ) )
			GameObject.Tags.Add( "bandit" );
	}

	/// <summary>Planar wish velocity in world units/sec (zero to brake).</summary>
	public void HostSetWishWorld( Vector3 planarWishVelocityWorld )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline )
			return;

		_wishWorldHorizontal = planarWishVelocityWorld.WithZ( 0f );
	}

	protected override void OnFixedUpdate()
	{
		var authoritative = !Networking.IsActive || Networking.IsHost;
		if ( !authoritative || !_controller.IsValid() )
			return;

		_controller.GroundAngle = Math.Clamp( GroundAngleDegrees, 0f, 90f );

		if ( _health.IsValid() && ( !_health.IsAlive || _health.IsDeadState ) )
			return;

		var sleepingPatrolDrift = _brain.IsValid()
		                          && _brain.State == ThornsBanditAiState.Patrol
		                          && _brain.LodTier == ThornsNpcLodTier.Sleeping;

		if ( _brain.IsValid() && !ThornsNpcLod.ShouldRunBanditAi( _brain.LodTier ) && !sleepingPatrolDrift )
		{
			if ( _wishWorldHorizontal.LengthSquared > 1f )
				ThornsBanditDebug.LogMotor( _brain, Vector3.Zero, $"motor zeroed — LOD sleeping tier state={_brain.State}" );

			_wishWorldHorizontal = Vector3.Zero;
			if ( _controller.IsOnGround )
				_controller.ApplyFriction( GroundFriction );

			return;
		}

		var wish = _wishWorldHorizontal;
		if ( sleepingPatrolDrift )
			wish *= 0.6f;

		if ( _brain.IsValid() && _brain.HostTryGetBanditPeerSeparationWish( out var peerSep ) )
		{
			wish += peerSep;
			if ( _brain.State == ThornsBanditAiState.Patrol && wish.LengthSquared > 1f && _wishWorldHorizontal.LengthSquared > 1f )
			{
				var minSpeed = _wishWorldHorizontal.Length * 0.72f;
				if ( wish.Length < minSpeed )
					wish = wish.Normal * minSpeed;
			}
		}

		wish = ThornsAiPlanarMovement.FilterWishVelocityAwayFromWater( Scene, GameObject.WorldPosition, wish );
		wish = ThornsAiSolidMovementBlocker.FilterWishVelocityWithSlide(
			Scene,
			GameObject,
			GameObject.WorldPosition,
			wish,
			Time.Delta,
			_controller.Radius,
			_controller.Height );

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

		if ( _brain.IsValid() )
			_brain.HostDepenetrateFromBanditPeers();
	}
}
