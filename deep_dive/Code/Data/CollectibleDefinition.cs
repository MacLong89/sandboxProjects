namespace DeepDive;

public sealed class CollectibleDefinition
{
	public string Id { get; init; }
	public string DisplayName { get; init; }
	public string Description { get; init; }
	public CollectibleRarity Rarity { get; init; }
	public float BaseValue { get; init; }
	public int CapacityCost { get; init; } = 1;
	public float MinDepth { get; init; }
	public float MaxDepth { get; init; } = 9999f;
	public float SpawnWeight { get; init; } = 1f;
	public Color Tint { get; init; } = Color.White;
	public Vector3 WorldSize { get; init; } = new( 0.9f, 0.9f, 0.9f );

	/// <summary>Optional sprite under textures/… — loot sits on the sand; swimming fauna patrol.</summary>
	public string TexturePath { get; init; }
	public float SpriteWorldHeight { get; init; } = 1.4f;
	public bool IsSwimming { get; init; }
	/// <summary>Must be revealed by scanner before pickup.</summary>
	public bool RequiresScan { get; init; }
	/// <summary>Optional tool that must be selected to salvage.</summary>
	public ToolKind? RequiredTool { get; init; }
}
