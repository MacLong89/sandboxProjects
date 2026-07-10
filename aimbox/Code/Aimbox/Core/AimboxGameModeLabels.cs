namespace Sandbox;

public static class AimboxGameModeLabels
{
	public static string Short( AimboxGameMode mode ) => mode switch
	{
		AimboxGameMode.TeamDeathmatch => "TDM",
		AimboxGameMode.Duel => "DUEL",
		AimboxGameMode.Survival => "SURVIVAL",
		AimboxGameMode.Range => "RANGE",
		AimboxGameMode.AimLevel1 => "GRID",
		AimboxGameMode.AimLevel2 => "FLICK",
		AimboxGameMode.AimLevel3 => "TRACK",
		AimboxGameMode.AimLevel4 => "mGRID",
		AimboxGameMode.AimLevel5 => "mFLICK",
		AimboxGameMode.AimLevel6 => "mTRACK",
		_ => "FFA"
	};

	public static string Long( AimboxGameMode mode ) => mode switch
	{
		AimboxGameMode.TeamDeathmatch => "Team Deathmatch",
		AimboxGameMode.FreeForAll => "Free For All",
		AimboxGameMode.Duel => "Duel",
		AimboxGameMode.Survival => "Survival",
		AimboxGameMode.Range => "Range",
		AimboxGameMode.AimLevel1 => "Grid",
		AimboxGameMode.AimLevel2 => "Flick",
		AimboxGameMode.AimLevel3 => "Track",
		AimboxGameMode.AimLevel4 => "mGrid",
		AimboxGameMode.AimLevel5 => "mFlick",
		AimboxGameMode.AimLevel6 => "mTrack",
		_ => mode.ToString()
	};

	public static string RosterLabel( AimboxGameMode mode ) => mode switch
	{
		AimboxGameMode.TeamDeathmatch => $"{AimboxArenaConfig.TdmRosterPerTeam}v{AimboxArenaConfig.TdmRosterPerTeam}",
		AimboxGameMode.Duel => "1v1",
		AimboxGameMode.Survival => "CO-OP",
		AimboxGameMode.Range => "SOLO",
		_ when AimboxAimModeRules.IsAimMode( mode ) => "SOLO",
		_ => "FFA"
	};
}
