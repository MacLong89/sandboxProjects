namespace Sandbox;

/// <summary>Single input ownership gate — only the active UI context may consume menu keys.</summary>
public static class YaUiInputRouter
{
	public static bool IsGameplayContext => YaUiManager.Local?.ActiveInputContext == YaUiInputContext.Gameplay;

	public static bool ShouldBlockGameplayInput =>
		YaUiManager.Local is { AnyModalActive: true }
		&& YaUiManager.Local.ActiveInputContext != YaUiInputContext.Gameplay;

	public static bool CanOpenScoreboard =>
		YaUiManager.Local is null || !YaUiManager.Local.IsVisible( YaUiSurfaceId.FullscreenControlsTutorial );

	public static bool CanOpenControls =>
		YaUiManager.Local is null || !YaUiManager.Local.IsVisible( YaUiSurfaceId.FullscreenScoreboard );
}
