using Dynasty.Core.Identifiers;
using Dynasty.Core.Interfaces;
using Dynasty.Domain.League;
using Dynasty.Systems.Season;

namespace Dynasty.Systems.Draft;

/// <summary>
/// Single source of truth for projected draft pick order.
/// </summary>
public static class DraftOrderHelper
{
	public static List<TeamId> GetPickOrder( LeagueState state, ILeagueRandom random = null )
	{
		var hasRecords = state.Teams.Values.Any( t =>
		{
			var stats = TeamRecordArchive.GetStandingsStats( state, t );
			return stats.Wins + stats.Losses + stats.Ties > 0;
		} );

		if ( hasRecords )
		{
			return state.Teams.Values
				.OrderBy( t => TeamRecordArchive.GetStandingsStats( state, t ).Wins )
				.ThenBy( t =>
				{
					var stats = TeamRecordArchive.GetStandingsStats( state, t );
					return stats.PointsFor - stats.PointsAgainst;
				} )
				.Select( t => t.Id )
				.ToList();
		}

		return state.Teams.Values
			.OrderBy( t => t.Prestige.Prestige )
			.ThenBy( t => random != null ? random.NextInt( 0, int.MaxValue ) : t.Id.Value.GetHashCode() )
			.Select( t => t.Id )
			.ToList();
	}
}
