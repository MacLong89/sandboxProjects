using Dynasty.Core.Attributes;
using Dynasty.Core.Enums;
using Dynasty.Core.Identifiers;
using Dynasty.Core.Interfaces;
using Dynasty.Domain.Contracts;
using Dynasty.Domain.Players;

namespace Dynasty.Domain.Factories;

public static class PlayerFactory
{
	public static PlayerState CreatePlayer(
		Position position,
		ILeagueDataDefinitions definitions,
		ILeagueRandom random,
		TeamId teamId,
		int age,
		bool isProspect = false )
	{
		var potential = random.NextInt( 55, 99 );
		var overall = isProspect
			? random.NextInt( 45, 78 )
			: RollOverallForVeteran( potential, random );

		var attributes = new Dictionary<string, int>();
		if ( PlayerAttributeKeys.ByPosition.TryGetValue( position, out var keys ) )
		{
			foreach ( var key in keys )
			{
				var attrMin = Math.Max( 1, overall - 15 );
				var attrMax = Math.Max( attrMin + 1, overall + 11 );
				attributes[key] = random.NextInt( attrMin, attrMax );
			}
		}

		var traits = new List<PlayerTrait>();
		if ( random.Chance( 0.25f ) ) traits.Add( PickTrait( random, positive: true ) );
		if ( random.Chance( 0.15f ) ) traits.Add( PickTrait( random, positive: false ) );

		var hiddenTraits = new List<PlayerTrait>();
		if ( isProspect )
		{
			if ( random.Chance( 0.35f ) ) hiddenTraits.Add( PickTrait( random, positive: true ) );
			if ( random.Chance( 0.20f ) ) hiddenTraits.Add( PickTrait( random, positive: false ) );
		}

		var personality = new PlayerPersonality
		{
			Ambition = random.NextInt( 25, 95 ),
			Loyalty = random.NextInt( 25, 95 ),
			Leadership = random.NextInt( 20, 90 ),
			WorkEthic = random.NextInt( 30, 95 ),
			Temperament = random.NextInt( 25, 90 ),
			Ego = random.NextInt( 15, 85 ),
			Marketability = random.NextInt( 20, 95 )
		};

		return new PlayerState
		{
			Id = PlayerId.New(),
			TeamId = teamId,
			Identity = new PlayerIdentity
			{
				FirstName = random.Pick( definitions.FirstNames ),
				LastName = random.Pick( definitions.LastNames ),
				Age = age,
				Position = position,
				College = random.Pick( definitions.Colleges ),
				Hometown = random.Pick( Hometowns ),
				Backstory = isProspect ? PickProspectBackstory( random ) : PickVeteranBackstory( random )
			},
			Ratings = new PlayerRatings
			{
				Overall = Math.Clamp( overall, 40, 99 ),
				Potential = potential,
				Attributes = attributes
			},
			Traits = traits,
			HiddenTraits = hiddenTraits,
			RookieSeason = isProspect ? 0 : 0,
			Personality = personality,
			Morale = new PlayerMoraleState { Morale = random.NextInt( 55, 85 ) },
			Contract = new ContractState
			{
				YearsRemaining = isProspect ? 4 : random.NextInt( 0, 4 ),
				AnnualSalary = random.NextInt( 750_000, 25_000_000 ),
				GuaranteedMoney = random.NextInt( 0, 10_000_000 ),
				SignedWithTeamId = teamId
			}
		};
	}

	static int RollOverallForVeteran( int potential, ILeagueRandom random )
	{
		var maxOverall = Math.Clamp( potential, 60, 94 );
		return random.NextInt( 60, maxOverall + 1 );
	}

	static readonly string[] Hometowns =
	{
		"Dallas, TX", "Miami, FL", "Atlanta, GA", "Los Angeles, CA", "Chicago, IL",
		"Houston, TX", "Phoenix, AZ", "Detroit, MI", "Seattle, WA", "Denver, CO",
		"Philadelphia, PA", "Cleveland, OH", "Kansas City, MO", "Nashville, TN", "Charlotte, NC",
		"New Orleans, LA", "Baltimore, MD", "Tampa, FL", "Portland, OR", "San Antonio, TX"
	};

	static string PickProspectBackstory( ILeagueRandom random ) => random.Pick( ProspectBackstories );

	static string PickVeteranBackstory( ILeagueRandom random ) => random.Pick( VeteranBackstories );

	static readonly string[] ProspectBackstories =
	{
		"Standout combine performer with raw tools.",
		"Productive college starter with a high floor.",
		"Small-school star who dominated his level.",
		"Late bloomer who broke out as a senior.",
		"Transfer with one year of elite tape.",
		"Team captain known for leadership and preparation.",
		"Explosive athlete with refinement still needed.",
		"Technician with limited upside but steady play.",
		"Junior declare with first-round buzz.",
		"Redshirt senior ready to contribute immediately."
	};

	static readonly string[] VeteranBackstories =
	{
		"Reliable veteran and locker room presence.",
		"Former Pro Bowl talent on the back nine.",
		"Special teams ace fighting for a roster spot.",
		"Bridge starter while a rookie develops.",
		"Cap-friendly role player with scheme versatility.",
		"Comeback story after missing time with injury.",
		"High-motor contributor on a friendly contract.",
		"Depth piece who knows the system inside out.",
		"Former draft pick looking to revive his career.",
		"Well-traveled journeyman with steady production."
	};

	static PlayerTrait PickTrait( ILeagueRandom random, bool positive )
	{
		var pool = positive
			? new[] { PlayerTrait.Clutch, PlayerTrait.IronMan, PlayerTrait.Leader, PlayerTrait.TeamFriendly }
			: new[] { PlayerTrait.InjuryProne, PlayerTrait.Diva, PlayerTrait.Choker, PlayerTrait.Lazy };

		return random.Pick( pool );
	}
}
