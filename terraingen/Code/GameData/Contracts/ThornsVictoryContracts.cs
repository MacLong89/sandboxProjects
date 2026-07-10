namespace Terraingen.GameData;

/// <summary>Stable ids for data-driven victory paths (extend without code changes to consumers).</summary>
public static class ThornsVictoryPathIds
{
	public const string Dominion = "dominion";
	public const string Ascension = "ascension";
	public const string Purification = "purification";
	public const string Apex = "apex";
}

public enum ThornsVictoryScope : byte
{
	Player,
	Guild,
	World
}

/// <summary>Catalog definition for one victory path (milestones, cap, progress sources).</summary>
public sealed class ThornsVictoryPathDefinition
{
	public string PathId { get; set; } = "";
	public string DisplayName { get; set; } = "";
	public string Summary { get; set; } = "";
	public string IconPath { get; set; } = "";
	public long TargetProgress { get; set; } = 10_000;
	public List<ThornsVictoryMilestoneDefinition> Milestones { get; set; } = new();
	/// <summary>Progress source key → points awarded per report.</summary>
	public Dictionary<string, int> SourceWeights { get; set; } = new( StringComparer.OrdinalIgnoreCase );
}

public sealed class ThornsVictoryMilestoneDefinition
{
	public string MilestoneId { get; set; } = "";
	public string Title { get; set; } = "";
	public long Threshold { get; set; }
	public string RewardPreview { get; set; } = "";
}

/// <summary>Resolved progress for one scope on one path (UI + logic).</summary>
public sealed class ThornsVictoryProgressEntry
{
	public string PathId { get; set; } = "";
	public ThornsVictoryScope Scope { get; set; }
	public string ScopeKey { get; set; } = "";
	public long CurrentProgress { get; set; }
	public long TotalProgress { get; set; }
	public float PercentComplete { get; set; }
	public int Rank { get; set; }
	public string LeaderDisplayName { get; set; } = "";
	public string LeaderScopeKey { get; set; } = "";
	public string TopGuildName { get; set; } = "";
	public string TopPlayerName { get; set; } = "";
}

public sealed class ThornsVictoryPathCardDto
{
	public string PathId { get; set; } = "";
	public string DisplayName { get; set; } = "";
	public string Summary { get; set; } = "";
	public string IconPath { get; set; } = "";
	public float PercentComplete { get; set; }
	public int PlayerRank { get; set; }
	public int GuildRank { get; set; }
	public string CurrentLeaderName { get; set; } = "";
	public string CurrentLeaderScope { get; set; } = "";
	public string NextMilestoneTitle { get; set; } = "";
	public long NextMilestoneThreshold { get; set; }
	public string NextMilestoneRewardPreview { get; set; } = "";
	public long PlayerProgress { get; set; }
	public long GuildProgress { get; set; }
	public long WorldProgress { get; set; }
	public long TargetProgress { get; set; }
}

public sealed class ThornsVictoryMilestoneRowDto
{
	public string MilestoneId { get; set; } = "";
	public string Title { get; set; } = "";
	public long Threshold { get; set; }
	public string RewardPreview { get; set; } = "";
	public bool Reached { get; set; }
}

public sealed class ThornsVictoryLeaderboardRowDto
{
	public int Rank { get; set; }
	public string DisplayName { get; set; } = "";
	public string ScopeKey { get; set; } = "";
	public bool IsNpcGuild { get; set; }
	public long Progress { get; set; }
	public float PercentComplete { get; set; }
}

public sealed class ThornsVictoryLeadershipChangeDto
{
	public string PathId { get; set; } = "";
	public string PathDisplayName { get; set; } = "";
	public ThornsVictoryScope Scope { get; set; }
	public string NewLeaderScopeKey { get; set; } = "";
	public string PreviousLeaderScopeKey { get; set; } = "";
	public string NewLeaderName { get; set; } = "";
	public string PreviousLeaderName { get; set; } = "";
	public string TimestampUtc { get; set; } = "";
}

public sealed class ThornsVictoryPathDetailDto
{
	public string PathId { get; set; } = "";
	public string DisplayName { get; set; } = "";
	public string Summary { get; set; } = "";
	public ThornsVictoryProgressEntry Player { get; set; } = new();
	public ThornsVictoryProgressEntry Guild { get; set; } = new();
	public ThornsVictoryProgressEntry World { get; set; } = new();
	public List<ThornsVictoryMilestoneRowDto> Milestones { get; set; } = new();
	public List<ThornsVictoryLeaderboardRowDto> TopPlayers { get; set; } = new();
	public List<ThornsVictoryLeaderboardRowDto> TopGuilds { get; set; } = new();
}

/// <summary>Owner snapshot for victory paths UI (built on host, no hardcoded path logic in screens).</summary>
public sealed class ThornsVictorySnapshot
{
	public string SelectedPathId { get; set; } = ThornsVictoryPathIds.Dominion;
	public List<ThornsVictoryPathCardDto> PathCards { get; set; } = new();
	public ThornsVictoryPathDetailDto SelectedDetail { get; set; } = new();
	public List<ThornsVictoryLeaderboardRowDto> TopPlayersOverall { get; set; } = new();
	public List<ThornsVictoryLeaderboardRowDto> TopGuildsOverall { get; set; } = new();
	public List<ThornsVictoryLeadershipChangeDto> RecentLeadershipChanges { get; set; } = new();
	public List<ThornsVictoryPathLeaderDto> CurrentLeadersByPath { get; set; } = new();
	public ThornsVictoryGuildSummaryDto GuildSummary { get; set; } = new();
	public List<ThornsVictoryGuildComparisonRowDto> GuildComparisonRows { get; set; } = new();
}

public sealed class ThornsVictoryPathLeaderDto
{
	public string PathId { get; set; } = "";
	public string PathDisplayName { get; set; } = "";
	public string PlayerLeaderName { get; set; } = "";
	public string GuildLeaderName { get; set; } = "";
	public long GuildProgress { get; set; }
	public float GuildPercentComplete { get; set; }
	public long WorldProgress { get; set; }
	public float WorldPercentComplete { get; set; }
}

/// <summary>Guild tab summary rows (lightweight).</summary>
public sealed class ThornsVictoryGuildSummaryDto
{
	public List<ThornsVictoryGuildPathRowDto> PathRows { get; set; } = new();
	public string ServerLeaderPathName { get; set; } = "";
	public string ServerLeaderGuildName { get; set; } = "";
}

public sealed class ThornsVictoryGuildPathRowDto
{
	public string PathId { get; set; } = "";
	public string DisplayName { get; set; } = "";
	public float PercentComplete { get; set; }
	public int GuildRank { get; set; }
	public string PathLeaderName { get; set; } = "";
}

/// <summary>One row in the guild victory comparison table.</summary>
public sealed class ThornsVictoryGuildComparisonRowDto
{
	public string GuildId { get; set; } = "";
	public string GuildName { get; set; } = "";
	public bool IsNpcGuild { get; set; }
	public bool IsEliminated { get; set; }
	public int MemberCount { get; set; }
	public int OverallRank { get; set; }
	public long TotalScore { get; set; }
	public List<ThornsVictoryGuildPathRowDto> PathRows { get; set; } = new();
}
