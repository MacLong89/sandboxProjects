namespace Sandbox;

/// <summary>Authorable species tuning — separate from perception/motor/combat code.</summary>
public sealed record ThornsWildlifeSpeciesDefinition(
	ThornsWildlifeSpeciesKind Kind,
	string DisplayName,
	bool IsPredator,
	float MaxHealth,
	float WanderSpeed,
	float ChaseSpeed,
	float WanderRadius,
	float LeashRadius,
	float AggroRadius,
	float LoseRadius,
	float FearRadius,
	float AttackRange,
	float MeleeDamage,
	float AttackCooldownSeconds,
	float HuntCommitSeconds,
	float IdleSecondsMin,
	float IdleSecondsMax,
	bool UseLineOfSight,
	float SenseHeightOffset,
	bool AllowPlayerMount = false,
	float MountRiderHeightUp = 56f,
	float MountRiderForward = 0f );

/// <summary>Static species table — vitals + speeds derived from <see cref="ThornsWildlifeVsPlayerBalance"/> (human-relative).</summary>
public static class ThornsWildlifeDefinitions
{
	// HP vs 100 human max; predator melee vs 10 primitive-tool reference.
	// Chase/flee (uu/s): human sprint ≈480 (walk 320 × 1.5); all species wander/chase speeds flow through ThornsWildlifeVsPlayerBalance (WildlifeLocomotionGlobalSpeedMul scales sprint-mul + tier chase constants).
	// Wander: patrol only — same scale, lower multiplier.
	// Wolf: declared after <see cref="Panther"/> — chase + wander + hunt radii mirror panther motor/perception (HP/melee stay wolf-tuned).

	public static readonly ThornsWildlifeSpeciesDefinition Deer = new(
		Kind: ThornsWildlifeSpeciesKind.Deer,
		DisplayName: "Deer",
		IsPredator: false,
		MaxHealth: ThornsWildlifeVsPlayerBalance.MaxHealthFromHumanMul( 0.95f ),
		WanderSpeed: ThornsWildlifeVsPlayerBalance.SpeedFromPlayerSprintMul( 0.377f ),
		ChaseSpeed: ThornsWildlifeVsPlayerBalance.WildlifeChaseFleeDeerMooseUnits,
		WanderRadius: 480f,
		LeashRadius: 3800f,
		AggroRadius: 0f,
		LoseRadius: 0f,
		FearRadius: 900f,
		AttackRange: 0f,
		MeleeDamage: ThornsWildlifeVsPlayerBalance.MeleeFromReferenceMul( 0f ),
		AttackCooldownSeconds: 999f,
		HuntCommitSeconds: 0f,
		IdleSecondsMin: 0.6f,
		IdleSecondsMax: 1.8f,
		UseLineOfSight: false,
		SenseHeightOffset: 52f,
		AllowPlayerMount: true,
		MountRiderHeightUp: 92f,
		MountRiderForward: 10f );

	public static readonly ThornsWildlifeSpeciesDefinition Fox = new(
		Kind: ThornsWildlifeSpeciesKind.Fox,
		DisplayName: "Fox",
		IsPredator: true,
		MaxHealth: ThornsWildlifeVsPlayerBalance.MaxHealthFromHumanMul( 0.55f ),
		WanderSpeed: ThornsWildlifeVsPlayerBalance.SpeedFromPlayerSprintMul( 0.399f ),
		ChaseSpeed: ThornsWildlifeVsPlayerBalance.SpeedFromPlayerSprintMul( 1.528f ),
		WanderRadius: 500f,
		LeashRadius: 3600f,
		AggroRadius: 700f,
		LoseRadius: 1700f,
		FearRadius: 0f,
		AttackRange: 78f,
		MeleeDamage: ThornsWildlifeVsPlayerBalance.MeleeFromReferenceMul( 0.55f ),
		AttackCooldownSeconds: 1.25f,
		HuntCommitSeconds: 6f,
		IdleSecondsMin: 0.5f,
		IdleSecondsMax: 1.4f,
		UseLineOfSight: true,
		SenseHeightOffset: 42f );

	public static readonly ThornsWildlifeSpeciesDefinition Bear = new(
		Kind: ThornsWildlifeSpeciesKind.Bear,
		DisplayName: "Bear",
		IsPredator: true,
		MaxHealth: ThornsWildlifeVsPlayerBalance.MaxHealthFromHumanMul( 5f ),
		WanderSpeed: ThornsWildlifeVsPlayerBalance.SpeedFromPlayerSprintMul( 0.269f ),
		ChaseSpeed: ThornsWildlifeVsPlayerBalance.SpeedFromPlayerSprintMul( 1.164f ),
		WanderRadius: 600f,
		LeashRadius: 4600f,
		AggroRadius: 1200f,
		LoseRadius: 2400f,
		FearRadius: 0f,
		AttackRange: 105f,
		MeleeDamage: ThornsWildlifeVsPlayerBalance.MeleeFromReferenceMul( 2.1f ),
		AttackCooldownSeconds: 1.55f,
		HuntCommitSeconds: 11f,
		IdleSecondsMin: 0.9f,
		IdleSecondsMax: 2.3f,
		UseLineOfSight: true,
		SenseHeightOffset: 64f );

	public static readonly ThornsWildlifeSpeciesDefinition Cougar = new(
		Kind: ThornsWildlifeSpeciesKind.Cougar,
		DisplayName: "Cougar",
		IsPredator: true,
		MaxHealth: ThornsWildlifeVsPlayerBalance.MaxHealthFromHumanMul( 1.02f ),
		WanderSpeed: ThornsWildlifeVsPlayerBalance.SpeedFromPlayerSprintMul( 0.377f ),
		ChaseSpeed: ThornsWildlifeVsPlayerBalance.WildlifeChaseFleeWolfPantherUnits,
		WanderRadius: 560f,
		LeashRadius: 4200f,
		AggroRadius: 1250f,
		LoseRadius: 2500f,
		FearRadius: 0f,
		AttackRange: 92f,
		MeleeDamage: ThornsWildlifeVsPlayerBalance.MeleeFromReferenceMul( 1.35f ),
		AttackCooldownSeconds: 1.05f,
		HuntCommitSeconds: 8f,
		IdleSecondsMin: 0.7f,
		IdleSecondsMax: 1.9f,
		UseLineOfSight: true,
		SenseHeightOffset: 52f );

	/// <summary>Same tuning as <see cref="Cougar"/> — skinned panther mesh + bespoke sequences.</summary>
	public static readonly ThornsWildlifeSpeciesDefinition Panther = new(
		Kind: ThornsWildlifeSpeciesKind.Panther,
		DisplayName: "Panther",
		IsPredator: true,
		MaxHealth: ThornsWildlifeVsPlayerBalance.MaxHealthFromHumanMul( 1.02f ),
		WanderSpeed: ThornsWildlifeVsPlayerBalance.SpeedFromPlayerSprintMul( 0.377f ),
		ChaseSpeed: ThornsWildlifeVsPlayerBalance.WildlifeChaseFleeWolfPantherUnits,
		WanderRadius: 560f,
		LeashRadius: 4200f,
		AggroRadius: 1250f,
		LoseRadius: 2500f,
		FearRadius: 0f,
		AttackRange: 92f,
		MeleeDamage: ThornsWildlifeVsPlayerBalance.MeleeFromReferenceMul( 1.35f ),
		AttackCooldownSeconds: 1.05f,
		HuntCommitSeconds: 8f,
		IdleSecondsMin: 0.7f,
		IdleSecondsMax: 1.9f,
		UseLineOfSight: true,
		SenseHeightOffset: 52f );

	/// <summary>
	/// Planar chase/flee speed and hunt radii match <see cref="Panther"/> (same motor wish + LOS/leash tuning). HP and bite damage stay wolf-specific.
	/// </summary>
	public static readonly ThornsWildlifeSpeciesDefinition Wolf = new(
		Kind: ThornsWildlifeSpeciesKind.Wolf,
		DisplayName: "Wolf",
		IsPredator: true,
		MaxHealth: ThornsWildlifeVsPlayerBalance.MaxHealthFromHumanMul( 1.12f ),
		WanderSpeed: Panther.WanderSpeed,
		ChaseSpeed: Panther.ChaseSpeed,
		WanderRadius: Panther.WanderRadius,
		LeashRadius: Panther.LeashRadius,
		AggroRadius: Panther.AggroRadius,
		LoseRadius: Panther.LoseRadius,
		FearRadius: 0f,
		AttackRange: Panther.AttackRange,
		MeleeDamage: ThornsWildlifeVsPlayerBalance.MeleeFromReferenceMul( 1.2f ),
		AttackCooldownSeconds: Panther.AttackCooldownSeconds,
		HuntCommitSeconds: Panther.HuntCommitSeconds,
		IdleSecondsMin: Panther.IdleSecondsMin,
		IdleSecondsMax: Panther.IdleSecondsMax,
		UseLineOfSight: Panther.UseLineOfSight,
		SenseHeightOffset: Panther.SenseHeightOffset );

	public static readonly ThornsWildlifeSpeciesDefinition Boar = new(
		Kind: ThornsWildlifeSpeciesKind.Boar,
		DisplayName: "Boar",
		IsPredator: false,
		MaxHealth: ThornsWildlifeVsPlayerBalance.MaxHealthFromHumanMul( 1.38f ),
		WanderSpeed: ThornsWildlifeVsPlayerBalance.SpeedFromPlayerSprintMul( 0.334f ),
		ChaseSpeed: ThornsWildlifeVsPlayerBalance.SpeedFromPlayerSprintMul( 1.401f ),
		WanderRadius: 500f,
		LeashRadius: 3900f,
		AggroRadius: 0f,
		LoseRadius: 0f,
		FearRadius: 780f,
		AttackRange: 0f,
		MeleeDamage: ThornsWildlifeVsPlayerBalance.MeleeFromReferenceMul( 0f ),
		AttackCooldownSeconds: 999f,
		HuntCommitSeconds: 0f,
		IdleSecondsMin: 0.8f,
		IdleSecondsMax: 2.0f,
		UseLineOfSight: false,
		SenseHeightOffset: 44f,
		AllowPlayerMount: true,
		MountRiderHeightUp: 52f,
		MountRiderForward: 6f );

	public static readonly ThornsWildlifeSpeciesDefinition Rabbit = new(
		Kind: ThornsWildlifeSpeciesKind.Rabbit,
		DisplayName: "Rabbit",
		IsPredator: false,
		MaxHealth: ThornsWildlifeVsPlayerBalance.MaxHealthFromHumanMul( 0.32f ),
		WanderSpeed: ThornsWildlifeVsPlayerBalance.SpeedFromPlayerSprintMul( 0.431f ),
		ChaseSpeed: ThornsWildlifeVsPlayerBalance.SpeedFromPlayerSprintMul( 1.983f ),
		WanderRadius: 420f,
		LeashRadius: 3200f,
		AggroRadius: 0f,
		LoseRadius: 0f,
		FearRadius: 950f,
		AttackRange: 0f,
		MeleeDamage: ThornsWildlifeVsPlayerBalance.MeleeFromReferenceMul( 0f ),
		AttackCooldownSeconds: 999f,
		HuntCommitSeconds: 0f,
		IdleSecondsMin: 0.4f,
		IdleSecondsMax: 1.2f,
		UseLineOfSight: false,
		SenseHeightOffset: 24f );

	public static readonly ThornsWildlifeSpeciesDefinition Elk = new(
		Kind: ThornsWildlifeSpeciesKind.Elk,
		DisplayName: "Elk",
		IsPredator: false,
		MaxHealth: ThornsWildlifeVsPlayerBalance.MaxHealthFromHumanMul( 2.15f ),
		WanderSpeed: ThornsWildlifeVsPlayerBalance.SpeedFromPlayerSprintMul( 0.366f ),
		ChaseSpeed: ThornsWildlifeVsPlayerBalance.WildlifeChaseFleeDeerMooseUnits,
		WanderRadius: 560f,
		LeashRadius: 4100f,
		AggroRadius: 0f,
		LoseRadius: 0f,
		FearRadius: 980f,
		AttackRange: 0f,
		MeleeDamage: ThornsWildlifeVsPlayerBalance.MeleeFromReferenceMul( 0f ),
		AttackCooldownSeconds: 999f,
		HuntCommitSeconds: 0f,
		IdleSecondsMin: 0.8f,
		IdleSecondsMax: 2.0f,
		UseLineOfSight: false,
		SenseHeightOffset: 62f,
		AllowPlayerMount: true,
		MountRiderHeightUp: 78f,
		MountRiderForward: 14f );

	public static readonly ThornsWildlifeSpeciesDefinition Moose = new(
		Kind: ThornsWildlifeSpeciesKind.Moose,
		DisplayName: "Moose",
		IsPredator: false,
		MaxHealth: ThornsWildlifeVsPlayerBalance.MaxHealthFromHumanMul( 3.4f ),
		WanderSpeed: ThornsWildlifeVsPlayerBalance.SpeedFromPlayerSprintMul( 0.323f ),
		ChaseSpeed: ThornsWildlifeVsPlayerBalance.WildlifeChaseFleeDeerMooseUnits,
		WanderRadius: 580f,
		LeashRadius: 4300f,
		AggroRadius: 0f,
		LoseRadius: 0f,
		FearRadius: 860f,
		AttackRange: 0f,
		MeleeDamage: ThornsWildlifeVsPlayerBalance.MeleeFromReferenceMul( 0f ),
		AttackCooldownSeconds: 999f,
		HuntCommitSeconds: 0f,
		IdleSecondsMin: 0.9f,
		IdleSecondsMax: 2.4f,
		UseLineOfSight: false,
		SenseHeightOffset: 70f,
		AllowPlayerMount: true,
		MountRiderHeightUp: 122f,
		MountRiderForward: 16f );

	public static readonly ThornsWildlifeSpeciesDefinition Bison = new(
		Kind: ThornsWildlifeSpeciesKind.Bison,
		DisplayName: "Bison",
		IsPredator: false,
		MaxHealth: ThornsWildlifeVsPlayerBalance.MaxHealthFromHumanMul( 4.25f ),
		WanderSpeed: ThornsWildlifeVsPlayerBalance.SpeedFromPlayerSprintMul( 0.302f ),
		ChaseSpeed: ThornsWildlifeVsPlayerBalance.SpeedFromPlayerSprintMul( 1.509f ),
		WanderRadius: 540f,
		LeashRadius: 4300f,
		AggroRadius: 0f,
		LoseRadius: 0f,
		FearRadius: 820f,
		AttackRange: 0f,
		MeleeDamage: ThornsWildlifeVsPlayerBalance.MeleeFromReferenceMul( 0f ),
		AttackCooldownSeconds: 999f,
		HuntCommitSeconds: 0f,
		IdleSecondsMin: 1.0f,
		IdleSecondsMax: 2.6f,
		UseLineOfSight: false,
		SenseHeightOffset: 72f,
		AllowPlayerMount: true,
		MountRiderHeightUp: 92f,
		MountRiderForward: 18f );

	public static ThornsWildlifeSpeciesDefinition Get( ThornsWildlifeSpeciesKind kind ) =>
		kind switch
		{
			ThornsWildlifeSpeciesKind.Wolf => Wolf,
			ThornsWildlifeSpeciesKind.Deer => Deer,
			ThornsWildlifeSpeciesKind.Fox => Fox,
			ThornsWildlifeSpeciesKind.Bear => Bear,
			ThornsWildlifeSpeciesKind.Cougar => Cougar,
			ThornsWildlifeSpeciesKind.Panther => Panther,
			ThornsWildlifeSpeciesKind.Boar => Boar,
			ThornsWildlifeSpeciesKind.Rabbit => Rabbit,
			ThornsWildlifeSpeciesKind.Elk => Elk,
			ThornsWildlifeSpeciesKind.Moose => Moose,
			ThornsWildlifeSpeciesKind.Bison => Bison,
			_ => Deer
		};

	public static string AbilitySummary( ThornsWildlifeSpeciesDefinition def )
	{
		if ( def is null )
			return "—";
		if ( def.IsPredator && def.MeleeDamage > 0.01f )
			return "Predator · melee bite";
		if ( def.IsPredator )
			return "Predator";
		return "Herbivore · avoids threats";
	}
}
