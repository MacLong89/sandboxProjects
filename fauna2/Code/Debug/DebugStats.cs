namespace Fauna2;

/// <summary>
/// Development metrics: per-system tick cost (exponential moving average),
/// entity counts, save size and replication-relevant numbers. Cheap enough to
/// stay enabled; the debug panel renders it.
/// </summary>
public static class DebugStats
{
	private static readonly Dictionary<string, float> _tickMs = new();

	public static IReadOnlyDictionary<string, float> TickMs => _tickMs;

	/// <summary>Returns a start timestamp for a timing section.</summary>
	public static long StartTimer() => System.Diagnostics.Stopwatch.GetTimestamp();

	/// <summary>Records elapsed ms for the section as a smoothed average.</summary>
	public static void StopTimer( string section, long startTimestamp )
	{
		var elapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - startTimestamp;
		var ms = elapsedTicks * 1000f / System.Diagnostics.Stopwatch.Frequency;

		if ( _tickMs.TryGetValue( section, out var avg ) )
			_tickMs[section] = avg * 0.95f + ms * 0.05f;
		else
			_tickMs[section] = ms;
	}

	public static float TotalTickMs() => _tickMs.Values.Sum();

	/// <summary>Rough count of networked gameplay objects (replication footprint).</summary>
	public static int NetworkedObjectCount() =>
		AnimalRegistry.Count + HabitatRegistry.Count + PlaceableRegistry.Count + 1; // +1 ZooCore

	/// <summary>
	/// Rough per-snapshot replication weight estimate: animals dominate since
	/// they move (transform sync); static objects only sync on change.
	/// </summary>
	public static string ReplicationEstimate()
	{
		var moving = AnimalRegistry.Count;
		var bytesPerMovingObject = 48; // transform + sync var deltas, ballpark
		return $"~{moving * bytesPerMovingObject} B/snap ({moving} movers)";
	}
}
