namespace Sandbox;

public static class AimboxLobbyTeamRules
{
	public static bool UsesTeamSelect( AimboxGameMode mode ) => mode switch
	{
		AimboxGameMode.FreeForAll or AimboxGameMode.Range or AimboxGameMode.Survival => false,
		_ => true
	};

	public static bool UsesMapSelect( AimboxGameMode mode ) =>
		!AimboxAimModeRules.IsAimMode( mode );
}
