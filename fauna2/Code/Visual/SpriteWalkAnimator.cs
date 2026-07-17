namespace Fauna2;

/// <summary>
/// Switches a <see cref="SpriteRenderer"/> between Idle and Walk clips based on movement,
/// with a subtle procedural bob when walk frames are not available yet.
/// Optional horizontal flip mirrors art that faces right by default when moving left.
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

	/// <summary>
	/// When true, negate local X so sprites that face right by default flip when moving left
	/// (mirror across the sprite Y axis). Zoo/wild animals enable this; player uses facing dirs instead.
	/// </summary>
	public bool FlipFacingHorizontal { get; set; }

	/// <summary>Ignore tiny X jitter when deciding facing.</summary>
	public float FacingFlipDeadzone { get; set; } = 0.35f;

	private SpriteRenderer _renderer;
	private Vector3 _lastSamplePosition;
	private Vector3 _baseLocalPosition;
	private bool _moving;
	private bool _hasWalkClip;
	private bool _faceLeft;

	protected override void OnStart()
	{
		_renderer = Components.Get<SpriteRenderer>();
		_baseLocalPosition = GameObject.LocalPosition;
		_hasWalkClip = HasWalkAnimation( _renderer );

		var root = MovementRoot.IsValid() ? MovementRoot : GameObject.Parent;
		_lastSamplePosition = root.IsValid() ? root.WorldPosition : GameObject.WorldPosition;

		if ( _hasWalkClip )
			_renderer.PlayAnimation( IdleAnimation );

		ApplyFacingScale( 1f );
	}

	protected override void OnUpdate()
	{
		if ( !_renderer.IsValid() )
			return;

		var root = MovementRoot.IsValid() ? MovementRoot : GameObject.Parent;
		if ( !root.IsValid() )
			return;

		var pos = root.WorldPosition;
		var delta = pos.WithZ( 0f ) - _lastSamplePosition.WithZ( 0f );
		var moved = delta.Length;
		_lastSamplePosition = pos;

		UpdateFacing( delta.x );

		var speed = moved / MathF.Max( Time.Delta, 0.0001f );
		var walking = speed >= MoveSpeedThreshold;

		if ( _hasWalkClip )
			UpdateClipAnimation( walking, speed );
		else
			UpdateProceduralBob( walking );
	}

	private void UpdateFacing( float deltaX )
	{
		if ( !FlipFacingHorizontal )
			return;

		if ( deltaX < -FacingFlipDeadzone )
			_faceLeft = true;
		else if ( deltaX > FacingFlipDeadzone )
			_faceLeft = false;
	}

	private void UpdateClipAnimation( bool walking, float speed )
	{
		if ( walking )
			_renderer.PlaybackSpeed = WalkPlaybackSpeed * (speed / GameConstants.PlayerWalkSpeed).Clamp( 0.75f, 1.35f );

		ApplyFacingScale( 1f );

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
			ApplyFacingScale( 1f );
			return;
		}

		_moving = true;
		var bob = MathF.Sin( Time.Now * ProceduralBobSpeed ) * ProceduralBobAmplitude;
		var squash = 1f - MathF.Abs( MathF.Sin( Time.Now * ProceduralBobSpeed ) ) * 0.04f;
		GameObject.LocalPosition = _baseLocalPosition.WithZ( _baseLocalPosition.z + bob );
		ApplyFacingScale( squash );
	}

	private void ApplyFacingScale( float squashZ )
	{
		var sx = FlipFacingHorizontal && _faceLeft ? -1f : 1f;
		GameObject.LocalScale = new Vector3( sx, 1f, squashZ );
	}

	private static bool HasWalkAnimation( SpriteRenderer renderer ) =>
		renderer.IsValid()
		&& renderer.Sprite.IsValid()
		&& renderer.Sprite.Animations.Any( a => a.Name == WalkAnimation && a.IsAnimated );

	public void ForceIdle()
	{
		_moving = false;
		GameObject.LocalPosition = _baseLocalPosition;
		ApplyFacingScale( 1f );

		if ( !_renderer.IsValid() || !_hasWalkClip )
			return;

		_renderer.PlaybackSpeed = 1f;
		_renderer.PlayAnimation( IdleAnimation );
	}
}
