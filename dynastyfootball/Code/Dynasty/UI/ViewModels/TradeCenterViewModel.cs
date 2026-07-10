using Dynasty.Core.Identifiers;
using Dynasty.Domain.League;
using Dynasty.Domain.Trades;

namespace Dynasty.UI.ViewModels;

public sealed class TradeCenterViewModel
{
	public TeamId TeamId { get; init; }
	public string TeamName { get; init; } = "";
	public IReadOnlyList<TradePartnerRow> Partners { get; init; } = Array.Empty<TradePartnerRow>();
	public IReadOnlyList<TradePlayerRow> Roster { get; init; } = Array.Empty<TradePlayerRow>();
	public IReadOnlyList<PendingTradeOfferRow> PendingOffers { get; init; } = Array.Empty<PendingTradeOfferRow>();
	public IReadOnlyList<DraftPickRow> DraftPicks { get; init; } = Array.Empty<DraftPickRow>();

	public static TradeCenterViewModel From( LeagueState state, TeamId userTeamId )
	{
		if ( state == null || userTeamId.IsEmpty || !state.Teams.TryGetValue( userTeamId, out var team ) )
			return new TradeCenterViewModel();

		var partners = state.Teams.Values
			.Where( t => t.Id.Value != userTeamId.Value )
			.OrderBy( t => t.Identity.City )
			.Select( t => new TradePartnerRow
			{
				TeamId = t.Id,
				Label = $"{t.Identity.City} {t.Identity.Name}",
				Abbreviation = t.Identity.Abbreviation,
				Record = $"{t.Record.Wins}-{t.Record.Losses}"
			} )
			.ToList();

		var roster = team.RosterPlayerIds
			.Select( id => state.Players.GetValueOrDefault( id ) )
			.Where( p => p != null && !p.IsRetired )
			.OrderByDescending( p => p.Ratings.Overall )
			.Select( p => new TradePlayerRow
			{
				PlayerId = p.Id,
				Name = p.Identity.FullName,
				Position = p.Identity.Position.ToString(),
				Overall = p.Ratings.Overall,
				AnnualSalary = p.Contract.AnnualSalary
			} )
			.ToList();

		var offers = state.PendingTradeOffers
			.Where( o => o.ToTeamId.Value == userTeamId.Value )
			.Select( o => PendingTradeOfferRow.From( state, o ) )
			.ToList();

		var picks = team.DraftPicks
			.Where( p => p.Season >= state.CurrentSeason )
			.OrderBy( p => p.Season )
			.ThenBy( p => p.Round )
			.Select( p => DraftPickRow.From( p ) )
			.ToList();

		return new TradeCenterViewModel
		{
			TeamId = userTeamId,
			TeamName = $"{team.Identity.City} {team.Identity.Name}",
			Partners = partners,
			Roster = roster,
			PendingOffers = offers,
			DraftPicks = picks
		};
	}

	public static IReadOnlyList<TradePlayerRow> GetPartnerRoster( LeagueState state, TeamId partnerId )
	{
		if ( state == null || partnerId.IsEmpty || !state.Teams.TryGetValue( partnerId, out var team ) )
			return Array.Empty<TradePlayerRow>();

		return team.RosterPlayerIds
			.Select( id => state.Players.GetValueOrDefault( id ) )
			.Where( p => p != null && !p.IsRetired )
			.OrderByDescending( p => p.Ratings.Overall )
			.Select( p => new TradePlayerRow
			{
				PlayerId = p.Id,
				Name = p.Identity.FullName,
				Position = p.Identity.Position.ToString(),
				Overall = p.Ratings.Overall,
				AnnualSalary = p.Contract.AnnualSalary
			} )
			.ToList();
	}
}

public sealed class PendingTradeOfferRow
{
	public Guid OfferId { get; init; }
	public string FromTeam { get; init; } = "";
	public string Summary { get; init; } = "";
	public float FairnessHint { get; init; }

	public static PendingTradeOfferRow From( LeagueState state, PendingTradeOffer offer )
	{
		state.Teams.TryGetValue( offer.FromTeamId, out var from );
		var eval = Dynasty.Systems.Trade.TradeSystem.EvaluateProposal( state, offer.Proposal );

		return new PendingTradeOfferRow
		{
			OfferId = offer.OfferId,
			FromTeam = from != null ? $"{from.Identity.City} {from.Identity.Name}" : "?",
			Summary = offer.Summary,
			FairnessHint = eval.FairnessScore
		};
	}
}

public sealed class DraftPickRow
{
	public string Label { get; init; } = "";

	public static DraftPickRow From( Domain.Teams.DraftPickAsset pick ) => new()
	{
		Label = $"{pick.Season} · Round {pick.Round} · #{pick.PickNumber}"
	};
}

public sealed class TradePlayerRow
{
	public PlayerId PlayerId { get; init; }
	public string Name { get; init; } = "";
	public string Position { get; init; } = "";
	public int Overall { get; init; }
	public int AnnualSalary { get; init; }
}
