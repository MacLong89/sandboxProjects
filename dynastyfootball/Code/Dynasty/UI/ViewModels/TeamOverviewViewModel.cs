using Dynasty.Core.Identifiers;
using Dynasty.Core.Positions;
using Dynasty.Domain.League;
using Dynasty.Domain.Players;

namespace Dynasty.UI.ViewModels;

public sealed class TeamOverviewViewModel
{
	public string DisplayName { get; init; }
	public string Abbreviation { get; init; }
	public int Prestige { get; init; }
	public int Morale { get; init; }
	public long Budget { get; init; }
	public long CapSpace { get; init; }
	public string BuildingWindow { get; init; }
	public string PlayStyle { get; init; }
	public int RosterCount { get; init; }
	public IReadOnlyList<RosterPlayerRow> Roster { get; init; } = Array.Empty<RosterPlayerRow>();

	public static TeamOverviewViewModel From( LeagueState state, TeamId teamId )
	{
		if ( state == null || !state.Teams.TryGetValue( teamId, out var team ) )
			return new TeamOverviewViewModel { DisplayName = "Unknown Team" };

		var displayPositions = RosterPositionHelper.BuildDisplayPositions( team, state );

		var roster = team.RosterPlayerIds
			.Select( id => state.Players.GetValueOrDefault( id ) )
			.Where( p => p != null && !p.IsRetired )
			.Select( p => RosterPlayerRow.From( p, displayPositions.GetValueOrDefault( p.Id, p.Identity.Position.ToString() ) ) )
			.OrderBy( p => RosterPositionHelper.SortIndex( p.Position ) )
			.ThenByDescending( p => p.Overall )
			.ThenBy( p => p.Name )
			.ToList();

		return new TeamOverviewViewModel
		{
			DisplayName = $"{team.Identity.City} {team.Identity.Name}",
			Abbreviation = team.Identity.Abbreviation,
			Prestige = team.Prestige.Prestige,
			Morale = team.Chemistry.Morale,
			Budget = team.Finances.Budget,
			CapSpace = team.Finances.SalaryCapSpace,
			BuildingWindow = team.BuildingWindow.ToString(),
			PlayStyle = team.PlayStyle.ToString(),
			RosterCount = team.RosterPlayerIds.Count,
			Roster = roster
		};
	}
}

public sealed class RosterPlayerRow
{
	public PlayerId Id { get; init; }
	public string Name { get; init; }
	public string Position { get; init; }
	public int Overall { get; init; }
	public int Age { get; init; }
	public string Health { get; init; }

	public static RosterPlayerRow From( PlayerState player, string displayPosition ) => new()
	{
		Id = player.Id,
		Name = player.Identity.FullName,
		Position = displayPosition,
		Overall = player.Ratings.Overall,
		Age = player.Identity.Age,
		Health = player.Injury.Severity == Core.Enums.InjurySeverity.None ? "Healthy" : player.Injury.Severity.ToString()
	};
}
