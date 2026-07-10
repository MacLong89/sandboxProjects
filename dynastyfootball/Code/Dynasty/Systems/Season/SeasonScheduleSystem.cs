using Dynasty.Core.Enums;
using Dynasty.Core.Interfaces;
using Dynasty.Domain.Factories;
using Dynasty.Domain.League;

namespace Dynasty.Systems.Season;

/// <summary>
/// Generates a fresh regular-season schedule each year when training camp begins.
/// </summary>
public sealed class SeasonScheduleSystem : ILeagueSystem
{
	public string SystemId => "season_schedule";

	private LeagueSystemContext _context;

	public void Register( LeagueSystemContext context ) => _context = context;

	public void OnLeagueCreated( LeagueState state ) { }

	public void OnPhaseEntered( LeaguePhase phase, LeagueState state )
	{
		if ( phase != LeaguePhase.Preseason )
			return;

		EnsureSeasonSchedule( state );
	}

	public void OnWeekAdvanced( LeagueState state ) { }
	public void OnSeasonEnded( LeagueState state ) { }

	void EnsureSeasonSchedule( LeagueState state )
	{
		if ( state.Schedule.Games.Any( g => g.Season == state.CurrentSeason && !g.IsPlayoffGame ) )
			return;

		var generated = ScheduleGenerator.GenerateRegularSeason(
			state,
			state.Settings.RegularSeasonWeeks,
			_context.Random );

		state.Schedule.Games.AddRange( generated.Games );
		state.Schedule.PlayoffBracket.Clear();
		state.BumpRevision( "season_schedule" );
	}
}
