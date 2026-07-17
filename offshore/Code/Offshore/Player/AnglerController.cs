namespace Offshore;

/// <summary>
/// Controllable angler. Side-scroller on foot: A/D in world X.
/// Avatar is world-anchored (not glued to the camera), so the dock can scroll away.
/// Boarding keeps the empty boat sprite at dock size and seats the fisherman on it.
/// </summary>
public sealed class AnglerController : Component
{
	public enum LocomotionMode
	{
		OnFoot,
		InBoat
	}
	private const int AnimIdle = 0;

	private const int AnimWalk = 1;

	private const int AnimCastCharge = 2;

	private const int AnimCastRelease = 3;

	[Property] public float WalkSpeed { get; set; } = OffshoreConstants.PlayerWalkSpeed;
	[Property] public float BoatSpeed { get; set; } = OffshoreConstants.BoatMoveSpeed;
	public LocomotionMode Mode { get; private set; } = LocomotionMode.OnFoot;

	public GameObject Avatar { get; private set; }

	public GameObject RodTip { get; set; }

	public Vector3 Velocity { get; private set; }

	/// <summary>Soft outbound cap while trip fuel depletes (set by BoatBoardController).</summary>
	public float BoatOutboardLimitX { get; set; } = OffshoreConstants.PlayerMaxX;

	/// <summary>World point the camera should track (Mario-style follow).</summary>
	public Vector3 FollowPoint => WorldPosition + new Vector3( 0f, 0f, Mode == LocomotionMode.InBoat ? 0.6f : 0.4f );

	private SpriteRenderer _sprite;

	private GameObject _boardedAvatar;

	private SpriteRenderer _boardedSprite;

	private string _boardedSpritePath = "";

	private float _facing = 1f;

	private int _animIndex = -1;

	private BoatDefinition _boardedBoat;

	private bool _usingBoatSprite;

	public void BindAvatar( GameObject avatar, SpriteRenderer sprite )
	{
		Avatar = avatar;
		_sprite = sprite;
		if ( _sprite is not null && _sprite.IsValid() )
		{
			_sprite.Size = new Vector2( OffshoreConstants.PlayerSpriteWidth, OffshoreConstants.PlayerSpriteHeight );
			_sprite.StartingAnimationName = "Idle";
			_animIndex = AnimIdle;
		}
	}
	public void EnterBoat( BoatDefinition boat )
	{
		_boardedBoat = boat;
		Mode = LocomotionMode.InBoat;
		BoatSpeed = boat?.MoveSpeed ?? OffshoreConstants.BoatMoveSpeed;
		BoatOutboardLimitX = OffshoreConstants.BoatMooringX + (boat?.TripRange ?? 28f);
		WorldPosition = new Vector3(
			OffshoreConstants.BoatMooringX,
			OffshoreConstants.FisherPlaneY,
			OffshoreConstants.BoatMooringZ );
		ApplyBoatAvatar( boat );
	}
	public void ExitBoatToDock()
	{
		Mode = LocomotionMode.OnFoot;
		_boardedBoat = null;
		BoatOutboardLimitX = OffshoreConstants.PlayerMaxX;
		WorldPosition = new Vector3(
			Math.Clamp( OffshoreConstants.PlayerStartX, OffshoreConstants.PlayerMinX, OffshoreConstants.PlayerMaxX ),
			OffshoreConstants.FisherPlaneY,
			OffshoreConstants.PlayerStartZ );
		RestoreFishermanAvatar();
	}
	protected override void OnUpdate()
	{
		var game = OffshoreGameController.Instance;
		if ( game is null )
			return;
		if ( CanMove( game ) )
			TickMove( game );
		else
			Velocity = Vector3.Zero;
		TickAnimation( game );
		SyncRodTip();
		ApplyFacing();
	}
	private static bool CanMove( OffshoreGameController game )
	{
		if ( game.StateMachine.BlocksGameplayInput )
			return false;
		return game.State is FishingSessionState.DockIdle
			or FishingSessionState.AimingCast
			or FishingSessionState.FishingFromBoat;
	}
	private void TickMove( OffshoreGameController game )
	{
		// s&box AnalogMove: .x = Forward/Back (W/S), .y = Left/Right (A/D)
		var analog = Input.AnalogMove;
		var wish = Vector3.Zero;
		// Side view: A/D along shore <-> sea (A = -X / left, D = +X / right on screen).
		wish.x = -analog.y;
		if ( wish.Length > 1f )
			wish = wish.Normal;
		var speed = Mode == LocomotionMode.InBoat ? BoatSpeed : WalkSpeed;
		Velocity = wish * speed;
		var next = WorldPosition + Velocity * Time.Delta;
		next.y = OffshoreConstants.FisherPlaneY;
		if ( Mode == LocomotionMode.InBoat )
		{
			next.z = OffshoreConstants.BoatMooringZ;
			var minX = OffshoreConstants.DockExitZoneMinX;
			var maxX = Math.Min( OffshoreConstants.PlayerMaxX, BoatOutboardLimitX );
			next.x = Math.Clamp( next.x, minX, maxX );
		}
		else
		{
			next.z = OffshoreConstants.PlayerStartZ;
			next.x = Math.Clamp( next.x, OffshoreConstants.PlayerMinX, OffshoreConstants.BoatZoneMaxX );
		}
		WorldPosition = next;
		if ( MathF.Abs( Velocity.x ) > 0.05f )
			_facing = MathF.Sign( Velocity.x );
	}
	private void TickAnimation( OffshoreGameController game )
	{
		if ( _sprite is null || !_sprite.IsValid() )
			return;
		if ( Mode == LocomotionMode.InBoat )
		{
			// Keep casting poses while fishing; otherwise idle in the boat (no walk).
			var anim = game.State switch
			{
				FishingSessionState.ChargingCast => AnimCastCharge,
				FishingSessionState.Casting => AnimCastRelease,
				FishingSessionState.HookInWater
					or FishingSessionState.WaitingForBite
					or FishingSessionState.BiteWindow
					or FishingSessionState.FishHooked
					or FishingSessionState.Reeling
					or FishingSessionState.CatchSuccess => AnimCastRelease,
				_ => AnimIdle
			};
			PlayAnim( anim );
			return;
		}
		PlayAnim( ResolveAnim( game ) );
	}
	private int ResolveAnim( OffshoreGameController game )
	{
		return game.State switch
		{
			FishingSessionState.ChargingCast => AnimCastCharge,
			FishingSessionState.Casting => AnimCastRelease,
			FishingSessionState.HookInWater
				or FishingSessionState.WaitingForBite
				or FishingSessionState.BiteWindow
				or FishingSessionState.FishHooked
				or FishingSessionState.Reeling
				or FishingSessionState.CatchSuccess => AnimCastRelease,
			_ when MathF.Abs( Velocity.x ) > 0.15f => AnimWalk,
			_ => AnimIdle
		};
	}
	private void PlayAnim( int index )
	{
		if ( _sprite is null || !_sprite.IsValid() )
			return;
		if ( index == _animIndex )
			return;
		_animIndex = index;
		_sprite.PlayAnimation( index );
		_sprite.PlaybackSpeed = 1f;
	}
	private void ApplyBoatAvatar( BoatDefinition boat )
	{
		if ( boat is null )
		{
			Log.Warning( "[Offshore Boat] ApplyBoatAvatar called with null boat" );
			return;
		}

		BoatCatalog.NormalizeSpritePaths( boat );
		// Same empty-boat plate + world size as the dock mooring — fisherman sits on top.
		var path = boat.DockSpritePath;
		var hasTex = OffshoreSprites.HasTexture( path );
		EnsureBoardedAvatar();
		var tex = OffshoreSprites.Load( path );
		var size = OffshoreSprites.BoatWorldSize( path, tex, worldHeight: OffshoreConstants.BoatWorldHeight );
		OffshoreSprites.ApplyBoatSprite( _boardedSprite, _boardedAvatar, tex, size, flipHorizontal: _facing < 0f );
		_boardedAvatar.Enabled = true;
		_boardedAvatar.LocalPosition = Vector3.Zero;

		if ( Avatar is not null && Avatar.IsValid() )
		{
			Avatar.Enabled = true;
			Avatar.LocalPosition = new Vector3( 0f, OffshoreConstants.BoatSeatOffsetY, OffshoreConstants.BoatSeatOffsetZ );
		}

		if ( _sprite is not null && _sprite.IsValid() )
		{
			_sprite.Sprite = OffshoreSprites.MakeFishermanSprite();
			_sprite.StartingAnimationName = "Idle";
			_sprite.Size = new Vector2( OffshoreConstants.PlayerSpriteWidth, OffshoreConstants.PlayerSpriteHeight );
		}

		_boardedSpritePath = path;
		_usingBoatSprite = true;
		_animIndex = -1;
		PlayAnim( AnimIdle );
		Log.Info(
			$"[Offshore Boat] BOARD id={boat.Id} path='{path}' hasTex={hasTex} size={size} (same as dock + seated angler)" );
	}

	private void RestoreFishermanAvatar()
	{
		Log.Info(
			$"[Offshore Boat] EXIT avatar restore prevBoarded='{_boardedSpritePath}' " +
			$"playerPos={WorldPosition}" );
		if ( _boardedAvatar is not null && _boardedAvatar.IsValid() )
			_boardedAvatar.Enabled = false;
		if ( Avatar is not null && Avatar.IsValid() )
		{
			Avatar.Enabled = true;
			Avatar.LocalPosition = Vector3.Zero;
		}
		if ( _sprite is not null && _sprite.IsValid() )
		{
			_sprite.Sprite = OffshoreSprites.MakeFishermanSprite();
			_sprite.StartingAnimationName = "Idle";
			_sprite.Size = new Vector2( OffshoreConstants.PlayerSpriteWidth, OffshoreConstants.PlayerSpriteHeight );
		}
		_usingBoatSprite = false;
		_boardedBoat = null;
		_boardedSpritePath = "";
		_animIndex = -1;
		PlayAnim( AnimIdle );
	}
	private void EnsureBoardedAvatar()
	{
		if ( _boardedAvatar is not null && _boardedAvatar.IsValid() && _boardedSprite is not null && _boardedSprite.IsValid() )
			return;

		_boardedAvatar = new GameObject( GameObject, true, "BoardedBoatAvatar" );
		_boardedAvatar.LocalPosition = Vector3.Zero;
		_boardedSprite = _boardedAvatar.Components.Create<SpriteRenderer>();
		OffshoreSprites.ConfigureBoatRenderer( _boardedSprite );
		_boardedAvatar.Enabled = false;
	}

	private void SyncRodTip()
	{
		if ( RodTip is null || !RodTip.IsValid() )
			return;

		var tipOffset = new Vector3(
			OffshoreConstants.RodTipOffsetX * _facing,
			OffshoreConstants.RodTipOffsetY,
			OffshoreConstants.RodTipOffsetZ + (Mode == LocomotionMode.InBoat ? 0.15f : 0f) );
		RodTip.WorldPosition = WorldPosition + tipOffset;
	}

	private void ApplyFacing()
	{
		if ( _usingBoatSprite )
		{
			if ( _boardedAvatar is not null && _boardedAvatar.IsValid() )
			{
				_boardedAvatar.LocalScale = Vector3.One;
				_boardedAvatar.LocalRotation = Rotation.Identity;
			}

			if ( _boardedSprite is not null && _boardedSprite.IsValid() )
				_boardedSprite.FlipHorizontal = _facing < 0f;
		}

		if ( Avatar is null || !Avatar.IsValid() )
			return;

		Avatar.LocalScale = new Vector3( _facing >= 0f ? 1f : -1f, 1f, 1f );
	}
}
