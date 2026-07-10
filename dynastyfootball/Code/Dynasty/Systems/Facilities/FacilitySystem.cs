using Dynasty.Core;
using Dynasty.Core.Enums;
using Dynasty.Core.Interfaces;
using Dynasty.Core.Identifiers;
using Dynasty.Domain.League;
using Dynasty.Systems.Franchise;

namespace Dynasty.Systems.Facilities;

public sealed class FacilitySystem : ILeagueSystem
{
	public string SystemId => "facilities";

	private LeagueSystemContext _context;
	private SeasonObjectiveSystem _seasonObjective;

	public void Register( LeagueSystemContext context ) => _context = context;

	public void SetSeasonObjectiveSystem( SeasonObjectiveSystem seasonObjective ) => _seasonObjective = seasonObjective;

	public void OnLeagueCreated( LeagueState state ) { }
	public void OnPhaseEntered( LeaguePhase phase, LeagueState state ) { }
	public void OnWeekAdvanced( LeagueState state ) { }
	public void OnSeasonEnded( LeagueState state ) { }

	public bool TryUpgrade( LeagueState state, TeamId teamId, FacilityType type )
	{
		if ( !state.Teams.TryGetValue( teamId, out var team ) )
			return false;

		var current = team.Facilities.Levels.GetValueOrDefault( type, 1 );
		if ( current >= 10 )
			return false;

		var cost = (current + 1) * 5_000_000L;
		if ( team.Finances.Budget < cost )
			return false;

		team.Finances.Budget -= cost;
		team.Facilities.Levels[type] = current + 1;
		_seasonObjective?.OnFacilityUpgraded( state, teamId, current + 1 );
		state.BumpRevision( "facility_upgrade" );
		return true;
	}
}
