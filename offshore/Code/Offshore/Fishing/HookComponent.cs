namespace Offshore;

/// <summary>Hook / bobber visual — bobber while flying and resting in water.</summary>
public sealed class HookComponent : Component
{
	private SpriteRenderer _sprite;
	private bool _visible;

	public Vector3 TipRestPosition { get; set; }
	public Vector3 WorldHookPosition => WorldPosition;

	protected override void OnStart()
	{
		_sprite = OffshoreSprites.Spawn(
			GameObject,
			OffshoreSprites.Paths.Bobber,
			new Vector2( 0.7f, 0.7f ),
			Vector3.Zero,
			"BobberSprite" );

		HideAtTip();
	}

	public void SetPosition( Vector3 worldPos )
	{
		WorldPosition = worldPos;
	}

	/// <summary>Projectile visual during cast flight.</summary>
	public void ShowInFlight()
	{
		_visible = true;
		SetSpriteEnabled( true );
		SwapSprite( OffshoreSprites.Paths.Bobber, new Vector2( 0.65f, 0.65f ) );
	}

	/// <summary>Resting bobber after a valid water landing.</summary>
	public void ShowBobberAtRest()
	{
		_visible = true;
		SetSpriteEnabled( true );
		SwapSprite( OffshoreSprites.Paths.Bobber, new Vector2( 0.75f, 0.75f ) );
	}

	public void ShowAsHook()
	{
		_visible = true;
		SetSpriteEnabled( true );
		SwapSprite( OffshoreSprites.Paths.Hook, new Vector2( 0.55f, 0.55f ) );
	}

	public void HideAtTip()
	{
		_visible = false;
		WorldPosition = TipRestPosition;
		SetSpriteEnabled( false );
	}

	public bool IsVisible => _visible;

	private void SetSpriteEnabled( bool enabled )
	{
		if ( _sprite.IsValid() )
			_sprite.GameObject.Enabled = enabled;
	}

	private void SwapSprite( string path, Vector2 size )
	{
		if ( !_sprite.IsValid() )
			return;

		_sprite.Sprite = OffshoreSprites.MakeSprite( OffshoreSprites.Load( path ) );
		_sprite.Size = size;
		_sprite.StartingAnimationName = "Default";
	}
}
