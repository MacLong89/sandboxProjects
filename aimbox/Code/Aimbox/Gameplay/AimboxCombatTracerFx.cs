namespace Sandbox;

/// <summary>Short-lived client tracer drawn with <see cref="SceneLineObject"/>.</summary>
[Category( "Aimbox" )]
public sealed class AimboxCombatTracerFx : Component
{
	const float DurationSeconds = 0.11f;
	const float StreakLengthWorld = 140f;
	const float HeadWidth = 1.4f;
	const float MidWidth = 0.91f;
	const float TailWidth = 0.175f;

	static Material _lineMaterial;

	Vector3 _start;
	Vector3 _end;
	AimboxCombatTracerSource _source;
	double _spawnTime;
	SceneLineObject _line;

	public void Init( Vector3 start, Vector3 end, AimboxCombatTracerSource source )
	{
		_start = start;
		_end = end;
		_source = source;
		_spawnTime = Time.Now;
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
		_line.Material = ResolveLineMaterial();
	}

	protected override void OnUpdate()
	{
		if ( _line is null )
		{
			GameObject.Destroy();
			return;
		}

		var age = (float)(Time.Now - _spawnTime);
		if ( age >= DurationSeconds )
		{
			DestroyTracer();
			return;
		}

		DrawTracer( age / DurationSeconds );
	}

	protected override void OnDestroy() => DestroyTracer();

	void DrawTracer( float lifeT )
	{
		var segment = _end - _start;
		var segmentLength = segment.Length;
		if ( segmentLength < 1f )
		{
			DestroyTracer();
			return;
		}

		var dir = segment / segmentLength;
		var tailFade = 1f - lifeT;

		// Fixed-length streak slides along the ray — width does not scale with hit distance.
		var headDist = segmentLength * Math.Clamp( lifeT * 1.35f, 0f, 1f );
		var tailDist = Math.Max( 0f, headDist - StreakLengthWorld );

		var head = _start + dir * headDist;
		var tail = _start + dir * tailDist;

		var bright = ResolveBrightColor( _source );
		var dim = bright.WithAlpha( 0.08f * tailFade );

		_line.RenderingEnabled = true;
		_line.StartLine();
		_line.AddLinePoint( tail, dim, TailWidth );
		_line.AddLinePoint( Vector3.Lerp( tail, head, 0.45f ), bright.WithAlpha( 0.55f * tailFade ), MidWidth );
		_line.AddLinePoint( head, bright.WithAlpha( 0.95f * tailFade ), HeadWidth );
		_line.EndLine();
	}

	static Color ResolveBrightColor( AimboxCombatTracerSource source ) =>
		source switch
		{
			AimboxCombatTracerSource.OtherPlayer => new Color( 1f, 0.62f, 0.22f ),
			_ => new Color( 1f, 0.94f, 0.58f ),
		};

	static Material ResolveLineMaterial()
	{
		if ( _lineMaterial is not null && _lineMaterial.IsValid )
			return _lineMaterial;

		var loaded = Material.Load( "materials/default/default_line.vmat" );
		if ( loaded.IsValid )
		{
			_lineMaterial = loaded.CreateCopy();
			_lineMaterial.Set( "Color", Texture.White );
			return _lineMaterial;
		}

		_lineMaterial = Material.Load( "materials/skybox/skybox_daytime_01.vmat" );
		return _lineMaterial;
	}

	void DestroyTracer()
	{
		_line?.Delete();
		_line = null;

		if ( GameObject.IsValid() )
			GameObject.Destroy();
	}
}
