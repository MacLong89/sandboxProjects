namespace Terraingen.UI;

/// <summary>
/// Shared HUD / debug readout updated each frame from gameplay code.
/// </summary>
public static class ThornsDebugState
{
	public static Vector3 WorldPosition { get; set; }

	public static string PositionLabel { get; set; } = "X: —  Y: —  Z: —";

	public static string FoliageLine { get; set; } = "Foliage: —";

	public static string DetailLine { get; set; } = "";

	public static string SubsystemLine { get; set; } = "";

	public static string SkyLine { get; set; } = "";

	public static string MinimapLine { get; set; } = "";
}
