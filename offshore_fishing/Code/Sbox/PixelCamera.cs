namespace Sandbox;

/// <summary>Simple clear-color camera; gameplay art is full-screen UI.</summary>
[Title( "Pixel Camera" )]
public sealed class PixelCamera : Component
{
	[Property] public float OrthoHeight { get; set; } = 360f;
	[Property] public Color ClearColor { get; set; } = new( 0.05f, 0.12f, 0.16f );

	CameraComponent _camera;

	protected override void OnStart() => Apply();
	protected override void OnUpdate() => Apply();

	void Apply()
	{
		_camera ??= Components.Get<CameraComponent>( true ) ?? Components.Create<CameraComponent>();
		_camera.IsMainCamera = true;
		_camera.Orthographic = true;
		_camera.OrthographicHeight = OrthoHeight;
		_camera.ZNear = 1f;
		_camera.ZFar = 5000f;
		_camera.BackgroundColor = ClearColor;
		WorldPosition = new Vector3( 0f, -500f, 0f );
		WorldRotation = Rotation.LookAt( Vector3.Left, Vector3.Up );
	}
}
