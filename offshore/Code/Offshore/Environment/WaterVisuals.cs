namespace Offshore;

/// <summary>
/// Legacy water tint hook. SceneBackdrop now owns the sky/water plate; this stays for
/// optional future layered foam strips without checkered mesh materials.
/// </summary>
public sealed class WaterVisuals : Component
{
	protected override void OnStart()
	{
		// Intentionally empty — opaque sunrise/water background is SceneBackdrop.
	}
}
