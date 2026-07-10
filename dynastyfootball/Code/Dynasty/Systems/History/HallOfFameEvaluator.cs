using Dynasty.Core.Stats;
using Dynasty.Domain.History;
using Dynasty.Domain.League;
using Dynasty.Domain.Players;

namespace Dynasty.Systems.History;

public static class HallOfFameEvaluator
{
	public static bool ShouldInduct( PlayerState player )
	{
		if ( player.Ratings.Overall >= 88 )
			return true;

		if ( player.Career.ChampionshipRings >= 2 )
			return true;

		var majorAwards = player.Career.Awards.Count( a =>
			a.Contains( "MVP", StringComparison.OrdinalIgnoreCase )
			|| a.Contains( "Player of the Year", StringComparison.OrdinalIgnoreCase ) );

		if ( majorAwards >= 1 )
			return true;

		var proBowls = player.Career.Awards.Count( a =>
			a.Contains( "Pro Bowl", StringComparison.OrdinalIgnoreCase ) );

		if ( proBowls >= 4 )
			return true;

		if ( player.Career.CareerStats.GetValueOrDefault( PlayerStatKeys.PassYds ) >= 40_000 )
			return true;

		if ( player.Career.CareerStats.GetValueOrDefault( PlayerStatKeys.RushYds ) >= 10_000 )
			return true;

		if ( player.Career.CareerStats.GetValueOrDefault( PlayerStatKeys.RecYds ) >= 12_000 )
			return true;

		if ( player.Career.CareerStats.GetValueOrDefault( PlayerStatKeys.Sacks ) >= 100 )
			return true;

		return player.Career.ChampionshipRings >= 1 && player.Ratings.Overall >= 82;
	}

	public static string BuildCitation( PlayerState player )
	{
		if ( player.Career.ChampionshipRings >= 2 )
			return $"{player.Career.ChampionshipRings}x champion · {player.Identity.Position} · OVR {player.Ratings.Overall}";

		var mvp = player.Career.Awards.FirstOrDefault( a => a.Contains( "MVP", StringComparison.OrdinalIgnoreCase ) );
		if ( mvp != null )
			return $"{mvp} · {player.Identity.Position} · OVR {player.Ratings.Overall}";

		if ( player.Career.CareerStats.GetValueOrDefault( PlayerStatKeys.PassYds ) >= 40_000 )
			return $"40,000+ career passing yards · OVR {player.Ratings.Overall}";

		if ( player.Career.CareerStats.GetValueOrDefault( PlayerStatKeys.RushYds ) >= 10_000 )
			return $"10,000+ career rushing yards · OVR {player.Ratings.Overall}";

		if ( player.Career.CareerStats.GetValueOrDefault( PlayerStatKeys.RecYds ) >= 12_000 )
			return $"12,000+ career receiving yards · OVR {player.Ratings.Overall}";

		return $"Elite {player.Identity.Position} · OVR {player.Ratings.Overall} · {player.Career.Awards.Count} honors";
	}

	public static void TryInduct( LeagueState state, PlayerState player, int season )
	{
		if ( !ShouldInduct( player ) )
			return;

		if ( state.History.HallOfFame.Any( h => h.PlayerId.Value == player.Id.Value ) )
			return;

		state.History.HallOfFame.Add( new HallOfFameEntry
		{
			PlayerId = player.Id,
			InductionSeason = season,
			Citation = BuildCitation( player )
		} );
	}
}
