using Dynasty.Core;
using Dynasty.Core.Enums;
using Dynasty.Core.Identifiers;
using Dynasty.Data;
using Dynasty.Domain.League;
using Dynasty.Domain.Teams;
using Dynasty.Systems.DepthChart;
using Dynasty.Systems.Franchise;
using Dynasty.Systems.Simulation;

namespace Dynasty.UI.ViewModels;

/// <summary>
/// Read-only projection for UI. No simulation or mutation logic.
/// </summary>
public sealed class LeagueHomeViewModel
{
	public string LeagueName { get; init; }
	public int Season { get; init; }
	public int Week { get; init; }
	public string Phase { get; init; }
	public string CalendarLabel { get; init; }
	public string CalendarMonth { get; init; }
	public int DayOfWeek { get; init; }
	public string NextSuggestedAction { get; init; }
	public int InboxActionCount { get; init; }
	public ulong StateRevision { get; init; }
	public bool IsHumanFired { get; init; }
	public FranchiseCardViewModel Franchise { get; init; } = new();
	public FranchiseStakesViewModel Stakes { get; init; } = new();
	public NextGameCardViewModel NextGame { get; init; } = new();
	public IReadOnlyList<TeamStandingRow> Standings { get; init; } = Array.Empty<TeamStandingRow>();
	public IReadOnlyList<NewsRow> RecentNews { get; init; } = Array.Empty<NewsRow>();

	public static LeagueHomeViewModel From( LeagueState state, TeamId userTeamId = default )
	{
		if ( state == null )
			return new LeagueHomeViewModel { LeagueName = "No League", Phase = "—" };

		var standings = state.Teams.Values
			.OrderByDescending( t => t.Record.Wins )
			.ThenBy( t => t.Record.Losses )
			.ThenBy( t => t.Identity.City )
			.Select( ( t, i ) => TeamStandingRow.From( t, i + 1, !userTeamId.IsEmpty && t.Id.Value == userTeamId.Value ) )
			.ToList();

		return new LeagueHomeViewModel
		{
			LeagueName = state.Settings.LeagueName,
			Season = state.CurrentSeason,
			Week = state.CurrentWeek,
			Phase = state.Phase.ToString(),
			CalendarLabel = state.Calendar?.Label ?? "",
			CalendarMonth = state.Calendar?.Month.ToString() ?? "",
			DayOfWeek = state.Calendar?.DayOfWeek ?? 1,
			NextSuggestedAction = state.NextSuggestedAction ?? "",
			InboxActionCount = state.Inbox.Count( m => m.RequiresAction && !m.IsResolved ),
			StateRevision = state.StateRevision,
			IsHumanFired = FranchiseRetentionSystem.IsHumanFired( state ),
			Franchise = FranchiseCardViewModel.From( state, userTeamId ),
			Stakes = FranchiseStakesViewModel.From( state, userTeamId ),
			NextGame = NextGameCardViewModel.From( state, userTeamId ),
			Standings = standings,
			RecentNews = state.News.Take( 8 ).Select( NewsRow.From ).ToList()
		};
	}
}

public sealed class FranchiseStakesViewModel
{
	public int DynastyScore { get; init; }
	public int OwnerJobSecurity { get; init; }
	public string SeasonObjective { get; init; } = "";
	public string OwnerMandate { get; init; } = "";
	public string RivalLabel { get; init; } = "";
	public bool ShowFtueHint { get; init; }

	public static FranchiseStakesViewModel From( LeagueState state, TeamId userTeamId )
	{
		var progress = state?.FranchiseProgress;
		var obj = progress?.SeasonObjective;
		var objectiveLine = obj == null
			? ""
			: obj.Completed
				? "Season mandate complete"
				: $"{obj.Description} ({obj.Progress}/{obj.Target})";

		var challenge = state?.Settings.ChallengeMode switch
		{
			ChallengeMode.Rebuild => "Rebuild: playoffs within 3 seasons",
			ChallengeMode.WinNow => "Win Now: championship within 5 seasons",
			ChallengeMode.DraftGenius => "Draft Genius: find 3 steals",
			_ => "Standard franchise"
		};

		return new FranchiseStakesViewModel
		{
			DynastyScore = progress?.DynastyScore ?? 0,
			OwnerJobSecurity = progress?.OwnerJobSecurity ?? 80,
			SeasonObjective = objectiveLine,
			OwnerMandate = challenge,
			RivalLabel = string.IsNullOrEmpty( progress?.RivalTeamAbbreviation ) ? "" : $"Rival: {progress.RivalTeamAbbreviation}",
			ShowFtueHint = FtueHelper.IsFtueActive( state )
		};
	}
}

public sealed class FranchiseCardViewModel
{
	public bool HasTeam { get; init; }
	public string DisplayName { get; init; } = "";
	public string Abbreviation { get; init; } = "";
	public string Record { get; init; } = "0-0";
	public int Prestige { get; init; }
	public int Morale { get; init; }
	public long CapSpace { get; init; }
	public int RosterCount { get; init; }
	public string BuildingWindow { get; init; } = "";
	public int StandingRank { get; init; }
	public int OffenseOverall { get; init; }
	public int DefenseOverall { get; init; }
	public int TeamOverall { get; init; }

	public int DepthChartFilled { get; init; }
	public int DepthChartTotal { get; init; }
	public string DepthChartLabel { get; init; } = "";

	public static FranchiseCardViewModel From( LeagueState state, TeamId userTeamId )
	{
		if ( userTeamId.IsEmpty || !state.Teams.TryGetValue( userTeamId, out var team ) )
			return new FranchiseCardViewModel();

		var rank = state.Teams.Values
			.OrderByDescending( t => t.Record.Wins )
			.ThenBy( t => t.Record.Losses )
			.Select( ( t, i ) => ( t.Id, Rank: i + 1 ) )
			.FirstOrDefault( x => x.Id.Value == userTeamId.Value )
			.Rank;

		var (filled, total) = DepthChartSystem.GetStarterCompletion( state, userTeamId );
		var profile = TeamProfileBuilder.Build( state, userTeamId );

		return new FranchiseCardViewModel
		{
			HasTeam = true,
			DisplayName = $"{team.Identity.City} {team.Identity.Name}",
			Abbreviation = team.Identity.Abbreviation,
			Record = FormatRecord( team ),
			Prestige = team.Prestige.Prestige,
			Morale = team.Chemistry.Morale,
			CapSpace = team.Finances.SalaryCapSpace,
			RosterCount = team.RosterPlayerIds.Count,
			BuildingWindow = team.BuildingWindow.ToString(),
			StandingRank = rank,
			OffenseOverall = profile.OffenseRating,
			DefenseOverall = profile.DefenseRating,
			TeamOverall = TeamProfileBuilder.ComputeOverallRating( profile ),
			DepthChartFilled = filled,
			DepthChartTotal = total,
			DepthChartLabel = total > 0 ? $"{filled}/{total} starters set" : ""
		};
	}

	static string FormatRecord( TeamState team )
	{
		var ties = team.Record.Ties > 0 ? $"-{team.Record.Ties}" : "";
		return $"{team.Record.Wins}-{team.Record.Losses}{ties}";
	}
}

public sealed class NextGameCardViewModel
{
	public bool HasGame { get; init; }
	public GameId GameId { get; init; }
	public int Week { get; init; }
	public string Opponent { get; init; } = "";
	public string OpponentAbbr { get; init; } = "";
	public bool IsHome { get; init; }
	public bool IsComplete { get; init; }
	public string MatchupLabel { get; init; } = "";
	public string PlayoffLabel { get; init; } = "";

	public bool IsUserGamePending { get; init; }

	public static NextGameCardViewModel From( LeagueState state, TeamId userTeamId )
	{
		if ( userTeamId.IsEmpty )
			return new NextGameCardViewModel();

		var game = state.Schedule.Games
			.Where( g => g.Season == state.CurrentSeason )
			.Where( g => g.HomeTeamId.Value == userTeamId.Value || g.AwayTeamId.Value == userTeamId.Value )
			.OrderBy( g => g.IsComplete ? 1 : 0 )
			.ThenBy( g => g.Week )
			.FirstOrDefault();

		if ( game == null )
			return new NextGameCardViewModel();

		var isHome = game.HomeTeamId.Value == userTeamId.Value;
		var opponentId = isHome ? game.AwayTeamId : game.HomeTeamId;
		var opponent = state.Teams[opponentId];
		var user = state.Teams[userTeamId];
		var playoff = game.IsPlayoffGame ? game.PlayoffRound.ToString() : "";

		return new NextGameCardViewModel
		{
			HasGame = true,
			GameId = game.Id,
			Week = game.Week,
			Opponent = $"{opponent.Identity.City} {opponent.Identity.Name}",
			OpponentAbbr = opponent.Identity.Abbreviation,
			IsHome = isHome,
			IsComplete = game.IsComplete,
			IsUserGamePending = !game.IsComplete && game.Week == state.CurrentWeek,
			MatchupLabel = isHome
				? $"{user.Identity.Abbreviation} vs {opponent.Identity.Abbreviation}"
				: $"{user.Identity.Abbreviation} @ {opponent.Identity.Abbreviation}",
			PlayoffLabel = playoff
		};
	}
}

public sealed class TeamStandingRow
{
	public TeamId TeamId { get; init; }
	public int Rank { get; init; }
	public string Abbreviation { get; init; }
	public string DisplayName { get; init; }
	public int Wins { get; init; }
	public int Losses { get; init; }
	public int Ties { get; init; }
	public bool IsUserTeam { get; init; }

	public static TeamStandingRow From( TeamState team, int rank, bool isUserTeam ) => new()
	{
		TeamId = team.Id,
		Rank = rank,
		Abbreviation = team.Identity.Abbreviation,
		DisplayName = $"{team.Identity.City} {team.Identity.Name}",
		Wins = team.Record.Wins,
		Losses = team.Record.Losses,
		Ties = team.Record.Ties,
		IsUserTeam = isUserTeam
	};
}

public sealed class NewsRow
{
	public Guid Id { get; init; }
	public string Headline { get; init; }
	public string Category { get; init; }
	public string WeekLabel { get; init; }
	public string Preview { get; init; }

	public static NewsRow From( Domain.News.NewsItem item ) => new()
	{
		Id = item.Id,
		Headline = item.Headline,
		Category = item.Category.ToString(),
		WeekLabel = $"S{item.Season} W{item.Week}",
		Preview = Truncate( item.Body, 80 )
	};

	static string Truncate( string text, int max )
	{
		if ( string.IsNullOrEmpty( text ) || text.Length <= max )
			return text ?? "";

		return text[..(max - 1)] + "…";
	}
}
