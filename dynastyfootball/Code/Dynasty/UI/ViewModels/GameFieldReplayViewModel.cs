using Dynasty.Core.Enums;
using Dynasty.Core.Identifiers;
using Dynasty.Domain.League;
using Dynasty.Domain.Simulation;
using Dynasty.Systems.Simulation;

namespace Dynasty.UI.ViewModels;

public sealed class GameFieldReplayViewModel
{
	public int BallYardLine { get; init; } = 50;
	public float BallNormalizedX { get; init; } = 0.5f;
	public string PossessionAbbr { get; init; } = "";
	public string EventLabel { get; init; } = "Kickoff";
	public string DownDistance { get; init; } = "";
	public string ScoreLine { get; init; } = "";
	public bool IsScoring { get; init; }
	public string EventClass { get; init; } = "";

	public static GameFieldReplayViewModel From(
		LeagueState state,
		SimEventRecord ev,
		TeamId homeTeamId,
		TeamId awayTeamId )
	{
		if ( state == null || ev == null )
			return new GameFieldReplayViewModel();

		var possession = ev.PossessionTeamId;
		var abbr = "—";
		if ( !possession.IsEmpty && state.Teams.TryGetValue( possession, out var team ) )
			abbr = team.Identity.Abbreviation;

		var isScoring = ev.Type is SimEventType.Score or SimEventType.FieldGoalAttempt;
		var eventClass = ev.Type switch
		{
			SimEventType.Score => "score",
			SimEventType.Turnover => "turnover",
			SimEventType.FieldGoalAttempt => "score",
			SimEventType.Punt => "punt",
			_ => "neutral"
		};

		var yardLine = Math.Clamp( ev.YardLine, 0, 100 );
		var normalized = yardLine / 100f;

		return new GameFieldReplayViewModel
		{
			BallYardLine = yardLine,
			BallNormalizedX = normalized,
			PossessionAbbr = abbr,
			EventLabel = ev.Description,
			DownDistance = ev.Type == SimEventType.DriveStart ? "" : SimReplayDisplay.FormatDriveSummary( ev.Quarter, ev.ClockSeconds ),
			ScoreLine = $"{ev.AwayScore} - {ev.HomeScore}",
			IsScoring = isScoring,
			EventClass = eventClass
		};
	}
}
