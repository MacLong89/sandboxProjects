namespace Sandbox;

/// <summary>
/// Canonical gameplay ray categories — each maps to a <see cref="ThornsTraceRuleSet"/> in <see cref="ThornsTraceRules"/>.
/// Use with <see cref="ThornsTraceUtility"/> so hitscan / interaction / AI / movement share one matrix.
/// </summary>
/// <remarks>
/// Matrix (intent — not engine layers):<br/>
/// <b>WeaponHitscan / WeaponFeedbackWorld</b> — hit damage surfaces first (<c>UseHitboxes</c>), then solid physics fallback; ignore attacker.<br/>
/// <b>InteractionUse</b> — solid world hits for LOS / crosshair; no hitboxes (thin props + consistent “first blocker”).<br/>
/// <b>BuildingPlacementView</b> — long eye ray for anchor; physics only, no hitboxes.<br/>
/// <b>BuildingStructurePickPiercing</b> — same as placement view; caller pierces manually.<br/>
/// <b>BuildingTerrainSupportDown</b> — tall down probe for slab support Z.<br/>
/// <b>MovementProbe</b> — pawn-relative ground / step / headroom; hitboxes + physics.<br/>
/// <b>AiLineOfSight</b> — bandit / wildlife LOS to player; hitboxes + physics; ignore self root.<br/>
/// <b>FootstepGround</b> — short down ray; physics only (cheap).<br/>
/// <b>TerrainChunkSnapDown</b> — pierce until terrain chunk hit.<br/>
/// <b>TerrainInteriorSampleDown</b> — editor / proc sample.<br/>
/// <b>AirdropGroundSnapDown</b> — long vertical snap.<br/>
/// <b>TamingWorldPick</b> — forward pick for wildlife id under crosshair (was missing explicit physics flags).
/// </remarks>
public enum ThornsTraceProfile : byte
{
	WeaponHitscan = 1,

	WeaponFeedbackWorld = 2,

	InteractionUse = 3,

	BuildingPlacementView = 4,

	BuildingStructurePickPiercing = 5,

	BuildingTerrainSupportDown = 6,

	MovementProbe = 7,

	AiLineOfSight = 8,

	FootstepGround = 9,

	TerrainChunkSnapDown = 10,

	TerrainInteriorSampleDown = 11,

	AirdropGroundSnapDown = 12,

	TamingWorldPick = 13
}
