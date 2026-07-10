namespace Sandbox;

public static class AimboxMetaScreenVisibility
{
	public static bool IsRedesignedScreen( AimboxMetaScreen screen ) => screen switch
	{
		AimboxMetaScreen.ModeSelect => AimboxMetaUiFlags.UseRedesignedPlayLobby,
		AimboxMetaScreen.CreateClass => AimboxMetaUiFlags.UseRedesignedLoadouts,
		AimboxMetaScreen.Barracks => AimboxMetaUiFlags.UseRedesignedProgression,
		AimboxMetaScreen.Scoreboard => AimboxMetaUiFlags.UseRedesignedScoreboard,
		AimboxMetaScreen.Challenges => AimboxMetaUiFlags.UseRedesignedChallenges,
		_ => false
	};

	public static bool ShouldShowScreenBackButton()
	{
		if ( AimboxMetaNavigation.ShouldShowMatchRecap )
			return false;

		var screen = AimboxMetaNavigation.CurrentScreen;
		if ( screen == AimboxMetaScreen.Armory )
			return true;

		if ( !IsRedesignedScreen( screen ) )
			return false;

		switch ( screen )
		{
			case AimboxMetaScreen.ModeSelect:
				return AimboxMetaNavigation.IsInIntermission;
			case AimboxMetaScreen.CreateClass:
				return AimboxMetaNavigation.IsInIntermission
					|| AimboxGame.Instance?.Phase != AimboxSessionPhase.Playing;
			case AimboxMetaScreen.Scoreboard:
				return !AimboxMetaNavigation.IsPostMatchFlow;
			default:
				return true;
		}
	}
}
