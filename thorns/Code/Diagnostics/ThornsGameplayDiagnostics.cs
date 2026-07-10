namespace Sandbox;

/// <summary>
/// Optional local profiling counters (off by default). Set <see cref="EnablePeriodicLogs"/> to spot hot-path abuse without a profiler.
/// </summary>
public static class ThornsGameplayDiagnostics
{
	public static bool EnablePeriodicLogs { get; set; }

	static int _placedStructureFullScans;
	static int _ghostPlacementResolves;
	static double _nextPeriodicLogRealtime = -1.0;

	public static void BumpPlacedStructureFullScan() => _placedStructureFullScans++;

	public static void BumpGhostPlacementResolve() => _ghostPlacementResolves++;

	/// <summary>Call once per frame from a local-owner gameplay component (safe if multiple callers share the same window).</summary>
	public static void TryFlushPeriodicLogs()
	{
		if ( !EnablePeriodicLogs || !Game.IsPlaying )
			return;

		var now = Time.Now;
		if ( now < _nextPeriodicLogRealtime )
			return;

		_nextPeriodicLogRealtime = now + 10.0;
		if ( _placedStructureFullScans == 0 && _ghostPlacementResolves == 0 )
			return;

		Log.Info(
			$"[Thorns][PerfDiag] ~10s: placedStructureFullScans={_placedStructureFullScans} ghostPlacementResolves={_ghostPlacementResolves}" );
		_placedStructureFullScans = 0;
		_ghostPlacementResolves = 0;
	}
}
