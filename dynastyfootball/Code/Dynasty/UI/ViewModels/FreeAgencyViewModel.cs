using Dynasty.Core;
using Dynasty.Core.Identifiers;
using Dynasty.Domain.Contracts;
using Dynasty.Domain.League;
using Dynasty.Domain.Players;

namespace Dynasty.UI.ViewModels;

public sealed class FreeAgencyViewModel
{
	public bool IsOpen { get; init; }
	public long CapSpace { get; init; }
	public int AvailableCount { get; init; }
	public int PendingOfferCount { get; init; }
	public IReadOnlyList<FreeAgentRow> Agents { get; init; } = Array.Empty<FreeAgentRow>();
	public IReadOnlyList<PendingOfferRow> PendingOffers { get; init; } = Array.Empty<PendingOfferRow>();

	public static FreeAgencyViewModel From( LeagueState state, TeamId teamId )
	{
		if ( state == null )
			return new FreeAgencyViewModel();

		state.Teams.TryGetValue( teamId, out var team );
		var capSpace = team?.Finances.SalaryCapSpace ?? 0;

		var agents = state.FreeAgency.AvailablePlayers
			.Select( id => state.Players.GetValueOrDefault( id ) )
			.Where( p => p != null && !p.IsRetired )
			.OrderByDescending( p => p.Ratings.Overall )
			.ThenBy( p => p.Identity.FullName )
			.Select( p => FreeAgentRow.From( p ) )
			.ToList();

		var pending = teamId.IsEmpty
			? new List<PendingOfferRow>()
			: state.FreeAgency.PendingOffers
				.Where( o => o.TeamId.Value == teamId.Value )
				.Select( o => PendingOfferRow.From( state, o ) )
				.ToList();

		return new FreeAgencyViewModel
		{
			IsOpen = state.FreeAgency.IsOpen,
			CapSpace = capSpace,
			AvailableCount = agents.Count,
			PendingOfferCount = pending.Count,
			Agents = agents,
			PendingOffers = pending
		};
	}
}

public sealed class FreeAgentRow
{
	public PlayerId Id { get; init; }
	public string Name { get; init; } = "";
	public string Position { get; init; } = "";
	public int Overall { get; init; }
	public int Age { get; init; }
	public int AskingSalary { get; init; }
	public string PreviousTeam { get; init; } = "—";

	public static FreeAgentRow From( PlayerState player ) => new()
	{
		Id = player.Id,
		Name = player.Identity.FullName,
		Position = player.Identity.Position.ToString(),
		Overall = player.Ratings.Overall,
		Age = player.Identity.Age,
		AskingSalary = EstimateAskingSalary( player )
	};

	static int EstimateAskingSalary( PlayerState player )
	{
		var baseSalary = player.Ratings.Overall * 140_000;
		if ( player.Contract.AnnualSalary > 0 )
			baseSalary = Math.Max( baseSalary, (int)( player.Contract.AnnualSalary * 0.85f ) );

		return Math.Max( 750_000, baseSalary );
	}
}

public sealed class PendingOfferRow
{
	public string PlayerName { get; init; } = "";
	public int Years { get; init; }
	public int AnnualSalary { get; init; }
	public string Status { get; init; } = "Pending";

	public static PendingOfferRow From( LeagueState state, ContractOffer offer )
	{
		var name = state.Players.TryGetValue( offer.PlayerId, out var player )
			? player.Identity.FullName
			: "Unknown";

		return new PendingOfferRow
		{
			PlayerName = name,
			Years = offer.Years,
			AnnualSalary = offer.AnnualSalary,
			Status = offer.Accepted ? "Signed" : "Pending"
		};
	}
}
