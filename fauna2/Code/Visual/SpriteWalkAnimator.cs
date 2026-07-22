namespace Fauna2;

/// <summary>
/// Directional facing swaps plus Bounce motion (procedural bob on idle) while moving.
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
	/// When true (and no 4-dir pack is wired), negate local X so right-facing art flips when moving left.
	/// </summary>
	public bool FlipFacingHorizontal { get; set; }

	/// <summary>Ignore tiny X jitter when deciding facing.</summary>
	public float FacingFlipDeadzone { get; set; } = 0.35f;

	/// <summary>Animal stem for facing-aware <see cref="PixelArt.CritterSprite"/> swaps.</summary>
	public string DirectionalCritterKey { get; set; }

	/// <summary>Guest variant index for facing-aware guest sprite swaps; -1 = unused.</summary>
	public int DirectionalGuestVariant { get; set; } = -1;

	/// <summary>When true, player visuals own facing swaps (ZooPlayerVisual); animator only applies Bounce.</summary>
	public bool PlayerOwnedFacing { get; set; }

	private SpriteRenderer _renderer;
	private Vector3 _lastSamplePosition;
	private Vector3 _baseLocalPosition;
	private bool _moving;
	private bool _hasIdleClip;
	private bool _faceLeft;
	private PlayerFacing _facing = PlayerFacing.Down;

	protected override void OnStart()
	{
		_renderer = Components.Get<SpriteRenderer>();
		_baseLocalPosition = GameObject.LocalPosition;

		var root = MovementRoot.IsValid() ? MovementRoot : GameObject.Parent;
		_lastSamplePosition = root.IsValid() ? root.WorldPosition : GameObject.WorldPosition;

		RefreshClipPresence();
		if ( _hasIdleClip )
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
		_lastSamplePosition = pos;

		UpdateFacing( delta );

		var speed = delta.Length / MathF.Max( Time.Delta, 0.0001f );
		var walking = speed >= MoveSpeedThreshold;
		UpdateProceduralBob( walking );
	}

	private bool UsesDirectionalSprites =>
		!PlayerOwnedFacing
		&& (!string.IsNullOrEmpty( DirectionalCritterKey ) || DirectionalGuestVariant >= 0);

	private void UpdateFacing( Vector3 delta )
	{
		if ( PlayerOwnedFacing )
			return;

		if ( UsesDirectionalSprites )
		{
			if ( delta.Length < FacingFlipDeadzone )
				return;

			var next = PlayerFacingExtensions.FromMove( delta );
			if ( next == _facing )
				return;

			_facing = next;
			ApplyDirectionalSprite();
			return;
		}

		if ( !FlipFacingHorizontal )
			return;

		// Screen-left/right is ±Y under the zoo camera (see PlayerFacingExtensions.FromMove).
		if ( delta.y > FacingFlipDeadzone )
			_faceLeft = true;
		else if ( delta.y < -FacingFlipDeadzone )
			_faceLeft = false;
	}

	private void ApplyDirectionalSprite()
	{
		if ( !_renderer.IsValid() )
			return;

		Sprite sprite = null;
		if ( !string.IsNullOrEmpty( DirectionalCritterKey ) )
			sprite = PixelArt.CritterSprite( DirectionalCritterKey, _facing );
		else if ( DirectionalGuestVariant >= 0 )
			sprite = PixelArt.GuestSpriteResource( DirectionalGuestVariant, _facing );

		if ( sprite is null || !sprite.IsValid() )
			return;

		_renderer.Sprite = sprite;
		_renderer.StartingAnimationName = IdleAnimation;
		RefreshClipPresence();

		if ( _hasIdleClip )
			_renderer.PlayAnimation( IdleAnimation );

		GameObject.LocalScale = new Vector3( 1f, 1f, GameObject.LocalScale.z );
	}

	private void UpdateProceduralBob( bool walking )
	{
		_renderer.PlaybackSpeed = 1f;

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
		if ( PlayerOwnedFacing )
		{
			GameObject.LocalScale = new Vector3( 1f, 1f, squashZ );
			return;
		}

		if ( UsesDirectionalSprites )
		{
			var mirrorLeft = _facing == PlayerFacing.Left && NeedsLeftMirror();
			GameObject.LocalScale = new Vector3( mirrorLeft ? -1f : 1f, 1f, squashZ );
			return;
		}

		var sx = FlipFacingHorizontal && _faceLeft ? -1f : 1f;
		GameObject.LocalScale = new Vector3( sx, 1f, squashZ );
	}

	private bool NeedsLeftMirror()
	{
		if ( !string.IsNullOrEmpty( DirectionalCritterKey ) )
			return !PixelArt.HasDedicatedAnimalFacing( DirectionalCritterKey, PlayerFacing.Left );

		if ( DirectionalGuestVariant >= 0 )
			return !PixelArt.HasDedicatedGuestFacing( DirectionalGuestVariant, PlayerFacing.Left );

		return false;
	}

	private void RefreshClipPresence() =>
		_hasIdleClip = HasIdleAnimation( _renderer );

	private static bool HasIdleAnimation( SpriteRenderer renderer ) =>
		renderer.IsValid()
		&& renderer.Sprite.IsValid()
		&& renderer.Sprite.Animations.Any( a => a.Name == IdleAnimation );

	public void ForceIdle()
	{
		_moving = false;
		GameObject.LocalPosition = _baseLocalPosition;
		ApplyFacingScale( 1f );

		if ( !_renderer.IsValid() || !_hasIdleClip )
			return;

		_renderer.PlaybackSpeed = 1f;
		_renderer.PlayAnimation( IdleAnimation );
	}
}
