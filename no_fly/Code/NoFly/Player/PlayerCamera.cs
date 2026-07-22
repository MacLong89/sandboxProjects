namespace NoFly;

/// <summary>
/// First-person eye camera for the local player.
/// </summary>
public sealed class PlayerCamera : Component
{
	[Property] public float EyeHeight { get; set; } = 64f;
	[Property] public float Fov { get; set; } = 80f;

	CameraComponent _camera;

	protected override void OnAwake()
	{
		_camera = Components.GetOrCreate<CameraComponent>();
		_camera.FieldOfView = Fov;
		_camera.IsMainCamera = true;
		_camera.ZNear = 1f;
		_camera.ZFar = 14000f;
	}

	protected override void OnUpdate()
	{
		var player = NoFlyGame.LocalPlayer;
		if ( !player.IsValid() )
		{
			// Lobby free-cam near entrance
			WorldPosition = new Vector3( -150, -280, 180 );
			WorldRotation = Rotation.LookAt( new Vector3( 400, 200, -40 ) );
			return;
		}

		var eye = player.EyeAngles;
		WorldPosition = player.WorldPosition + Vector3.Up * EyeHeight;
		WorldRotation = Rotation.From( eye );
		_camera.FieldOfView = Fov;
		_camera.IsMainCamera = true;
	}
}
