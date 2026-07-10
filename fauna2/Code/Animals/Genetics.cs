namespace Fauna2;

/// <summary>
/// An individual animal's heritable data. Designed to grow: stat modifiers and
/// traits are open-ended string-keyed data so future genetics (recessive genes,
/// hybrids, mutations) can extend it without breaking saves.
/// </summary>
public sealed class AnimalGenome
{
	/// <summary>Multipliers applied to base stats, e.g. "appeal" → 1.08.</summary>
	public Dictionary<string, float> StatModifiers { get; set; } = new();

	/// <summary>Open-ended trait tags, e.g. "fertile", "longlived".</summary>
	public List<string> Traits { get; set; } = new();

	/// <summary>Hidden inherited gene tags. Matching recessives can express as variants/traits later.</summary>
	public List<string> Recessives { get; set; } = new();

	/// <summary>Variant resource name, empty for standard animals.</summary>
	public string VariantId { get; set; } = "";

	/// <summary>How many generations of captive breeding led here.</summary>
	public int Generation { get; set; }

	public string Personality { get; set; } = "";
	public string ParentAId { get; set; } = "";
	public string ParentBId { get; set; } = "";
	public string ParentAName { get; set; } = "";
	public string ParentBName { get; set; } = "";
	public string BloodlineName { get; set; } = "";

	public float Stat( string key, float fallback = 1f ) =>
		StatModifiers.TryGetValue( key, out var v ) ? v : fallback;

	public bool HasTrait( string trait ) => Traits.Contains( trait );
}

/// <summary>
/// Pluggable genetics service. Swap the implementation to change how offspring
/// inherit stats, roll variants or produce hybrids.
/// </summary>
public interface IGeneticsService
{
	AnimalGenome CreateWild( AnimalDefinition def );
	AnimalGenome Breed( AnimalComponent parentA, AnimalComponent parentB );

	/// <summary>
	/// Extension point: given two (possibly different) species, return the
	/// species the offspring should be. Default returns parent A's species.
	/// Hybrids/cryptids/mythicals plug in here.
	/// </summary>
	AnimalDefinition ResolveOffspringSpecies( AnimalDefinition a, AnimalDefinition b );
}

/// <summary>Default genetics: small stat variation, weighted variant rolls.</summary>
public sealed class StandardGenetics : IGeneticsService
{
	public static readonly string[] HeritableStats = { "appeal", "fertility", "resilience" };
	private static readonly string[] PersonalityPool = { "Gentle", "Bold", "Curious", "Playful", "Shy", "Watchful", "Mischievous", "Calm" };
	private static readonly string[] RecessivePool = { "albino", "golden", "melanistic", "sapphire", "arctic", "spotted", "longlived", "fertile" };

	public AnimalGenome CreateWild( AnimalDefinition def )
	{
		var genome = new AnimalGenome();

		foreach ( var stat in HeritableStats )
			genome.StatModifiers[stat] = Game.Random.Float( 0.92f, 1.08f );

		genome.Personality = PersonalityPool[Game.Random.Int( 0, PersonalityPool.Length - 1 )];
		genome.BloodlineName = $"{def.DisplayName} Line";
		genome.Recessives.Add( RecessivePool[Game.Random.Int( 0, RecessivePool.Length - 1 )] );
		if ( Game.Random.Float() < 0.25f )
			genome.Traits.Add( genome.Personality.ToLowerInvariant() );

		genome.VariantId = RollVariant( def, GameConstants.VariantChanceWild );
		return genome;
	}

	public AnimalGenome Breed( AnimalComponent parentA, AnimalComponent parentB )
	{
		var def = parentA.Definition;
		var genome = new AnimalGenome
		{
			Generation = Math.Max( parentA.Genome.Generation, parentB.Genome.Generation ) + 1,
			ParentAId = parentA.AnimalId,
			ParentBId = parentB.AnimalId,
			ParentAName = parentA.AnimalName,
			ParentBName = parentB.AnimalName,
			BloodlineName = !string.IsNullOrEmpty( parentA.Genome.BloodlineName )
				? parentA.Genome.BloodlineName
				: $"{parentA.Definition?.DisplayName ?? "Sanctuary"} Line",
			Personality = Game.Random.Float() < 0.5f ? parentA.Genome.Personality : parentB.Genome.Personality,
		};

		// Inherit: average of parents plus mutation wiggle.
		foreach ( var stat in HeritableStats )
		{
			var inherited = (parentA.Genome.Stat( stat ) + parentB.Genome.Stat( stat )) * 0.5f;
			genome.StatModifiers[stat] = (inherited * Game.Random.Float( 0.95f, 1.07f )).Clamp( 0.75f, 1.5f );
		}

		// Traits carry over with a 50% chance each.
		foreach ( var trait in parentA.Genome.Traits.Concat( parentB.Genome.Traits ).Distinct() )
		{
			if ( Game.Random.Float() < 0.5f )
				genome.Traits.Add( trait );
		}

		foreach ( var gene in parentA.Genome.Recessives.Concat( parentB.Genome.Recessives ).Distinct() )
		{
			var copies = 0;
			if ( parentA.Genome.Recessives.Contains( gene ) ) copies++;
			if ( parentB.Genome.Recessives.Contains( gene ) ) copies++;
			if ( copies >= 2 || Game.Random.Float() < 0.35f )
				genome.Recessives.Add( gene );
		}

		if ( Game.Random.Float() < 0.16f )
			genome.Recessives.Add( RecessivePool[Game.Random.Int( 0, RecessivePool.Length - 1 )] );

		// Variant inheritance: a variant parent strongly boosts odds.
		var chance = GameConstants.VariantChanceBred;
		chance *= 1f + (ZooState.Instance?.Prestige ?? 0) / 200f;        // prestige breeding bonus
		if ( !string.IsNullOrEmpty( parentA.VariantId ) || !string.IsNullOrEmpty( parentB.VariantId ) )
			chance *= 4f;

		genome.VariantId = RollVariant( def, chance );
		genome.VariantId = ExpressRecessiveVariant( def, genome ) ?? genome.VariantId;

		// Direct variant inheritance beats a fresh roll half the time.
		var parentVariant = !string.IsNullOrEmpty( parentA.VariantId ) ? parentA.VariantId : parentB.VariantId;
		if ( string.IsNullOrEmpty( genome.VariantId ) && !string.IsNullOrEmpty( parentVariant ) && Game.Random.Float() < 0.25f )
			genome.VariantId = parentVariant;

		return genome;
	}

	public AnimalDefinition ResolveOffspringSpecies( AnimalDefinition a, AnimalDefinition b )
	{
		if ( a == b ) return a;

		var hybrid = HybridSystem.Resolve( a, b );
		return hybrid;
	}

	private static string RollVariant( AnimalDefinition def, float chance )
	{
		if ( Game.Random.Float() >= chance ) return "";

		var candidates = Defs.Variants.Where( v => v.AppliesTo( def ) ).ToList();
		if ( candidates.Count == 0 ) return "";

		var total = candidates.Sum( v => v.RarityWeight );
		var roll = Game.Random.Float( 0, total );

		foreach ( var v in candidates )
		{
			roll -= v.RarityWeight;
			if ( roll <= 0f ) return v.ResourceName;
		}

		return candidates[^1].ResourceName;
	}

	private static string ExpressRecessiveVariant( AnimalDefinition def, AnimalGenome genome )
	{
		if ( Game.Random.Float() >= GameConstants.RecessiveExpressionChance + (FranchiseSystem.Instance?.LegacyBreedingBonus ?? 0f) )
			return null;

		foreach ( var gene in genome.Recessives )
		{
			var match = Defs.Variants.FirstOrDefault( v =>
				Defs.ResourceStem( v.ResourceName ).Equals( gene, StringComparison.OrdinalIgnoreCase )
				&& v.AppliesTo( def ) );
			if ( match is not null )
			{
				if ( !genome.Traits.Contains( $"recessive:{gene}" ) )
					genome.Traits.Add( $"recessive:{gene}" );
				return match.ResourceName;
			}
		}

		return null;
	}
}
