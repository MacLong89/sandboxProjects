namespace DeepDive;

/// <summary>
/// Switches diver SpriteRenderer clips (Idle / Swim / SwimUp / SwimDown / Harpoon)
/// and flips horizontally for left/right facing.
/// </summary>
public sealed class DiverSwimAnimator : Component
{
	public GameObject MovementRoot { get; set; }
	public float MoveSpeedThreshold { get; set; } = 2.5f;
	public float VerticalDominance { get; set; } = 1.15f;
	public float FacingFlipDeadzone { get; set; } = 0.45f;
	public float HarpoonPoseSeconds { get; set; } = 0.42f;

	private SpriteRenderer _renderer;
	private string _clip = DeepDivePixelArt.IdleAnimation;
	private bool _faceLeft;
	private Vector3 _baseLocalScale = Vector3.One;
	private TimeUntil _harpoonPoseEnds;

	public void PlayHarpoon( Vector3 targetWorld )
	{
		_harpoonPoseEnds = HarpoonPoseSeconds;
		_clip = DeepDivePixelArt.HarpoonAnimation;
		if ( _renderer.IsValid() )
			_renderer.PlayAnimation( _clip );

		var root = MovementRoot.IsValid() ? MovementRoot : GameObject.Parent;
		if ( !root.IsValid() )
			return;

		var delta = targetWorld - root.WorldPosition;
		if ( delta.x > FacingFlipDeadzone )
			_faceLeft = true;
		else if ( delta.x < -FacingFlipDeadzone )
			_faceLeft = false;
	}

	protected override void OnStart()
	{
		_renderer = Components.Get<SpriteRenderer>();
		_baseLocalScale = GameObject.LocalScale;
		if ( MathF.Abs( _baseLocalScale.x ) < 0.001f )
			_baseLocalScale = Vector3.One;

		if ( _renderer.IsValid() )
			_renderer.PlayAnimation( DeepDivePixelArt.IdleAnimation );

		ApplyFacing();
	}

	protected override void OnUpdate()
	{
		if ( !_renderer.IsValid() )
			return;

		if ( !_harpoonPoseEnds )
		{
			ApplyFacing();
			return;
		}

		var velocity = SampleVelocity();
		UpdateFacing( velocity.x );
		UpdateClip( velocity );
		ApplyFacing();
	}

	private Vector3 SampleVelocity()
	{
		var root = MovementRoot.IsValid() ? MovementRoot : GameObject.Parent;
		if ( root.IsValid() )
		{
			var diver = root.Components.Get<DiverController>();
			if ( diver is not null )
				return diver.Velocity;
		}

		return DiverController.Instance?.Velocity ?? Vector3.Zero;
	}

	private void UpdateFacing( float vx )
	{
		// Play plane: +X is screen-left with the +Y camera, so positive X faces left.
		if ( vx > FacingFlipDeadzone )
			_faceLeft = true;
		else if ( vx < -FacingFlipDeadzone )
			_faceLeft = false;
	}

	private void UpdateClip( Vector3 velocity )
	{
		var speed = velocity.Length;
		string next;
		if ( speed < MoveSpeedThreshold )
		{
			next = DeepDivePixelArt.IdleAnimation;
		}
		else
		{
			var absX = MathF.Abs( velocity.x );
			var absZ = MathF.Abs( velocity.z );
			if ( absZ > absX * VerticalDominance )
				next = velocity.z > 0f ? DeepDivePixelArt.SwimUpAnimation : DeepDivePixelArt.SwimDownAnimation;
			else
				next = DeepDivePixelArt.SwimAnimation;
		}

		if ( next == _clip )
			return;

		_clip = next;
		_renderer.PlayAnimation( _clip );
	}

	private void ApplyFacing()
	{
		var sx = MathF.Abs( _baseLocalScale.x );
		if ( sx < 0.001f ) sx = 1f;
		GameObject.LocalScale = _baseLocalScale.WithX( _faceLeft ? -sx : sx );
	}
}
