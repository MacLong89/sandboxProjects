using Dynasty.Core.Enums;
using Dynasty.Core.Interfaces;
using Dynasty.Domain.League;

namespace Dynasty.Systems.Chemistry;

public sealed class ChemistrySystem : ILeagueSystem
{
	public string SystemId => "chemistry";

	private LeagueSystemContext _context;

	public void Register( LeagueSystemContext context ) => _context = context;

	public void OnLeagueCreated( LeagueState state ) { }
	public void OnPhaseEntered( LeaguePhase phase, LeagueState state ) { }

	public void OnWeekAdvanced( LeagueState state )
	{
		foreach ( var team in state.Teams.Values )
			Recalculate( state, team );
	}

	public void OnSeasonEnded( LeagueState state ) { }

	void Recalculate( LeagueState state, Domain.Teams.TeamState team )
	{
		var players = team.RosterPlayerIds
			.Select( id => state.Players.GetValueOrDefault( id ) )
			.Where( p => p != null && !p.IsRetired )
			.ToList();

		var leaders = players.Count( p => p.Traits.Contains( PlayerTrait.Leader ) );
		var divas = players.Count( p => p.Traits.Contains( PlayerTrait.Diva ) );
		var winPct = team.Record.Wins + team.Record.Losses > 0
			? team.Record.Wins / (float)(team.Record.Wins + team.Record.Losses )
			: 0.5f;

		team.Chemistry.Leadership = Math.Clamp( 40 + leaders * 8, 0, 100 );
		team.Chemistry.LockerRoomHealth = Math.Clamp( 70 - divas * 6 + (int)(winPct * 20), 0, 100 );
		team.Chemistry.Morale = Math.Clamp( (team.Chemistry.LockerRoomHealth + team.Chemistry.Leadership + (int)(winPct * 100)) / 3, 0, 100 );
	}
}
