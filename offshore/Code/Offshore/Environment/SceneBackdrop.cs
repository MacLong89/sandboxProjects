namespace Offshore;

/// <summary>
/// Scene art host. Layers stack here: sky → ocean → (dock/props later).
/// </summary>
public sealed class SceneBackdrop : Component
{
	protected override void OnStart()
	{
		WorldPosition = Vector3.Zero;
		Components.Create<SkyLayer>();
		Components.Create<OceanLayer>();
	}
}
