namespace Fauna2;

/// <summary>Biomes a habitat can provide and an animal can prefer.</summary>
public enum Biome
{
	Forest,
	Rainforest,
	Grassland,
	Desert,
	Arctic,
	Swamp,
	Alpine,
	Coastal,
}

public enum AnimalRarity
{
	Common,
	Uncommon,
	Rare,
	Exotic,
	Legendary,
}

public enum AnimalLocomotion
{
	Walker,
	Grazer,
	Predator,
	Hopper,
	Bird,
	Climber,
	Heavy,
	Swimmer,
	Marine,
}

/// <summary>
/// Data-driven animal species definition. Create one .animal resource per
/// species — no code changes needed to add new animals.
/// </summary>
[AssetType( Name = "Fauna Animal", Extension = "animal", Category = "Fauna" )]
public sealed class AnimalDefinition : GameResource
{
	[Property] public string DisplayName { get; set; } = "Animal";
	[Property] public string Species { get; set; } = "animal";
	[Property, TextArea] public string Description { get; set; } = "";

	[Property] public Biome Biome { get; set; } = Biome.Grassland;
	[Property] public HabitatSizeTier MinHabitatSize { get; set; } = HabitatSizeTier.Small;
	[Property] public AnimalRarity Rarity { get; set; } = AnimalRarity.Common;

	// ── Acquisition ─────────────────────────────────────────
	[Property] public int Cost { get; set; } = 500;
	[Property] public int UnlockLevel { get; set; } = 1;
	[Property] public int RequiredPrestige { get; set; } = 0;
	[Property, Range( 0, 1 )] public float CatchDifficulty { get; set; } = 0.3f;
	[Property] public bool RequiresTranquilizer { get; set; }

	// Wild danger. Zero aggression means this species never initiates attacks.
	[Property, Range( 0, 1 )] public float WildAggression { get; set; }
	[Property, Range( 0, 1 )] public float WildAttackDifficulty { get; set; }
	[Property, Range( 0, 1 )] public float WildAttackPenaltyFraction { get; set; }

	// ── Guests ──────────────────────────────────────────────
	[Property] public float GuestAppeal { get; set; } = 5f;

	// ── Needs ───────────────────────────────────────────────
	/// <summary>Habitat area (units²) this animal wants for itself.</summary>
	[Property] public float SpaceNeed { get; set; } = 40_000f;
	/// <summary>Happiness required before this species considers breeding.</summary>
	[Property] public float BreedingHappiness { get; set; } = 70f;
	/// <summary>0 = trivially easy, 1 = nearly impossible.</summary>
	[Property, Range( 0, 1 )] public float BreedingDifficulty { get; set; } = 0.3f;
	/// <summary>Animals of the same species nearby make this one happier.</summary>
	[Property] public bool IsSocial { get; set; } = true;

	// ── Lifecycle (seconds, tuned for session pacing) ───────
	[Property] public float AdultAge { get; set; } = 180f;
	[Property] public float ElderAge { get; set; } = 3600f;

	// ── Presentation ────────────────────────────────────────
	[Property] public float MoveSpeed { get; set; } = 60f;
	[Property] public AnimalLocomotion Locomotion { get; set; } = AnimalLocomotion.Walker;
	/// <summary>Typical adult size in meters (shoulder height; body length for low-profile reptiles). Drives sprite scale vs the player.</summary>
	[Property] public float RealWorldHeightMeters { get; set; }
	[Property] public Vector3 BodyScale { get; set; } = new Vector3( 0.8f, 0.5f, 0.5f );
	[Property] public Color BodyTint { get; set; } = Color.White;

	/// <summary>Resale/derived value of one animal of this species.</summary>
	public int BaseValue => Cost;
}
