using Dynasty.Core.Identifiers;
using Dynasty.Core.Interfaces;
using Dynasty.Domain.League;
using Dynasty.Domain.Schedule;

namespace Dynasty.Domain.Factories;

public static class ScheduleGenerator
{
	public static ScheduleState GenerateRegularSeason( LeagueState league, int weeks, ILeagueRandom random )
	{
		var schedule = new ScheduleState();
		var teamIds = league.Teams.Keys.ToList();

		for ( var week = 1; week <= weeks; week++ )
		{
			var shuffled = teamIds.OrderBy( _ => random.NextInt( 0, int.MaxValue ) ).ToList();
			for ( var i = 0; i < shuffled.Count - 1; i += 2 )
			{
				var home = shuffled[i];
				var away = shuffled[i + 1];
				schedule.Games.Add( new ScheduledGame
				{
					Id = GameId.New(),
					Season = league.CurrentSeason,
					Week = week,
					HomeTeamId = home,
					AwayTeamId = away
				} );
			}
		}

		return schedule;
	}
}
