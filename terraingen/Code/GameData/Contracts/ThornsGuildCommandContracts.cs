namespace Terraingen.GameData;

/// <summary>Host-built guild command center snapshot — UI consumes this, never computes progression.</summary>
public sealed class ThornsGuildCommandSnapshotDto
{
	public string OwnGuildId { get; set; } = "";
	public string SelectedVictoryPathId { get; set; } = ThornsVictoryPathIds.Dominion;
	public ThornsGuildVictoryProgressSnapshot Victory { get; set; } = new();
	public List<ThornsVictoryGuildComparisonRowDto> ComparisonRows { get; set; } = new();
	public List<ThornsVictoryPathLeaderDto> PathLeaders { get; set; } = new();
	public List<ThornsVictoryLeaderboardRowDto> GlobalRankings { get; set; } = new();
	public List<ThornsGuildActivityDto> WorldActivity { get; set; } = new();
}

/// <summary>Guild-scoped victory path cards for the command center.</summary>
public sealed class ThornsGuildVictoryProgressSnapshot
{
	public List<ThornsGuildVictoryPathEntryDto> Paths { get; set; } = new();
}

/// <summary>One victory path card row — built from catalog + guild progress on host.</summary>
public sealed class ThornsGuildVictoryPathEntryDto
{
	public string PathId { get; set; } = "";
	public string DisplayName { get; set; } = "";
	public string Summary { get; set; } = "";
	public string IconPath { get; set; } = "";
	public float PercentComplete { get; set; }
	public int GuildRank { get; set; }
	public string CurrentMilestoneTitle { get; set; } = "";
	public string NextMilestoneTitle { get; set; } = "";
	public string NextMilestoneRewardPreview { get; set; } = "";
	public List<string> RewardPreviewItems { get; set; } = new();
	public string StatusLabel { get; set; } = "";
	public string PathLeaderGuildName { get; set; } = "";
	public long GuildProgress { get; set; }
	public long TargetProgress { get; set; }
}

public sealed class ThornsGuildOverviewDto
{
	public string LeaderName { get; set; } = "—";
	public int ServerRank { get; set; }
	public long VictoryScore { get; set; }
	public int MemberCount { get; set; }
	public string SelectedPathId { get; set; } = "";
	public float SelectedPathPercent { get; set; }
}

public sealed class ThornsGuildNoticeDto
{
	public string Message { get; set; } = "";
	public string AuthorName { get; set; } = "";
	public string TimestampUtc { get; set; } = "";
}
