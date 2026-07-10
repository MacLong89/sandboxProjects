namespace Sandbox;

/// <summary>Feature toggles for redesigned meta UI vs legacy panels in <see cref="AimboxMetaRoot"/>.</summary>
public static class AimboxMetaUiFlags
{
	public const bool UseLegacyLobbyMenu = false;
	public const bool UseLegacyLoadoutsScreen = false;
	public const bool UseLegacyProgressionScreen = false;
	public const bool UseLegacyScoreboardScreen = false;
	public const bool UseLegacyPlayLobby = false;
	public const bool UseLegacyChallengesScreen = false;
	public const bool UseLegacyMatchRecap = false;

	public static bool UseRedesignedMainMenu => !UseLegacyLobbyMenu;
	public static bool UseRedesignedLoadouts => !UseLegacyLoadoutsScreen;
	public static bool UseRedesignedProgression => !UseLegacyProgressionScreen;
	public static bool UseRedesignedScoreboard => !UseLegacyScoreboardScreen;
	public static bool UseRedesignedPlayLobby => !UseLegacyPlayLobby;
	public static bool UseRedesignedChallenges => !UseLegacyChallengesScreen;
}
