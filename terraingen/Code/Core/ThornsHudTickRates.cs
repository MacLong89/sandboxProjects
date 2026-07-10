namespace Terraingen.Core;

/// <summary>Shared HUD / prompt refresh intervals (seconds).</summary>
public static class ThornsHudTickRates
{
	public const float InteractionPromptSeconds = 0.1f;
	public const float MinimapBlipSeconds = 0.05f;
	public const float MinimapMarkersSeconds = 0.25f;
	public const float MapSnapshotSeconds = 1f;
	public const float JourneyWorldUnlockSeconds = 0.5f;
	public const float MenuMapBlipSeconds = 0.2f;
	public const float MenuOverlayPollSeconds = 0.25f;
	public const float ModelPreviewRenderSeconds = 1f / 30f;
	public const float WorldContainerSlotPollSeconds = 0.25f;
}
