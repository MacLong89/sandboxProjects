namespace Sandbox;

/// <summary>
/// Tag names and groupings referenced by trace diagnostics / documentation.
/// Physics filtering is tag-driven in Thorns where possible — keep aligned with <see cref="ThornsCollisionTags"/>.
/// </summary>
public static class ThornsTraceLayers
{
	/// <summary>World static / terrain-backed solids (movement + generic traces).</summary>
	public const string Terrain = ThornsCollisionTags.TerrainChunk;

	public const string Solid = ThornsCollisionTags.Solid;

	public const string World = ThornsCollisionTags.World;

	public const string Structure = ThornsCollisionTags.Structure;

	public const string ResourceNode = ThornsCollisionTags.ResourceNode;

	public const string Creature = "creature";

	/// <summary>Decorative foliage scatter — traces that should ignore this when we add explicit filters.</summary>
	public const string FoliageDecor = "thorns_foliage";

	public const string LootCrate = "thorns_loot_crate";

	public const string DeathCrate = "thorns_death_crate";
}
