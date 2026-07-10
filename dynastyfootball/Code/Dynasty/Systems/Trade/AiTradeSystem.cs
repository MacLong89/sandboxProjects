using Dynasty.Core;
using Dynasty.Core.Enums;
using Dynasty.Core.Interfaces;
using Dynasty.Core.Identifiers;
using Dynasty.Domain.League;
using Dynasty.Domain.Players;
using Dynasty.Domain.Trades;
using Dynasty.Systems.Draft;
using Dynasty.Systems.Franchise;
using Dynasty.Systems.News;

namespace Dynasty.Systems.Trade;

/// <summary>
/// AI teams propose and execute trades — creates league activity and inbox drama for the human GM.
/// </summary>
public sealed class AiTradeSystem : ILeagueSystem
{
	public string SystemId => "ai_trade";

	private LeagueSystemContext _context;
	private TradeSystem _trade;
	private InboxSystem _inbox;
	private NewsSystem _news;

	public void Register( LeagueSystemContext context ) => _context = context;

	public void SetTradeSystem( TradeSystem trade ) => _trade = trade;

	public void SetInboxSystem( InboxSystem inbox ) => _inbox = inbox;

	public void SetNewsSystem( NewsSystem news ) => _news = news;

	public void OnLeagueCreated( LeagueState state ) { }

	public void OnPhaseEntered( LeaguePhase phase, LeagueState state )
	{
		if ( phase == LeaguePhase.RegularSeason && state.CurrentWeek is >= 8 and <= 10 )
			RunTradeActivity( state, humanOfferChance: 0.45f );
	}

	public void OnWeekAdvanced( LeagueState state )
	{
		if ( state.Phase != LeaguePhase.RegularSeason )
			return;

		var humanChance = state.CurrentWeek switch
		{
			>= 9 => 0.5f,
			>= 6 => 0.3f,
			>= 4 => 0.15f,
			_ => 0.05f
		};

		RunTradeActivity( state, humanChance );
	}

	public void OnSeasonEnded( LeagueState state ) { }

	void RunTradeActivity( LeagueState state, float humanOfferChance )
	{
		if ( _trade == null || !TradeSystem.IsTradeWindowOpen( state ) )
			return;

		var human = GmAssignmentHelper.GetHumanTeamId( state );
		var aiTeams = state.Teams.Values
			.Where( t => !GmAssignmentHelper.IsHumanTeam( state, t.Id ) )
			.OrderBy( _ => _context.Random.NextInt( 0, int.MaxValue ) )
			.Take( 4 )
			.ToList();

		var humanOffered = false;
		foreach ( var team in aiTeams )
		{
			if ( _context.Random.Chance( 0.55f ) )
				continue;

			var partner = PickTradePartner( state, team.Id, human );
			if ( partner.IsEmpty )
				continue;

			var proposal = BuildProposal( state, team.Id, partner );
			if ( proposal == null )
				continue;

			var targetHuman = !human.IsEmpty && partner.Value == human.Value;

			if ( targetHuman && !humanOffered && _context.Random.Chance( humanOfferChance ) )
			{
				QueueHumanOffer( state, team.Id, partner, proposal );
				humanOffered = true;
				continue;
			}

			if ( _trade.TryExecute( state, proposal, maxFairnessDelta: 6f ) )
				PublishAiTradeNews( state, team.Id, partner );
		}
	}

	TeamId PickTradePartner( LeagueState state, TeamId from, TeamId human )
	{
		var candidates = state.Teams.Values
			.Where( t => t.Id.Value != from.Value )
			.ToList();

		if ( candidates.Count == 0 )
			return TeamId.Empty;

		if ( !human.IsEmpty && _context.Random.Chance( 0.35f ) )
			return human;

		return _context.Random.Pick( candidates ).Id;
	}

	TradeProposal BuildProposal( LeagueState state, TeamId from, TeamId to )
	{
		if ( !state.Teams.TryGetValue( from, out var fromTeam ) || !state.Teams.TryGetValue( to, out var toTeam ) )
			return null;

		var sendPlayer = PickTradePlayer( fromTeam, state, outgoing: true );
		if ( sendPlayer == null )
			return null;

		TradeAsset receiveAsset;
		if ( _context.Random.Chance( 0.3f ) )
		{
			var pick = toTeam.DraftPicks
				.Where( p => p.Season >= state.CurrentSeason + 1 && p.Round >= 3 )
				.OrderByDescending( p => p.Round )
				.FirstOrDefault();

			if ( pick == null )
				return null;

			receiveAsset = new TradeAsset { Type = TradeAssetType.DraftPick, PickId = pick.Id };
		}
		else
		{
			var recvPlayer = PickTradePlayer( toTeam, state, outgoing: false, minOvr: sendPlayer.Ratings.Overall - 8 );
			if ( recvPlayer == null )
				return null;

			receiveAsset = new TradeAsset { Type = TradeAssetType.Player, PlayerId = recvPlayer.Id };
		}

		return new TradeProposal
		{
			InitiatingTeamId = from,
			Participants =
			[
				new TradeParticipant
				{
					TeamId = from,
					Sending = [new TradeAsset { Type = TradeAssetType.Player, PlayerId = sendPlayer.Id }],
					Receiving = [receiveAsset]
				},
				new TradeParticipant
				{
					TeamId = to,
					Sending = [receiveAsset],
					Receiving = [new TradeAsset { Type = TradeAssetType.Player, PlayerId = sendPlayer.Id }]
				}
			]
		};
	}

	static PlayerState PickTradePlayer( Domain.Teams.TeamState team, LeagueState state, bool outgoing, int minOvr = 0 )
	{
		var pool = team.RosterPlayerIds
			.Select( id => state.Players.GetValueOrDefault( id ) )
			.Where( p => p != null && !p.IsRetired && p.Ratings.Overall >= minOvr )
			.OrderByDescending( p => p.Ratings.Overall )
			.ToList();

		if ( pool.Count == 0 )
			return null;

		if ( outgoing )
			return pool.Skip( Math.Min( 2, pool.Count - 1 ) ).FirstOrDefault() ?? pool.Last();

		return pool.FirstOrDefault( p => p.Ratings.Overall <= pool[0].Ratings.Overall + 5 );
	}

	void QueueHumanOffer( LeagueState state, TeamId from, TeamId to, TradeProposal proposal )
	{
		if ( !state.Teams.TryGetValue( from, out var fromTeam ) )
			return;

		var eval = _trade.Evaluate( state, proposal );
		var summary = BuildOfferSummary( state, proposal );

		var offer = new PendingTradeOffer
		{
			Proposal = proposal,
			FromTeamId = from,
			ToTeamId = to,
			Season = state.CurrentSeason,
			Week = state.CurrentWeek,
			Summary = summary
		};

		_trade.QueuePendingOffer( state, offer );

		_inbox?.Add( state, InboxCategory.Trade, InboxPriority.High,
			$"Trade offer: {fromTeam.Identity.Abbreviation}",
			$"{summary} (Fairness: {eval.FairnessScore:+0.0;-0.0}). Review in Trade Center or respond before week ends.",
			true, to, navigateTab: "trades" );

		_news?.Publish( state, NewsCategory.Trade,
			$"{fromTeam.Identity.Abbreviation} seeks trade with you",
			summary );
	}

	static string BuildOfferSummary( LeagueState state, TradeProposal proposal )
	{
		var from = proposal.Participants.FirstOrDefault( p => p.TeamId.Value == proposal.InitiatingTeamId.Value );
		var to = proposal.Participants.FirstOrDefault( p => p.TeamId.Value != proposal.InitiatingTeamId.Value );
		if ( from == null || to == null )
			return "Trade proposal";

		var sendName = DescribeAsset( state, from.Sending.FirstOrDefault() );
		var recvName = DescribeAsset( state, to.Sending.FirstOrDefault() );
		return $"They offer {sendName} for your {recvName}";
	}

	static string DescribeAsset( LeagueState state, TradeAsset asset )
	{
		if ( asset == null )
			return "?";

		return asset.Type switch
		{
			TradeAssetType.Player when state.Players.TryGetValue( asset.PlayerId, out var p )
				=> $"{p.Identity.FullName} ({p.Ratings.Overall} OVR)",
			TradeAssetType.DraftPick when DraftPickRegistry.FindPick( state, asset.PickId ) is { } pick
				=> $"{pick.Season} R{pick.Round} pick",
			_ => "asset"
		};
	}

	void PublishAiTradeNews( LeagueState state, TeamId from, TeamId to )
	{
		if ( !state.Teams.TryGetValue( from, out var fromTeam ) || !state.Teams.TryGetValue( to, out var toTeam ) )
			return;

		_news?.Publish( state, NewsCategory.Trade,
			$"Trade: {fromTeam.Identity.Abbreviation} ↔ {toTeam.Identity.Abbreviation}",
			"League front offices stay active ahead of the deadline." );
	}
}
