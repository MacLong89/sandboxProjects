namespace Dynasty.UI.Management;

public sealed class UiWindowDefinition
{
	public UiWindowType Type { get; init; }
	public string LayerId { get; init; }
	public DynastyUiPriority Priority { get; init; }
	public UiWindowGroup Group { get; init; }
	public UiScreenRegion Region { get; init; }
	public DynastyUiInputContext InputContext { get; init; }
	public bool IsModal { get; init; } = true;
	public bool BlocksHudInput { get; init; } = true;
	public bool DismissOnEscape { get; init; } = true;
	public bool DismissOnBackdrop { get; init; }
	public bool SuppressesHud { get; init; } = true;
	public bool AllowsNotifications { get; init; } = true;
	public bool AllowsTooltips { get; init; } = true;
}
