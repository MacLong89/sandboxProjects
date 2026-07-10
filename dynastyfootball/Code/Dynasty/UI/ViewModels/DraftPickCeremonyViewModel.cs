using Dynasty.Core.Identifiers;
using Dynasty.Domain.League;

namespace Dynasty.UI.ViewModels;

public sealed class DraftPickCeremonyViewModel
{
	public int Round { get; init; }
	public int OverallPick { get; init; }
	public string TeamAbbr { get; init; } = "";
	public string PlayerName { get; init; } = "";
	public string Position { get; init; } = "";
	public int Overall { get; init; }
	public int ConsensusRank { get; init; }
	public string Grade { get; init; } = "";
	public string Headline { get; init; } = "";
	public bool IsSteal { get; init; }
	public bool IsReach { get; init; }

	public static DraftPickCeremonyViewModel From( LeagueState state, TeamId teamId, PlayerId prospectId, int round, int overallPick )
	{
		if ( state == null )
			return null;

		var prospect = state.Draft.Prospects.FirstOrDefault( p => p.Id.Value == prospectId.Value );
		if ( prospect == null )
			return null;

		state.Teams.TryGetValue( teamId, out var team );
		var rank = prospect.ConsensusRank;
		var isSteal = rank > 0 && overallPick >= rank + 12;
		var isReach = rank > 0 && overallPick + 12 <= rank;

		var grade = isSteal ? "STEAL" : isReach ? "REACH" : overallPick <= rank + 5 ? "SOLID" : "VALUE";

		return new DraftPickCeremonyViewModel
		{
			Round = round,
			OverallPick = overallPick,
			TeamAbbr = team?.Identity.Abbreviation ?? "???",
			PlayerName = prospect.Player.Identity.FullName,
			Position = prospect.Player.Identity.Position.ToString(),
			Overall = prospect.Player.Ratings.Overall,
			ConsensusRank = rank,
			Grade = grade,
			IsSteal = isSteal,
			IsReach = isReach,
			Headline = isSteal
				? "Front office fleeces the board!"
				: isReach
					? "Bold pick — will it pay off?"
					: "New addition to the roster"
		};
	}
}
