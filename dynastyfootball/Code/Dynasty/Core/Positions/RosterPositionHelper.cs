using Dynasty.Core.Enums;
using Dynasty.Core.Identifiers;
using Dynasty.Domain.League;
using Dynasty.Domain.Players;
using Dynasty.Domain.Teams;

namespace Dynasty.Core.Positions;

public static class RosterPositionHelper
{
	public static readonly string[] SortOrder =
	{
		"QB", "RB", "WR", "TE", "LT", "LG", "C", "RG", "RT",
		"DE", "DT", "OLB", "LB", "CB", "S", "K", "P", "LS", "FB"
	};

	public static int SortIndex( string displayPosition )
	{
		for ( var i = 0; i < SortOrder.Length; i++ )
		{
			if ( SortOrder[i] == displayPosition )
				return i;
		}

		return SortOrder.Length;
	}

	public static Dictionary<PlayerId, string> BuildDisplayPositions( TeamState team, LeagueState state )
	{
		var map = new Dictionary<PlayerId, string>();
		var players = team.RosterPlayerIds
			.Select( id => state.Players.GetValueOrDefault( id ) )
			.Where( p => p != null && !p.IsRetired )
			.ToList();

		AssignOlSlots( players, Position.OT, "LT", "RT", map );
		AssignOlSlots( players, Position.OG, "LG", "RG", map );
		AssignLinebackerSlots( players, map );

		foreach ( var player in players )
		{
			if ( map.ContainsKey( player.Id ) )
				continue;

			map[player.Id] = player.Identity.Position switch
			{
				Position.FB => "RB",
				Position.C => "C",
				Position.QB => "QB",
				Position.RB => "RB",
				Position.WR => "WR",
				Position.TE => "TE",
				Position.DE => "DE",
				Position.DT => "DT",
				Position.CB => "CB",
				Position.S => "S",
				Position.K => "K",
				Position.P => "P",
				Position.LS => "LS",
				_ => player.Identity.Position.ToString()
			};
		}

		return map;
	}

	public static string GetDisplayPosition( PlayerState player, TeamState team, LeagueState state )
	{
		var map = BuildDisplayPositions( team, state );
		return map.GetValueOrDefault( player.Id, player.Identity.Position.ToString() );
	}

	static void AssignOlSlots(
		List<PlayerState> players,
		Position position,
		string leftLabel,
		string rightLabel,
		Dictionary<PlayerId, string> map )
	{
		var group = players
			.Where( p => p.Identity.Position == position && !map.ContainsKey( p.Id ) )
			.OrderByDescending( p => p.Ratings.Overall )
			.ToList();

		for ( var i = 0; i < group.Count; i++ )
			map[group[i].Id] = i % 2 == 0 ? leftLabel : rightLabel;
	}

	static void AssignLinebackerSlots( List<PlayerState> players, Dictionary<PlayerId, string> map )
	{
		var lbs = players
			.Where( p => p.Identity.Position == Position.LB && !map.ContainsKey( p.Id ) )
			.OrderByDescending( p => p.Ratings.Overall )
			.ToList();

		for ( var i = 0; i < lbs.Count; i++ )
			map[lbs[i].Id] = i == 0 ? "OLB" : "LB";
	}
}
