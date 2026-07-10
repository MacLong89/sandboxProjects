namespace Fauna2;

/// <summary>
/// Switches a <see cref="SpriteRenderer"/> between Idle and Walk clips based on movement,
/// with a subtle procedural bob when walk frames are not available yet.
/// </summary>
public sealed class SpriteWalkAnimator : Component
{
	public const string IdleAnimation = "Idle";
	public const string WalkAnimation = "Walk";

	/// <summary>World object whose position is sampled to detect movement.</summary>
	public GameObject MovementRoot { get; set; }

	public float MoveSpeedThreshold { get; set; } = 12f;
	public float WalkPlaybackSpeed { get; set; } = 1f;
	public float ProceduralBobAmplitude { get; set; } = 2.5f;
	public float ProceduralBobSpeed { get; set; } = 14f;

	private SpriteRenderer _renderer;
	private Vector3 _lastSamplePosition;
	private Vector3 _baseLocalPosition;
	private bool _moving;
	private bool _hasWalkClip;

	protected override void OnStart()
	{
		_renderer = Components.Get<SpriteRenderer>();
		_baseLocalPosition = GameObject.LocalPosition;
		_hasWalkClip = HasWalkAnimation( _renderer );

		var root = MovementRoot.IsValid() ? MovementRoot : GameObject.Parent;
		_lastSamplePosition = root.IsValid() ? root.WorldPosition : GameObject.WorldPosition;

		if ( _hasWalkClip )
			_renderer.PlayAnimation( IdleAnimation );
	}

	protected override void OnUpdate()
	{
		if ( !_renderer.IsValid() )
			return;

		var root = MovementRoot.IsValid() ? MovementRoot : GameObject.Parent;
		if ( !root.IsValid() )
			return;

		var pos = root.WorldPosition;
		var moved = pos.WithZ( 0 ).Distance( _lastSamplePosition.WithZ( 0 ) );
		_lastSamplePosition = pos;

		var speed = moved / MathF.Max( Time.Delta, 0.0001f );
		var walking = speed >= MoveSpeedThreshold;

		if ( _hasWalkClip )
			UpdateClipAnimation( walking, speed );
		else
			UpdateProceduralBob( walking );
	}

	private void UpdateClipAnimation( bool walking, float speed )
	{
		if ( walking )
			_renderer.PlaybackSpeed = WalkPlaybackSpeed * (speed / GameConstants.PlayerWalkSpeed).Clamp( 0.75f, 1.35f );

		if ( walking == _moving )
			return;

		_moving = walking;
		_renderer.PlaybackSpeed = walking ? WalkPlaybackSpeed : 1f;
		_renderer.PlayAnimation( walking ? WalkAnimation : IdleAnimation );
	}

	private void UpdateProceduralBob( bool walking )
	{
		if ( !walking )
		{
			_moving = false;
			GameObject.LocalPosition = _baseLocalPosition;
			GameObject.LocalScale = Vector3.One;
			return;
		}

		_moving = true;
		var bob = MathF.Sin( Time.Now * ProceduralBobSpeed ) * ProceduralBobAmplitude;
		var squash = 1f - MathF.Abs( MathF.Sin( Time.Now * ProceduralBobSpeed ) ) * 0.04f;
		GameObject.LocalPosition = _baseLocalPosition.WithZ( _baseLocalPosition.z + bob );
		GameObject.LocalScale = new Vector3( 1f, 1f, squash );
	}

	private static bool HasWalkAnimation( SpriteRenderer renderer ) =>
		renderer.IsValid()
		&& renderer.Sprite.IsValid()
		&& renderer.Sprite.Animations.Any( a => a.Name == WalkAnimation && a.IsAnimated );

	public void ForceIdle()
	{
		_moving = false;
		GameObject.LocalPosition = _baseLocalPosition;
		GameObject.LocalScale = Vector3.One;

		if ( !_renderer.IsValid() || !_hasWalkClip )
			return;

		_renderer.PlaybackSpeed = 1f;
		_renderer.PlayAnimation( IdleAnimation );
	}
}
