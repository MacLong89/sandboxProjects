using Dynasty.Core.Enums;

using Dynasty.Core.Identifiers;

using Dynasty.Domain.Coaches;

using Dynasty.Domain.League;

using Dynasty.Domain.Players;

using Dynasty.Domain.Simulation;

using Dynasty.Domain.Teams;



namespace Dynasty.Systems.Simulation;



public static class TeamProfileBuilder

{

	public static TeamSimulationProfile Build( LeagueState league, TeamId teamId )

	{

		if ( !league.Teams.TryGetValue( teamId, out var team ) )

			throw new ArgumentException( $"Unknown team {teamId}" );



		var offense = DepthChartRatingHelper.GetOffenseRating( league, team );

		var defense = DepthChartRatingHelper.GetDefenseRating( league, team );

		var special = DepthChartRatingHelper.GetSpecialTeamsRating( league, team );

		var coaching = GetCoachingRating( league, team );

		var chemistry = (team.Chemistry.Morale + team.Chemistry.LockerRoomHealth + team.Chemistry.Leadership) / 3;



		var players = team.RosterPlayerIds

			.Select( id => league.Players.GetValueOrDefault( id ) )

			.Where( p => p != null && !p.IsRetired )

			.ToList();

		var injuryPenalty = players.Count( p => p.Injury.Severity != InjurySeverity.None ) * 2.5f;



		var profile = new TeamSimulationProfile

		{

			TeamId = teamId,

			OffenseRating = offense,

			DefenseRating = defense,

			SpecialTeamsRating = special,

			CoachingRating = coaching,

			ChemistryRating = chemistry,

			InjuryPenalty = injuryPenalty

		};



		ApplyPlayStyleModifier( profile, team.PlayStyle );

		Season.WeeklyGamePlanSystem.ApplyGamePlanBonus( team, profile );
		return profile;

	}



	public static int ComputeOverallRating( TeamSimulationProfile profile )

	{

		var rating = profile.OffenseRating * 0.45f

			+ profile.DefenseRating * 0.30f

			+ profile.SpecialTeamsRating * 0.10f

			+ profile.CoachingRating * 0.10f

			+ profile.ChemistryRating * 0.05f

			- profile.InjuryPenalty;



		return Math.Clamp( (int)Math.Round( rating ), 0, 99 );

	}



	static void ApplyPlayStyleModifier( TeamSimulationProfile profile, TeamPlayStyle playStyle )

	{

		switch ( playStyle )

		{

			case TeamPlayStyle.AirRaid:

				profile.OffenseRating = Math.Clamp( profile.OffenseRating + 3, 0, 99 );

				profile.DefenseRating = Math.Clamp( profile.DefenseRating - 2, 0, 99 );

				break;

			case TeamPlayStyle.GroundAndPound:

				profile.OffenseRating = Math.Clamp( profile.OffenseRating + 2, 0, 99 );

				profile.SpecialTeamsRating = Math.Clamp( profile.SpecialTeamsRating + 1, 0, 99 );

				break;

			case TeamPlayStyle.DefensiveDynasty:

				profile.DefenseRating = Math.Clamp( profile.DefenseRating + 4, 0, 99 );

				profile.OffenseRating = Math.Clamp( profile.OffenseRating - 2, 0, 99 );

				break;

			case TeamPlayStyle.VeteranLeadership:

				profile.CoachingRating = Math.Clamp( profile.CoachingRating + 2, 0, 99 );

				profile.ChemistryRating = Math.Clamp( profile.ChemistryRating + 3, 0, 99 );

				break;

			case TeamPlayStyle.YouthMovement:

				profile.OffenseRating = Math.Clamp( profile.OffenseRating + 1, 0, 99 );

				break;

		}

	}



	static int GetCoachingRating( LeagueState league, TeamState team )

	{

		var coaches = team.CoachIds

			.Where( id => league.Coaches.TryGetValue( id, out _ ) )

			.Select( id => league.Coaches[id] )

			.ToList();



		if ( coaches.Count == 0 ) return 60;



		var hc = coaches.FirstOrDefault( c => c.Role == CoachRole.HeadCoach );

		var oc = coaches.FirstOrDefault( c => c.Role == CoachRole.OffensiveCoordinator );

		var dc = coaches.FirstOrDefault( c => c.Role == CoachRole.DefensiveCoordinator );



		return (int)(

			(hc?.Ratings.GamePlanning ?? 65) * 0.5f

			+ (oc?.Ratings.GamePlanning ?? 65) * 0.25f

			+ (dc?.Ratings.GamePlanning ?? 65) * 0.25f );

	}

}


