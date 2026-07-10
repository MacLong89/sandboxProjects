namespace Terraingen.Player;

using Sandbox;
using Sandbox.Movement;
using Terraingen.Combat;
using Terraingen.TerrainGen;
using Terraingen.UI.Core;

// Swim / wade on the flat terrain sea sheet (no water trigger volume required).
[Icon( "pool" )]
public sealed class ThornsTerrainWaterMoveMode : MoveMode
{
	[Property] public int SwimModePriority { get; set; } = 12;
	[Property] public int WadeModePriority { get; set; } = 2;
	[Property] public float WadeSpeedMultiplier { get; set; } = 0.55f;
	[Property] public float FloatTargetFeetSubmerge { get; set; } = 24f;
	[Property] public float BuoyancyStrength { get; set; } = 420f;
	[Property] public float VerticalDrag { get; set; } = 6f;
	[Property] public float SwimLinearDamping { get; set; } = 0.35f;

	ThornsWaterBodyState _state;

	public override bool AllowGrounding => !_state.ShouldSwim;
	public override bool AllowFalling => !_state.ShouldSwim;

	protected override void OnFixedUpdate()
	{
		RefreshWaterState();
	}

	void RefreshWaterState()
	{
		_state = default;

		if ( !CanSimulate() )
			return;

		ThornsNaturalWaterBody.TrySample( Scene, GameObject, out _state );
	}

	bool CanSimulate()
	{
		if ( !Game.IsPlaying || !GameObject.IsValid() )
			return false;

		if ( Components.Get<ThornsPlayerMountController>()?.IsMounted == true )
			return false;

		var health = Components.Get<ThornsPlayerHealth>();
		if ( health is { IsValid: true } && ( !health.IsAlive || health.IsDeadState ) )
			return false;

		if ( !ThornsLocalPlayer.IsLocallyControlledPawn( GameObject ) )
			return false;

		var controller = Components.Get<PlayerController>();
		if ( !controller.IsValid() || !controller.UseInputControls )
			return false;

		if ( ThornsMenuHost.IsOpen || ThornsMenuHost.IsWorldContainerOpen || ThornsMenuHost.IsRadioShopOpen || ThornsMenuHost.IsResearchOpen )
			return false;

		return true;
	}

	public override int Score( PlayerController controller )
	{
		RefreshWaterState();

		if ( !_state.IsActive )
			return -100;

		if ( _state.ShouldSwim )
			return SwimModePriority;

		if ( _state.ShouldWade )
			return WadeModePriority;

		return -100;
	}

	public override void OnModeBegin()
	{
		if ( Controller.IsValid() )
			Controller.IsSwimming = _state.ShouldSwim;
	}

	public override void OnModeEnd( MoveMode next )
	{
		if ( Controller.IsValid() )
			Controller.IsSwimming = false;
	}

	public override Vector3 UpdateMove( Rotation eyes, Vector3 input )
	{
		if ( _state.ShouldSwim )
		{
			var planarInput = new Vector3( input.x, input.y, 0f );
			var wish = base.UpdateMove( eyes, planarInput );
			var speed = wish.Length > 1e-3f ? wish.Length : ResolvePlanarMoveSpeed( Controller );

			if ( Input.Down( "jump" ) )
				wish += Vector3.Up * speed;
			if ( Input.Down( "duck" ) )
				wish += Vector3.Down * speed;

			if ( wish.Length > speed + 1e-3f )
				wish = wish.ClampLength( speed );

			return wish;
		}

		if ( _state.ShouldWade )
			return base.UpdateMove( eyes, input ) * WadeSpeedMultiplier;

		return base.UpdateMove( eyes, input );
	}

	static float ResolvePlanarMoveSpeed( PlayerController controller )
	{
		if ( !controller.IsValid() )
			return 110f;

		var run = Input.Down( controller.AltMoveButton );
		if ( controller.RunByDefault )
			run = !run;

		if ( run )
		{
			var gameplay = controller.Components.Get<ThornsPlayerGameplay>();
			if ( gameplay.IsValid() && !gameplay.CanSprint )
				run = false;
		}

		return run ? controller.RunSpeed : controller.WalkSpeed;
	}

	public override void UpdateRigidBody( Rigidbody body )
	{
		if ( _state.ShouldSwim )
		{
			body.Gravity = false;
			body.LinearDamping = SwimLinearDamping;
			body.AngularDamping = 1f;
			return;
		}

		base.UpdateRigidBody( body );
	}

	public override void PrePhysicsStep()
	{
		if ( !_state.ShouldSwim || !Controller.IsValid() )
			return;

		var body = Controller.Body;
		if ( !body.IsValid() )
			return;

		Controller.PreventGrounding( 0.2f );
		ApplySwimBuoyancy( body );
	}

	public override void PostPhysicsStep()
	{
		if ( !_state.ShouldSwim || !Controller.IsValid() )
			return;

		var body = Controller.Body;
		if ( !body.IsValid() )
			return;

		if ( Input.Down( "duck" ) )
			return;

		var pos = GameObject.WorldPosition;
		var targetFeetZ = _state.WaterSurfaceZ - FloatTargetFeetSubmerge;
		if ( pos.z >= targetFeetZ - 6f )
			return;

		var vel = body.Velocity;
		vel.z = MathF.Max( vel.z, 90f );
		body.Velocity = vel;
	}

	void ApplySwimBuoyancy( Rigidbody body )
	{
		var dt = Math.Clamp( Time.Delta, 0.001f, 0.05f );
		var pos = GameObject.WorldPosition;
		var vel = body.Velocity;

		var targetFeetZ = _state.WaterSurfaceZ - FloatTargetFeetSubmerge;
		var zError = targetFeetZ - pos.z;
		vel.z += ( zError * BuoyancyStrength - vel.z * VerticalDrag ) * dt;

		if ( !Input.Down( "duck" ) )
		{
			var minZ = _state.TerrainFloorZ + 8f;
			if ( pos.z < minZ && vel.z < 0f )
				vel.z = MathF.Max( vel.z, ( minZ - pos.z ) / dt );
		}

		body.Velocity = vel;
	}
}
