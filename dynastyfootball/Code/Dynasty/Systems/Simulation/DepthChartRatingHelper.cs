using Dynasty.Core.Enums;
using Dynasty.Data;
using Dynasty.Domain.League;
using Dynasty.Domain.Players;
using Dynasty.Domain.Teams;
using Dynasty.Systems.Formation;

namespace Dynasty.Systems.Simulation;

/// <summary>
/// Derives team ratings from depth-chart starters. Falls back to roster averages when starters are unset.
/// </summary>
public static class DepthChartRatingHelper
{
	public static int GetOffenseRating( LeagueState league, TeamState team )
	{
		var layout = FormationLayoutRegistry.Get( team.ActiveOffenseFormation );
		return AverageStarterRating( league, team, layout.Slots, offensive: true );
	}

	public static int GetDefenseRating( LeagueState league, TeamState team )
	{
		var layout = FormationLayoutRegistry.Get( team.ActiveDefenseFormation );
		return AverageStarterRating( league, team, layout.Slots, offensive: false );
	}

	public static int GetSpecialTeamsRating( LeagueState league, TeamState team )
	{
		var layout = FormationLayoutRegistry.GetSpecialTeams();
		var rating = AverageStarterRating( league, team, layout.Slots, offensive: false, specialTeams: true );
		return rating > 0 ? rating : 65;
	}

	static int AverageStarterRating(
		LeagueState league,
		TeamState team,
		IEnumerable<FormationSlot> slots,
		bool offensive,
		bool specialTeams = false )
	{
		var ratings = new List<int>();
		foreach ( var slot in slots )
		{
			var starterId = Dynasty.Data.DepthChart.GetStarter( team.DepthChart, slot.SlotKey );
			if ( starterId.IsEmpty )
				continue;

			if ( !league.Players.TryGetValue( starterId, out var player ) || player.IsRetired )
				continue;

			if ( specialTeams )
			{
				if ( player.Identity.Position is not (Position.K or Position.P or Position.LS) )
					continue;
			}
			else if ( !MatchesSide( player, offensive ) )
				continue;

			ratings.Add( ApplyInjuryPenalty( player ) );
		}

		if ( ratings.Count == 0 )
			return FallbackRosterAverage( league, team, offensive, specialTeams );

		return (int)Math.Round( ratings.Average() );
	}

	static int ApplyInjuryPenalty( PlayerState player )
	{
		var ovr = player.Ratings.Overall;
		return player.Injury.Severity switch
		{
			InjurySeverity.Questionable => Math.Max( 40, ovr - 2 ),
			InjurySeverity.Doubtful => Math.Max( 40, ovr - 6 ),
			InjurySeverity.Out => Math.Max( 40, ovr - 12 ),
			InjurySeverity.SeasonEnding or InjurySeverity.CareerThreatening => Math.Max( 40, ovr - 20 ),
			_ => ovr
		};
	}

	static bool MatchesSide( PlayerState player, bool offensive )
	{
		var group = Core.Attributes.PlayerAttributeKeys.GetGroup( player.Identity.Position );
		if ( offensive )
		{
			return group is PositionGroup.Quarterback or PositionGroup.RunningBack or PositionGroup.WideReceiver
				or PositionGroup.TightEnd or PositionGroup.OffensiveLine;
		}

		return group is PositionGroup.DefensiveLine or PositionGroup.Linebacker or PositionGroup.DefensiveBack;
	}

	static int FallbackRosterAverage( LeagueState league, TeamState team, bool offensive, bool specialTeams )
	{
		var players = team.RosterPlayerIds
			.Select( id => league.Players.GetValueOrDefault( id ) )
			.Where( p => p != null && !p.IsRetired )
			.ToList();

		if ( players.Count == 0 )
			return 60;

		if ( specialTeams )
		{
			var st = players.Where( p => p.Identity.Position is Position.K or Position.P ).ToList();
			return st.Count > 0 ? (int)st.Average( p => p.Ratings.Overall ) : 65;
		}

		var filtered = players.Where( p => MatchesSide( p, offensive ) ).ToList();
		if ( filtered.Count == 0 )
			filtered = players;

		return (int)filtered.Average( p => p.Ratings.Overall );
	}
}
