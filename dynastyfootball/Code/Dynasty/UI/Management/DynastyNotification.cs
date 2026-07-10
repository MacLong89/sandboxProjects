namespace Dynasty.UI.Management;

public sealed class DynastyNotification
{
	public Guid Id { get; init; } = Guid.NewGuid();
	public string Message { get; init; }
	public bool IsError { get; init; }
	public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;
	public int DurationMs { get; init; } = 5000;
}
