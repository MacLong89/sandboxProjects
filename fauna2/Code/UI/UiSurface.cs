namespace Fauna2.UI;

/// <summary>Every major UI surface registered with the central manager.</summary>
public enum UiSurface
{
	None,
	MainMenu,
	Page,
	AnimalInspect,
	HabitatInspect,
	Debug,
	BuildMode,
}

public readonly struct UiOpenRequest
{
	public UiSurface Surface { get; init; }
	public UiPage Page { get; init; }
	public string TargetId { get; init; }
	public BuildCategory? BuildCategory { get; init; }
	public bool ToggleIfActive { get; init; }
}
