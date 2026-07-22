namespace CatchACritter;

/// <summary>Third-person follow camera with a gentle overhead angle.</summary>
public sealed class PlayerCamera : Component
{
	[Property] public float Distance { get; set; } = 340f;
	[Property] public float Height { get; set; } = 150f;

	CameraComponent _camera;

	protected override void OnAwake()
	{
		_camera = Components.GetOrCreate<CameraComponent>();
		_camera.FieldOfView = 65f;
		_camera.IsMainCamera = true;
		_camera.ZFar = 16000f;
		_camera.BackgroundColor = Color.Parse( "#8fd6f2" ) ?? Color.Cyan;

		SetupPostProcessing();
	}

	/// <summary>
	/// Saturday-morning-cartoon grade. Without this the default Hable Filmic
	/// tonemapper leaves the low-poly island looking pastel and washed out.
	/// </summary>
	void SetupPostProcessing()
	{
		var tone = Components.GetOrCreate<Tonemapping>();
		tone.Mode = Tonemapping.TonemappingMode.AgX; // punchy, great outdoors
		tone.AutoExposureEnabled = true;
		tone.MinimumExposure = 1f;   // never dim below neutral on the bright island
		tone.MaximumExposure = 2f;
		tone.ExposureCompensation = 0.15f;

		var adjust = Components.GetOrCreate<ColorAdjustments>();
		adjust.Saturation = 1.22f;
		adjust.Contrast = 1.05f;
		adjust.Brightness = 1.05f;
	}

	protected override void OnUpdate()
	{
		var player = CritterPlayer.Local;
		if ( !player.IsValid() )
		{
			// Slow orbit over the island while connecting.
			var t = Time.Now * 0.08f;
			WorldPosition = new Vector3( MathF.Cos( t ) * 1900f, MathF.Sin( t ) * 1900f, 1250f );
			WorldRotation = Rotation.LookAt( new Vector3( 0, 0, 60f ) - WorldPosition );
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
