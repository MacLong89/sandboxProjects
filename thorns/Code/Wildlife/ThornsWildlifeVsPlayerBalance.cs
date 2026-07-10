namespace Sandbox;

/// <summary>
/// Wildlife combat/locomotion expressed as ratios to an abstract <b>human survivor</b> baseline (game units, not literal m/s).
/// Locomotion uses the player's <b>sustained sprint</b> cap (walk × <see cref="ThornsVitals.SprintSpeedMultiplier"/>), not walk alone.
/// HP and predator melee still use <see cref="ThornsHealth"/> default and primitive-tool damage as references.
/// </summary>
public static class ThornsWildlifeVsPlayerBalance
{
	/// <summary>Default <see cref="ThornsPawnMovement.WalkSpeed"/> — update if human walk retunes.</summary>
	public const float HumanNominalWalkSpeed = 320f;

	/// <summary>Standing sprint planar speed when vitals apply <see cref="ThornsVitals.SprintSpeedMultiplier"/> (no crouch).</summary>
	public static float HumanNominalSprintSpeed => HumanNominalWalkSpeed * ThornsVitals.SprintSpeedMultiplier;

	/// <summary>Default spawn <see cref="ThornsHealth.MaxHealth"/> on a humanoid pawn.</summary>
	public const float HumanNominalMaxHealth = 100f;

	/// <summary>Light melee reference: <see cref="ThornsToolMeleeCombat.CombatIdPrimitive"/> base damage (stick / improvised).</summary>
	public const float HumanNominalLightMeleeReference = 10f;

	/// <summary>Applied to every wildlife wander/chase speed from this module. 1.0 aligns chase tiers with terraingen <c>ThornsAnimalSpeciesCatalog</c> BaseSpeed.</summary>
	public const float WildlifeLocomotionGlobalSpeedMul = 1f;

	/// <summary>Human sprint ≈25 mph design baseline; deer/moose flee-chase ≈35 mph → <see cref="WildlifeChaseFleeDeerMooseUnits"/>.</summary>
	public const float WildlifeChaseFleeDeerMooseUnits = 650f * WildlifeLocomotionGlobalSpeedMul;

	/// <summary>Wolf/panther flee-chase ≈40 mph vs same human baseline → <see cref="WildlifeChaseFleeWolfPantherUnits"/>.</summary>
	public const float WildlifeChaseFleeWolfPantherUnits = 740f * WildlifeLocomotionGlobalSpeedMul;

	/// <summary>
	/// Legacy name — not a gameplay speed cap. Large bound for optional telemetry / anim sampling only (see panther driver).
	/// </summary>
	public const float WildlifeChaseSpeedClampMax = 500_000f;

	/// <summary>
	/// Planar wish length above (human sprint × this) uses the chase velocity snap in <see cref="ThornsWildlifeMotor"/> — keep below full sprint so Attack pursuit (fraction of <c>ChaseSpeed</c>) still snaps.
	/// </summary>
	public const float WildlifeChaseVelocitySnapWishVsHumanSprint = 0.52f;

	/// <param name="humanSprintMul">1 = player sprint (see <see cref="HumanNominalSprintSpeed"/>). No upper clamp — tuned species use explicit <c>ChaseSpeed</c> where needed.</param>
	public static float SpeedFromPlayerSprintMul( float humanSprintMul ) =>
		WildlifeLocomotionGlobalSpeedMul * Math.Max( 45f, HumanNominalSprintSpeed * humanSprintMul );

	public static float MaxHealthFromHumanMul( float humanHpMul ) =>
		Math.Max( 12f, HumanNominalMaxHealth * humanHpMul );

	public static float MeleeFromReferenceMul( float meleeMulVsPrimitive ) =>
		meleeMulVsPrimitive <= 0.004f ? 0f : Math.Max( 3.5f, HumanNominalLightMeleeReference * meleeMulVsPrimitive );
}
