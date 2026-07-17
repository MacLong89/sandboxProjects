namespace FinalOutpost;

/// <summary>
/// Fixed-angle isometric RTS camera over the outpost, matching the Clash of Clans feel:
/// the view angle never rotates. The player can only pan and zoom.
/// </summary>
public sealed class OutpostCamera : Component
{
	public static OutpostCamera Instance { get; private set; }

	// Fixed Clash-of-Clans-style isometric framing.
	private const float FixedYaw = 45f;
	private const float FixedPitch = 55f;

	private CameraComponent _camera;
	private float _distance = 950f;
	private Vector3 _focus = Vector3.Zero;

	protected override void OnAwake()
	{
		Instance = this;
		_camera = Components.GetOrCreate<CameraComponent>();
		_camera.FieldOfView = GameConstants.CameraFov;
		_camera.ZNear = 10f;
		_camera.ZFar = 20000f;
		_camera.BackgroundColor = new Color( 0.55f, 0.78f, 0.95f );
		_camera.IsMainCamera = true;
	}

	protected override void OnDestroy()
	{
		if ( Instance == this ) Instance = null;
		Mouse.Visibility = MouseVisibility.Visible;
	}

	protected override void OnStart()
	{
		Log.Info( "[FinalOutpost] OutpostCamera OnStart" );
		foreach ( var sceneCam in Scene.GetAllComponents<CameraComponent>() )
		{
			if ( sceneCam == _camera ) continue;
			sceneCam.IsMainCamera = false;
			if ( sceneCam.GameObject.Name is "StartupCamera" or "FallbackCamera" )
				sceneCam.GameObject.Destroy();
		}

		_focus = Vector3.Zero;
		ApplyTransform();
		Mouse.Visibility = MouseVisibility.Visible;
		BindHudCamera();
		Log.Info( $"[FinalOutpost] OutpostCamera ready pos={WorldPosition} rot={WorldRotation}" );
	}

	protected override void OnUpdate()
	{
		var core = GameCore.Instance;
		if ( core?.IsUiBlocking != true )
		{
			TickPan();
			TickZoom();
		}

		ApplyTransform();
		BindHudCamera();
	}

	private void BindHudCamera()
	{
		if ( _camera is null || !_camera.IsMainCamera )
			return;

		// Always follow the active main camera — boot HUD may have bound to FallbackCamera first.
		foreach ( var screen in Scene.GetAllComponents<ScreenPanel>() )
			screen.TargetCamera = _camera;
	}

	public bool ScreenToGround( Vector2 screen, out Vector3 ground )
	{
		ground = default;
		if ( _camera is null ) return false;

		var ray = _camera.ScreenPixelToRay( screen );
		if ( MathF.Abs( ray.Forward.z ) < 0.0001f ) return false;

		var t = -ray.Position.z / ray.Forward.z;
		if ( t < 0f ) return false;

		ground = ray.Position + ray.Forward * t;
		return true;
	}

	public Vector2 MouseGround => ScreenToGround( Mouse.Position, out var g ) ? new Vector2( g.x, g.y ) : Vector2.Zero;

	private void TickPan()
	{
		var pan = Vector3.Zero;

		if ( Input.Down( "Forward" ) ) pan += Vector3.Forward;
		if ( Input.Down( "Backward" ) ) pan += Vector3.Backward;
		if ( Input.Down( "Left" ) ) pan += Vector3.Left;
		if ( Input.Down( "Right" ) ) pan += Vector3.Right;

		// Middle-mouse drag pan.
		if ( Input.Down( "Run" ) && Mouse.Delta.Length > 0.5f )
		{
			var rot = Rotation.FromYaw( FixedYaw );
			pan += -rot.Right * Mouse.Delta.x * 0.35f;
			pan += rot.Forward.WithZ( 0f ).Normal * Mouse.Delta.y * 0.35f;
		}

		if ( pan.Length > 0.01f )
		{
			var rot = Rotation.FromYaw( FixedYaw );
			var move = rot * pan.WithZ( 0f ).Normal * GameConstants.CameraPanSpeed * Time.Delta;
			_focus += move;

			var limit = GameConstants.ActiveTerrainHalfExtent - 150f;
			_focus = _focus.WithX( Math.Clamp( _focus.x, -limit, limit ) )
				.WithY( Math.Clamp( _focus.y, -limit, limit ) );
		}
	}

	private void TickZoom()
	{
		var scroll = Input.MouseWheel.y;
		if ( MathF.Abs( scroll ) < 0.01f ) return;

		_distance = Math.Clamp( _distance - scroll * GameConstants.CameraZoomSpeed, GameConstants.CameraMinDistance, GameConstants.CameraMaxDistance );
	}

	private void ApplyTransform()
	{
		var rot = Rotation.From( new Angles( FixedPitch, FixedYaw, 0f ) );
		WorldPosition = _focus - rot.Forward * _distance;
		WorldRotation = rot;
	}
}
