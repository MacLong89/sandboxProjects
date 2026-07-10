namespace Terraingen.Animals;

/// <summary>
/// Built-in species using models under Assets/models/.
/// Combat/movement stats are scaled relative to real-world white-tailed deer (~100 kg, ~30 mph sprint)
/// so each species feels like its real counterpart in relation to the others.
/// </summary>
public static class ThornsAnimalSpeciesCatalog
{
	public const ushort WolfId = 1;
	public const ushort PantherId = 2;
	public const ushort DeerId = 3;
	public const ushort MooseId = 5;

	public static void RegisterAll( Action<ThornsAnimalSpeciesData> register )
	{
		register( CreateWolf() );
		register( CreatePanther() );
		register( CreateDeer() );
		register( CreateMoose() );
	}

	// Gray wolf — ~45 kg, ~37 mph sprint, endurance pack hunter, strong bite.
	static ThornsAnimalSpeciesData CreateWolf()
	{
		var s = new ThornsAnimalSpeciesData();
		s.SpeciesId = WolfId;
		s.Key = "wolf";
		s.DisplayName = "Wolf";
		s.BehaviorType = ThornsAnimalBehaviorType.Predator;
		s.SocialMode = ThornsAnimalSocialMode.Pack;
		s.GroupSpawnCountMin = 3;
		s.GroupSpawnCountMax = 4;
		s.GroupSpawnRadius = 180f;
		s.PackHuntJoinRadius = 2000f;
		s.ModelPath = "models/wolf/wolf.vmdl";
		s.AnimPrefix = "wolf";
		s.BaseHealth = 55f;
		s.BaseDamage = 13f;
		s.BaseSpeed = 372f;
		// ~1.05× player sprint (560 in/s) at default animal_speed_multiplier 1.3: 372 × 1.22 × 1.3 ≈ 590.
		s.SprintSpeedMultiplier = 1.22f;
		s.SprintAccelSeconds = 0.95f;
		s.SprintDecelSeconds = 0.85f;
		s.TameTier = 2;
		s.DetectionRange = 2775f;
		s.DetectionInterval = 0.65f;
		s.AttackRange = 88f;
		s.AttackCooldown = 1.35f;
		s.MaxChaseDistance = 5200f;
		s.WanderRadius = 750f;
		s.IgnorePlayers = false;
		s.AttackPlayers = true;
		s.PlayerFightChance = 0.5f;
		s.PreyTargetIds = new[] { DeerId, MooseId, PantherId };
		s.CanAttackSpeciesIds = new[] { DeerId, MooseId, PantherId };
		return s;
	}

	// Cougar / mountain lion — ~70 kg, ~50 mph burst (fastest here), ambush bite killer.
	static ThornsAnimalSpeciesData CreatePanther()
	{
		var s = new ThornsAnimalSpeciesData();
		s.SpeciesId = PantherId;
		s.Key = "panther";
		s.DisplayName = "Panther";
		s.BehaviorType = ThornsAnimalBehaviorType.Predator;
		s.SocialMode = ThornsAnimalSocialMode.Solitary;
		s.ModelPath = "models/panther/panther.vmdl";
		s.AnimPrefix = "panther";
		s.BaseHealth = 68f;
		s.BaseDamage = 17f;
		s.BaseSpeed = 450f;
		// ~1.10× player sprint (560 in/s) at default animal_speed_multiplier 1.3: 450 × 1.05 × 1.3 ≈ 614.
		s.SprintSpeedMultiplier = 1.05f;
		s.SprintAccelSeconds = 0.45f;
		s.SprintDecelSeconds = 0.72f;
		s.TameTier = 3;
		s.DetectionRange = 2025f;
		s.DetectionInterval = 0.7f;
		s.AttackRange = 102f;
		s.AttackCooldown = 1.15f;
		s.MaxChaseDistance = 4200f;
		s.WanderRadius = 550f;
		s.IgnorePlayers = false;
		s.AttackPlayers = true;
		s.PlayerFightChance = 0.7f;
		s.PreyTargetIds = new[] { DeerId, MooseId, WolfId };
		s.CanAttackSpeciesIds = new[] { DeerId, MooseId, WolfId };
		return s;
	}

	// White-tailed deer — ~100 kg prey, ~30 mph sprint, fragile but alert and fast to flee.
	static ThornsAnimalSpeciesData CreateDeer()
	{
		var s = new ThornsAnimalSpeciesData();
		s.SpeciesId = DeerId;
		s.Key = "deer";
		s.DisplayName = "Deer";
		s.BehaviorType = ThornsAnimalBehaviorType.Prey;
		s.SocialMode = ThornsAnimalSocialMode.Herd;
		s.GroupSpawnCountMin = 8;
		s.GroupSpawnCountMax = 9;
		s.GroupSpawnRadius = 320f;
		s.ModelPath = "models/deer/deer.vmdl";
		s.AnimPrefix = "deer";
		s.BaseHealth = 45f;
		s.BaseDamage = 0f;
		s.BaseSpeed = 268f;
		s.SprintSpeedMultiplier = 2.1f;
		s.SprintAccelSeconds = 0.62f;
		s.SprintDecelSeconds = 1.05f;
		s.TameTier = 1;
		s.DetectionRange = 350f;
		s.DetectionInterval = 0.5f;
		s.AttackRange = 0f;
		s.FleeSafeDistance = 420f;
		s.WanderRadius = 650f;
		s.IgnorePlayers = false;
		s.PlayerFightChance = 0f;
		s.ThreatSpeciesIds = new[] { WolfId, PantherId };
		return s;
	}

	// Moose — ~500 kg, heavy but can hit ~35 mph; slow cruise, devastating charge, poor eyesight.
	static ThornsAnimalSpeciesData CreateMoose()
	{
		var s = new ThornsAnimalSpeciesData();
		s.SpeciesId = MooseId;
		s.Key = "moose";
		s.DisplayName = "Moose";
		s.BehaviorType = ThornsAnimalBehaviorType.Mixed;
		s.SocialMode = ThornsAnimalSocialMode.Solitary;
		s.ModelPath = "models/moose/moose.vmdl";
		s.AnimPrefix = "moose";
		s.BaseHealth = 145f;
		s.BaseDamage = 24f;
		s.BaseSpeed = 224f;
		s.SprintSpeedMultiplier = 1.8f;
		s.SprintAccelSeconds = 1.55f;
		s.SprintDecelSeconds = 1.35f;
		s.TameTier = 4;
		s.DetectionRange = 220f;
		s.DetectionInterval = 0.85f;
		s.AttackRange = 118f;
		s.AttackCooldown = 2.1f;
		s.MaxChaseDistance = 2800f;
		s.FleeSafeDistance = 370f;
		s.WanderRadius = 450f;
		s.IgnorePlayers = false;
		s.AttackPlayers = true;
		s.PlayerFightChance = 0.3f;
		s.ThreatSpeciesIds = new[] { WolfId, PantherId };
		s.CanAttackSpeciesIds = new[] { WolfId, PantherId };
		return s;
	}
}
