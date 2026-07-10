using Dynasty.Core;
using Dynasty.Core.Enums;
using Dynasty.Core.Identifiers;
using Dynasty.Domain.League;

namespace Dynasty.UI.ViewModels;

public sealed class DraftRoomViewModel
{
	public bool IsActive { get; init; }
	public bool IsUserOnClock { get; init; }
	public int Round { get; init; }
	public string OnClock { get; init; }
	public IReadOnlyList<ProspectRow> Prospects { get; init; } = Array.Empty<ProspectRow>();

	public static DraftRoomViewModel From( LeagueState state, TeamId viewingTeamId )
	{
		if ( state == null )
			return new DraftRoomViewModel();

		var current = state.Draft.Order.ElementAtOrDefault( state.Draft.CurrentPickIndex );
		var onClock = current != null && state.Teams.TryGetValue( current.TeamId, out var team )
			? team.Identity.Abbreviation
			: "—";

		var prospects = state.Draft.Prospects
			.Where( p => !p.IsDrafted )
			.OrderBy( p => p.ConsensusRank )
			.Take( 25 )
			.Select( p => ProspectRow.From( state, p, viewingTeamId ) )
			.ToList();

		var userOnClock = current != null && GmAssignmentHelper.IsHumanTeam( state, current.TeamId );

		return new DraftRoomViewModel
		{
			IsActive = state.Draft.IsActive,
			IsUserOnClock = userOnClock,
			Round = state.Draft.CurrentRound,
			OnClock = onClock,
			Prospects = prospects
		};
	}
}

public sealed class ProspectRow
{
	public PlayerId Id { get; init; }
	public string Name { get; init; }
	public string Position { get; init; }
	public Position PositionEnum { get; init; }
	public string Age { get; init; }
	public int AgeYears { get; init; }
	public string Overall { get; init; }
	public int? OverallRating { get; init; }
	public string Potential { get; init; }
	public int? PotentialRating { get; init; }
	public int Rank { get; init; }

	public static ProspectRow From( LeagueState state, Domain.Draft.ProspectState prospect, TeamId teamId )
	{
		var scouting = prospect.Player.Scouting;
		var identity = prospect.Player.Identity;
		return new ProspectRow
		{
			Id = prospect.Id,
			Name = identity.FullName,
			Position = identity.Position.ToString(),
			PositionEnum = identity.Position,
			Age = identity.Age.ToString(),
			AgeYears = identity.Age,
			Overall = scouting.OverallRevealed ? prospect.Player.Ratings.Overall.ToString() : "??",
			OverallRating = scouting.OverallRevealed ? prospect.Player.Ratings.Overall : null,
			Potential = scouting.PotentialRevealed ? prospect.Player.Ratings.Potential.ToString() : "??",
			PotentialRating = scouting.PotentialRevealed ? prospect.Player.Ratings.Potential : null,
			Rank = prospect.ConsensusRank
		};
	}
}
