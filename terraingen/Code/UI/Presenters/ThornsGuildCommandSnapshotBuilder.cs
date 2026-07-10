namespace Terraingen.UI.Presenters;

using Terraingen.GameData;
using Terraingen.NpcGuild;
using Terraingen.Player;
using Terraingen.Victory;

/// <summary>Host-only assembly of guild command center snapshots from authoritative services.</summary>
public static class ThornsGuildCommandSnapshotBuilder
{
	public static ThornsGuildCommandSnapshotDto Build(
		string guildId,
		ThornsVictorySnapshot victory )
	{
		var command = new ThornsGuildCommandSnapshotDto
		{
			OwnGuildId = guildId ?? "",
			SelectedVictoryPathId = victory?.SelectedPathId ?? ThornsVictoryPathIds.Dominion,
			ComparisonRows = FilterRealGuildRows( victory?.GuildComparisonRows ),
			PathLeaders = victory?.CurrentLeadersByPath?.ToList() ?? new List<ThornsVictoryPathLeaderDto>(),
			GlobalRankings = FilterRealGuildRankings( victory?.TopGuildsOverall )
		};

		command.Victory = ThornsVictoryManager.Instance?.BuildGuildVictoryProgressSnapshot( guildId, victory?.SelectedPathId )
		                  ?? new ThornsGuildVictoryProgressSnapshot();

		return command;
	}

	public static ThornsGuildOverviewDto BuildOverview(
		ThornsGuildSnapshotDto guild,
		ThornsVictoryGuildComparisonRowDto comparisonRow,
		ThornsVictorySnapshot victory,
		ThornsNpcGuildRivalDto rival )
	{
		var leader = guild.Members.FirstOrDefault( m => string.Equals( m.Rank, "Leader", StringComparison.OrdinalIgnoreCase ) )
		             ?? guild.Members.FirstOrDefault();
		var totalScore = comparisonRow?.TotalScore ?? SumGuildVictoryScore( guild.GuildId, victory );
		var selectedPathId = victory?.SelectedPathId ?? ThornsVictoryPathIds.Dominion;
		var selectedPct = comparisonRow?.PathRows?
			.FirstOrDefault( p => string.Equals( p.PathId, selectedPathId, StringComparison.OrdinalIgnoreCase ) )
			?.PercentComplete ?? 0f;

		return new ThornsGuildOverviewDto
		{
			LeaderName = leader?.DisplayName ?? "—",
			ServerRank = comparisonRow?.OverallRank ?? 0,
			VictoryScore = totalScore,
			MemberCount = guild.Members.Count,
			SelectedPathId = selectedPathId,
			SelectedPathPercent = selectedPct
		};
	}

	static long SumGuildVictoryScore( string guildId, ThornsVictorySnapshot victory )
	{
		if ( string.IsNullOrWhiteSpace( guildId ) || victory?.GuildComparisonRows is null )
			return 0;

		return FilterRealGuildRows( victory.GuildComparisonRows )
			.FirstOrDefault( r => string.Equals( r.GuildId, guildId, StringComparison.OrdinalIgnoreCase ) )
			?.TotalScore ?? 0;
	}

	static List<ThornsVictoryGuildComparisonRowDto> FilterRealGuildRows(
		IEnumerable<ThornsVictoryGuildComparisonRowDto> rows )
	{
		if ( rows is null )
			return new List<ThornsVictoryGuildComparisonRowDto>();

		return rows
			.Where( r => r is not null && ThornsGuildWorldService.Instance?.ShouldIncludeInServerRankings( r.GuildId ) == true )
			.ToList();
	}

	static List<ThornsVictoryLeaderboardRowDto> FilterRealGuildRankings(
		IEnumerable<ThornsVictoryLeaderboardRowDto> rows )
	{
		if ( rows is null )
			return new List<ThornsVictoryLeaderboardRowDto>();

		return rows
			.Where( r => r is not null && ThornsGuildWorldService.Instance?.ShouldIncludeInServerRankings( r.ScopeKey ) == true )
			.ToList();
	}
}
