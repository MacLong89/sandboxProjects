using Dynasty.Core;
using Dynasty.Core.Identifiers;
using Dynasty.Domain.League;
using Dynasty.Domain.Teams;
using Dynasty.Systems.DepthChart;
using Dynasty.Systems.Simulation;

namespace Dynasty.UI.ViewModels;

public sealed class TeamProfileViewModel
{
	public TeamId TeamId { get; init; }
	public bool IsUserTeam { get; init; }
	public string DisplayName { get; init; } = "";
	public string Abbreviation { get; init; } = "";
	public string Record { get; init; } = "0-0";
	public int StandingRank { get; init; }
	public int OffenseOverall { get; init; }
	public int DefenseOverall { get; init; }
	public int TeamOverall { get; init; }
	public int Prestige { get; init; }
	public int Morale { get; init; }
	public long CapSpace { get; init; }
	public int RosterCount { get; init; }
	public string BuildingWindow { get; init; } = "";
	public string DepthChartLabel { get; init; } = "";
	public IReadOnlyList<TeamRecentGameRow> RecentGames { get; init; } = Array.Empty<TeamRecentGameRow>();

	public static TeamProfileViewModel From( LeagueState state, TeamId teamId, TeamId userTeamId = default )
	{
		if ( state == null || teamId.IsEmpty || !state.Teams.TryGetValue( teamId, out var team ) )
			return new TeamProfileViewModel();

		var rank = GetStandingRank( state, teamId );
		var (filled, total) = DepthChartSystem.GetStarterCompletion( state, teamId );
		var profile = TeamProfileBuilder.Build( state, teamId );

		var recentGames = state.Schedule.Games
			.Where( g => g.Season == state.CurrentSeason && g.IsComplete )
			.Where( g => g.HomeTeamId.Value == teamId.Value || g.AwayTeamId.Value == teamId.Value )
			.OrderByDescending( g => g.Week )
			.ThenByDescending( g => g.Id.Value )
			.Take( 8 )
			.Select( g => TeamRecentGameRow.From( state, g, teamId ) )
			.ToList();

		return new TeamProfileViewModel
		{
			TeamId = teamId,
			IsUserTeam = !userTeamId.IsEmpty && teamId.Value == userTeamId.Value,
			DisplayName = $"{team.Identity.City} {team.Identity.Name}",
			Abbreviation = team.Identity.Abbreviation,
			Record = FormatRecord( team ),
			StandingRank = rank,
			OffenseOverall = profile.OffenseRating,
			DefenseOverall = profile.DefenseRating,
			TeamOverall = TeamProfileBuilder.ComputeOverallRating( profile ),
			Prestige = team.Prestige.Prestige,
			Morale = team.Chemistry.Morale,
			CapSpace = team.Finances.SalaryCapSpace,
			RosterCount = team.RosterPlayerIds.Count,
			BuildingWindow = team.BuildingWindow.ToString(),
			DepthChartLabel = total > 0 ? $"{filled}/{total} starters set" : "",
			RecentGames = recentGames
		};
	}

	public static int GetStandingRank( LeagueState state, TeamId teamId )
	{
		return state.Teams.Values
			.OrderByDescending( t => t.Record.Wins )
			.ThenBy( t => t.Record.Losses )
			.Select( ( t, i ) => ( t.Id, Rank: i + 1 ) )
			.FirstOrDefault( x => x.Id.Value == teamId.Value )
			.Rank;
	}

	static string FormatRecord( TeamState team )
	{
		var ties = team.Record.Ties > 0 ? $"-{team.Record.Ties}" : "";
		return $"{team.Record.Wins}-{team.Record.Losses}{ties}";
	}
}

public sealed class TeamRecentGameRow
{
	public GameId GameId { get; init; }
	public int Week { get; init; }
	public string MatchupLabel { get; init; } = "";
	public string ScoreLine { get; init; } = "";
	public string ResultLabel { get; init; } = "";

	public static TeamRecentGameRow From( LeagueState state, Domain.Schedule.ScheduledGame game, TeamId teamId )
	{
		var isHome = game.HomeTeamId.Value == teamId.Value;
		var opponent = state.Teams[isHome ? game.AwayTeamId : game.HomeTeamId];
		var teamScore = isHome ? game.Result.HomeScore : game.Result.AwayScore;
		var oppScore = isHome ? game.Result.AwayScore : game.Result.HomeScore;
		var won = teamScore > oppScore;
		var tied = teamScore == oppScore;

		var matchup = isHome
			? $"vs {opponent.Identity.Abbreviation}"
			: $"@ {opponent.Identity.Abbreviation}";

		return new TeamRecentGameRow
		{
			GameId = game.Id,
			Week = game.Week,
			MatchupLabel = matchup,
			ScoreLine = $"{teamScore}-{oppScore}",
			ResultLabel = tied ? "T" : won ? "W" : "L"
		};
	}
}
