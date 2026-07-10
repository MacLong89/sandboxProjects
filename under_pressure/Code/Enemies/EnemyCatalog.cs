namespace UnderPressure;

/// <summary>The pests that harass a job. Grouped into on-brand families, all defeated with
/// the same cleaning tools rather than weapons.</summary>
public enum EnemyKind
{
	// Neighborhood nuisances
	Pigeon,
	Wasp,
	Rat,
	Raccoon,
	// Site hazards
	OilLeech,
	// Rival crew
	StickerBandit,
	RivalWasher,
	// Boss
	StrayDog,
	// Hitman contracts
	ContractTarget,
	ContractBodyguard,
}

/// <summary>Flavor grouping used for HUD text and design intent.</summary>
public enum EnemyFamily
{
	Grime,
	Nuisance,
	Hazard,
	Rival,
	Boss,
	Contract,
}

/// <summary>How a pest moves around its spawn point.</summary>
public enum MoveStyle
{
	Static,
	Ground,
	Hover,
}

/// <summary>How a pest harasses the player once attacks are unlocked.</summary>
public enum AttackStyle
{
	None,
	/// <summary>Wasps — drains stamina/water when they get close.</summary>
	Sting,
	/// <summary>Pigeons — swoops in and splats grime near the player.</summary>
	Dive,
	/// <summary>Rivals — pickpockets cash on contact.</summary>
	Rob,
	/// <summary>Rival crew — blasts you with their own pressure washer.</summary>
	SprayFight,
}

/// <summary>Which visual pipeline a pest uses: citizen humanoid or real animal model.</summary>
public enum PestVisualKind
{
	/// <summary>Real-world animal — uses <see cref="EnemyDef.ModelPath"/> when the asset exists.</summary>
	Animal,
	/// <summary>Person — Facepunch citizen with box fallback.</summary>
	Humanoid,
}

/// <summary>Static description of a pest: how tough it is, what tool defeats it, how it
/// moves, and how aggressively it re-soils cleaned cells.</summary>
public sealed class EnemyDef
{
	public EnemyKind Kind { get; init; }
	public EnemyFamily Family { get; init; }
	public string Name { get; init; }

	/// <summary>Total scrub "soak" it absorbs before it's defeated (tool power × seconds).</summary>
	public float Health { get; init; } = 3f;

	/// <summary>The tool that actually damages this pest. Using the wrong tool does nothing.</summary>
	public ToolType DamagedBy { get; init; } = ToolType.PressureWasher;

	public MoveStyle Move { get; init; } = MoveStyle.Ground;
	public float MoveRadius { get; init; } = 120f;
	public float MoveSpeed { get; init; } = 60f;

	/// <summary>Resting height above the spawn point (for hovering/perching pests).</summary>
	public float HoverHeight { get; init; }

	/// <summary>Overall visual scale.</summary>
	public float Scale { get; init; } = 1f;

	/// <summary>Cash awarded on defeat (before earnings multipliers).</summary>
	public double Bounty { get; init; } = 8;

	// --- Re-soiling: undoing the player's work ---
	/// <summary>Seconds between re-soil pulses. 0 disables re-soiling.</summary>
	public float ResoilPeriod { get; init; }
	public float ResoilRadius { get; init; } = 34f;
	public float ResoilAmount { get; init; } = 0.6f;

	/// <summary>Seconds before a defeated pest returns. 0 means it's gone for good.</summary>
	public float RespawnDelay { get; init; }

	// --- Player harassment (active from <see cref="GameConstants.PestAttackUnlockJob"/> onward) ---
	public AttackStyle Attack { get; init; }
	/// <summary>Seconds between attacks while the player is in range.</summary>
	public float AttackPeriod { get; init; }
	/// <summary>How close the player must be before attacks fire.</summary>
	public float AttackRange { get; init; } = 110f;
	/// <summary>How far out the pest will chase the player (0 = wander only).</summary>
	public float ChaseRange { get; init; }
	/// <summary>Attack potency: sting/dive drain, robbery $, spray-fight knockback force.</summary>
	public float AttackStrength { get; init; }

	public Color Body { get; init; } = Color.White;
	public Color Accent { get; init; } = Color.White;

	/// <summary>Humanoid vs animal model pipeline.</summary>
	public PestVisualKind VisualKind { get; init; } = PestVisualKind.Animal;

	/// <summary>Optional drop-in model for <see cref="PestVisualKind.Animal"/> pests (see <see cref="PestModels"/>).</summary>
	public string ModelPath { get; init; }
}

/// <summary>One pest placed in a job at a world position.</summary>
public sealed class EnemySpawnDef
{
	public EnemyKind Kind { get; init; }
	public Vector3 Position { get; init; }
}

public static class EnemyCatalog
{
	public static readonly IReadOnlyList<EnemyDef> All = new List<EnemyDef>
	{
		new()
		{
			Kind = EnemyKind.Pigeon, Family = EnemyFamily.Nuisance, Name = "Pigeon",
			VisualKind = PestVisualKind.Animal, ModelPath = PestModels.Pigeon,
			Health = 2f, DamagedBy = ToolType.PressureWasher,
			Move = MoveStyle.Hover, MoveRadius = 60f, MoveSpeed = 40f, HoverHeight = 40f, Scale = 0.8f,
			Bounty = 8, ResoilPeriod = 4f, ResoilRadius = 30f, ResoilAmount = 1f,
			Attack = AttackStyle.Dive, AttackPeriod = 5f, AttackRange = 200f, ChaseRange = 280f, AttackStrength = 14f,
			Body = new Color( 0.62f, 0.64f, 0.68f ), Accent = new Color( 0.98f, 0.42f, 0.08f ),
		},
		new()
		{
			Kind = EnemyKind.Wasp, Family = EnemyFamily.Nuisance, Name = "Wasp",
			VisualKind = PestVisualKind.Animal, ModelPath = PestModels.Wasp,
			Health = 2.5f, DamagedBy = ToolType.PressureWasher,
			Move = MoveStyle.Hover, MoveRadius = 130f, MoveSpeed = 130f, HoverHeight = 70f, Scale = 0.6f,
			Bounty = 14, ResoilPeriod = 0f,
			Attack = AttackStyle.Sting, AttackPeriod = 3.5f, AttackRange = 130f, ChaseRange = 220f, AttackStrength = 22f,
			Body = new Color( 1f, 0.72f, 0.05f ), Accent = new Color( 0.08f, 0.08f, 0.10f ),
		},
		new()
		{
			Kind = EnemyKind.Rat, Family = EnemyFamily.Nuisance, Name = "Rat",
			VisualKind = PestVisualKind.Animal, ModelPath = PestModels.Rat,
			Health = 3f, DamagedBy = ToolType.PressureWasher,
			Move = MoveStyle.Ground, MoveRadius = 170f, MoveSpeed = 78f, Scale = 0.85f,
			Bounty = 12, ResoilPeriod = 3f, ResoilRadius = 34f, ResoilAmount = 0.8f,
			Body = new Color( 0.42f, 0.38f, 0.34f ), Accent = new Color( 0.62f, 0.52f, 0.44f ),
		},
		new()
		{
			Kind = EnemyKind.Raccoon, Family = EnemyFamily.Nuisance, Name = "Raccoon",
			VisualKind = PestVisualKind.Animal, ModelPath = PestModels.Raccoon,
			Health = 4f, DamagedBy = ToolType.ScrubBrush,
			Move = MoveStyle.Ground, MoveRadius = 150f, MoveSpeed = 34f, Scale = 1f,
			Bounty = 10, ResoilPeriod = 3.5f, ResoilRadius = 40f, ResoilAmount = 0.7f,
			Body = new Color( 0.42f, 0.44f, 0.48f ), Accent = new Color( 0.12f, 0.12f, 0.14f ),
		},
		new()
		{
			Kind = EnemyKind.OilLeech, Family = EnemyFamily.Hazard, Name = "Leech",
			VisualKind = PestVisualKind.Animal, ModelPath = PestModels.Leech,
			Health = 3f, DamagedBy = ToolType.PressureWasher,
			Move = MoveStyle.Ground, MoveRadius = 140f, MoveSpeed = 26f, Scale = 1f,
			Bounty = 11, ResoilPeriod = 2.6f, ResoilRadius = 46f, ResoilAmount = 0.9f,
			Body = new Color( 0.12f, 0.11f, 0.14f ), Accent = new Color( 0.48f, 0.38f, 0.62f ),
		},
		new()
		{
			Kind = EnemyKind.StickerBandit, Family = EnemyFamily.Rival, Name = "Sticker Bandit",
			VisualKind = PestVisualKind.Humanoid,
			Health = 3.5f, DamagedBy = ToolType.PressureWasher,
			Move = MoveStyle.Ground, MoveRadius = 190f, MoveSpeed = 105f, Scale = 1f,
			Bounty = 18, ResoilPeriod = 3.2f, ResoilRadius = 30f, ResoilAmount = 1f,
			Attack = AttackStyle.Rob, AttackPeriod = 5.5f, AttackRange = 95f, ChaseRange = 240f, AttackStrength = 28,
			Body = new Color( 0.2f, 0.24f, 0.3f ), Accent = new Color( 1f, 0.12f, 0.32f ),
		},
		new()
		{
			Kind = EnemyKind.RivalWasher, Family = EnemyFamily.Rival, Name = "Rival Washer",
			VisualKind = PestVisualKind.Humanoid,
			Health = 6f, DamagedBy = ToolType.PressureWasher,
			Move = MoveStyle.Ground, MoveRadius = 220f, MoveSpeed = 88f, Scale = 1.05f,
			Bounty = 32, ResoilPeriod = 2.8f, ResoilRadius = 36f, ResoilAmount = 0.85f,
			Attack = AttackStyle.SprayFight, AttackPeriod = 2.4f, AttackRange = 90f, ChaseRange = 300f, AttackStrength = 140f,
			Body = new Color( 0.88f, 0.18f, 0.12f ), Accent = new Color( 0.12f, 0.14f, 0.18f ),
		},
		new()
		{
			Kind = EnemyKind.StrayDog, Family = EnemyFamily.Boss, Name = "Stray Dog",
			VisualKind = PestVisualKind.Animal, ModelPath = PestModels.StrayDog,
			Health = 40f, DamagedBy = ToolType.PressureWasher,
			Move = MoveStyle.Ground, MoveRadius = 120f, MoveSpeed = 48f, Scale = 1.6f,
			Bounty = 250, ResoilPeriod = 2f, ResoilRadius = 80f, ResoilAmount = 0.8f, RespawnDelay = 0f,
			Attack = AttackStyle.Dive, AttackPeriod = 4f, AttackRange = 180f, ChaseRange = 260f, AttackStrength = 20f,
			Body = new Color( 0.48f, 0.36f, 0.22f ), Accent = new Color( 0.22f, 0.18f, 0.14f ),
		},
		new()
		{
			Kind = EnemyKind.ContractTarget, Family = EnemyFamily.Contract, Name = "The Mark",
			VisualKind = PestVisualKind.Humanoid,
			Health = 8f, DamagedBy = ToolType.Gun,
			Move = MoveStyle.Ground, MoveRadius = 160f, MoveSpeed = 52f, Scale = 1f,
			Bounty = 1200, ResoilPeriod = 0f, RespawnDelay = 0f,
			Attack = AttackStyle.None,
			Body = new Color( 0.28f, 0.30f, 0.36f ), Accent = new Color( 0.82f, 0.72f, 0.48f ),
		},
		new()
		{
			Kind = EnemyKind.ContractBodyguard, Family = EnemyFamily.Contract, Name = "Bodyguard",
			VisualKind = PestVisualKind.Humanoid,
			Health = 14f, DamagedBy = ToolType.Gun,
			Move = MoveStyle.Ground, MoveRadius = 140f, MoveSpeed = 72f, Scale = 1.05f,
			Bounty = 350, ResoilPeriod = 0f, RespawnDelay = 0f,
			Attack = AttackStyle.Sting, AttackPeriod = 3.2f, AttackRange = 110f, ChaseRange = 280f, AttackStrength = 18f,
			Body = new Color( 0.14f, 0.16f, 0.20f ), Accent = new Color( 0.72f, 0.12f, 0.12f ),
		},
	};

	public static EnemyDef Get( EnemyKind kind ) => All.First( e => e.Kind == kind );
}
