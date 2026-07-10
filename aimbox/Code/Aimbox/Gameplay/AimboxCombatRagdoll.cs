namespace Sandbox;

public static class AimboxCombatRagdoll
{
	public const float LifetimeSeconds = 1f;

	public static GameObject SpawnFromCitizenBody( GameObject pawn, Vector3 damageOrigin, float lifetime = LifetimeSeconds )
	{
		if ( pawn is null || !pawn.IsValid() )
			return default;

		var body = AimboxCitizenPresentation.FindChild( pawn, AimboxCitizenPresentation.BodyChildName );
		if ( !body.IsValid() )
			return default;

		var source = body.Components.Get<SkinnedModelRenderer>();
		if ( !source.IsValid() )
			return default;

		var ragdollGo = new GameObject( true, "Combat Ragdoll" );
		ragdollGo.WorldTransform = pawn.WorldTransform;

		var mainBody = ragdollGo.Components.Create<SkinnedModelRenderer>();
		mainBody.CopyFrom( source );
		mainBody.UseAnimGraph = false;

		var physics = ragdollGo.Components.Create<ModelPhysics>();
		physics.Model = mainBody.Model;
		physics.Renderer = mainBody;
		physics.CopyBonesFrom( source, true );

		ApplyImpulse( physics, pawn.WorldPosition, damageOrigin );

		var lifetimeComp = ragdollGo.Components.Create<AimboxCombatRagdollLifetime>();
		lifetimeComp.Lifetime = lifetime;
		return ragdollGo;
	}

	static void ApplyImpulse( ModelPhysics physics, Vector3 pawnPosition, Vector3 damageOrigin )
	{
		if ( physics is null || !physics.IsValid() )
			return;

		var dir = (pawnPosition - damageOrigin).WithZ( 0f );
		if ( dir.Length < 1f )
			dir = Vector3.Random.WithZ( 0f );

		dir = (dir.Normal + Vector3.Up * 0.4f).Normal;

		foreach ( var body in physics.Bodies )
		{
			var rb = body.Component;
			if ( !rb.IsValid() )
				continue;

			rb.ApplyImpulse( dir * rb.Mass * 5f );
		}
	}
}

[Title( "Aimbox Combat Ragdoll Lifetime" )]
[Category( "Aimbox" )]
public sealed class AimboxCombatRagdollLifetime : Component
{
	[Property] public float Lifetime { get; set; } = 1f;
	TimeUntil _destroyAt;

	protected override void OnStart()
	{
		_destroyAt = Lifetime;
	}

	protected override void OnUpdate()
	{
		if ( _destroyAt )
			GameObject.Destroy();
	}
}
