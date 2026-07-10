using Dynasty.Core.Enums;
using Dynasty.Core.Identifiers;
using Dynasty.Domain.League;

namespace Dynasty.Core;

/// <summary>
/// Shared franchise workflow checks used by systems, view models, and time advance.
/// </summary>
public static class FranchiseWorkflowHelper
{
	public static bool IsHumanOnDraftClock( LeagueState state )
	{
		if ( state == null || state.Phase != LeaguePhase.Draft || !state.Draft.IsActive )
			return false;

		var current = state.Draft.Order.ElementAtOrDefault( state.Draft.CurrentPickIndex );
		return current != null && !current.IsComplete && GmAssignmentHelper.IsHumanTeam( state, current.TeamId );
	}
}
