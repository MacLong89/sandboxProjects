namespace Sandbox;

/// <summary>
/// Named collision <i>roles</i> (not engine layers) — documentation for which traces and systems should consider an object.
/// Apply via tags, dedicated child colliders, or comments on spawn helpers.
/// </summary>
public static class ThornsCollisionRoles
{
	public const string PlayerMovementCollision = nameof(PlayerMovementCollision);

	public const string PlayerHitboxCollision = nameof(PlayerHitboxCollision);

	public const string NpcMovementCollision = nameof(NpcMovementCollision);

	public const string NpcHitboxCollision = nameof(NpcHitboxCollision);

	public const string AnimalMovementCollision = nameof(AnimalMovementCollision);

	public const string AnimalHitboxCollision = nameof(AnimalHitboxCollision);

	public const string BuildingSolidCollision = nameof(BuildingSolidCollision);

	public const string BuildingPlacementCollision = nameof(BuildingPlacementCollision);

	public const string ProjectileHitCollision = nameof(ProjectileHitCollision);

	public const string MeleeHitCollision = nameof(MeleeHitCollision);

	public const string InteractionTraceCollision = nameof(InteractionTraceCollision);

	public const string ResourceHarvestCollision = nameof(ResourceHarvestCollision);

	public const string LootContainerCollision = nameof(LootContainerCollision);

	public const string TriggerOnlyCollision = nameof(TriggerOnlyCollision);

	public const string NoCollisionVisualOnly = nameof(NoCollisionVisualOnly);
}
