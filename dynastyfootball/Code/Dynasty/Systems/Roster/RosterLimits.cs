using Dynasty.Core.Identifiers;
using Dynasty.Domain.League;

namespace Dynasty.Systems.Roster;

public static class RosterLimits
{
	public static bool CanAddPlayer( LeagueState state, TeamId teamId )
	{
		if ( state == null || !state.Teams.TryGetValue( teamId, out var team ) )
			return false;

		return team.RosterPlayerIds.Count < state.Settings.RosterSize;
	}

	public static bool IsOverLimit( LeagueState state, TeamId teamId )
	{
		if ( state == null || !state.Teams.TryGetValue( teamId, out var team ) )
			return false;

		return team.RosterPlayerIds.Count > state.Settings.RosterSize;
	}
}
