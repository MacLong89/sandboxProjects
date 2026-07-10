namespace Terraingen.Animals;

/// <summary>Data-driven species tuning — register via <see cref="ThornsAnimalSpeciesRegistry"/>.</summary>
public sealed class ThornsAnimalSpeciesData
{
	public ushort SpeciesId { get; set; }
	public string Key { get; set; } = "";
	public string DisplayName { get; set; } = "";
	public ThornsAnimalBehaviorType BehaviorType { get; set; }

	public string ModelPath { get; set; } = "";
	public string AnimPrefix { get; set; } = "";

	public float BaseHealth { get; set; } = 50f;
	public float BaseDamage { get; set; }
	/// <summary>Base move speed roll; wander uses half this, running actions use <see cref="SprintSpeedMultiplier"/>.</summary>
	public float BaseSpeed { get; set; } = 300f;

	/// <summary>Applied to <see cref="BaseSpeed"/> during chase, flee, and tamed follow (not mounted).</summary>
	public float SprintSpeedMultiplier { get; set; } = 1.85f;

	/// <summary>Seconds to ramp from standstill to full sprint speed (chase/flee/sprint-follow).</summary>
	public float SprintAccelSeconds { get; set; } = 1f;

	/// <summary>Seconds to bleed from full sprint down to stop or slow wander speed.</summary>
	public float SprintDecelSeconds { get; set; } = 0.9f;

	public float DetectionRange { get; set; } = 1200f;
	public float AttackRange { get; set; } = 80f;
	public float AttackCooldown { get; set; } = 1.5f;
	public float DetectionInterval { get; set; } = 0.75f;

	public float MaxChaseDistance { get; set; } = 4000f;
	public float FleeSafeDistance { get; set; }
	public float WanderRadius { get; set; } = 600f;
	public float WanderIntervalMin { get; set; } = 3f;
	public float WanderIntervalMax { get; set; } = 6f;
	public float ChaseDestinationInterval { get; set; } = 0.75f;
	public float IdlePauseMin { get; set; } = 0.5f;
	public float IdlePauseMax { get; set; } = 1.5f;
	public float MaxWanderSlopeDegrees { get; set; } = 35f;

	/// <summary>Companion tier for UI (1 = common prey … 4 = apex).</summary>
	public int TameTier { get; set; } = 1;

	public ThornsAnimalSocialMode SocialMode { get; set; } = ThornsAnimalSocialMode.Solitary;
	public int GroupSpawnCountMin { get; set; } = 1;
	public int GroupSpawnCountMax { get; set; } = 1;
	public float GroupSpawnRadius { get; set; } = 250f;
	public float PackHuntJoinRadius { get; set; } = 1800f;

	public bool SpawnsInGroups => SocialMode is ThornsAnimalSocialMode.Herd or ThornsAnimalSocialMode.Pack;
	public bool HuntsInGroups => SocialMode == ThornsAnimalSocialMode.Pack;

	public bool IgnorePlayers { get; set; }
	public bool AttackPlayers { get; set; }

	/// <summary>Chance to fight a detected player (0 = always flee, 1 = always attack). Unset (-1) uses behavior-type defaults.</summary>
	public float PlayerFightChance { get; set; } = -1f;

	public ushort[] PreyTargetIds { get; set; } = Array.Empty<ushort>();
	public ushort[] ThreatSpeciesIds { get; set; } = Array.Empty<ushort>();
	public ushort[] CanAttackSpeciesIds { get; set; } = Array.Empty<ushort>();

	public float FleeSafeDistanceOrDefault => FleeSafeDistance > 0f ? FleeSafeDistance : DetectionRange * 1.5f;

	/// <summary>Planar acceleration toward sprint/wander targets (in/s²).</summary>
	public float ResolveSprintAcceleration( float maxSprintSpeedInchesPerSec ) =>
		maxSprintSpeedInchesPerSec / MathF.Max( SprintAccelSeconds, 0.05f );

	/// <summary>Planar deceleration when slowing or stopping (in/s²).</summary>
	public float ResolveSprintDeceleration( float maxSprintSpeedInchesPerSec ) =>
		maxSprintSpeedInchesPerSec / MathF.Max( SprintDecelSeconds, 0.05f );

	public float ResolvePlayerFightChance() =>
		PlayerFightChance >= 0f ? PlayerFightChance : BehaviorType switch
		{
			ThornsAnimalBehaviorType.Prey => 0f,
			ThornsAnimalBehaviorType.Predator => 1f,
			ThornsAnimalBehaviorType.Mixed => 0.5f,
			_ => 0f,
		};

	public bool IsPreyTarget( ushort speciesId )
	{
		if ( PreyTargetIds is null )
			return false;

		for ( var i = 0; i < PreyTargetIds.Length; i++ )
			if ( PreyTargetIds[i] == speciesId )
				return true;
		return false;
	}

	public bool IsThreatSpecies( ushort speciesId )
	{
		if ( ThreatSpeciesIds is null )
			return false;

		for ( var i = 0; i < ThreatSpeciesIds.Length; i++ )
			if ( ThreatSpeciesIds[i] == speciesId )
				return true;
		return false;
	}

	public bool CanAttackSpecies( ushort speciesId )
	{
		if ( CanAttackSpeciesIds is null )
			return false;

		for ( var i = 0; i < CanAttackSpeciesIds.Length; i++ )
			if ( CanAttackSpeciesIds[i] == speciesId )
				return true;
		return false;
	}
}
