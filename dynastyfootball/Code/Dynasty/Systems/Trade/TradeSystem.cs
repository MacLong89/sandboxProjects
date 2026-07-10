using Dynasty.Core.Enums;
using Dynasty.Core.Events;
using Dynasty.Core.Interfaces;
using Dynasty.Core.Identifiers;
using Dynasty.Domain.League;
using Dynasty.Domain.Trades;
using Dynasty.Systems.DepthChart;
using Dynasty.Systems.Draft;

namespace Dynasty.Systems.Trade;

public sealed class TradeSystem : ILeagueSystem
{
	public string SystemId => "trade";

	private LeagueSystemContext _context;
	private DepthChartSystem _depthChart;

	public void Register( LeagueSystemContext context ) => _context = context;

	public void SetDepthChartSystem( DepthChartSystem depthChart ) => _depthChart = depthChart;

	public void OnLeagueCreated( LeagueState state ) => DraftPickRegistry.EnsureFuturePickInventory( state );

	public void OnPhaseEntered( LeaguePhase phase, LeagueState state ) { }
	public void OnWeekAdvanced( LeagueState state ) => ExpireStaleOffers( state );

	public void OnSeasonEnded( LeagueState state )
	{
		ExpireStaleOffers( state );
		DraftPickRegistry.EnsureFuturePickInventory( state );
	}

	public TradeEvaluation Evaluate( LeagueState state, TradeProposal proposal )
		=> EvaluateProposal( state, proposal );

	public static TradeEvaluation EvaluateProposal( LeagueState state, TradeProposal proposal )
	{
		var fairness = 0f;
		foreach ( var participant in proposal.Participants )
		{
			var sendValue = SumAssetValues( state, participant.Sending );
			var recvValue = SumAssetValues( state, participant.Receiving );
			fairness += recvValue - sendValue;
		}

		return new TradeEvaluation
		{
			FairnessScore = fairness,
			AcceptRecommendation = Math.Abs( fairness ) < 8f
		};
	}

	public bool TryExecute( LeagueState state, TradeProposal proposal, float maxFairnessDelta = 8f )
	{
		if ( !IsTradeWindowOpen( state ) )
			return false;

		if ( !ValidateAssets( state, proposal ) )
			return false;

		var evaluation = Evaluate( state, proposal );
		if ( Math.Abs( evaluation.FairnessScore ) > maxFairnessDelta )
			return false;

		var completed = new List<(TradeAsset Asset, TeamId From, TeamId To)>();

		foreach ( var participant in proposal.Participants )
		{
			var partnerTeamId = GetTradePartner( proposal, participant.TeamId );
			foreach ( var asset in participant.Sending )
			{
				if ( asset.Type == TradeAssetType.Player && !Roster.RosterLimits.CanAddPlayer( state, partnerTeamId ) )
				{
					RollbackTransfers( state, completed );
					return false;
				}

				if ( !TradeAssetTransfer.Transfer( state, asset, participant.TeamId, partnerTeamId, _depthChart ) )
				{
					RollbackTransfers( state, completed );
					return false;
				}

				completed.Add( ( asset, participant.TeamId, partnerTeamId ) );
			}
		}

		var involvedTeams = proposal.Participants.Select( p => p.TeamId ).Distinct();
		TradeAssetTransfer.RecalculateCapForTeams( state, involvedTeams );

		proposal.Status = TradeStatus.Accepted;
		state.PendingTradeOffers.RemoveAll( o => o.Proposal.TradeId == proposal.TradeId );

		_context.Events.Publish( new TradeCompletedEvent(
			_context.Events.NextSequence(),
			_context.Clock.UtcNow,
			proposal.TradeId,
			proposal.Participants.Select( p => p.TeamId ).ToList() ) );

		state.BumpRevision( "trade_executed" );
		return true;
	}

	public bool TryAcceptPendingOffer( LeagueState state, Guid offerId )
	{
		var offer = state.PendingTradeOffers.FirstOrDefault( o => o.OfferId == offerId );
		if ( offer == null )
			return false;

		return TryExecute( state, offer.Proposal, maxFairnessDelta: 12f );
	}

	public void RejectPendingOffer( LeagueState state, Guid offerId )
	{
		state.PendingTradeOffers.RemoveAll( o => o.OfferId == offerId );
		state.BumpRevision( "trade_rejected" );
	}

	public void QueuePendingOffer( LeagueState state, PendingTradeOffer offer )
	{
		state.PendingTradeOffers.RemoveAll( o =>
			o.FromTeamId.Value == offer.FromTeamId.Value
			&& o.ToTeamId.Value == offer.ToTeamId.Value );

		state.PendingTradeOffers.Add( offer );
		state.BumpRevision( "trade_offer_queued" );
	}

	static bool ValidateAssets( LeagueState state, TradeProposal proposal )
	{
		foreach ( var participant in proposal.Participants )
		{
			foreach ( var asset in participant.Sending )
			{
				switch ( asset.Type )
				{
					case TradeAssetType.Player:
						if ( !state.Players.ContainsKey( asset.PlayerId ) )
							return false;
						if ( !state.Teams.TryGetValue( participant.TeamId, out var team ) )
							return false;
						if ( !team.RosterPlayerIds.Contains( asset.PlayerId ) )
							return false;
						break;

					case TradeAssetType.DraftPick:
						if ( DraftPickRegistry.GetPickOwner( state, asset.PickId ).Value != participant.TeamId.Value )
							return false;
						break;

					case TradeAssetType.Cash:
						if ( !state.Teams.TryGetValue( participant.TeamId, out var cashTeam ) )
							return false;
						if ( cashTeam.Finances.Budget < asset.CashAmount )
							return false;
						break;
				}
			}
		}

		return true;
	}

	static void ExpireStaleOffers( LeagueState state )
	{
		if ( state.PendingTradeOffers.Count == 0 )
			return;

		var removed = state.PendingTradeOffers.RemoveAll( o =>
			o.Season < state.CurrentSeason
			|| ( o.Season == state.CurrentSeason && o.Week < state.CurrentWeek ) );

		if ( removed > 0 )
			state.BumpRevision( "trade_offers_expired" );
	}

	public static bool IsTradeWindowOpen( LeagueState state )
	{
		return state.Phase switch
		{
			LeaguePhase.Offseason or LeaguePhase.FreeAgency or LeaguePhase.Draft or LeaguePhase.Preseason => true,
			LeaguePhase.RegularSeason => state.CurrentWeek <= 10,
			_ => false
		};
	}

	public static string GetTradeWindowMessage( LeagueState state )
		=> IsTradeWindowOpen( state )
			? ""
			: "Trades are closed outside the regular season (through Week 10) and offseason.";

	float SumAssetValue( LeagueState state, List<TradeAsset> assets )
		=> SumAssetValues( state, assets );

	static float SumAssetValues( LeagueState state, List<TradeAsset> assets )
	{
		var total = 0f;
		foreach ( var asset in assets )
		{
			total += asset.Type switch
			{
				TradeAssetType.Player when state.Players.TryGetValue( asset.PlayerId, out var p ) => p.Ratings.Overall,
				TradeAssetType.DraftPick => DraftPickRegistry.GetAssetValue( state, asset.PickId ),
				TradeAssetType.Cash => asset.CashAmount / 1_000_000f,
				_ => 0f
			};
		}

		return total;
	}

	static TeamId GetTradePartner( TradeProposal proposal, TeamId fromTeam )
	{
		foreach ( var participant in proposal.Participants )
		{
			if ( participant.TeamId.Value != fromTeam.Value )
				return participant.TeamId;
		}

		return fromTeam;
	}

	static void RollbackTransfers( LeagueState state, List<(TradeAsset Asset, TeamId From, TeamId To)> completed )
	{
		for ( var i = completed.Count - 1; i >= 0; i-- )
		{
			var (asset, from, to) = completed[i];
			TradeAssetTransfer.Transfer( state, asset, to, from, depthChart: null );
		}
	}
}

public sealed class TradeEvaluation
{
	public float FairnessScore { get; set; }
	public bool AcceptRecommendation { get; set; }
}
