namespace Fauna2;

/// <summary>Animated player billboard synced to movement and facing.</summary>
public sealed class ZooPlayerVisual : Component
{
	private SpriteRenderer _sprite;
	private ZooPlayerController _controller;
	private PlayerFacing _facing = PlayerFacing.Down;
	private bool _loggedScale;
	private bool _loggedDelayedDiag;
	private TimeUntil _delayedDiag;

	protected override void OnStart()
	{
		_controller = Components.Get<ZooPlayerController>();
		BuildSprite( _controller?.Facing ?? PlayerFacing.Down );
		ScheduleDelayedDiag();
	}

	private void ScheduleDelayedDiag() => _delayedDiag = 2f;

	protected override void OnUpdate()
	{
		UpdateFacing();

		if ( _loggedDelayedDiag || _delayedDiag ) return;

		if ( GameManager.Instance is null || !GameManager.Instance.GameStarted )
		{
			ScheduleDelayedDiag();
			return;
		}

		_loggedDelayedDiag = true;
		Fauna2RenderDiagnostics.LogRendererState( "player-after-start", _sprite );
	}

	private void UpdateFacing()
	{
		if ( _controller is null )
			_controller = Components.Get<ZooPlayerController>();
		if ( _controller is null )
			return;

		var facing = _controller.Facing;
		if ( facing == _facing && _sprite.IsValid() )
			return;

		_facing = facing;
		BuildSprite( facing );
	}

	private void BuildSprite( PlayerFacing facing )
	{
		var worldSize = GameConstants.Tiles( GameConstants.PlayerSpriteTiles );
		var sprite = PixelArt.PlayerSpriteResource( facing );

		if ( !_sprite.IsValid() )
		{
			_sprite = WorldSprites.Spawn(
				GameObject,
				sprite,
				worldSize,
				new Vector3( 0, 0, 2f ),
				"PlayerSprite",
				layer: WorldSprites.PlayerLayer,
				dynamicDepthSort: true,
				sourcePixels: PixelArt.SuppliedSpriteSourcePixels,
				movementRoot: GameObject,
				walkAnimator: true );

			var animator = _sprite.GameObject.Components.Get<SpriteWalkAnimator>();
			if ( animator is not null )
				animator.PlayerOwnedFacing = true;
		}
		else
		{
			_sprite.Sprite = sprite;
			_sprite.StartingAnimationName = PixelArt.IdleAnimationName;
			_sprite.PlayAnimation( PixelArt.IdleAnimationName );
		}

		if ( !_loggedScale )
		{
			_loggedScale = true;
			Log.Info( $"[Fauna2 Scale] Player uses animated supplied sprite '{SuppliedSpriteManifest.PlayerSpritePath}', size={GameConstants.PlayerSpriteTiles:0.##} tiles ({worldSize:0.##} world units), layer={WorldSprites.PlayerLayer:0.##}." );
			if ( _sprite.IsValid() )
				Log.Info( $"[Fauna2 Render] Player sprite renderer size={_sprite.Size}, worldPos={_sprite.GameObject.WorldPosition}, enabled={_sprite.Enabled}." );
		}
	}
}
