namespace RunGun;

/// <summary>Slow enemy projectiles the player strafes to dodge.</summary>
public sealed class Projectile : Component
{
	public float Damage { get; set; } = 20f;
	public float Speed { get; set; } = GameConstants.ProjectileSpeed;
	public Vector3 Velocity { get; set; }
	public bool Dead { get; private set; }

	private ModelRenderer _renderer;

	public void Setup( Vector3 origin, Vector3 direction, float damage, Color tint )
	{
		Damage = damage;
		Velocity = direction.Normal * Speed;
		WorldPosition = origin;

		var go = GameObject;
		go.LocalScale = MeshPrimitives.BoxScale( new Vector3( 18f, 18f, 18f ) );
		_renderer = go.Components.Get<ModelRenderer>() ?? go.Components.Create<ModelRenderer>();
		_renderer.Model = MeshPrimitives.Box;
		_renderer.MaterialOverride = MeshPrimitives.Mat;
		_renderer.Tint = tint;
	}

	public void Tick( float dt )
	{
		if ( Dead ) return;
		WorldPosition += Velocity * dt;
	}

	public void Kill()
	{
		Dead = true;
		GameObject?.Destroy();
	}
}
