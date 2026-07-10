namespace Dynasty.LeagueNet;

/// <summary>
/// Lightweight replication envelope. Full league JSON is sent on revision change; clients never mutate.
/// </summary>
public sealed class LeagueStateSnapshot
{
	public ulong StateRevision { get; set; }
	public string SerializedLeague { get; set; } = "";
	public long LastEventSequence { get; set; }
}
