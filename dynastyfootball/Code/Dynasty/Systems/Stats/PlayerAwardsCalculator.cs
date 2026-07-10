using Dynasty.Core.Attributes;
using Dynasty.Core.Enums;
using Dynasty.Core.Identifiers;
using Dynasty.Core.Stats;
using Dynasty.Domain.History;
using Dynasty.Domain.League;
using Dynasty.Domain.Players;

namespace Dynasty.Systems.Stats;

public static class PlayerAwardsCalculator
{
	public static void GrantSeasonAwards( LeagueState state )
	{
		var season = state.CurrentSeason;
		var active = state.Players.Values.Where( p => !p.IsRetired && p.Career.SeasonStats.Count > 0 ).ToList();
		if ( active.Count == 0 )
			return;

		Grant( state, season, "League MVP", PickMvp( active ) );
		Grant( state, season, "Offensive Player of the Year", PickOffensivePlayer( active ) );
		Grant( state, season, "Defensive Player of the Year", PickDefensivePlayer( active ) );
		GrantRookieAward( state, season, active );
		GrantProBowl( state, season, active );
		UpdateSingleSeasonRecords( state, season, active );
	}

	static void Grant( LeagueState state, int season, string awardName, PlayerState player )
	{
		if ( player == null )
			return;

		state.History.Awards.Add( new AwardRecord
		{
			Season = season,
			AwardName = awardName,
			PlayerId = player.Id,
			TeamId = player.TeamId
		} );

		player.Career.Awards.Add( $"{season} {awardName}" );
	}

	static void GrantRookieAward( LeagueState state, int season, List<PlayerState> active )
	{
		var rookies = active.Where( p => p.RookieSeason == season ).ToList();
		if ( rookies.Count == 0 )
			return;

		var best = rookies.OrderByDescending( RookieScore ).First();
		Grant( state, season, "Rookie of the Year", best );
	}

	static void GrantProBowl( LeagueState state, int season, List<PlayerState> active )
	{
		var byPosition = active
			.GroupBy( p => p.Identity.Position )
			.SelectMany( g => g.OrderByDescending( PositionScore ).Take( 2 ) );

		foreach ( var player in byPosition )
			player.Career.Awards.Add( $"{season} Pro Bowl" );
	}

	static void UpdateSingleSeasonRecords( LeagueState state, int season, List<PlayerState> active )
	{
		TryRecord( state, season, "Single-Season Passing Yards", PlayerStatKeys.PassYds, active );
		TryRecord( state, season, "Single-Season Rushing Yards", PlayerStatKeys.RushYds, active );
		TryRecord( state, season, "Single-Season Receiving Yards", PlayerStatKeys.RecYds, active );
		TryRecord( state, season, "Single-Season Sacks", PlayerStatKeys.Sacks, active );
	}

	static void TryRecord( LeagueState state, int season, string recordName, string statKey, List<PlayerState> active )
	{
		var leader = active
			.Select( p => ( Player: p, Value: p.Career.SeasonStats.GetValueOrDefault( statKey ) ) )
			.Where( x => x.Value > 0 )
			.OrderByDescending( x => x.Value )
			.FirstOrDefault();

		if ( leader.Player == null )
			return;

		var existing = state.History.Records.FirstOrDefault( r => r.StatKey == statKey );
		if ( existing != null && leader.Value <= existing.Value )
			return;

		if ( existing != null )
			state.History.Records.Remove( existing );

		state.History.Records.Add( new StatRecord
		{
			RecordName = recordName,
			StatKey = statKey,
			Value = leader.Value,
			PlayerId = leader.Player.Id,
			Season = season
		} );
	}

	static PlayerState PickMvp( List<PlayerState> players )
		=> players.OrderByDescending( MvpScore ).FirstOrDefault();

	static PlayerState PickOffensivePlayer( List<PlayerState> players )
		=> players
			.Where( p => IsOffense( p.Identity.Position ) )
			.OrderByDescending( OffensiveScore )
			.FirstOrDefault();

	static PlayerState PickDefensivePlayer( List<PlayerState> players )
		=> players
			.Where( p => IsDefense( p.Identity.Position ) )
			.OrderByDescending( DefensiveScore )
			.FirstOrDefault();

	static bool IsOffense( Position position ) => PlayerAttributeKeys.GetGroup( position ) is
		PositionGroup.Quarterback or PositionGroup.RunningBack or PositionGroup.WideReceiver or PositionGroup.TightEnd;

	static bool IsDefense( Position position ) => PlayerAttributeKeys.GetGroup( position ) is
		PositionGroup.DefensiveLine or PositionGroup.Linebacker or PositionGroup.DefensiveBack;

	static float MvpScore( PlayerState p )
	{
		var s = p.Career.SeasonStats;
		return s.GetValueOrDefault( PlayerStatKeys.PassYds ) * 0.04f
			+ s.GetValueOrDefault( PlayerStatKeys.PassTd ) * 4f
			+ s.GetValueOrDefault( PlayerStatKeys.RushYds ) * 0.1f
			+ s.GetValueOrDefault( PlayerStatKeys.RushTd ) * 6f
			+ s.GetValueOrDefault( PlayerStatKeys.RecYds ) * 0.1f
			+ s.GetValueOrDefault( PlayerStatKeys.RecTd ) * 6f
			+ s.GetValueOrDefault( PlayerStatKeys.Tackles ) * 1.2f
			+ s.GetValueOrDefault( PlayerStatKeys.Sacks ) * 5f
			+ s.GetValueOrDefault( PlayerStatKeys.Int ) * 6f
			+ p.Ratings.Overall * 0.15f;
	}

	static float OffensiveScore( PlayerState p )
	{
		var s = p.Career.SeasonStats;
		return s.GetValueOrDefault( PlayerStatKeys.PassYds ) * 0.05f
			+ s.GetValueOrDefault( PlayerStatKeys.PassTd ) * 5f
			+ s.GetValueOrDefault( PlayerStatKeys.RushYds ) * 0.12f
			+ s.GetValueOrDefault( PlayerStatKeys.RushTd ) * 6f
			+ s.GetValueOrDefault( PlayerStatKeys.RecYds ) * 0.12f
			+ s.GetValueOrDefault( PlayerStatKeys.RecTd ) * 6f;
	}

	static float DefensiveScore( PlayerState p )
	{
		var s = p.Career.SeasonStats;
		return s.GetValueOrDefault( PlayerStatKeys.Tackles ) * 1.5f
			+ s.GetValueOrDefault( PlayerStatKeys.Sacks ) * 6f
			+ s.GetValueOrDefault( PlayerStatKeys.Int ) * 8f
			+ s.GetValueOrDefault( PlayerStatKeys.Tfl ) * 2f;
	}

	static float RookieScore( PlayerState p ) => MvpScore( p );

	static float PositionScore( PlayerState p ) => MvpScore( p );
}
