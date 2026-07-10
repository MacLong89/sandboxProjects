using Dynasty.Core.Enums;
using Dynasty.Core.Identifiers;
using Dynasty.Domain.League;
using Dynasty.Domain.Trades;
using Dynasty.Systems.Contracts;
using Dynasty.Systems.DepthChart;
using Dynasty.Systems.Draft;

namespace Dynasty.Systems.Trade;

/// <summary>
/// Executes movement of trade assets. Used exclusively by <see cref="TradeSystem"/>.
/// </summary>
public static class TradeAssetTransfer
{
	public static bool Transfer( LeagueState state, TradeAsset asset, TeamId from, TeamId to, DepthChartSystem depthChart )
	{
		return asset.Type switch
		{
			TradeAssetType.Player => TransferPlayer( state, asset.PlayerId, from, to, depthChart ),
			TradeAssetType.DraftPick => DraftPickRegistry.TryTransferPick( state, asset.PickId, from, to ),
			TradeAssetType.Cash => TransferCash( state, asset.CashAmount, from, to ),
			_ => false
		};
	}

	static bool TransferPlayer( LeagueState state, PlayerId playerId, TeamId from, TeamId to, DepthChartSystem depthChart )
	{
		if ( !state.Teams.TryGetValue( from, out var fromTeam ) || !state.Teams.TryGetValue( to, out var toTeam ) )
			return false;

		if ( !fromTeam.RosterPlayerIds.Contains( playerId ) )
			return false;

		fromTeam.RosterPlayerIds.Remove( playerId );
		toTeam.RosterPlayerIds.Add( playerId );

		if ( state.Players.TryGetValue( playerId, out var player ) )
		{
			player.TeamId = to;
			player.Contract.SignedWithTeamId = to;
		}

		depthChart?.OnPlayerReleased( state, from, playerId );
		depthChart?.EnsureTeamDepthChart( state, to );
		return true;
	}

	static bool TransferCash( LeagueState state, int amount, TeamId from, TeamId to )
	{
		if ( amount <= 0 )
			return true;

		if ( !state.Teams.TryGetValue( from, out var fromTeam ) || !state.Teams.TryGetValue( to, out var toTeam ) )
			return false;

		if ( fromTeam.Finances.Budget < amount )
			return false;

		fromTeam.Finances.Budget -= amount;
		toTeam.Finances.Budget += amount;
		return true;
	}

	public static void RecalculateCapForTeams( LeagueState state, IEnumerable<TeamId> teamIds )
	{
		foreach ( var teamId in teamIds.Distinct() )
		{
			if ( state.Teams.TryGetValue( teamId, out var team ) )
				SalaryCapHelper.RecalculateCapSpace( state, team );
		}
	}
}
