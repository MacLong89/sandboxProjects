namespace Offshore;

/// <summary>
/// Side-view camera locked on the fisherman (and boat later). Player stays screen-center;
/// the world scrolls underneath â€” no directional look-ahead.
/// </summary>
public sealed class OffshoreCameraController : Component
{
	[Property] public float Distance { get; set; } = OffshoreConstants.CamDistance;

	private CameraComponent _camera;

	public HookComponent Hook { get; set; }
	public GameObject RodTip { get; set; }
	public AnglerController Player { get; set; }

	protected override void OnAwake()
	{
		_camera = Components.GetOrCreate<CameraComponent>();
		_camera.FieldOfView = OffshoreConstants.CamFov;
		_camera.ZNear = OffshoreConstants.CamZNear;
		_camera.ZFar = OffshoreConstants.CamZFar;
		_camera.BackgroundColor = new Color( 0.12f, 0.14f, 0.18f );

		foreach ( var cam in Scene.GetAllComponents<CameraComponent>() )
		{
			if ( cam != _camera )
				cam.IsMainCamera = false;
		}

		_camera.IsMainCamera = true;
	}

	protected override void OnUpdate()
	{
		if ( !_camera.IsValid() )
			return;

		var anchor = ResolveAnchor();
		var focusX = anchor.x;
		// Keep a fixed horizon band so world Z tweaks (dock / player height) show as real screen offsets.
		var focusZ = OffshoreConstants.CamBaseZ;

		// While the hook is out, bias a little toward it horizontally — keep Z locked (no sink).
		if ( Hook is not null && Hook.IsVisible )
		{
			var hook = Hook.WorldPosition;
			focusX = MathX.Lerp( focusX, hook.x, OffshoreConstants.CamLookBias * 0.35f );
		}

		var focus = new Vector3( focusX, 0f, focusZ );
		// Camera on -Y so world +X reads as screen-right.
		WorldPosition = new Vector3( focus.x, -Distance, focus.z );
		var lookAt = focus;

		var dir = lookAt - WorldPosition;
		if ( dir.Length > 0.001f )
			WorldRotation = Rotation.LookAt( dir.Normal, Vector3.Up );
	}

	private Vector3 ResolveAnchor()
	{
		if ( Player is not null && Player.IsValid() )
			return Player.FollowPoint;

		if ( RodTip is not null && RodTip.IsValid() )
			return RodTip.WorldPosition;

		return new Vector3( OffshoreConstants.CamDockAnchorX, 0f, OffshoreConstants.CamBaseZ );
	}
}
