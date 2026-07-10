namespace Dynasty.Systems.Simulation;

/// <summary>
/// Shared formatting for drive-summary replay UI (not play-by-play).
/// </summary>
public static class SimReplayDisplay
{
	public static string FormatClock( int seconds )
	{
		var clamped = Math.Max( 0, seconds );
		var mins = clamped / 60;
		var secs = clamped % 60;
		return $"{mins}:{secs:D2}";
	}

	public static string FormatDriveSummary( int quarter, int clockSeconds )
		=> $"Q{quarter} · {FormatClock( clockSeconds )}";
}
