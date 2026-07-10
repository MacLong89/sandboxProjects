using Dynasty.Core;
using Dynasty.Core.Enums;
using Dynasty.Core.Identifiers;
using Dynasty.Domain.League;

namespace Dynasty.UI.ViewModels;

public static class ScheduleWeekHelper
{
	public static int GetFocusWeek( LeagueState state )
	{
		if ( state == null )
			return 1;

		var (min, max) = GetSeasonWeekRange( state );
		return Math.Clamp( Math.Max( 1, state.CurrentWeek ), min, max );
	}

	public static (int Min, int Max) GetSeasonWeekRange( LeagueState state )
	{
		if ( state == null )
			return (1, 1);

		var weeks = state.Schedule.Games
			.Where( g => g.Season == state.CurrentSeason )
			.Select( g => g.Week )
			.ToList();

		if ( weeks.Count == 0 )
		{
			var fallbackMax = state.Phase switch
			{
				LeaguePhase.Preseason => state.Settings.PreseasonWeeks,
				LeaguePhase.RegularSeason => state.Settings.RegularSeasonWeeks,
				LeaguePhase.Playoffs => state.Settings.PlayoffWeeks,
				_ => state.Settings.RegularSeasonWeeks
			};

			return (1, Math.Max( 1, fallbackMax ) );
		}

		return (weeks.Min(), weeks.Max());
	}

	public static int ClampViewWeek( LeagueState state, int requestedWeek )
	{
		var (min, max) = GetSeasonWeekRange( state );
		return Math.Clamp( requestedWeek, min, max );
	}

	public static string FormatPhaseLabel( LeaguePhase phase ) => phase switch
	{
		LeaguePhase.Preseason => "Preseason",
		LeaguePhase.RegularSeason => "Regular Season",
		LeaguePhase.Playoffs => "Playoffs",
		_ => ""
	};
}

public sealed class ScheduleViewModel
{
	public int Season { get; init; }
	public int Week { get; init; }
	public int FocusWeek { get; init; }
	public string PhaseLabel { get; init; } = "";
	public bool IsViewingCurrentWeek { get; init; }
	public IReadOnlyList<ScheduleGameRow> Games { get; init; } = Array.Empty<ScheduleGameRow>();

	public static ScheduleViewModel From( LeagueState state, int week, bool userGamesOnly = false )
	{
		if ( state == null )
			return new ScheduleViewModel();

		var focusWeek = ScheduleWeekHelper.GetFocusWeek( state );
		var viewWeek = ScheduleWeekHelper.ClampViewWeek( state, week );
		var userTeam = GmAssignmentHelper.GetHumanTeamId( state );
		var games = state.Schedule.Games
			.Where( g => g.Season == state.CurrentSeason && g.Week == viewWeek )
			.Where( g => !userGamesOnly || !userTeam.IsEmpty && ( g.HomeTeamId.Value == userTeam.Value || g.AwayTeamId.Value == userTeam.Value ) )
			.OrderBy( g => g.IsPlayoffGame )
			.ThenBy( g => g.HomeTeamId.Value )
			.Select( g => ScheduleGameRow.From( state, g, userTeam ) )
			.ToList();

		return new ScheduleViewModel
		{
			Season = state.CurrentSeason,
			Week = viewWeek,
			FocusWeek = focusWeek,
			PhaseLabel = ScheduleWeekHelper.FormatPhaseLabel( state.Phase ),
			IsViewingCurrentWeek = viewWeek == focusWeek,
			Games = games
		};
	}
}

public sealed class ScheduleGameRow
{
	public GameId GameId { get; init; }
	public string AwayAbbreviation { get; init; } = "";
	public string HomeAbbreviation { get; init; } = "";
	public string AwayRecord { get; init; } = "";
	public string HomeRecord { get; init; } = "";
	public int? AwayScore { get; init; }
	public int? HomeScore { get; init; }
	public bool IsComplete { get; init; }
	public bool IsUserTeam { get; init; }
	public string PlayoffLabel { get; init; } = "";
	public string ActionLabel => IsComplete ? "Watch" : "Simulate";

	public static ScheduleGameRow From( LeagueState state, Domain.Schedule.ScheduledGame game, TeamId userTeam )
	{
		var homeTeam = state.Teams[game.HomeTeamId];
		var awayTeam = state.Teams[game.AwayTeamId];
		var isUser = !userTeam.IsEmpty && ( game.HomeTeamId.Value == userTeam.Value || game.AwayTeamId.Value == userTeam.Value );
		var playoffLabel = game.IsPlayoffGame ? game.PlayoffRound.ToString() : "";

		return new ScheduleGameRow
		{
			GameId = game.Id,
			AwayAbbreviation = awayTeam.Identity.Abbreviation,
			HomeAbbreviation = homeTeam.Identity.Abbreviation,
			AwayRecord = FormatRecord( awayTeam.Record ),
			HomeRecord = FormatRecord( homeTeam.Record ),
			AwayScore = game.IsComplete ? game.Result.AwayScore : null,
			HomeScore = game.IsComplete ? game.Result.HomeScore : null,
			IsComplete = game.IsComplete,
			IsUserTeam = isUser,
			PlayoffLabel = playoffLabel
		};
	}

	static string FormatRecord( Domain.Teams.TeamRecord record )
	{
		if ( record.Ties > 0 )
			return $"{record.Wins}-{record.Losses}-{record.Ties}";

		return $"{record.Wins}-{record.Losses}";
	}
}
