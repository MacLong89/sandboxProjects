namespace SceneLab;

/// <summary>First-person walk camera — no gameplay systems.</summary>
public sealed class SceneLabPawn : Component
{
	private PlayerController _controller;
	private GameObject _view;
	private CameraComponent _camera;
	private Angles _look;
	private int _lastBuild = -1;

	protected override void OnAwake()
	{
		_controller = Components.GetOrCreate<PlayerController>();
		_controller.UseCameraControls = false;
		_controller.UseLookControls = false;
		_controller.UseAnimatorControls = false;
		_controller.EnablePressing = false;
		_controller.BodyHeight = 72f;
		_controller.BodyRadius = 16f;
		_controller.WalkSpeed = 200f;
		_controller.RunSpeed = 360f;

		_view = new GameObject( GameObject, true, "View" );
		_view.LocalPosition = new Vector3( 0f, 0f, 64f );

		_camera = _view.Components.Create<CameraComponent>();
		_camera.FieldOfView = 80f;
		_camera.ZNear = 2f;
		_camera.ZFar = 20000f;
		_camera.BackgroundColor = new Color( 0.45f, 0.72f, 0.95f );
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
		var boot = SceneLabBoot.Instance;
		if ( boot is null )
			return;

		if ( boot.BuildGeneration != _lastBuild )
			SnapToSpawn();

		Mouse.Visibility = MouseVisibility.Hidden;

		var look = Input.AnalogLook;
		var pitch = _look.pitch - look.pitch;
		if ( pitch < -85f )
			pitch = -85f;
		else if ( pitch > 85f )
			pitch = 85f;
		_look.pitch = pitch;
		_look.yaw += look.yaw;
		_look.roll = 0f;
		ApplyLook();

		_controller.UseInputControls = true;
	}

	public void SnapToSpawn()
	{
		var boot = SceneLabBoot.Instance;
		if ( boot is null )
			return;

		_lastBuild = boot.BuildGeneration;
		GameObject.WorldPosition = boot.SpawnPosition;
		_look = new Angles( 8f, boot.SpawnYaw, 0f );
		ApplyLook();

		if ( _controller.Body.IsValid() )
			_controller.Body.Velocity = Vector3.Zero;
	}

	private void ApplyLook()
	{
		// PlayerController wish dir follows EyeAngles — must stay in sync with the camera.
		_controller.EyeAngles = _look;
		GameObject.WorldRotation = Rotation.FromYaw( _look.yaw );
		if ( _view.IsValid() )
			_view.LocalRotation = Rotation.FromAxis( Vector3.Right, _look.pitch );
	}

	private void ActivateCamera()
	{
		foreach ( var cam in Scene.GetAllComponents<CameraComponent>() )
		{
			if ( cam == _camera ) continue;
			cam.IsMainCamera = false;
		}

		if ( _camera.IsValid() )
			_camera.IsMainCamera = true;
	}
}
