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
	public const float FreezeTimeSeconds = 3f;
	public const float DuelRoundResetSeconds = 2f;
	public const float DuelRoundFreezeSeconds = 2f;
	public const int PointsPerKill = 100;

	public const int FfaScoreLimit = 5000;
	public const int TdmTeamScoreLimit = 5000;
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

	public static int GetSurvivalWaveBotCount( int wave ) =>
		Math.Max( 1, wave * SurvivalBotsPerWave );
}
