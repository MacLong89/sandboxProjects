namespace Sandbox;

/// <summary>Data-driven UI open request — prefer this over hardcoded OpenX() calls.</summary>
public readonly struct YaUiRequest
{
	public YaUiSurfaceId Surface { get; init; }
	public YaUiLayer Layer { get; init; }
	public bool Modal { get; init; }
	public YaUiInputContext InputContext { get; init; }
	public YaUiScreenRegion Region { get; init; }
	public bool RequiresMouse { get; init; }
	public bool SuppressWhenCombat { get; init; }
}
