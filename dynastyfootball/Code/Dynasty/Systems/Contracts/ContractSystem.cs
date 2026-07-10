using Dynasty.Core.Enums;
using Dynasty.Core.Events;
using Dynasty.Core.Interfaces;
using Dynasty.Core.Identifiers;
using Dynasty.Domain.Contracts;
using Dynasty.Domain.League;

using Dynasty.Systems.Roster;

namespace Dynasty.Systems.Contracts;

public sealed class ContractSystem : ILeagueSystem
{
	public string SystemId => "contract";

	private LeagueSystemContext _context;
	private FreeAgencyAlertSystem _faAlerts;

	public void Register( LeagueSystemContext context ) => _context = context;

	public void SetFreeAgencyAlertSystem( FreeAgencyAlertSystem faAlerts ) => _faAlerts = faAlerts;

	public void OnLeagueCreated( LeagueState state ) => SalaryCapHelper.RecalculateAllTeams( state );

	public void OnPhaseEntered( LeaguePhase phase, LeagueState state )
	{
		if ( phase == LeaguePhase.FreeAgency || phase == LeaguePhase.Offseason )
			state.FreeAgency.IsOpen = true;
	}

	public void OnWeekAdvanced( LeagueState state )
	{
		ProcessExpiringContracts( state );
		ResolveOffers( state );
	}

	public void OnSeasonEnded( LeagueState state ) => ReleaseFreeAgents( state );

	public bool SubmitOffer( LeagueState state, ContractOffer offer )
	{
		if ( !state.FreeAgency.IsOpen )
			return false;

		if ( !state.FreeAgency.AvailablePlayers.Contains( offer.PlayerId ) )
			return false;

		if ( !state.Teams.TryGetValue( offer.TeamId, out var team ) )
			return false;

		if ( !SalaryCapHelper.CanAffordAnnual( team, offer.AnnualSalary ) )
			return false;

		offer.ExpiresUtc = _context.Clock.UtcNow.AddDays( 3 );
		state.FreeAgency.PendingOffers.Add( offer );
		TryResolveSingleOffer( state, offer );
		state.BumpRevision( "contract_offer" );
		return true;
	}

	public bool TryAiSign( LeagueState state, ContractOffer offer )
	{
		if ( !state.FreeAgency.IsOpen )
			return false;

		if ( !state.FreeAgency.AvailablePlayers.Contains( offer.PlayerId ) )
			return false;

		if ( !state.Teams.TryGetValue( offer.TeamId, out var team ) )
			return false;

		if ( !SalaryCapHelper.CanAffordAnnual( team, offer.AnnualSalary ) )
			return false;

		return SignContract( state, state.Players[offer.PlayerId], team, offer );
	}

	public bool TryResolveSingleOffer( LeagueState state, ContractOffer offer )
	{
		if ( offer == null || offer.Accepted )
			return false;

		if ( !state.Players.TryGetValue( offer.PlayerId, out var player ) )
			return false;

		if ( !state.Teams.TryGetValue( offer.TeamId, out var team ) )
			return false;

		if ( !SalaryCapHelper.CanAffordAnnual( team, offer.AnnualSalary ) )
			return false;

		var acceptance = ComputeAcceptance( state, player, team, offer );
		if ( !_context.Random.Chance( acceptance ) )
			return false;

		return SignContract( state, player, team, offer );
	}

	void ResolveOffers( LeagueState state )
	{
		foreach ( var offer in state.FreeAgency.PendingOffers.Where( o => !o.Accepted ).ToList() )
		{
			if ( !state.Players.TryGetValue( offer.PlayerId, out var player ) )
				continue;

			if ( !state.Teams.TryGetValue( offer.TeamId, out var team ) )
				continue;

			if ( !SalaryCapHelper.CanAffordAnnual( team, offer.AnnualSalary ) )
				continue;

			var acceptance = ComputeAcceptance( state, player, team, offer );
			if ( !_context.Random.Chance( acceptance ) )
				continue;

			SignContract( state, player, team, offer );
		}
	}

	float ComputeAcceptance( LeagueState state, Domain.Players.PlayerState player, Domain.Teams.TeamState team, ContractOffer offer )
	{
		var moneyScore = Math.Clamp( offer.AnnualSalary / 15_000_000f, 0.2f, 1f );
		var prestigeScore = team.Prestige.Prestige / 100f;
		var successScore = team.Prestige.RecentSuccessScore / 100f;
		var loyalty = player.Traits.Contains( PlayerTrait.TeamFriendly ) ? 0.15f : 0f;
		var divaPenalty = player.Traits.Contains( PlayerTrait.Diva ) ? -0.1f : 0f;
		var workEthicBonus = player.Personality.WorkEthic >= 75 ? 0.05f : 0f;
		var ambitionPenalty = player.Personality.Ambition >= 80 && prestigeScore < 0.5f ? -0.08f : 0f;

		return Math.Clamp(
			moneyScore * 0.45f + prestigeScore * 0.25f + successScore * 0.15f + loyalty + divaPenalty + workEthicBonus + ambitionPenalty,
			0.05f, 0.95f );
	}

	bool SignContract( LeagueState state, Domain.Players.PlayerState player, Domain.Teams.TeamState team, ContractOffer offer )
	{
		if ( !RosterLimits.CanAddPlayer( state, team.Id ) )
			return false;

		player.TeamId = team.Id;
		player.Contract = new ContractState
		{
			YearsRemaining = offer.Years,
			AnnualSalary = offer.AnnualSalary,
			GuaranteedMoney = offer.GuaranteedMoney,
			SignedWithTeamId = team.Id
		};

		if ( !team.RosterPlayerIds.Contains( player.Id ) )
			team.RosterPlayerIds.Add( player.Id );

		player.Morale.Morale = Math.Clamp( player.Morale.Morale + 8, 0, 99 );
		player.Morale.TradeRequested = false;

		state.FreeAgency.AvailablePlayers.Remove( player.Id );
		offer.Accepted = true;
		state.FreeAgency.CompletedOfferIds.Add( offer.OfferId );
		state.FreeAgency.PendingOffers.Remove( offer );

		SalaryCapHelper.RecalculateCapSpace( state, team );

		_context.Events.Publish( new ContractSignedEvent(
			_context.Events.NextSequence(),
			_context.Clock.UtcNow,
			player.Id,
			team.Id,
			offer.Years,
			offer.AnnualSalary ) );

		_faAlerts?.OnPlayerSigned( state, player.Id, team.Id );

		state.BumpRevision( "contract_signed" );
		return true;
	}

	void ProcessExpiringContracts( LeagueState state )
	{
		foreach ( var player in state.Players.Values.Where( p => !p.IsRetired && p.Contract.YearsRemaining > 0 ) )
			player.Contract.YearsRemaining = Math.Max( 0, player.Contract.YearsRemaining - (state.Phase == LeaguePhase.Offseason ? 1 : 0) );
	}

	void ReleaseFreeAgents( LeagueState state )
	{
		foreach ( var player in state.Players.Values.Where( p => p.Contract.YearsRemaining <= 0 && !p.IsRetired ) )
		{
			if ( state.Teams.TryGetValue( player.TeamId, out var team ) )
			{
				team.RosterPlayerIds.Remove( player.Id );
				SalaryCapHelper.RecalculateCapSpace( state, team );
			}

			player.TeamId = TeamId.New();
			if ( !state.FreeAgency.AvailablePlayers.Contains( player.Id ) )
				state.FreeAgency.AvailablePlayers.Add( player.Id );
		}
	}
}
