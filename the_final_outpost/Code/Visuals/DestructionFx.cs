namespace FinalOutpost;

/// <summary>Short-lived red particle burst when units or structures are destroyed.</summary>
public static class DestructionFx
{
	sealed class Particle
	{
		public GameObject Go;
		public ModelRenderer Renderer;
		public Vector3 Velocity;
		public float Life;
		public float MaxLife;
		public float BaseSize;
		public Color StartColor;
	}

	static readonly List<Particle> Active = new();
	static readonly Stack<Particle> Pool = new();

	static readonly Color BloodBright = new( 1f, 0.12f, 0.08f );
	static readonly Color BloodDark = new( 0.65f, 0.05f, 0.04f );

	public static void Burst( Vector3 worldPos, float scale = 1f )
	{
		scale = MathF.Max( 0.2f, scale );
		var ground = OutpostTerrain.SampleHeight( worldPos.x, worldPos.y );
		// Prefer the caller's height (e.g. wall-mounted) so bursts stay at the hit.
		var z = MathF.Max( ground + 28f * scale, worldPos.z + 20f * scale );

		var count = scale < 0.5f ? Game.Random.Int( 8, 12 ) : Game.Random.Int( 14, 22 );
		for ( var i = 0; i < count; i++ )
		{
			var p = Pool.Count > 0 ? Pool.Pop() : CreateParticle();
			if ( p.Go is null || !p.Go.IsValid() )
				p = CreateParticle();

			var angle = Game.Random.Float( 0f, MathF.PI * 2f );
			var speed = Game.Random.Float( 90f, 220f ) * scale;
			var upward = Game.Random.Float( 60f, 180f ) * scale;
			p.Velocity = new Vector3( MathF.Cos( angle ) * speed, MathF.Sin( angle ) * speed, upward );
			p.MaxLife = Game.Random.Float( 0.45f, 0.85f );
			p.Life = p.MaxLife;
			p.StartColor = Color.Lerp( BloodDark, BloodBright, Game.Random.Float( 0.35f, 1f ) );
			p.BaseSize = Game.Random.Float( 10f, 18f ) * scale;

			p.Go.LocalScale = MeshPrimitives.BoxScale( new Vector3( p.BaseSize, p.BaseSize, p.BaseSize * 0.9f ) );
			p.Go.WorldPosition = new Vector3(
				worldPos.x + Game.Random.Float( -12f, 12f ),
				worldPos.y + Game.Random.Float( -12f, 12f ),
				z + Game.Random.Float( -8f, 14f ) );
			p.Go.Enabled = true;
			if ( p.Renderer is not null && p.Renderer.IsValid() )
			{
				p.Renderer.Enabled = true;
				p.Renderer.Tint = p.StartColor;
			}

			Active.Add( p );
		}
	}

	public static void Tick( float dt )
	{
		if ( dt <= 0f ) return;

		for ( var i = Active.Count - 1; i >= 0; i-- )
		{
			var p = Active[i];
			if ( p.Go is null || !p.Go.IsValid() )
			{
				Active.RemoveAt( i );
				continue;
			}

			p.Life -= dt;
			if ( p.Life <= 0f )
			{
				Recycle( p );
				Active.RemoveAt( i );
				continue;
			}

			p.Velocity += new Vector3( 0f, 0f, -380f * dt );
			p.Go.WorldPosition += p.Velocity * dt;

			var t = Math.Clamp( p.Life / p.MaxLife, 0f, 1f );
			var size = p.BaseSize * (0.35f + 0.65f * t);
			p.Go.LocalScale = MeshPrimitives.BoxScale( new Vector3( size, size, size * 0.9f ) );
			if ( p.Renderer is not null && p.Renderer.IsValid() )
				p.Renderer.Tint = Color.Lerp( BloodDark, p.StartColor, t );
		}
	}

	/// <summary>Soft clear — only used on full combat teardown after particles can finish.</summary>
	public static void Clear()
	{
		for ( var i = Active.Count - 1; i >= 0; i-- )
			Recycle( Active[i] );

		Active.Clear();
	}

	static Particle CreateParticle()
	{
		var go = new GameObject( true, "DestructionParticle" );
		go.Tags.Add( "destructionfx" );
		var mr = go.Components.Create<ModelRenderer>();
		mr.Model = MeshPrimitives.Box;
		mr.MaterialOverride = MeshPrimitives.Mat;
		mr.Tint = BloodBright;
		return new Particle { Go = go, Renderer = mr };
	}

	static void Recycle( Particle p )
	{
		if ( p.Go is not null && p.Go.IsValid() )
			p.Go.Enabled = false;

		p.Velocity = default;
		p.Life = 0f;
		Pool.Push( p );
	}
}
