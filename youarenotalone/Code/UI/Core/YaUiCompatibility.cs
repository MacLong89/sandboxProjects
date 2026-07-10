namespace Sandbox;

/// <summary>Global rules for which UI surfaces may coexist. No panel decides this locally.</summary>
public static class YaUiCompatibility
{
	static readonly YaUiSurfaceId[] FullscreenModals =
	[
		YaUiSurfaceId.FullscreenPracticeChoice,
		YaUiSurfaceId.FullscreenControlsTutorial,
		YaUiSurfaceId.FullscreenScoreboard,
		YaUiSurfaceId.FullscreenRoundVictory
	];

	/// <summary>Higher index = wins conflicts.</summary>
	static readonly YaUiSurfaceId[] ConflictPriority =
	[
		YaUiSurfaceId.HudCombat,
		YaUiSurfaceId.HudTopObjective,
		YaUiSurfaceId.HudTopLeftHints,
		YaUiSurfaceId.HudCrosshair,
		YaUiSurfaceId.PassiveParanoia,
		YaUiSurfaceId.PassiveDamage,
		YaUiSurfaceId.NotificationEventFeed,
		YaUiSurfaceId.NotificationFloatingStack,
		YaUiSurfaceId.NotificationLobbyHint,
		YaUiSurfaceId.NotificationRoundStart,
		YaUiSurfaceId.FullscreenScoreboard,
		YaUiSurfaceId.FullscreenControlsTutorial,
		YaUiSurfaceId.FullscreenPracticeChoice,
		YaUiSurfaceId.FullscreenRoundVictory,
		YaUiSurfaceId.CriticalDeathOverlay
	];

	public static bool CanCoexist( YaUiSurfaceId a, YaUiSurfaceId b )
	{
		if ( a == b )
			return true;

		if ( a == YaUiSurfaceId.CriticalDeathOverlay || b == YaUiSurfaceId.CriticalDeathOverlay )
			return false;

		if ( IsFullscreenModal( a ) && IsFullscreenModal( b ) )
			return false;

		if ( a == YaUiSurfaceId.FullscreenScoreboard && b == YaUiSurfaceId.FullscreenControlsTutorial )
			return false;
		if ( a == YaUiSurfaceId.FullscreenControlsTutorial && b == YaUiSurfaceId.FullscreenScoreboard )
			return false;

		if ( a == YaUiSurfaceId.FullscreenRoundVictory && IsFullscreenModal( b ) )
			return false;
		if ( b == YaUiSurfaceId.FullscreenRoundVictory && IsFullscreenModal( a ) )
			return false;

		if ( a == YaUiSurfaceId.FullscreenPracticeChoice && b == YaUiSurfaceId.NotificationLobbyHint )
			return false;
		if ( b == YaUiSurfaceId.FullscreenPracticeChoice && a == YaUiSurfaceId.NotificationLobbyHint )
			return false;

		if ( IsTopCenterNotification( a ) && IsTopCenterNotification( b ) )
			return false;

		// HUD + notifications/tooltips always allowed unless death/modal blocks.
		return true;
	}

	static bool IsTopCenterNotification( YaUiSurfaceId id ) => id is
		YaUiSurfaceId.NotificationRoundStart
		or YaUiSurfaceId.NotificationLobbyHint;

	public static int GetConflictPriority( YaUiSurfaceId id )
	{
		for ( var i = 0; i < ConflictPriority.Length; i++ )
		{
			if ( ConflictPriority[i] == id )
				return i;
		}

		return 0;
	}

	static bool IsFullscreenModal( YaUiSurfaceId id )
	{
		foreach ( var m in FullscreenModals )
		{
			if ( m == id )
				return true;
		}

		return false;
	}

	public static bool BlocksGameplayHud( YaUiSurfaceId id ) => id switch
	{
		YaUiSurfaceId.CriticalDeathOverlay => true,
		YaUiSurfaceId.FullscreenPracticeChoice => true,
		YaUiSurfaceId.FullscreenControlsTutorial => true,
		YaUiSurfaceId.FullscreenScoreboard => true,
		YaUiSurfaceId.FullscreenRoundVictory => true,
		_ => false
	};

	public static bool IsModal( YaUiSurfaceId id ) => id switch
	{
		YaUiSurfaceId.CriticalDeathOverlay => true,
		YaUiSurfaceId.FullscreenPracticeChoice => true,
		YaUiSurfaceId.FullscreenControlsTutorial => true,
		YaUiSurfaceId.FullscreenScoreboard => true,
		YaUiSurfaceId.FullscreenRoundVictory => true,
		_ => false
	};

	public static YaUiInputContext InputContextFor( YaUiSurfaceId id ) => id switch
	{
		YaUiSurfaceId.CriticalDeathOverlay => YaUiInputContext.Spectating,
		YaUiSurfaceId.FullscreenScoreboard => YaUiInputContext.Scoreboard,
		YaUiSurfaceId.FullscreenPracticeChoice => YaUiInputContext.Menu,
		YaUiSurfaceId.FullscreenControlsTutorial => YaUiInputContext.Menu,
		YaUiSurfaceId.FullscreenRoundVictory => YaUiInputContext.Modal,
		_ => YaUiInputContext.Gameplay
	};
}
