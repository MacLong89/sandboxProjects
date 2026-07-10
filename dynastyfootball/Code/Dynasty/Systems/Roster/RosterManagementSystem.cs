using Dynasty.Core.Enums;
using Dynasty.Core.Events;
using Dynasty.Core.Identifiers;
using Dynasty.Core.Interfaces;
using Dynasty.Domain.League;
using Dynasty.Domain.Players;
using Dynasty.Domain.Teams;
using Dynasty.Domain.Trades;
using Dynasty.Systems.Contracts;

namespace Dynasty.Systems.Roster;

public sealed class RosterManagementSystem : ILeagueSystem
{
	public const int AttributeTrainingCost = 100;
	public const int TraitUnlockCost = 250;

	public string SystemId => "roster_management";

	private LeagueSystemContext _context;

	public void Register( LeagueSystemContext context ) => _context = context;

	public void OnLeagueCreated( LeagueState state ) { }
	public void OnPhaseEntered( LeaguePhase phase, LeagueState state ) { }
	public void OnWeekAdvanced( LeagueState state ) { }
	public void OnSeasonEnded( LeagueState state ) { }

	public bool ReleasePlayer( LeagueState state, TeamId teamId, PlayerId playerId )
	{
		if ( !state.Teams.TryGetValue( teamId, out var team ) )
			return false;

		if ( !state.Players.TryGetValue( playerId, out var player ) )
			return false;

		if ( player.TeamId.Value != teamId.Value || !team.RosterPlayerIds.Contains( playerId ) )
			return false;

		var deadMoney = player.Contract.GuaranteedMoney / 2;
		team.Finances.DeadCap += deadMoney;
		team.RosterPlayerIds.Remove( playerId );

		player.TeamId = TeamId.Empty;
		player.Contract.YearsRemaining = 0;
		player.Contract.SignedWithTeamId = TeamId.Empty;
		player.Contract.AnnualSalary = 0;

		SalaryCapHelper.RecalculateCapSpace( state, team );

		if ( !state.FreeAgency.AvailablePlayers.Contains( playerId ) )
			state.FreeAgency.AvailablePlayers.Add( playerId );

		state.BumpRevision( "player_released" );
		return true;
	}

	public bool ExtendContract( LeagueState state, TeamId teamId, PlayerId playerId, int years, int annualSalary )
	{
		if ( years < 1 || years > 7 || annualSalary < 750_000 )
			return false;

		if ( !state.Teams.TryGetValue( teamId, out var team ) )
			return false;

		if ( !state.Players.TryGetValue( playerId, out var player ) )
			return false;

		if ( player.TeamId.Value != teamId.Value )
			return false;

		if ( !SalaryCapHelper.CanAffordAnnual( team, annualSalary ) )
			return false;

		var previousSalary = player.Contract.AnnualSalary;
		player.Contract.YearsRemaining = years;
		player.Contract.AnnualSalary = annualSalary;
		player.Contract.GuaranteedMoney = (int)(annualSalary * years * 0.4f );
		player.Contract.SignedWithTeamId = teamId;

		SalaryCapHelper.RecalculateCapSpace( state, team );
		if ( team.Finances.SalaryCapSpace < 0 )
		{
			player.Contract.AnnualSalary = previousSalary;
			SalaryCapHelper.RecalculateCapSpace( state, team );
			return false;
		}

		state.BumpRevision( "contract_extended" );
		return true;
	}

	public bool TrainAttribute( LeagueState state, TeamId teamId, PlayerId playerId, string attributeKey )
	{
		if ( !TryGetRosterPlayer( state, teamId, playerId, out var player ) )
			return false;

		if ( player.DevelopmentPoints < AttributeTrainingCost )
			return false;

		if ( !player.Ratings.Attributes.ContainsKey( attributeKey ) )
			return false;

		if ( player.Ratings.Attributes[attributeKey] >= player.Ratings.Potential )
			return false;

		player.DevelopmentPoints -= AttributeTrainingCost;
		player.Ratings.Attributes[attributeKey]++;
		RecalculateOverall( player );

		state.BumpRevision( "player_trained" );
		return true;
	}

	public bool UnlockPositiveTrait( LeagueState state, TeamId teamId, PlayerId playerId )
	{
		if ( !TryGetRosterPlayer( state, teamId, playerId, out var player ) )
			return false;

		if ( player.DevelopmentPoints < TraitUnlockCost )
			return false;

		var positives = new[] { PlayerTrait.Clutch, PlayerTrait.IronMan, PlayerTrait.Leader, PlayerTrait.TeamFriendly };
		var available = positives.Where( t => !player.Traits.Contains( t ) ).ToList();
		if ( available.Count == 0 )
			return false;

		player.DevelopmentPoints -= TraitUnlockCost;
		player.Traits.Add( _context.Random.Pick( available ) );

		state.BumpRevision( "trait_unlocked" );
		return true;
	}

	public TradeProposal BuildPlayerTradeProposal(
		LeagueState state,
		TeamId fromTeamId,
		PlayerId outgoingPlayerId,
		TeamId toTeamId,
		PlayerId incomingPlayerId )
	{
		if ( !TryGetRosterPlayer( state, fromTeamId, outgoingPlayerId, out _ ) )
			return null;

		if ( !TryGetRosterPlayer( state, toTeamId, incomingPlayerId, out _ ) )
			return null;

		if ( outgoingPlayerId.Value == incomingPlayerId.Value )
			return null;

		return new TradeProposal
		{
			InitiatingTeamId = fromTeamId,
			Participants =
			[
				new TradeParticipant
				{
					TeamId = fromTeamId,
					Sending = [new TradeAsset { Type = TradeAssetType.Player, PlayerId = outgoingPlayerId }],
					Receiving = [new TradeAsset { Type = TradeAssetType.Player, PlayerId = incomingPlayerId }]
				},
				new TradeParticipant
				{
					TeamId = toTeamId,
					Sending = [new TradeAsset { Type = TradeAssetType.Player, PlayerId = incomingPlayerId }],
					Receiving = [new TradeAsset { Type = TradeAssetType.Player, PlayerId = outgoingPlayerId }]
				}
			]
		};
	}

	static bool TryGetRosterPlayer( LeagueState state, TeamId teamId, PlayerId playerId, out PlayerState player )
	{
		player = null;
		if ( !state.Teams.TryGetValue( teamId, out var team ) )
			return false;

		if ( !state.Players.TryGetValue( playerId, out player ) )
			return false;

		if ( !team.RosterPlayerIds.Contains( playerId ) )
			return false;

		if ( player.TeamId.Value != teamId.Value )
			player.TeamId = teamId;

		return true;
	}

	static void RecalculateOverall( PlayerState player )
	{
		if ( player.Ratings.Attributes.Count == 0 )
			return;

		var avg = (int)player.Ratings.Attributes.Values.Average();
		player.Ratings.Overall = Math.Min( player.Ratings.Potential, Math.Max( player.Ratings.Overall, avg ) );
	}
}
