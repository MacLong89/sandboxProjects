using Dynasty.Core.Enums;
using Dynasty.Core.Interfaces;
using Dynasty.Domain.League;

namespace Dynasty.Systems.Retirement;

public sealed class RetirementSystem : ILeagueSystem
{
	public string SystemId => "retirement";

	private LeagueSystemContext _context;
	private History.HistorySystem _history;

	public void Register( LeagueSystemContext context ) => _context = context;

	public void SetHistorySystem( History.HistorySystem history ) => _history = history;

	public void OnLeagueCreated( LeagueState state ) { }

	public void OnPhaseEntered( LeaguePhase phase, LeagueState state )
	{
		if ( phase == LeaguePhase.Offseason && state.OffseasonSubPhase == OffseasonSubPhase.Retirements )
			ProcessRetirements( state );
	}

	public void OnWeekAdvanced( LeagueState state ) { }
	public void OnSeasonEnded( LeagueState state ) => ProcessRetirements( state );

	void ProcessRetirements( LeagueState state )
	{
		foreach ( var player in state.Players.Values.Where( p => !p.IsRetired ).ToList() )
		{
			var retireChance = player.Identity.Age switch
			{
				>= 38 => 0.45f,
				>= 35 => 0.18f,
				>= 33 => 0.06f,
				_ => 0.01f
			};

			if ( player.Ratings.Overall < 55 ) retireChance += 0.12f;
			if ( player.Injury.Severity == InjurySeverity.SeasonEnding ) retireChance += 0.08f;

			if ( !_context.Random.Chance( retireChance ) )
				continue;

			player.IsRetired = true;
			if ( state.Teams.TryGetValue( player.TeamId, out var team ) )
				team.RosterPlayerIds.Remove( player.Id );

			_history?.RecordRetirement( state, player.Id, player.TeamId );
		}

		state.BumpRevision( "retirements" );
	}
}
