using Dynasty.Core;
using Dynasty.Core.Enums;
using Dynasty.Core.Interfaces;
using Dynasty.Data;
using Dynasty.Domain.League;
using Dynasty.Domain.Players;
using Dynasty.Systems.Franchise;

namespace Dynasty.Systems.Franchise;

/// <summary>
/// Updates individual player morale from results, playing time, and personality.
/// </summary>
public sealed class PlayerMoraleSystem : ILeagueSystem
{
	public string SystemId => "player_morale";

	private InboxSystem _inbox;

	public void Register( LeagueSystemContext context ) { }

	public void SetInboxSystem( InboxSystem inbox ) => _inbox = inbox;

	public void OnLeagueCreated( LeagueState state ) { }
	public void OnPhaseEntered( LeaguePhase phase, LeagueState state ) { }

	public void OnWeekAdvanced( LeagueState state )
	{
		if ( state.Phase is not (LeaguePhase.RegularSeason or LeaguePhase.Playoffs or LeaguePhase.Preseason) )
			return;

		foreach ( var team in state.Teams.Values )
			EvaluatePlayingTime( state, team );
	}

	public void OnSeasonEnded( LeagueState state ) { }

	public void ApplyGameResultMorale( LeagueState state, TeamId homeId, TeamId awayId, int homeScore, int awayScore )
	{
		if ( homeScore == awayScore )
			return;

		var homeWon = homeScore > awayScore;
		ApplyTeamGameMorale( state, homeId, homeWon );
		ApplyTeamGameMorale( state, awayId, !homeWon );
	}

	void ApplyTeamGameMorale( LeagueState state, TeamId teamId, bool won )
	{
		if ( !state.Teams.TryGetValue( teamId, out var team ) )
			return;

		foreach ( var playerId in team.RosterPlayerIds )
		{
			if ( !state.Players.TryGetValue( playerId, out var player ) || player.IsRetired )
				continue;

			var delta = won ? 3 : -5;
			if ( player.Traits.Contains( PlayerTrait.Diva ) )
				delta = won ? 2 : -8;
			if ( player.Traits.Contains( PlayerTrait.TeamFriendly ) )
				delta = won ? 4 : -3;

			AdjustMorale( player, delta );

			if ( player.Morale.Morale <= 30 && player.Personality.Ego >= 65 && !player.Morale.TradeRequested )
			{
				player.Morale.TradeRequested = true;
				player.Morale.LastConcern = "Wants a bigger role or a fresh start.";

				if ( GmAssignmentHelper.IsHumanTeam( state, teamId ) )
				{
					_inbox?.Add( state, InboxCategory.Roster, InboxPriority.High,
						$"{player.Identity.FullName} requests a trade",
						player.Morale.LastConcern,
						false, teamId, navigateTab: "team" );
				}
			}
		}

		state.BumpRevision( "player_morale_game" );
	}

	void EvaluatePlayingTime( LeagueState state, Domain.Teams.TeamState team )
	{
		var roster = team.RosterPlayerIds
			.Select( id => state.Players.GetValueOrDefault( id ) )
			.Where( p => p != null && !p.IsRetired )
			.OrderByDescending( p => p.Ratings.Overall )
			.ToList();

		if ( roster.Count == 0 )
			return;

		var starterIds = new HashSet<Guid>();
		if ( team.DepthChart != null )
		{
			foreach ( var list in team.DepthChart.Values )
			{
				if ( list == null || list.Count == 0 )
					continue;

				starterIds.Add( list[0].Value );
			}
		}

		var topPlayers = roster.Take( 6 ).ToList();
		foreach ( var player in topPlayers )
		{
			if ( starterIds.Contains( player.Id.Value ) )
				continue;

			if ( player.Identity.Position is Position.K or Position.P or Position.LS )
				continue;

			AdjustMorale( player, -4 );
			player.Morale.LastConcern = "Frustrated with limited playing time.";

			if ( player.Morale.Morale <= 35 && player.Personality.Ambition >= 70 && GmAssignmentHelper.IsHumanTeam( state, team.Id ) )
			{
				_inbox?.Add( state, InboxCategory.Roster, InboxPriority.Normal,
					$"{player.Identity.LastName} wants more snaps",
					$"{player.Identity.FullName} ({player.Ratings.Overall} OVR) is not in your starting lineup.",
					false, team.Id, navigateTab: "team" );
			}
		}
	}

	static void AdjustMorale( PlayerState player, int delta )
	{
		player.Morale.Morale = Math.Clamp( player.Morale.Morale + delta, 5, 99 );

		if ( player.Morale.Morale >= 75 && player.Morale.TradeRequested && player.Personality.Loyalty >= 50 )
			player.Morale.TradeRequested = false;
	}
}
