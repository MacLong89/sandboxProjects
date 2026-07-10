namespace Sandbox;

/// <summary>Unified population budget telemetry — read-only snapshots for scaling diagnostics (does not alter limits).</summary>
public static class ThornsPopulationBudgetTelemetry
{
	public static ThornsPopulationTelemetrySnapshot HostCaptureSnapshot()
	{
		return new ThornsPopulationTelemetrySnapshot
		{
			WildlifeCount = ThornsPopulationDirector.HostWildlifeGlobalCount,
			BanditCount = ThornsPopulationDirector.HostBanditGlobalCount,
			FutureNpcCount = ThornsPopulationDirector.HostGetPopulationCount( ThornsPopulationKind.FutureNpc ),
			EventNpcCount = ThornsPopulationDirector.HostGetPopulationCount( ThornsPopulationKind.EventNpc ),
			WildlifeBudget = ThornsPopulationDirector.HostGetPopulationBudget( ThornsPopulationKind.Wildlife ),
			BanditWandererBudget = ThornsPopulationDirector.HostGetPopulationBudget( ThornsPopulationKind.BanditWanderer ),
			FutureNpcBudget = ThornsPopulationDirector.HostGetPopulationBudget( ThornsPopulationKind.FutureNpc ),
			EventNpcBudget = ThornsPopulationDirector.HostGetPopulationBudget( ThornsPopulationKind.EventNpc ),
			LosTracesUsedThisFixed = ThornsPopulationDirector.HostLosTracesUsedThisFixed,
			LosTracesMaxPerFixed = ThornsPerformanceBudgets.HostWildlifeMaxLosRaysPerFixed,
			LosFixedStepSerial = ThornsPopulationDirector.HostLosFixedStepSerial,
			PerceptionPlayerQueriesPerSec = ThornsAiPerceptionMetrics.PlayerSpatialQueriesPerSec,
			PerceptionLosTracesPerSec = ThornsAiPerceptionMetrics.LosTracesPerSec,
			PerceptionLosSkipsPerSec = ThornsAiPerceptionMetrics.LosBudgetSkipsPerSec,
			PerceptionWildlifeCallsPerSec = ThornsAiPerceptionMetrics.WildlifePerceptionCallsPerSec,
			PlayerCacheRootCount = ThornsPopulationDirector.HostPlayerCacheRootCount,
			PlayerSpatialGridCells = ThornsPopulationDirector.HostPlayerSpatialGridCells,
			PlayerSpatialGridPlayers = ThornsPopulationDirector.HostPlayerSpatialGridPlayers,
			WildlifePeerSpatialGridCells = ThornsAiPerceptionMetrics.LastWildlifeSpatialGridCells,
			WildlifePeerSpatialGridBrains = ThornsAiPerceptionMetrics.LastWildlifeSpatialGridBrains,
		};
	}
}

/// <summary>Point-in-time population + budget telemetry for <c>population_audit</c> and future HUD panels.</summary>
public readonly struct ThornsPopulationTelemetrySnapshot
{
	public int WildlifeCount { get; init; }
	public int BanditCount { get; init; }
	public int FutureNpcCount { get; init; }
	public int EventNpcCount { get; init; }

	public ThornsPopulationBudgetSnapshot WildlifeBudget { get; init; }
	public ThornsPopulationBudgetSnapshot BanditWandererBudget { get; init; }
	public ThornsPopulationBudgetSnapshot FutureNpcBudget { get; init; }
	public ThornsPopulationBudgetSnapshot EventNpcBudget { get; init; }

	public int LosTracesUsedThisFixed { get; init; }
	public int LosTracesMaxPerFixed { get; init; }
	public int LosFixedStepSerial { get; init; }

	public float PerceptionPlayerQueriesPerSec { get; init; }
	public float PerceptionLosTracesPerSec { get; init; }
	public float PerceptionLosSkipsPerSec { get; init; }
	public float PerceptionWildlifeCallsPerSec { get; init; }

	public int PlayerCacheRootCount { get; init; }
	public int PlayerSpatialGridCells { get; init; }
	public int PlayerSpatialGridPlayers { get; init; }
	public int WildlifePeerSpatialGridCells { get; init; }
	public int WildlifePeerSpatialGridBrains { get; init; }
}
