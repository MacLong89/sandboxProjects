namespace Terraingen.TerrainGen;

/// <summary>
/// Simple fly camera for terrain prototype review (no gameplay).
/// </summary>
[Title( "Terrain Fly Camera" )]
[Category( "Terrain" )]
[Icon( "videocam" )]
public sealed class TerrainFlyCamera : Component
{
	[Property] public float MoveSpeed { get; set; } = 1200f;
	[Property] public float FastMultiplier { get; set; } = 4f;
	[Property] public float LookSensitivity { get; set; } = 0.15f;

	Angles _viewAngles;

	protected override void OnEnabled()
	{
		_viewAngles = WorldRotation.Angles();
		Mouse.Visibility = MouseVisibility.Hidden;
	}

	protected override void OnDisabled()
	{
		Mouse.Visibility = MouseVisibility.Visible;
	}

	protected override void OnUpdate()
	{
		_viewAngles += Input.AnalogLook * LookSensitivity;
		_viewAngles.pitch = _viewAngles.pitch.Clamp( -89f, 89f );
		WorldRotation = _viewAngles.ToRotation();

		var move = Input.AnalogMove;
		var speed = MoveSpeed * (Input.Down( "run" ) ? FastMultiplier : 1f);
		var wish = WorldRotation * move.WithZ( Input.Down( "jump" ) ? 1f : Input.Down( "duck" ) ? -1f : 0f );
		WorldPosition += wish * speed * Time.Delta;
	}
}
