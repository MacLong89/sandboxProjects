using Dynasty.Core.Enums;
using Dynasty.Core.Interfaces;
using Dynasty.Domain.League;

namespace Dynasty.Systems.Fans;

public sealed class FanSystem : ILeagueSystem
{
	public string SystemId => "fans";

	private LeagueSystemContext _context;

	public void Register( LeagueSystemContext context )
	{
		_context = context;
		context.Events.Subscribe<Core.Events.GameSimulatedEvent>( OnGameSimulated );
	}

	public void OnLeagueCreated( LeagueState state ) { }
	public void OnPhaseEntered( LeaguePhase phase, LeagueState state ) { }

	public void OnWeekAdvanced( LeagueState state )
	{
		foreach ( var team in state.Teams.Values )
			UpdateFanMetrics( team );
	}

	public void OnSeasonEnded( LeagueState state ) { }

	void OnGameSimulated( Core.Events.GameSimulatedEvent e )
	{
		// Win/loss fan impact applied when service passes league
	}

	public void ApplyGameResult( LeagueState state, Core.Events.GameSimulatedEvent e )
	{
		if ( !state.Teams.TryGetValue( e.HomeTeamId, out var home ) || !state.Teams.TryGetValue( e.AwayTeamId, out var away ) )
			return;

		var homeWon = e.HomeScore > e.AwayScore;
		home.Fans.Happiness = Math.Clamp( home.Fans.Happiness + (homeWon ? 2 : -2), 0, 100 );
		away.Fans.Happiness = Math.Clamp( away.Fans.Happiness + (homeWon ? -2 : 2), 0, 100 );
	}

	void UpdateFanMetrics( Domain.Teams.TeamState team )
	{
		var stadiumLevel = team.Facilities.Levels.GetValueOrDefault( FacilityType.Stadium, 1 );
		var amenities = team.Facilities.Levels.GetValueOrDefault( FacilityType.FanAmenities, 1 );
		team.Fans.Attendance = 50_000 + stadiumLevel * 2_500 + team.Fans.Happiness * 200;
		team.Fans.Popularity = Math.Clamp( (team.Prestige.Prestige + team.Fans.Happiness + amenities * 5) / 2, 0, 100 );
	}
}
