namespace Sandbox;

/// <summary>Species×species relationship lookup — drives hunt/flee/stand-ground decisions.</summary>
public static class ThornsAnimalRelationshipTable
{
	public static ThornsAnimalRelationshipKind Resolve(
		ThornsWildlifeSpeciesKind observer,
		ThornsWildlifeSpeciesKind target,
		int nearbyPackMembers = 0 )
	{
		if ( observer == target )
			return observer switch
			{
				ThornsWildlifeSpeciesKind.Wolf => ThornsAnimalRelationshipKind.Curious,
				ThornsWildlifeSpeciesKind.Elk => ThornsAnimalRelationshipKind.Curious,
				_ => ThornsAnimalRelationshipKind.Ignore,
			};

		return ( observer, target ) switch
		{
			( ThornsWildlifeSpeciesKind.Wolf, ThornsWildlifeSpeciesKind.Elk ) => ThornsAnimalRelationshipKind.Hunt,
			( ThornsWildlifeSpeciesKind.Wolf, ThornsWildlifeSpeciesKind.Deer ) => ThornsAnimalRelationshipKind.Hunt,
			( ThornsWildlifeSpeciesKind.Wolf, ThornsWildlifeSpeciesKind.Rabbit ) => ThornsAnimalRelationshipKind.Hunt,
			( ThornsWildlifeSpeciesKind.Wolf, ThornsWildlifeSpeciesKind.Moose ) =>
				nearbyPackMembers >= 3 ? ThornsAnimalRelationshipKind.Attack : ThornsAnimalRelationshipKind.Avoid,
			( ThornsWildlifeSpeciesKind.Wolf, ThornsWildlifeSpeciesKind.Panther ) =>
				nearbyPackMembers >= 2 ? ThornsAnimalRelationshipKind.Attack : ThornsAnimalRelationshipKind.Avoid,
			( ThornsWildlifeSpeciesKind.Wolf, ThornsWildlifeSpeciesKind.Wolf ) => ThornsAnimalRelationshipKind.Curious,

			( ThornsWildlifeSpeciesKind.Panther, ThornsWildlifeSpeciesKind.Elk ) => ThornsAnimalRelationshipKind.Hunt,
			( ThornsWildlifeSpeciesKind.Panther, ThornsWildlifeSpeciesKind.Deer ) => ThornsAnimalRelationshipKind.Hunt,
			( ThornsWildlifeSpeciesKind.Panther, ThornsWildlifeSpeciesKind.Rabbit ) => ThornsAnimalRelationshipKind.Hunt,
			( ThornsWildlifeSpeciesKind.Panther, ThornsWildlifeSpeciesKind.Wolf ) => ThornsAnimalRelationshipKind.Avoid,
			( ThornsWildlifeSpeciesKind.Panther, ThornsWildlifeSpeciesKind.Moose ) => ThornsAnimalRelationshipKind.Avoid,
			( ThornsWildlifeSpeciesKind.Panther, ThornsWildlifeSpeciesKind.Panther ) => ThornsAnimalRelationshipKind.Ignore,

			( ThornsWildlifeSpeciesKind.Elk, ThornsWildlifeSpeciesKind.Wolf ) => ThornsAnimalRelationshipKind.Fear,
			( ThornsWildlifeSpeciesKind.Elk, ThornsWildlifeSpeciesKind.Panther ) => ThornsAnimalRelationshipKind.Fear,
			( ThornsWildlifeSpeciesKind.Elk, ThornsWildlifeSpeciesKind.Moose ) => ThornsAnimalRelationshipKind.Avoid,
			( ThornsWildlifeSpeciesKind.Elk, ThornsWildlifeSpeciesKind.Elk ) => ThornsAnimalRelationshipKind.Curious,

			( ThornsWildlifeSpeciesKind.Deer, ThornsWildlifeSpeciesKind.Wolf ) => ThornsAnimalRelationshipKind.Fear,
			( ThornsWildlifeSpeciesKind.Deer, ThornsWildlifeSpeciesKind.Panther ) => ThornsAnimalRelationshipKind.Fear,
			( ThornsWildlifeSpeciesKind.Deer, ThornsWildlifeSpeciesKind.Moose ) => ThornsAnimalRelationshipKind.Avoid,

			( ThornsWildlifeSpeciesKind.Rabbit, ThornsWildlifeSpeciesKind.Wolf ) => ThornsAnimalRelationshipKind.Fear,
			( ThornsWildlifeSpeciesKind.Rabbit, ThornsWildlifeSpeciesKind.Panther ) => ThornsAnimalRelationshipKind.Fear,

			( ThornsWildlifeSpeciesKind.Moose, ThornsWildlifeSpeciesKind.Wolf ) => ThornsAnimalRelationshipKind.Defend,
			( ThornsWildlifeSpeciesKind.Moose, ThornsWildlifeSpeciesKind.Panther ) => ThornsAnimalRelationshipKind.Defend,
			( ThornsWildlifeSpeciesKind.Moose, ThornsWildlifeSpeciesKind.Elk ) => ThornsAnimalRelationshipKind.Ignore,
			( ThornsWildlifeSpeciesKind.Moose, ThornsWildlifeSpeciesKind.Deer ) => ThornsAnimalRelationshipKind.Ignore,

			_ => ResolveFallback( observer, target ),
		};
	}

	static ThornsAnimalRelationshipKind ResolveFallback(
		ThornsWildlifeSpeciesKind observer,
		ThornsWildlifeSpeciesKind target )
	{
		var obsDef = ThornsWildlifeDefinitions.Get( observer );
		var tgtDef = ThornsWildlifeDefinitions.Get( target );

		if ( obsDef.IsPredator && !tgtDef.IsPredator )
			return ThornsAnimalRelationshipKind.Hunt;

		if ( !obsDef.IsPredator && tgtDef.IsPredator )
			return ThornsAnimalRelationshipKind.Fear;

		return ThornsAnimalRelationshipKind.Ignore;
	}

	public static bool ShouldFlee( ThornsAnimalRelationshipKind rel ) =>
		rel is ThornsAnimalRelationshipKind.Fear or ThornsAnimalRelationshipKind.Avoid;

	public static bool ShouldHunt( ThornsAnimalRelationshipKind rel ) =>
		rel is ThornsAnimalRelationshipKind.Hunt or ThornsAnimalRelationshipKind.Attack;

	public static bool ShouldStandGround( ThornsAnimalRelationshipKind rel ) =>
		rel is ThornsAnimalRelationshipKind.Defend or ThornsAnimalRelationshipKind.Attack;
}
