namespace PawnShop;

/// <summary>Rotates its GameObject around local Z. Used for the ceiling fan.</summary>
public sealed class Spinner : Component
{
	[Property] public float DegreesPerSecond { get; set; } = 90f;

	protected override void OnUpdate()
	{
		LocalRotation = LocalRotation.RotateAroundAxis( Vector3.Up, DegreesPerSecond * Time.Delta );
	}
}
