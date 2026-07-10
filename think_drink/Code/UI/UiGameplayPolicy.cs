namespace ThinkDrink.UI;

/// <summary>Gameplay-state-aware UI rules — lower priority systems defer during active rounds.</summary>
public static class UiGameplayPolicy
{
	public static MatchPhase CurrentPhase => MatchManager.Instance?.Phase ?? MatchPhase.Lobby;

	public static bool IsActiveRoundPhase =>
		CurrentPhase is MatchPhase.BuzzIn or MatchPhase.Answering or MatchPhase.StealAttempt;

	public static bool IsShellPhase =>
		CurrentPhase is MatchPhase.Lobby or MatchPhase.PostMatch;

	public static bool AllowsWindow( UiWindowId window )
	{
		var def = UiWindowRegistry.Get( window );
		if ( def.Id == UiWindowId.None ) return false;

		if ( window == UiWindowId.Onboarding )
			return CurrentPhase == MatchPhase.Lobby;

		if ( def.Group == UiWindowGroup.OverlayPanel )
		{
			if ( !IsShellPhase ) return false;
			if ( LobbyManager.Instance?.CountdownActive == true ) return false;
			return true;
		}

		if ( window == UiWindowId.BoardTuner )
			return IsShellPhase;

		if ( def.Group == UiWindowGroup.DevTool )
			return IsShellPhase;

		return def.AllowedDuringActiveRound || !IsActiveRoundPhase;
	}

	public static bool ShouldDeferNotification( UiNotificationKind kind )
	{
		if ( !IsActiveRoundPhase ) return false;

		return kind switch
		{
			UiNotificationKind.LevelUp => true,
			UiNotificationKind.Toast => false,
			UiNotificationKind.Flash => false,
			_ => false
		};
	}

	public static bool ShouldSuppressHudChrome => UIManager.IsOverlayOpen || UIManager.OnboardingActive;
}
