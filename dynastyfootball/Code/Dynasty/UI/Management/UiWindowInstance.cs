namespace Dynasty.UI.Management;

public sealed class UiWindowInstance
{
	public UiWindowType Type { get; init; }
	public UiWindowDefinition Definition { get; init; }
	public object Payload { get; set; }
	public int StackOrder { get; set; }
	public DateTime OpenedUtc { get; init; } = DateTime.UtcNow;
	public Action OnDismiss { get; set; }
}
