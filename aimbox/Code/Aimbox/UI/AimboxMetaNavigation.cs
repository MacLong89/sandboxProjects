namespace Sandbox;

public static class AimboxMetaNavigation
{
	public const float IntermissionDurationSeconds = 30f;
	public const float ReadyCountdownSeconds = 3f;

	public static AimboxMetaScreen CurrentScreen { get; private set; } = AimboxMetaScreen.None;
	public static int EditingLoadoutIndex { get; set; }
	public static AimboxMatchSummary PendingMatchSummary { get; set; }
	public static bool IsInIntermission { get; private set; }

	public static bool IsPauseMenuOpen =>
		!IsInIntermission && CurrentScreen != AimboxMetaScreen.None;

	public static bool IsInGamePauseMenu =>
		IsPauseMenuOpen;

	public static bool IsIntermissionUiOpen => IsInIntermission && CurrentScreen != AimboxMetaScreen.None;

	public static bool IsMetaOpen => IsPauseMenuOpen;

	public static bool IsPostMatchOpen => IsInIntermission && CurrentScreen == AimboxMetaScreen.PostMatch;

	public static bool IsPostMatchFlow { get; private set; }

	public static bool MatchRecapDismissed { get; private set; }

	public static bool ShouldShowMatchRecap =>
		AimboxMetaUiFlags.UseLegacyMatchRecap
		&& IsInIntermission
		&& CurrentScreen == AimboxMetaScreen.PostMatch
		&& PendingMatchSummary is not null
		&& !MatchRecapDismissed;

	public static bool IsInGameScoreboard =>
		!IsInIntermission && CurrentScreen == AimboxMetaScreen.Scoreboard;

	/// <summary>Meta UI host must stay enabled for in-game scoreboard overlay.</summary>
	public static bool RequiresMetaUiHost =>
		BlocksGameplay || IsInGameScoreboard;

	public static bool BlocksGameplay
	{
		get
		{
			var game = AimboxGame.Instance;
			if ( game is not null )
			{
				if ( game.IsAttachmentLabScene )
					return IsPauseMenuOpen;

				if ( game.Phase != AimboxSessionPhase.Playing )
					return true;
			}

			// Hold-tab scoreboard is HUD-only — keep moving and shooting.
			if ( CurrentScreen == AimboxMetaScreen.Scoreboard )
				return false;

			return IsPauseMenuOpen;
		}
	}

	/// <summary>
	/// True when a meta overlay needs a visible mouse cursor.
	/// AUDIT FIX C6 (2026-07-13): Scoreboard must NOT unlock the cursor.
	/// BlocksGameplay intentionally keeps scoreboard combat-enabled (hold-tab HUD),
	/// but the old check treated any CurrentScreen != None as cursor-needed —
	/// that left mouse free while look/fire still ran. If you regress aim during
	/// scoreboard, verify this exclusion before changing BlocksGameplay.
	/// </summary>
	public static bool RequiresMouseCursor()
	{
		if ( CurrentScreen == AimboxMetaScreen.None )
			return false;

		// Hold-tab / post-match scoreboard: combat+look stay active, so keep cursor locked.
		if ( CurrentScreen == AimboxMetaScreen.Scoreboard )
			return false;

		return true;
	}

	public static void OpenMainMenu()
	{
		if ( IsInIntermission )
			return;

		CurrentScreen = AimboxMetaScreen.MainMenu;
		ApplyPresentationState();
	}

	public static void OpenScoreboard()
	{
		CurrentScreen = AimboxMetaScreen.Scoreboard;
		ApplyPresentationState();
	}

	public static void OpenScreen( AimboxMetaScreen screen )
	{
		if ( IsInIntermission && screen == AimboxMetaScreen.MainMenu )
			screen = AimboxMetaScreen.PostMatch;

		if ( IsInIntermission && screen == AimboxMetaScreen.ModeSelect && AimboxLobbyAuthority.IsJoiner )
			screen = AimboxMetaScreen.PostMatch;

		CurrentScreen = screen;
		TrackOnboardingNavigation( screen );
		ApplyPresentationState();
	}

	static void TrackOnboardingNavigation( AimboxMetaScreen screen )
	{
		switch ( screen )
		{
			case AimboxMetaScreen.ModeSelect:
				AimboxOnboardingTips.NotifyPlayLobbyVisited();
				break;
			case AimboxMetaScreen.CreateClass:
				AimboxOnboardingTips.NotifyLoadoutsVisited();
				break;
		}
	}

	public static void Close()
	{
		if ( IsInIntermission )
			return;

		CurrentScreen = AimboxMetaScreen.None;
		ApplyPresentationState();
	}

	public static void HandlePauseToggleInput()
	{
		if ( IsInIntermission )
			return;

		if ( AimboxGame.Instance?.Phase != AimboxSessionPhase.Playing )
			return;

		if ( Input.Pressed( "Score" ) )
		{
			if ( CurrentScreen == AimboxMetaScreen.Scoreboard )
				Close();
			else
				OpenScoreboard();

			return;
		}

		if ( !Input.Pressed( "Menu" ) )
			return;

		if ( CurrentScreen == AimboxMetaScreen.Scoreboard )
		{
			Close();
			return;
		}

		if ( CurrentScreen == AimboxMetaScreen.None )
			OpenMainMenu();
		else
			Close();
	}

	public static void EnterLobby()
	{
		IsInIntermission = true;
		IsPostMatchFlow = false;
		PendingMatchSummary = null;
		MatchRecapDismissed = true;
		CurrentScreen = AimboxMetaScreen.PostMatch;
		ApplyPresentationState();
	}

	public static void EnterIntermission( AimboxMatchSummary summary )
	{
		IsInIntermission = true;
		IsPostMatchFlow = true;
		PendingMatchSummary = summary;
		MatchRecapDismissed = true;
		CurrentScreen = AimboxMetaScreen.Scoreboard;
		ApplyPresentationState();
	}

	public static void ContinuePostMatchFromScoreboard()
	{
		if ( !IsInIntermission || !IsPostMatchFlow || PendingMatchSummary is null )
			return;

		CurrentScreen = AimboxMetaScreen.Barracks;
		ApplyPresentationState();
	}

	public static void ContinuePostMatchToMainMenu()
	{
		if ( !IsInIntermission || !IsPostMatchFlow )
			return;

		IsPostMatchFlow = false;
		MatchRecapDismissed = true;
		AimboxGame.Instance?.ResetIntermissionTimer();
		CurrentScreen = AimboxMetaScreen.PostMatch;
		ApplyPresentationState();
	}

	public static void ResetMatchRecap() => MatchRecapDismissed = false;

	public static void DismissMatchRecap()
	{
		if ( MatchRecapDismissed )
			return;

		MatchRecapDismissed = true;
		AimboxGame.Instance?.ResetIntermissionTimer();
		CurrentScreen = AimboxMetaScreen.PostMatch;
		ApplyPresentationState();
	}

	public static void LeaveIntermission()
	{
		IsInIntermission = false;
		IsPostMatchFlow = false;
		PendingMatchSummary = null;
		CurrentScreen = AimboxMetaScreen.None;
		ApplyPresentationState();
	}

	public static void ShowPostMatch( AimboxMatchSummary summary ) => EnterIntermission( summary );

	public static void DismissPostMatch() => LeaveIntermission();

	public static void SyncCursorState() => AimboxCursor.Sync();

	public static void ApplyPresentationState()
	{
		AimboxGame.Instance?.SyncMetaUiActive();
		AimboxCursor.Sync();
		AimboxMenuMusic.Sync();
	}
}
