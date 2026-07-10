using Dynasty.Core;
using Dynasty.Core.Enums;
using Dynasty.Core.Interfaces;
using Dynasty.Core.Identifiers;
using Dynasty.Domain.League;
using Dynasty.Systems.Franchise;

namespace Dynasty.Systems.Contracts;

/// <summary>
/// Notifies the human GM when AI teams sign free agents they were pursuing.
/// </summary>
public sealed class FreeAgencyAlertSystem : ILeagueSystem
{
	public string SystemId => "free_agency_alert";

	private InboxSystem _inbox;

	public void Register( LeagueSystemContext context ) { }

	public void SetInboxSystem( InboxSystem inbox ) => _inbox = inbox;

	public void OnLeagueCreated( LeagueState state ) { }
	public void OnPhaseEntered( LeaguePhase phase, LeagueState state ) { }
	public void OnWeekAdvanced( LeagueState state ) { }
	public void OnSeasonEnded( LeagueState state ) { }

	public void OnPlayerSigned( LeagueState state, PlayerId playerId, TeamId signingTeamId )
	{
		if ( GmAssignmentHelper.IsHumanTeam( state, signingTeamId ) )
			return;

		var human = GmAssignmentHelper.GetHumanTeamId( state );
		if ( human.IsEmpty )
			return;

		var hadHumanOffer = state.FreeAgency.PendingOffers.Any( o =>
			o.PlayerId.Value == playerId.Value && o.TeamId.Value == human.Value );

		if ( !hadHumanOffer )
			return;

		if ( !state.Players.TryGetValue( playerId, out var player ) )
			return;

		if ( !state.Teams.TryGetValue( signingTeamId, out var signingTeam ) )
			return;

		_inbox?.Add( state, InboxCategory.Contract, InboxPriority.High,
			$"FA target signed: {player.Identity.LastName}",
			$"{signingTeam.Identity.Abbreviation} signed {player.Identity.FullName} ({player.Ratings.Overall} OVR) — you had a pending offer.",
			false, human, navigateTab: "freeagency" );

		state.BumpRevision( "fa_target_lost" );
	}
}
