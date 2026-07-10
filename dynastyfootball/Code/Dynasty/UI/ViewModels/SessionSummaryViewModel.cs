using Dynasty.Core;
using Dynasty.Core.Enums;
using Dynasty.Core.Identifiers;
using Dynasty.Domain.Franchise;
using Dynasty.Domain.League;
using Dynasty.Domain.Teams;
using Dynasty.Systems.DepthChart;
using Dynasty.Systems.Simulation;

namespace Dynasty.UI.ViewModels;

public sealed class SessionSummaryViewModel
{
	public string Headline { get; init; } = "";
	public string RecordLine { get; init; } = "";
	public string DynastyLine { get; init; } = "";
	public string JobSecurityLine { get; init; } = "";
	public string CliffhangerLine { get; init; } = "";
	public string NextStepLine { get; init; } = "";

	public static SessionSummaryViewModel From( LeagueState state, TeamId userTeamId )
	{
		if ( state == null || userTeamId.IsEmpty || !state.Teams.TryGetValue( userTeamId, out var team ) )
			return new SessionSummaryViewModel { Headline = "See you next time." };

		var progress = state.FranchiseProgress;
		var record = $"{team.Record.Wins}-{team.Record.Losses}";
		var headline = state.Phase switch
		{
			LeaguePhase.Draft when state.Draft.IsActive => "The draft board is waiting.",
			LeaguePhase.FreeAgency => "Free agency is heating up.",
			LeaguePhase.Playoffs => "Playoff pressure is on.",
			_ => $"Season {state.CurrentSeason}, Week {state.CurrentWeek}"
		};

		var cliffhanger = BuildCliffhanger( state, team, progress );
		var next = state.NextSuggestedAction ?? "Review your inbox when you return.";

		return new SessionSummaryViewModel
		{
			Headline = headline,
			RecordLine = $"Record: {record}",
			DynastyLine = $"Dynasty Score: {progress?.DynastyScore ?? 0}",
			JobSecurityLine = $"Job Security: {progress?.OwnerJobSecurity ?? 0}%",
			CliffhangerLine = cliffhanger,
			NextStepLine = next
		};
	}

	static string BuildCliffhanger( LeagueState state, TeamState team, FranchiseProgressState progress )
	{
		if ( state.Phase == LeaguePhase.Draft && state.Draft.IsActive )
			return "You're on the clock — the league won't wait forever.";

		if ( !string.IsNullOrEmpty( progress?.RivalTeamAbbreviation ) )
		{
			var rivalGame = state.Schedule.Games.FirstOrDefault( g =>
				g.Season == state.CurrentSeason
				&& !g.IsComplete
				&& ( g.HomeTeamId.Value == team.Id.Value || g.AwayTeamId.Value == team.Id.Value )
				&& ( g.HomeTeamId.Value == progress.RivalTeamId.Value || g.AwayTeamId.Value == progress.RivalTeamId.Value ) );

			if ( rivalGame != null )
				return $"Rival week vs {progress.RivalTeamAbbreviation} is coming — bragging rights on the line.";
		}

		if ( progress?.OwnerJobSecurity <= 30 )
			return "The owner is watching closely. Your next decision matters.";

		if ( team.Record.Wins >= 2 && team.Record.Losses == 0 )
			return "You're undefeated — can you keep the streak alive?";

		return "Your franchise story continues next session.";
	}
}
