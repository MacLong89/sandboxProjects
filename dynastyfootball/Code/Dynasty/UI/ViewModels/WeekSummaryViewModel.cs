using Dynasty.Core.Enums;
using Dynasty.Core.Identifiers;
using Dynasty.Domain.League;
using Dynasty.Domain.Teams;
using Dynasty.Systems.Season;

namespace Dynasty.UI.ViewModels;

public sealed class WeekSummaryViewModel
{
	public string Title { get; init; } = "Week Summary";
	public string Headline { get; init; } = "";
	public IReadOnlyList<string> Lines { get; init; } = Array.Empty<string>();

	public static WeekSummaryViewModel Build(
		LeagueState state,
		TeamId humanTeamId,
		int closedSeason,
		int closedWeek,
		LeaguePhase closedPhase )
	{
		if ( state == null )
			return new WeekSummaryViewModel();

		var lines = new List<string>();
		var title = $"{PhaseLabel( closedPhase )} · Season {closedSeason} · Week {closedWeek}";

		var weekNews = state.News
			.Where( n => n.Season == closedSeason && n.Week == closedWeek )
			.Take( 5 )
			.ToList();

		if ( weekNews.Count > 0 )
		{
			lines.Add( "League headlines:" );
			foreach ( var item in weekNews )
				lines.Add( $"• {item.Headline}" );
		}

		if ( !humanTeamId.IsEmpty && state.Teams.TryGetValue( humanTeamId, out var team ) )
		{
			var userGame = state.Schedule.Games.FirstOrDefault( g =>
				g.Season == closedSeason
				&& g.Week == closedWeek
				&& g.IsComplete
				&& ( g.HomeTeamId.Value == humanTeamId.Value || g.AwayTeamId.Value == humanTeamId.Value ) );

			if ( userGame?.Result != null )
			{
				var isHome = userGame.HomeTeamId.Value == humanTeamId.Value;
				var us = isHome ? userGame.Result.HomeScore : userGame.Result.AwayScore;
				var them = isHome ? userGame.Result.AwayScore : userGame.Result.HomeScore;
				var oppId = isHome ? userGame.AwayTeamId : userGame.HomeTeamId;
				state.Teams.TryGetValue( oppId, out var opponent );
				var oppAbbr = opponent?.Identity.Abbreviation ?? "???";
				var result = us > them ? "Win" : us < them ? "Loss" : "Tie";
				lines.Add( $"Your game: {result} vs {oppAbbr} ({us}-{them})" );
			}

			lines.Add( $"Record: {TeamRecordArchive.FormatRecord( team.Record.Wins, team.Record.Losses, team.Record.Ties )}" );

			var rank = state.Teams.Values
				.OrderByDescending( t => t.Record.Wins )
				.ThenBy( t => t.Record.Losses )
				.Select( ( t, i ) => ( t.Id, Rank: i + 1 ) )
				.FirstOrDefault( x => x.Id.Value == humanTeamId.Value )
				.Rank;
			if ( rank > 0 )
				lines.Add( $"League rank: #{rank}" );
		}

		var newInjuries = state.Players.Values
			.Where( p => !humanTeamId.IsEmpty && p.TeamId.Value == humanTeamId.Value )
			.Where( p => p.Injury.Severity != InjurySeverity.None && p.Injury.WeeksRemaining > 0 )
			.Take( 3 )
			.Select( p => $"{p.Identity.FullName}: {p.Injury.Description}" )
			.ToList();

		if ( newInjuries.Count > 0 )
		{
			lines.Add( "Injury report:" );
			foreach ( var injury in newInjuries )
				lines.Add( $"• {injury}" );
		}

		var actionable = state.Inbox
			.Where( m => !m.IsResolved && m.RequiresAction )
			.OrderByDescending( m => m.Priority )
			.Take( 3 )
			.Select( m => m.Subject )
			.ToList();

		if ( actionable.Count > 0 )
		{
			lines.Add( "Needs your attention:" );
			foreach ( var item in actionable )
				lines.Add( $"• {item}" );
		}
		else if ( !string.IsNullOrEmpty( state.NextSuggestedAction ) )
		{
			lines.Add( $"Up next: {state.NextSuggestedAction}" );
		}

		if ( lines.Count == 0 )
			lines.Add( "The league moved forward — check Inbox and News for details." );

		var headline = state.Phase switch
		{
			LeaguePhase.Preseason => "Training camp rolls on.",
			LeaguePhase.RegularSeason => "Another week in the books.",
			LeaguePhase.Playoffs => "Playoff race update.",
			LeaguePhase.Offseason => $"Offseason: {state.OffseasonSubPhase}",
			LeaguePhase.FreeAgency => "Free agency continues.",
			LeaguePhase.Draft => "Draft board update.",
			_ => "Week complete."
		};

		return new WeekSummaryViewModel
		{
			Title = title,
			Headline = headline,
			Lines = lines
		};
	}

	static string PhaseLabel( LeaguePhase phase ) => phase switch
	{
		LeaguePhase.Preseason => "Preseason",
		LeaguePhase.RegularSeason => "Regular Season",
		LeaguePhase.Playoffs => "Playoffs",
		LeaguePhase.Offseason => "Offseason",
		LeaguePhase.FreeAgency => "Free Agency",
		LeaguePhase.Draft => "Draft",
		_ => phase.ToString()
	};
}
