using Dynasty.Core.Enums;
using Dynasty.Core.Interfaces;
using Dynasty.Domain.League;
using Dynasty.Domain.Players;

namespace Dynasty.Systems.Development;

public sealed class PlayerDevelopmentSystem : ILeagueSystem
{
	public string SystemId => "player_development";

	private LeagueSystemContext _context;

	public void Register( LeagueSystemContext context ) => _context = context;

	public void OnLeagueCreated( LeagueState state ) { }

	public void OnPhaseEntered( LeaguePhase phase, LeagueState state ) { }

	public void OnWeekAdvanced( LeagueState state )
	{
		if ( state.Phase == LeaguePhase.Offseason )
			DevelopAllPlayers( state, weekly: false );
		else if ( state.Phase is LeaguePhase.RegularSeason or LeaguePhase.Preseason )
			DevelopAllPlayers( state, weekly: true );
	}

	public void OnSeasonEnded( LeagueState state ) => DevelopAllPlayers( state, weekly: false );

	void DevelopAllPlayers( LeagueState state, bool weekly )
	{
		foreach ( var player in state.Players.Values.Where( p => !p.IsRetired ) )
		{
			if ( !state.Teams.TryGetValue( player.TeamId, out var team ) )
				continue;

			var coachingBonus = GetDevelopmentBonus( state, team );
			var facilityBonus = team.Facilities.Levels.GetValueOrDefault( FacilityType.TrainingFacility, 1 ) * 2;
			var traitMod = player.Traits.Contains( PlayerTrait.Lazy ) ? -3 : player.Traits.Contains( PlayerTrait.Leader ) ? 2 : 0;
			var workEthicMod = player.Personality.WorkEthic >= 80 ? 2 : player.Personality.WorkEthic <= 35 ? -2 : 0;

			var ageFactor = player.Identity.Age switch
			{
				< 24 => 4,
				< 27 => 2,
				< 30 => 0,
				< 33 => -2,
				_ => -4
			};

			var potentialGap = player.Ratings.Potential - player.Ratings.Overall;
			var growthChance = weekly ? 0.15f : 0.55f;

			if ( potentialGap > 0 && _context.Random.Chance( growthChance ) )
			{
				var delta = _context.Random.NextInt( 0, 3 ) + coachingBonus / 10 + facilityBonus / 5 + traitMod + workEthicMod + Math.Max( 0, ageFactor );
				player.Ratings.Overall = Math.Min( player.Ratings.Potential, player.Ratings.Overall + delta );
				BumpAttributes( player, delta, positive: true );
			}
			else if ( ageFactor < 0 && _context.Random.Chance( weekly ? 0.08f : 0.35f ) )
			{
				var delta = _context.Random.NextInt( 0, 2 );
				player.Ratings.Overall = Math.Max( 40, player.Ratings.Overall - delta );
				BumpAttributes( player, delta, positive: false );
			}

			player.DevelopmentPoints += coachingBonus + facilityBonus + ageFactor + workEthicMod;
		}

		state.BumpRevision( "player_development" );
	}

	static int GetDevelopmentBonus( LeagueState state, Domain.Teams.TeamState team )
	{
		var hc = team.CoachIds
			.Select( id => state.Coaches.GetValueOrDefault( id ) )
			.FirstOrDefault( c => c?.Role == CoachRole.HeadCoach );

		return hc?.Ratings.Development ?? 60;
	}

	static void BumpAttributes( PlayerState player, int delta, bool positive )
	{
		foreach ( var key in player.Ratings.Attributes.Keys.ToList() )
		{
			var value = player.Ratings.Attributes[key];
			player.Ratings.Attributes[key] = positive
				? Math.Min( 99, value + delta )
				: Math.Max( 40, value - delta );
		}
	}
}
