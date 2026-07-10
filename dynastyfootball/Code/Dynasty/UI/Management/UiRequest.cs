namespace Dynasty.UI.Management;

/// <summary>
/// Data-driven UI command. All open/close operations must use this type.
/// </summary>
public sealed class UiRequest
{
	public UiRequestAction Action { get; init; }
	public UiWindowType Window { get; init; }
	public object Payload { get; init; }
	public bool Force { get; init; }

	public static UiRequest Open( UiWindowType window, object payload = null, bool force = false ) => new()
	{
		Action = UiRequestAction.Open,
		Window = window,
		Payload = payload,
		Force = force
	};

	public static UiRequest Close( UiWindowType window ) => new()
	{
		Action = UiRequestAction.Close,
		Window = window
	};

	public static UiRequest CloseTopmost() => new() { Action = UiRequestAction.CloseTopmost };

	public static UiRequest CloseAll() => new() { Action = UiRequestAction.CloseAll };
}

public enum UiRequestResult
{
	Success,
	AlreadyOpen,
	BlockedByCompatibility,
	DeferredByGameplay,
	NotOpen,
	Invalid
}
