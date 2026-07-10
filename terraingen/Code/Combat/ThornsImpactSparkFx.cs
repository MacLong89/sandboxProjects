namespace Terraingen.Combat;

using Sandbox;

/// <summary>Short-lived impact spark burst for harvest/combat hits.</summary>
[Category( "Thorns/Combat" )]
public sealed class ThornsImpactSparkFx : Component
{
	const float DurationSeconds = 0.14f;

	Color _color;
	double _spawnTime;
	SceneLineObject _line;

	public static void Spawn( Scene scene, Vector3 position, Color color )
	{
		if ( scene is null || !scene.IsValid() || Application.IsDedicatedServer || Application.IsHeadless )
			return;

		var go = scene.CreateObject();
		go.Name = "ThornsImpactSparkFx";
		go.WorldPosition = position;
		var fx = go.Components.Create<ThornsImpactSparkFx>();
		fx._color = color;
		fx._spawnTime = Time.Now;
	}

	protected override void OnStart()
	{
		if ( Application.IsDedicatedServer || Application.IsHeadless || Scene is null || !Scene.IsValid() )
			return;

		_line = new SceneLineObject( Scene.SceneWorld );
		_line.RenderingEnabled = false;
		_line.Flags.IsOpaque = false;
		_line.Flags.IsTranslucent = true;
		_line.Flags.CastShadows = false;
		_line.StartCap = SceneLineObject.CapStyle.None;
		_line.EndCap = SceneLineObject.CapStyle.None;
		_line.Face = SceneLineObject.FaceMode.Camera;
		_line.Material = ResolveSparkMaterial();
	}

	protected override void OnUpdate()
	{
		if ( _line is null )
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

		DrawBurst( age / DurationSeconds );
	}

	protected override void OnDestroy() => DestroyFx();

	void DrawBurst( float lifeT )
	{
		var fade = 1f - lifeT;
		var len = 18f + (42f - 18f) * lifeT;
		_line.RenderingEnabled = true;
		_line.StartLine();

		for ( var i = 0; i < 6; i++ )
		{
			var angle = i * (MathF.PI * 2f / 6f);
			var dir = new Vector3( MathF.Cos( angle ), MathF.Sin( angle ), 0.15f ).Normal;
			var end = dir * len;
			_line.AddLinePoint( Vector3.Zero, _color.WithAlpha( 0.08f * fade ), 1f );
			_line.AddLinePoint( end, _color.WithAlpha( 0.85f * fade ), 2.4f );
		}

		_line.EndLine();
	}

	void DestroyFx()
	{
		_line?.Delete();
		_line = null;
		if ( GameObject.IsValid() )
			GameObject.Destroy();
	}

	static Material _sparkMaterial;

	static Material ResolveSparkMaterial()
	{
		if ( _sparkMaterial is not null && _sparkMaterial.IsValid )
			return _sparkMaterial;

		var loaded = Material.Load( "materials/default/default_line.vmat" );
		if ( loaded.IsValid )
		{
			_sparkMaterial = loaded.CreateCopy();
			_sparkMaterial.Set( "Color", Texture.White );
		}

		return _sparkMaterial;
	}
}
