namespace Offshore;

/// <summary>
/// Underwater host — seafloor flora, swimming fish schools, and seabirds.
/// </summary>
public sealed class UnderwaterDecor : Component
{
	protected override void OnStart()
	{
		WorldPosition = Vector3.Zero;
		Components.Create<SeafloorFlora>();
		Components.Create<AmbientFishSchool>();
	}
}
