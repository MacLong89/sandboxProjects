namespace SkyEmpire;

/// <summary>Third-person follow camera with a gentle overhead angle.</summary>
public sealed class PlayerCamera : Component
{
	[Property] public float Distance { get; set; } = 360f;
	[Property] public float Height { get; set; } = 160f;

	CameraComponent _camera;

	protected override void OnAwake()
	{
		_camera = Components.GetOrCreate<CameraComponent>();
		_camera.FieldOfView = 65f;
		_camera.IsMainCamera = true;
		_camera.ZFar = 20000f;
		_camera.BackgroundColor = Color.Parse( "#8fd6f2" ) ?? Color.Cyan;
	}

	protected override void OnUpdate()
	{
		var player = TycoonPlayer.Local;
		if ( !player.IsValid() )
		{
			// Slow orbit over the sky ring while connecting.
			var t = Time.Now * 0.06f;
			WorldPosition = new Vector3( MathF.Cos( t ) * 4200f, MathF.Sin( t ) * 4200f, 1900f );
			WorldRotation = Rotation.LookAt( new Vector3( 0, 0, 100f ) - WorldPosition );
			return;
		}

		var eye = player.EyeAngles;
		var rot = Rotation.From( eye );
		var target = player.WorldPosition + Vector3.Up * 55f;
		var desired = target - rot.Forward * Distance + Vector3.Up * Height;
		WorldPosition = WorldPosition.LerpTo( desired, Time.Delta * 14f );
		WorldRotation = Rotation.LookAt( target + Vector3.Up * 25f - WorldPosition );
		_camera.IsMainCamera = true;
	}
}
