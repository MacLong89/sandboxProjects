namespace Sandbox;

public static class AimboxArenaConfig
{
	public const float MapScale = 5f;

	/// <summary>Half-width of the legacy open floor (X axis).</summary>
	public const float ArenaHalfWidth = 520f * MapScale;

	/// <summary>Half-length of the legacy open floor (Y axis).</summary>
	public const float ArenaHalfLength = 620f * MapScale;

	/// <summary>Legacy alias — use <see cref="ArenaHalfWidth"/>.</summary>
	public const float FloorHalfSize = ArenaHalfWidth;

	public const float SpawnEdgeInset = 0.92f;

	public static float SpawnRadius => FloorHalfSize * SpawnEdgeInset;

	public const float MatchDurationSeconds = 300f;
	public const float AimMatchDurationSeconds = 60f;
	public const float DuelRoundResetSeconds = 2f;
	public const float DuelRoundFreezeSeconds = 2f;
	public const int PointsPerKill = 100;

	/// <summary>15 kills — reachable inside a 5-minute FFA.</summary>
	public const int FfaScoreLimit = 1500;
	/// <summary>25 team kills — reachable inside a 5-minute TDM.</summary>
	public const int TdmTeamScoreLimit = 2500;
	public const int DuelKillLimit = 10;
	public const int TdmRosterPerTeam = 8;

	public static AimboxArenaMap ActiveMap =>
		AimboxGame.Instance?.ActiveArenaMap ?? AimboxArenaMap.Yard;

	public static string MapDisplayName =>
		AimboxMapCatalog.Get( ActiveMap ).DisplayName;

	public static float ActiveCombatScale =>
		AimboxMapDesignRules.CombatScale( AimboxMapCatalog.Get( ActiveMap ).Layout );

	public const int SurvivalBotsPerWave = 2;
	public const int SurvivalWave1BotCount = SurvivalBotsPerWave;
	public const int SurvivalHardModeStartWave = 5;
	public const float SurvivalHardStatMultiplier = 1.5f;
	/// <summary>
	/// AUDIT FIX H5 (2026-07-13): SurvivalComplete was never written true — clearing waves
	/// only incremented forever. After this many waves are cleared, Mark SurvivalComplete.
	/// Raise/lower carefully; scoreboard + ShouldEnd depend on it.
	/// </summary>
	public const int SurvivalFinalWave = 10;

	public static int GetSurvivalWaveBotCount( int wave ) =>
		Math.Max( 1, wave * SurvivalBotsPerWave );
}
