namespace UnderPressure;

/// <summary>First-person walk camera for the level viewer — no washer, van, or combat.</summary>
public sealed class LevelViewerPawn : Component
{
	private PlayerController _controller;
	private GameObject _view;
	private CameraComponent _camera;
	private Angles _look;
	private int _lastLoadGeneration = -1;

	protected override void OnAwake()
	{
		_controller = Components.GetOrCreate<PlayerController>();
		_controller.UseCameraControls = false;
		_controller.UseLookControls = false;
		_controller.UseAnimatorControls = false;
		_controller.EnablePressing = false;
		_controller.BodyHeight = 72f;
		_controller.BodyRadius = 16f;
		_controller.WalkSpeed = GameConstants.WalkSpeedBase;
		_controller.RunSpeed = GameConstants.WalkSpeedBase * GameConstants.RunMultiplier;

		_view = new GameObject( GameObject, true, "View" );
		_view.LocalPosition = new Vector3( 0f, 0f, GameConstants.EyeHeight );

		_camera = _view.Components.Create<CameraComponent>();
		_camera.FieldOfView = GameConstants.FieldOfView;
		_camera.ZNear = 2f;
		_camera.ZFar = 12000f;
		_camera.BackgroundColor = new Color( 0.42f, 0.74f, 0.98f );
		_camera.EnablePostProcessing = true;
	}

	protected override void OnStart()
	{
		ActivateCamera();
		SnapToSpawn();
	}

	protected override void OnDestroy()
	{
		Mouse.Visibility = MouseVisibility.Visible;
	}

	protected override void OnUpdate()
	{
		var viewer = LevelViewer.Instance;
		if ( viewer is null )
			return;

		if ( viewer.LoadGeneration != _lastLoadGeneration )
			SnapToSpawn();

		Mouse.Visibility = MouseVisibility.Hidden;

		var look = Input.AnalogLook;
		_look.pitch = Math.Clamp( _look.pitch - look.pitch, -85f, 85f );
		_look.yaw += look.yaw;
		_look.roll = 0f;
		ApplyLook();

		_controller.UseInputControls = true;
	}

	public void SnapToSpawn()
	{
		var viewer = LevelViewer.Instance;
		if ( viewer is null )
			return;

		_lastLoadGeneration = viewer.LoadGeneration;
		GameObject.WorldPosition = viewer.SpawnPosition + Vector3.Up * 8f;
		_look = new Angles( 10f, viewer.SpawnYaw, 0f );
		ApplyLook();

		if ( _controller.Body.IsValid() )
			_controller.Body.Velocity = Vector3.Zero;
	}

	private void ApplyLook()
	{
		_controller.EyeAngles = _look;
		GameObject.WorldRotation = Rotation.FromYaw( _look.yaw );
		_view.LocalRotation = Rotation.FromAxis( Vector3.Right, _look.pitch );
	}

	private void ActivateCamera()
	{
		foreach ( var cam in Scene.GetAllComponents<CameraComponent>() )
		{
			if ( cam == _camera ) continue;
			cam.IsMainCamera = false;
		}

		_camera.IsMainCamera = true;
	}
}
