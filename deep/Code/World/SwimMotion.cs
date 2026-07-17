namespace Deep;

/// <summary>Simple patrol / bob used by fauna and drifting hazards.</summary>
public sealed class SwimMotion : Component
{
	public Vector3 Origin { get; set; }
	public float AmpX { get; set; } = 3f;
	public float AmpZ { get; set; } = 1.2f;
	public float Speed { get; set; } = 0.7f;
	public float Phase { get; set; }
	public bool FaceMotion { get; set; } = true;

	private float _t;
	private float _prevX;
	private GameObject _spriteGo;
	private Vector3 _baseScale = Vector3.One;

	public void Configure( Vector3 origin, float ampX, float ampZ, float speed, float phase = 0f )
	{
		Origin = origin;
		AmpX = ampX;
		AmpZ = ampZ;
		Speed = speed;
		Phase = phase;
		_t = phase;
		_prevX = origin.x;
		WorldPosition = origin;
	}

	protected override void OnStart()
	{
		foreach ( var child in GameObject.Children )
		{
			if ( child.Components.Get<SpriteRenderer>() is not null )
			{
				_spriteGo = child;
				_baseScale = child.LocalScale;
				if ( MathF.Abs( _baseScale.x ) < 0.001f )
					_baseScale = Vector3.One;
				break;
			}
		}
	}

	protected override void OnUpdate()
	{
		_t += Time.Delta * Speed;
		var x = Origin.x + MathF.Sin( _t ) * AmpX;
		var z = Origin.z + MathF.Sin( _t * 1.7f + Phase ) * AmpZ;
		WorldPosition = new Vector3( x, Origin.y, z );

		if ( FaceMotion && _spriteGo.IsValid() )
		{
			var dx = x - _prevX;
			if ( MathF.Abs( dx ) > 0.01f )
			{
				var sx = MathF.Abs( _baseScale.x );
				var faceLeft = dx < 0f;
				_spriteGo.LocalScale = _baseScale.WithX( faceLeft ? -sx : sx );
			}
		}

		_prevX = x;
	}
}
