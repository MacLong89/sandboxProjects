namespace Terraingen.Combat;

/// <summary>Short-lived client tracer drawn with <see cref="SceneLineObject"/>.</summary>
[Category( "Thorns/Combat" )]
public sealed class ThornsCombatTracerFx : Component
{
	const float MinDurationSeconds = 0.14f;
	const float MaxDurationSeconds = 0.42f;
	const float VisualTravelSpeed = 26000f;
	const float StartWidth = 4.2f;
	const float EndWidth = 0.55f;
	const float MinStreakLength = 72f;
	const float MaxStreakLength = 880f;
	const float StreakLengthFraction = 0.18f;

	static Material _lineMaterial;

	Vector3 _start;
	Vector3 _end;
	Vector3 _dir;
	float _hitLength;
	float _durationSeconds;
	float _streakLength;
	ThornsCombatTracerSource _source;
	double _spawnTime;
	SceneLineObject _line;

	public void Init( Vector3 start, Vector3 end, ThornsCombatTracerSource source )
	{
		_start = start;
		_end = end;
		_source = source;
		_spawnTime = Time.Now;

		var travel = end - start;
		_hitLength = travel.Length;
		_dir = _hitLength >= 1f ? travel / _hitLength : Vector3.Forward;
		_durationSeconds = ResolveDuration( _hitLength );
		_streakLength = ResolveStreakLength( _hitLength );
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
		if ( age >= _durationSeconds )
		{
			DestroyTracer();
			return;
		}

		DrawTracer( age / _durationSeconds );
	}

	protected override void OnDestroy() => DestroyTracer();

	void DrawTracer( float lifeT )
	{
		if ( _hitLength < 1f )
		{
			DestroyTracer();
			return;
		}

		var fade = 1f - lifeT * lifeT;
		var travelT = Math.Clamp( lifeT * 1.04f, 0.05f, 1f );
		var headDist = _hitLength * travelT;
		var tailDist = Math.Max( 0f, headDist - _streakLength );

		var head = _start + _dir * headDist;
		var tail = _start + _dir * tailDist;
		var mid = Vector3.Lerp( tail, head, 0.42f );

		var bright = ResolveBrightColor( _source );
		var dim = bright.WithAlpha( 0.14f * fade );

		_line.RenderingEnabled = true;
		_line.StartLine();
		_line.AddLinePoint( tail, dim, EndWidth );
		_line.AddLinePoint( mid, bright.WithAlpha( 0.72f * fade ), StartWidth * 0.72f );
		_line.AddLinePoint( head, bright.WithAlpha( 0.98f * fade ), StartWidth );
		_line.EndLine();
	}

	static float ResolveDuration( float hitLength ) =>
		Math.Clamp( hitLength / VisualTravelSpeed, MinDurationSeconds, MaxDurationSeconds );

	static float ResolveStreakLength( float hitLength )
	{
		if ( hitLength < 1f )
			return hitLength;

		var cap = Math.Min( MaxStreakLength, hitLength );
		var min = Math.Min( MinStreakLength, cap );
		return Math.Clamp( hitLength * StreakLengthFraction, min, cap );
	}

	static Color ResolveBrightColor( ThornsCombatTracerSource source ) =>
		source switch
		{
			ThornsCombatTracerSource.Npc => new Color( 1f, 0.38f, 0.28f ),
			ThornsCombatTracerSource.OtherPlayer => new Color( 1f, 0.62f, 0.22f ),
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
