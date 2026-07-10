namespace Terraingen.Multiplayer;

/// <summary>Serialized victory progress stored in the world save file.</summary>
public sealed class ThornsVictoryPersistentStateDto
{
	public Dictionary<string, Dictionary<string, long>> PlayerProgressByAccount { get; set; } = new();
	public Dictionary<string, Dictionary<string, long>> GuildProgressByGuildId { get; set; } = new();
	public Dictionary<string, long> WorldProgressByPath { get; set; } = new();
	public Dictionary<string, string> LastPlayerLeaderByPath { get; set; } = new();
	public Dictionary<string, string> LastGuildLeaderByPath { get; set; } = new();
	public List<ThornsVictoryLeadershipChangePersistentDto> LeadershipChanges { get; set; } = new();
}

public sealed class ThornsVictoryLeadershipChangePersistentDto
{
	public string PathId { get; set; } = "";
	public byte Scope { get; set; }
	public string NewLeaderScopeKey { get; set; } = "";
	public string PreviousLeaderScopeKey { get; set; } = "";
	public string TimestampUtc { get; set; } = "";
}
