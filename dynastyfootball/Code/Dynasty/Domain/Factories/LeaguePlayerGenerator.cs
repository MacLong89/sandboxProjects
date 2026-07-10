using Dynasty.Core.Enums;
using Dynasty.Core.Identifiers;
using Dynasty.Core.Interfaces;
using Dynasty.Domain.League;
using Dynasty.Domain.Players;
using Dynasty.Domain.Teams;

namespace Dynasty.Domain.Factories;

/// <summary>
/// Generates league-wide player pools and position-balanced rosters.
/// </summary>
public static class LeaguePlayerGenerator
{
	public const int DefaultRosterSize = 53;
	public const int DefaultLeaguePoolSize = 1700;
	public const int DefaultRookieClassSize = 300;
	public const int DefaultMinPerPositionPerTeam = 2;

	static readonly Position[] AllPositions = Enum.GetValues<Position>();

	public static List<Position> BuildPositionSlots( int totalCount, int teamCount, int minPerPositionPerTeam, ILeagueRandom random )
	{
		var minPerPosition = teamCount * minPerPositionPerTeam;
		var slots = new List<Position>( totalCount );

		foreach ( var pos in AllPositions )
		{
			for ( var i = 0; i < minPerPosition && slots.Count < totalCount; i++ )
				slots.Add( pos );
		}

		while ( slots.Count < totalCount )
			slots.Add( AllPositions[random.NextInt( 0, AllPositions.Length )] );

		return slots.OrderBy( _ => random.NextInt( 0, int.MaxValue ) ).ToList();
	}

	public static void GenerateLeaguePool(
		LeagueState league,
		ILeagueDataDefinitions definitions,
		ILeagueRandom random,
		int poolSize,
		int minPerPositionPerTeam )
	{
		var positions = BuildPositionSlots( poolSize, league.Settings.TeamCount, minPerPositionPerTeam, random );

		foreach ( var position in positions )
		{
			var age = random.NextInt( 22, 35 );
			var player = PlayerFactory.CreatePlayer( position, definitions, random, TeamId.Empty, age );
			league.Players[player.Id] = player;
		}
	}

	public static void PopulateTeamRoster(
		LeagueState league,
		TeamState team,
		ILeagueDataDefinitions definitions,
		ILeagueRandom random,
		int rosterSize,
		int minPerPositionPerTeam )
	{
		var positions = BuildPositionSlots( rosterSize, 1, minPerPositionPerTeam, random );

		foreach ( var position in positions )
		{
			var age = random.NextInt( 22, 36 );
			var player = PlayerFactory.CreatePlayer( position, definitions, random, team.Id, age );
			league.Players[player.Id] = player;
			team.RosterPlayerIds.Add( player.Id );
		}
	}

	public static List<PlayerState> GenerateRookieClass(
		ILeagueDataDefinitions definitions,
		ILeagueRandom random,
		int classSize,
		int minPerPositionPerTeam )
	{
		var positions = BuildPositionSlots( classSize, 1, minPerPositionPerTeam, random );
		var players = new List<PlayerState>( classSize );

		foreach ( var position in positions )
		{
			var player = PlayerFactory.CreatePlayer( position, definitions, random, TeamId.Empty, age: 22, isProspect: true );
			players.Add( player );
		}

		return players;
	}
}
