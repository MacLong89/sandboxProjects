namespace Fauna2;

using Fauna2.UI;

/// <summary>
/// Top-down orthographic camera — follows the local player, scroll zoom, no rotation.
/// Square tiles read as squares (no diamond oblique view).
/// </summary>
public sealed class ZooCameraController : Component
{
	public static ZooCameraController Instance { get; private set; }

	[Property] public float MinOrthoHeight { get; set; }
	[Property] public float MaxOrthoHeight { get; set; }
	[Property] public float ZoomSpeed { get; set; } = 0.15f;
	[Property] public float FollowDistance { get; set; } = 4000f;

	public Vector3 FocusPoint { get; private set; }

	private CameraComponent _camera;
	private bool _screenPanelsBound;
	private float _yaw = GameConstants.CameraYaw;
	private float _pitch = GameConstants.CameraPitch;
	private float _targetOrthoHeight = GameConstants.CameraOrthoHeight;
	private float _orthoHeight = GameConstants.CameraOrthoHeight;

	protected override void OnAwake()
	{
		Instance = this;
		FocusPoint = Vector3.Zero;

		MinOrthoHeight = GameConstants.CameraMinOrthoHeight;
		MaxOrthoHeight = GameConstants.CameraMaxOrthoHeight;
		_targetOrthoHeight = _targetOrthoHeight.Clamp( MinOrthoHeight, MaxOrthoHeight );
		_orthoHeight = _orthoHeight.Clamp( MinOrthoHeight, MaxOrthoHeight );

		_camera = Components.GetOrCreate<CameraComponent>();
		_camera.Orthographic = true;
		_camera.IsMainCamera = true;
		_camera.OrthographicHeight = _orthoHeight;
		_camera.BackgroundColor = new Color( 0.45f, 0.58f, 0.42f );

		Fauna2Debug.Info( "Camera", $"OnAwake pos={GameObject.WorldPosition} ortho={_camera.Orthographic} " +
			$"orthoH={_orthoHeight:0.#} main={_camera.IsMainCamera} bg={_camera.BackgroundColor}" );

		UiWorldProjection.BindScreenPanelCamera( Scene, _camera );
		_screenPanelsBound = true;
	}

	protected override void OnUpdate()
	{
		if ( !_screenPanelsBound && _camera.IsValid() )
		{
			UiWorldProjection.BindScreenPanelCamera( Scene, _camera );
			_screenPanelsBound = true;
		}

		var gameStarted = GameManager.Instance?.GameStarted == true;

		Mouse.Visibility = MouseVisibility.Visible;

		var player = PlayerState.Local;
		var playerFocus = player.IsValid() && gameStarted ? player.FeetPosition : Vector3.Zero;

		var wheel = Input.MouseWheel.y;
		var zoomingIn = wheel > 0.001f;
		if ( MathF.Abs( wheel ) > 0.001f )
		{
			var zoomFactor = 1f - wheel * ZoomSpeed;
			_targetOrthoHeight = (_targetOrthoHeight * zoomFactor).Clamp( MinOrthoHeight, MaxOrthoHeight );
		}

		_orthoHeight = _orthoHeight.LerpTo( _targetOrthoHeight, Time.Delta * 8f );
		if ( _camera.IsValid() )
			_camera.OrthographicHeight = _orthoHeight;

		FocusPoint = ResolveFocusPoint( playerFocus, _orthoHeight, zoomingIn );

		var rot = Rotation.From( _pitch, _yaw, 0 );
		GameObject.WorldRotation = rot;
		// Keep the player on the view axis — placing the camera straight above focus
		// shifts the screen center when the camera is pitched (Stardew-style oblique view).
		GameObject.WorldPosition = FocusPoint - rot.Forward * FollowDistance;

		if ( player.IsValid() && gameStarted && MathF.Abs( wheel ) > 0.001f )
		{
			CorrectFocusToPlayer( playerFocus );
			GameObject.WorldPosition = FocusPoint - rot.Forward * FollowDistance;
		}

		if ( !_camLog )
		{
			_camLog = 5f;
			Fauna2Debug.Info( "Camera",
				$"follow focus={FocusPoint} camPos={GameObject.WorldPosition} orthoH={_orthoHeight:0.#} " +
				$"gameStarted={gameStarted} player={player.IsValid()}" );
		}
	}

	/// <summary>Follow the player; only clamp when zoomed out so the view does not leave the world.</summary>
	private static Vector3 ResolveFocusPoint( Vector3 playerFocus, float orthoHeight, bool zoomingIn )
	{
		var clamped = ClampFocusToWorldView( playerFocus, orthoHeight );

		if ( !zoomingIn )
			return clamped;

		if ( clamped.Distance( playerFocus ) <= 1f )
			return playerFocus;

		// Zooming in — pull focus to the player so scroll always recenters on them.
		var snap = 1f - MathF.Exp( -Time.Delta * 24f );
		return Vector3.Lerp( clamped, playerFocus, snap );
	}

	/// <summary>Keep the view inside the wilderness square at the current zoom level.</summary>
	private static Vector3 ClampFocusToWorldView( Vector3 focus, float orthoHeight )
	{
		var aspect = Screen.Aspect > 0.01f ? Screen.Aspect : 16f / 9f;
		var halfX = MathF.Max( orthoHeight * aspect, 1f );
		var halfY = MathF.Max( orthoHeight, 1f );
		var worldHalf = GameConstants.WorldHalfExtent;

		var maxX = MathF.Max( worldHalf - halfX, 0f );
		var maxY = MathF.Max( worldHalf - halfY, 0f );

		return new Vector3(
			focus.x.Clamp( -maxX, maxX ),
			focus.y.Clamp( -maxY, maxY ),
			focus.z );
	}

	/// <summary>Pitch + ortho zoom can drift the ground point under screen center — nudge focus back to the player.</summary>
	private void CorrectFocusToPlayer( Vector3 playerFeet )
	{
		if ( !_camera.IsValid() )
			return;

		var screenCenter = new Vector2( Screen.Width * 0.5f, Screen.Height * 0.5f );
		var ray = _camera.ScreenPixelToRay( screenCenter );
		if ( MathF.Abs( ray.Forward.z ) < 0.0001f )
			return;

		var t = (playerFeet.z - ray.Position.z) / ray.Forward.z;
		if ( t < 0f )
			return;

		var groundUnderCenter = ray.Project( t );
		var delta = playerFeet.WithZ( 0 ) - groundUnderCenter.WithZ( 0 );
		if ( delta.LengthSquared < 4f )
			return;

		FocusPoint = ClampFocusToWorldView( FocusPoint + delta, _orthoHeight );
	}

	protected override void OnDestroy()
	{
		if ( Instance == this ) Instance = null;
	}

	private TimeUntil _camLog;

	public float GetOrthoHeight() => _orthoHeight;
}
