namespace FinalOutpost;

using Sandbox.Citizen;

public enum ZombieKind
{
	Walker,
	Runner,
	Swarm,
	Brute,
	Bomber,
	Giant,
	Armored,
	Splitter
}

/// <summary>Static definition for a zombie archetype. Night scaling is applied on top of these multipliers.</summary>
public sealed class ZombieTypeDef
{
	public ZombieKind Kind { get; init; }
	public string Name { get; init; }
	public string Description { get; init; }
	public int IntroNight { get; init; }
	public float HpMult { get; init; } = 1f;
	public float SpeedMult { get; init; } = 1f;
	public float DamageMult { get; init; } = 1f;
	public float Scale { get; init; } = 1f;
	public Color Tint { get; init; }
	public float DuckLevel { get; init; }
	public float AnimSpeedMult { get; init; } = 1f;
	public CitizenAnimationHelper.MoveStyles MoveStyle { get; init; } = CitizenAnimationHelper.MoveStyles.Walk;
	/// <summary>Ignores walls as obstacles (vaults over them).</summary>
	public bool CanJumpWalls { get; init; }
	/// <summary>Ignores everything except the command post.</summary>
	public bool BeeLinesCore { get; init; }
	/// <summary>Flat bonus damage applied once when a bomber reaches the core, then it dies.</summary>
	public float CoreExplosionDamage { get; init; }
	/// <summary>Bullet damage multiplier (Armored).</summary>
	public float DamageTakenMult { get; init; } = 1f;
	/// <summary>Spawn weight once this type is unlocked.</summary>
	public int SpawnWeight { get; init; } = 10;
	/// <summary>On death, spawn this many mini-walkers (Splitter).</summary>
	public int SplitCount { get; init; }

	public bool Unlocked( int night ) => night >= IntroNight;
}

public static class ZombieCatalog
{
	public static readonly IReadOnlyList<ZombieTypeDef> All = new List<ZombieTypeDef>
	{
		new()
		{
			Kind = ZombieKind.Walker, Name = "Walker", IntroNight = 1,
			Description = "Standard infected — steady, predictable, and the backbone of every horde.",
			Tint = new Color( 0.35f, 0.98f, 0.42f ), SpawnWeight = 40
		},
		new()
		{
			Kind = ZombieKind.Runner, Name = "Runner", IntroNight = 3,
			Description = "Fast and agile; vaults perimeter walls to breach your base quickly.",
			HpMult = 0.65f, SpeedMult = 1.55f, DamageMult = 0.85f, Scale = 0.92f,
			Tint = new Color( 0.95f, 0.98f, 0.25f ),
			CanJumpWalls = true, SpawnWeight = 18,
			MoveStyle = CitizenAnimationHelper.MoveStyles.Run,
			AnimSpeedMult = 1.45f
		},
		new()
		{
			Kind = ZombieKind.Swarm, Name = "Swarm", IntroNight = 4,
			Description = "Tiny runners that flood the line — fragile alone, dangerous in numbers.",
			HpMult = 0.35f, SpeedMult = 1.35f, DamageMult = 0.6f, Scale = 0.58f,
			Tint = new Color( 0.75f, 1f, 0.35f ),
			SpawnWeight = 22,
			DuckLevel = 0.72f,
			MoveStyle = CitizenAnimationHelper.MoveStyles.Run,
			AnimSpeedMult = 1.75f
		},
		new()
		{
			Kind = ZombieKind.Brute, Name = "Brute", IntroNight = 5,
			Description = "Heavy and slow with crushing melee damage and a huge health pool.",
			HpMult = 2f, SpeedMult = 0.75f, DamageMult = 1.45f, Scale = 1.38f,
			Tint = new Color( 0.12f, 0.62f, 0.18f ),
			SpawnWeight = 14,
			DuckLevel = 0.12f, AnimSpeedMult = 0.65f
		},
		new()
		{
			Kind = ZombieKind.Bomber, Name = "Bomber", IntroNight = 7,
			Description = "Ignores walls and defenses, beelines the command post, then detonates.",
			HpMult = 0.55f, SpeedMult = 1.25f, DamageMult = 0f, Scale = 0.88f,
			Tint = new Color( 0.98f, 0.48f, 0.12f ),
			BeeLinesCore = true, CoreExplosionDamage = 85f, SpawnWeight = 10,
			MoveStyle = CitizenAnimationHelper.MoveStyles.Run,
			AnimSpeedMult = 1.15f
		},
		new()
		{
			Kind = ZombieKind.Splitter, Name = "Splitter", IntroNight = 8,
			Description = "Unstable carrier that fractures into smaller walkers when killed.",
			HpMult = 0.9f, SpeedMult = 1.05f, DamageMult = 0.95f, Scale = 1.05f,
			Tint = new Color( 0.35f, 0.92f, 0.78f ),
			SplitCount = 2, SpawnWeight = 12
		},
		new()
		{
			Kind = ZombieKind.Giant, Name = "Giant", IntroNight = 10,
			Description = "Colossal tank that vaults walls and soaks enormous damage.",
			HpMult = 5f, SpeedMult = 0.55f, DamageMult = 1.8f, Scale = 1.95f,
			Tint = new Color( 0.08f, 0.42f, 0.12f ),
			CanJumpWalls = true, SpawnWeight = 8,
			AnimSpeedMult = 0.5f
		},
		new()
		{
			Kind = ZombieKind.Armored, Name = "Armored", IntroNight = 12,
			Description = "Plated hide shrugs off bullets — bring sustained fire or heavy hits.",
			HpMult = 1.5f, SpeedMult = 0.85f, DamageMult = 1.1f, Scale = 1.22f,
			Tint = new Color( 0.42f, 0.48f, 0.55f ),
			DamageTakenMult = 0.5f, SpawnWeight = 12,
			AnimSpeedMult = 0.78f
		}
	};

	public static ZombieTypeDef Get( ZombieKind kind ) => All.First( t => t.Kind == kind );

	public static ZombieTypeDef PickForNight( int night )
	{
		var pool = new List<ZombieTypeDef>();
		var weight = 0;

		foreach ( var def in All )
		{
			if ( !def.Unlocked( night ) ) continue;
			pool.Add( def );
			weight += def.SpawnWeight;
		}

		if ( pool.Count == 0 )
			return Get( ZombieKind.Walker );

		var roll = Game.Random.Int( 0, weight - 1 );
		foreach ( var def in pool )
		{
			roll -= def.SpawnWeight;
			if ( roll < 0 )
				return def;
		}

		return pool[^1];
	}
}
