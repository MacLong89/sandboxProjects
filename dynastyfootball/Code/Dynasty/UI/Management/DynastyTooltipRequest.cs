namespace Dynasty.UI.Management;

public sealed class DynastyTooltipRequest
{
	public string Text { get; init; }
	public float ScreenX { get; init; }
	public float ScreenY { get; init; }
	public string Anchor { get; init; } = "top-left";
}
