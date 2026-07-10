namespace Terraingen.Combat;

using Sandbox;

/// <summary>Short-lived blood droplets for wildlife / NPC / player damage hits.</summary>
[Category( "Thorns/Combat" )]
public sealed class ThornsBloodSplatterFx : Component
{
	const float DurationSeconds = 0.95f;
	const float Gravity = 820f;

	static readonly Color DarkBlood = new( 0.42f, 0.04f, 0.05f );
	static readonly Color BrightBlood = new( 0.78f, 0.1f, 0.08f );

	static Model _sphereModel;

	readonly List<BloodDrop> _drops = new();

	Vector3 _sprayForward;
	Vector3 _origin;
	float _intensity = 1f;
	bool _heavy;
	double _spawnTime;

	sealed class BloodDrop
	{
		public GameObject Root;
		public ModelRenderer Renderer;
		public Vector3 Velocity;
		public float Spin;
	}

	public void Init( Vector3 sprayDirection, float intensity, bool heavyKillBurst )
	{
		_sprayForward = sprayDirection.LengthSquared > 0.25f ? sprayDirection.Normal : Vector3.Up;
		_intensity = Math.Clamp( intensity, 0.45f, 2.2f );
		_heavy = heavyKillBurst;
		_origin = GameObject.WorldPosition;
		_spawnTime = Time.Now;
	}

	protected override void OnStart()
	{
		if ( Application.IsDedicatedServer || Application.IsHeadless || Scene is null || !Scene.IsValid() )
			return;

		var model = ResolveSphereModel();
		if ( !model.IsValid || model.IsError )
		{
			DestroyFx();
			return;
		}

		var count = _heavy ? 10 : 8;
		var scale = ResolveDropScale( model );
		for ( var i = 0; i < count; i++ )
			SpawnDrop( model, scale, i, count );
	}

	protected override void OnUpdate()
	{
		if ( _drops.Count == 0 )
		{
			DestroyFx();
			return;
		}

		var age = (float)(Time.Now - _spawnTime);
		if ( age >= DurationSeconds )
		{
			DestroyFx();
			return;
		}

		var fade = 1f - (age / DurationSeconds);
		var dt = Math.Clamp( Time.Delta, 0.001f, 0.05f );

		for ( var i = _drops.Count - 1; i >= 0; i-- )
		{
			var drop = _drops[i];
			if ( !drop.Root.IsValid() )
			{
				_drops.RemoveAt( i );
				continue;
			}

			drop.Velocity += Vector3.Down * Gravity * dt;
			drop.Root.WorldPosition += drop.Velocity * dt;
			drop.Root.LocalRotation *= Rotation.FromYaw( drop.Spin * dt );

			var tint = Color.Lerp( DarkBlood, BrightBlood, 0.35f + (i % 3) * 0.18f ).WithAlpha( fade );
			if ( drop.Renderer.IsValid() )
				drop.Renderer.Tint = tint;
		}
	}

	protected override void OnDestroy() => DestroyFx();

	void SpawnDrop( Model model, float scale, int index, int count )
	{
		var yaw = index * (MathF.PI * 2f / count);
		var lateral = new Vector3( MathF.Cos( yaw ), MathF.Sin( yaw ), 0f );
		var burst = (_sprayForward * Game.Random.Float( 0.45f, 1.05f )
		             + lateral * Game.Random.Float( 0.35f, 0.95f )
		             + Vector3.Up * Game.Random.Float( 0.25f, 0.85f)).Normal;

		var speed = Game.Random.Float( 95f, 185f ) * _intensity;
		var offset = lateral * Game.Random.Float( 0f, 8f ) + Vector3.Up * Game.Random.Float( 0f, 6f );

		var go = Scene.CreateObject( true );
		go.Name = "BloodDrop";
		go.Parent = GameObject;
		go.WorldPosition = _origin + offset;

		var renderer = go.Components.Create<ModelRenderer>();
		renderer.Model = model;
		renderer.Tint = BrightBlood;
		renderer.RenderOptions.Game = true;
		renderer.RenderOptions.Overlay = false;
		go.LocalScale = Vector3.One * scale * Game.Random.Float( 0.82f, 1.12f );

		_drops.Add( new BloodDrop
		{
			Root = go,
			Renderer = renderer,
			Velocity = burst * speed,
			Spin = Game.Random.Float( -240f, 240f ),
		} );
	}

	static float ResolveDropScale( Model model )
	{
		var size = model.Bounds.Size;
		var maxAxis = MathF.Max( size.x, MathF.Max( size.y, size.z ) );
		if ( maxAxis < 0.01f )
			return 0.12f;

		var targetDiameter = 3.2f;
		return targetDiameter / maxAxis;
	}

	static Model ResolveSphereModel()
	{
		if ( _sphereModel.IsValid() && !_sphereModel.IsError )
			return _sphereModel;

		foreach ( var path in new[] { "models/dev/sphere.vmdl", "models/dev/box.vmdl" } )
		{
			var model = Model.Load( path );
			if ( model.IsValid && !model.IsError )
			{
				_sphereModel = model;
				return model;
			}
		}

		return default;
	}

	void DestroyFx()
	{
		for ( var i = _drops.Count - 1; i >= 0; i-- )
		{
			var drop = _drops[i];
			if ( drop.Root.IsValid() )
				drop.Root.Destroy();
		}

		_drops.Clear();

		if ( GameObject.IsValid() )
			GameObject.Destroy();
	}
}
