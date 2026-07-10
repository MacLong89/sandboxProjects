using Dynasty.Core.Attributes;
using Dynasty.Core.Enums;
using Dynasty.Core.Identifiers;
using Dynasty.Core.Interfaces;
using Dynasty.Core.Stats;
using Dynasty.Domain.League;
using Dynasty.Domain.Players;
using Dynasty.Domain.Schedule;

namespace Dynasty.Systems.Simulation;

public static class BoxScoreGenerator
{
	public static Dictionary<PlayerId, Dictionary<string, int>> Generate(
		LeagueState state,
		ScheduledGame game,
		int homeScore,
		int awayScore,
		ILeagueRandom random )
	{
		var boxScores = new Dictionary<PlayerId, Dictionary<string, int>>();

		GenerateTeamOffense( state, game.HomeTeamId, homeScore, awayScore, boxScores, random );
		GenerateTeamOffense( state, game.AwayTeamId, awayScore, homeScore, boxScores, random );
		GenerateTeamDefense( state, game.HomeTeamId, awayScore, boxScores, random );
		GenerateTeamDefense( state, game.AwayTeamId, homeScore, boxScores, random );

		return boxScores;
	}

	static void GenerateTeamOffense(
		LeagueState state,
		TeamId teamId,
		int pointsFor,
		int pointsAgainst,
		Dictionary<PlayerId, Dictionary<string, int>> boxScores,
		ILeagueRandom random )
	{
		if ( !state.Teams.TryGetValue( teamId, out var team ) )
			return;

		var roster = GetRosterPlayers( state, team );
		if ( roster.Count == 0 )
			return;

		var qb = PickTop( roster, Position.QB );
		var rb = PickTop( roster, Position.RB ) ?? PickTop( roster, Position.FB );
		var wrs = PickTopN( roster, Position.WR, 3 );
		var te = PickTop( roster, Position.TE );
		var k = PickTop( roster, Position.K );

		var tdCount = Math.Max( 0, pointsFor / 7 );
		var fgCount = Math.Max( 0, ( pointsFor - tdCount * 7 ) / 3 );
		var xpMade = tdCount;

		var passYds = random.NextInt( 140, 320 ) + pointsFor * random.NextInt( 18, 32 );
		var rushYds = random.NextInt( 60, 160 ) + pointsFor * random.NextInt( 6, 14 );
		var recYds = (int)( passYds * ( 0.82f + random.NextFloat() * 0.14f ) );

		if ( qb != null )
		{
			var stats = GetOrCreate( boxScores, qb.Id );
			var att = random.NextInt( 22, 42 );
			var comp = Math.Clamp( (int)( att * ( 0.52f + qb.Ratings.Overall * 0.004f ) ), 10, att );
			StatAggregator.Add( stats, PlayerStatKeys.Games, 1 );
			StatAggregator.Add( stats, PlayerStatKeys.Att, att );
			StatAggregator.Add( stats, PlayerStatKeys.Comp, comp );
			StatAggregator.Add( stats, PlayerStatKeys.PassYds, passYds );
			StatAggregator.Add( stats, PlayerStatKeys.PassTd, Math.Max( 0, tdCount - random.NextInt( 0, 2 ) ) );
			StatAggregator.Add( stats, PlayerStatKeys.PassInt, random.NextInt( 0, pointsAgainst > 20 ? 3 : 2 ) );
			StatAggregator.Add( stats, PlayerStatKeys.RushAtt, random.NextInt( 2, 8 ) );
			StatAggregator.Add( stats, PlayerStatKeys.RushYds, random.NextInt( 5, 45 ) );
		}

		if ( rb != null )
		{
			var stats = GetOrCreate( boxScores, rb.Id );
			StatAggregator.Add( stats, PlayerStatKeys.Games, 1 );
			StatAggregator.Add( stats, PlayerStatKeys.RushAtt, random.NextInt( 8, 22 ) );
			StatAggregator.Add( stats, PlayerStatKeys.RushYds, rushYds );
			StatAggregator.Add( stats, PlayerStatKeys.RushTd, random.NextInt( 0, Math.Max( 1, tdCount ) ) );
			StatAggregator.Add( stats, PlayerStatKeys.Targets, random.NextInt( 1, 6 ) );
			StatAggregator.Add( stats, PlayerStatKeys.Rec, random.NextInt( 0, 5 ) );
			StatAggregator.Add( stats, PlayerStatKeys.RecYds, random.NextInt( 0, 40 ) );
		}

		var receivers = new List<PlayerState>();
		if ( te != null ) receivers.Add( te );
		receivers.AddRange( wrs );
		DistributeReceiving( receivers, recYds, tdCount, boxScores, random );

		if ( k != null && ( fgCount > 0 || xpMade > 0 ) )
		{
			var stats = GetOrCreate( boxScores, k.Id );
			StatAggregator.Add( stats, PlayerStatKeys.Games, 1 );
			StatAggregator.Add( stats, PlayerStatKeys.FgMade, fgCount );
			StatAggregator.Add( stats, PlayerStatKeys.FgAtt, fgCount + random.NextInt( 0, 2 ) );
			StatAggregator.Add( stats, PlayerStatKeys.XpMade, xpMade );
		}
	}

	static void GenerateTeamDefense(
		LeagueState state,
		TeamId teamId,
		int pointsAllowed,
		Dictionary<PlayerId, Dictionary<string, int>> boxScores,
		ILeagueRandom random )
	{
		if ( !state.Teams.TryGetValue( teamId, out var team ) )
			return;

		var roster = GetRosterPlayers( state, team );
		var defenders = roster
			.Where( p => PlayerAttributeKeys.GetGroup( p.Identity.Position ) is
				PositionGroup.DefensiveLine or PositionGroup.Linebacker or PositionGroup.DefensiveBack )
			.OrderByDescending( p => p.Ratings.Overall )
			.Take( 6 )
			.ToList();

		if ( defenders.Count == 0 )
			return;

		var tacklePool = random.NextInt( 40, 72 ) + pointsAllowed;
		var sackPool = random.NextInt( 0, 4 ) + pointsAllowed / 10;
		var intPool = random.NextInt( 0, pointsAllowed > 24 ? 3 : 2 );

		var weights = defenders.Select( p => Math.Max( 1, p.Ratings.Overall ) ).ToArray();
		var totalWeight = weights.Sum();

		for ( var i = 0; i < defenders.Count; i++ )
		{
			var share = weights[i] / (float)totalWeight;
			var stats = GetOrCreate( boxScores, defenders[i].Id );
			StatAggregator.Add( stats, PlayerStatKeys.Games, 1 );
			StatAggregator.Add( stats, PlayerStatKeys.Tackles, Math.Max( 1, (int)( tacklePool * share ) ) );
			if ( i < 2 )
			{
				StatAggregator.Add( stats, PlayerStatKeys.Sacks, Math.Max( 0, (int)Math.Round( sackPool * share * 1.4f ) ) );
				StatAggregator.Add( stats, PlayerStatKeys.Tfl, random.NextInt( 0, 3 ) );
			}

			if ( intPool > 0 && i == 0 && random.Chance( 0.45f ) )
			{
				StatAggregator.Add( stats, PlayerStatKeys.Int, 1 );
				intPool--;
			}
		}
	}

	static void DistributeReceiving(
		List<PlayerState> receivers,
		int recYds,
		int tdCount,
		Dictionary<PlayerId, Dictionary<string, int>> boxScores,
		ILeagueRandom random )
	{
		if ( receivers.Count == 0 )
			return;

		var remainingYds = recYds;
		var remainingTd = tdCount;
		for ( var i = 0; i < receivers.Count; i++ )
		{
			var player = receivers[i];
			var share = i == receivers.Count - 1
				? remainingYds
				: random.NextInt( Math.Max( 1, remainingYds / ( receivers.Count - i ) / 2 ), Math.Max( 2, remainingYds / ( receivers.Count - i ) ) );

			share = Math.Clamp( share, 0, remainingYds );
			remainingYds -= share;

			var stats = GetOrCreate( boxScores, player.Id );
			StatAggregator.Add( stats, PlayerStatKeys.Games, 1 );
			StatAggregator.Add( stats, PlayerStatKeys.Targets, random.NextInt( 3, 11 ) );
			StatAggregator.Add( stats, PlayerStatKeys.Rec, random.NextInt( 2, 8 ) );
			StatAggregator.Add( stats, PlayerStatKeys.RecYds, share );

			if ( remainingTd > 0 && random.Chance( 0.55f ) )
			{
				StatAggregator.Add( stats, PlayerStatKeys.RecTd, 1 );
				remainingTd--;
			}
		}
	}

	static List<PlayerState> GetRosterPlayers( LeagueState state, Domain.Teams.TeamState team )
		=> team.RosterPlayerIds
			.Select( id => state.Players.GetValueOrDefault( id ) )
			.Where( p => p != null && !p.IsRetired )
			.ToList();

	static PlayerState PickTop( List<PlayerState> roster, Position position )
		=> roster.Where( p => p.Identity.Position == position ).OrderByDescending( p => p.Ratings.Overall ).FirstOrDefault();

	static List<PlayerState> PickTopN( List<PlayerState> roster, Position position, int count )
		=> roster.Where( p => p.Identity.Position == position ).OrderByDescending( p => p.Ratings.Overall ).Take( count ).ToList();

	static Dictionary<string, int> GetOrCreate( Dictionary<PlayerId, Dictionary<string, int>> boxScores, PlayerId playerId )
	{
		if ( !boxScores.TryGetValue( playerId, out var stats ) )
		{
			stats = new Dictionary<string, int>();
			boxScores[playerId] = stats;
		}

		return stats;
	}
}
