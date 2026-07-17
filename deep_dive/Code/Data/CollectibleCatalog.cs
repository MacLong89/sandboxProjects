namespace DeepDive;

public static class CollectibleCatalog
{
	public static IReadOnlyList<CollectibleDefinition> All { get; } =
	[
		new()
		{
			Id = "seashell", DisplayName = "Seashell", Description = "A common shallow shell.",
			Rarity = CollectibleRarity.Common, BaseValue = 12f, MinDepth = 2f, MaxDepth = 60f,
			SpawnWeight = 3.2f, Tint = new Color( 0.95f, 0.65f, 0.75f ), WorldSize = new( 0.8f, 0.5f, 0.7f ),
			TexturePath = "textures/loot/seashell.png", SpriteWorldHeight = 1.1f
		},
		new()
		{
			Id = "old_coin", DisplayName = "Old Coin", Description = "Tarnished but valuable.",
			Rarity = CollectibleRarity.Common, BaseValue = 18f, MinDepth = 5f, MaxDepth = 90f,
			SpawnWeight = 2.6f, Tint = new Color( 0.9f, 0.75f, 0.2f ), WorldSize = new( 0.55f, 0.55f, 0.15f ),
			TexturePath = "textures/loot/old_coin.png", SpriteWorldHeight = 0.95f
		},
		new()
		{
			Id = "scrap_metal", DisplayName = "Scrap Metal", Description = "Rusty salvage.",
			Rarity = CollectibleRarity.Common, BaseValue = 15f, MinDepth = 8f, MaxDepth = 140f,
			SpawnWeight = 2.4f, Tint = new Color( 0.55f, 0.55f, 0.58f ), WorldSize = new( 0.9f, 0.7f, 0.6f ),
			TexturePath = "textures/loot/scrap_metal.png", SpriteWorldHeight = 1.25f
		},
		new()
		{
			Id = "pearl", DisplayName = "Pearl", Description = "A pale gem from a clam.",
			Rarity = CollectibleRarity.Uncommon, BaseValue = 45f, MinDepth = 25f, MaxDepth = 140f,
			SpawnWeight = 1.4f, Tint = new Color( 0.85f, 0.9f, 1f ), WorldSize = new( 0.55f, 0.55f, 0.55f ),
			TexturePath = "textures/loot/pearl.png", SpriteWorldHeight = 0.9f
		},
		new()
		{
			Id = "reef_fish", DisplayName = "Reef Fish", Description = "A darting tropical fish — catch it!",
			Rarity = CollectibleRarity.Uncommon, BaseValue = 40f, MinDepth = 8f, MaxDepth = 95f,
			SpawnWeight = 1.8f, Tint = new Color( 1f, 0.85f, 0.2f ), WorldSize = new( 1.1f, 0.6f, 0.7f ),
			TexturePath = "textures/creatures/reef_fish.png", SpriteWorldHeight = 1.8f, IsSwimming = true
		},
		new()
		{
			Id = "silver_locket", DisplayName = "Silver Locket", Description = "Someone's lost keepsake.",
			Rarity = CollectibleRarity.Uncommon, BaseValue = 55f, MinDepth = 40f, MaxDepth = 160f,
			SpawnWeight = 1.1f, Tint = new Color( 0.75f, 0.78f, 0.85f ), WorldSize = new( 0.6f, 0.35f, 0.7f ),
			SpriteWorldHeight = 1.0f
		},
		new()
		{
			Id = "lost_relic", DisplayName = "Lost Relic", Description = "A small golden idol.",
			Rarity = CollectibleRarity.Rare, BaseValue = 120f, MinDepth = 70f, MaxDepth = 200f,
			SpawnWeight = 0.55f, Tint = new Color( 0.95f, 0.7f, 0.15f ), WorldSize = new( 0.7f, 0.7f, 1.1f ),
			SpriteWorldHeight = 1.4f
		},
		new()
		{
			Id = "ancient_statue", DisplayName = "Ancient Statue", Description = "Weathered stone head.",
			Rarity = CollectibleRarity.Epic, BaseValue = 220f, MinDepth = 110f, MaxDepth = 220f,
			SpawnWeight = 0.28f, Tint = new Color( 0.55f, 0.5f, 0.45f ), WorldSize = new( 1.2f, 1.1f, 1.4f ),
			SpriteWorldHeight = 2.0f
		},
		new()
		{
			Id = "sunken_crown", DisplayName = "Sunken Crown", Description = "Jewels still gleam.",
			Rarity = CollectibleRarity.Epic, BaseValue = 280f, MinDepth = 140f, MaxDepth = 250f,
			SpawnWeight = 0.2f, Tint = new Color( 0.9f, 0.55f, 0.95f ), WorldSize = new( 1.1f, 0.8f, 0.7f ),
			SpriteWorldHeight = 1.5f
		},
		new()
		{
			Id = "wreck_crate", DisplayName = "Wreck Crate", Description = "Sealed salvage — scan to unlock.",
			Rarity = CollectibleRarity.Rare, BaseValue = 150f, MinDepth = 60f, MaxDepth = 220f,
			SpawnWeight = 0.7f, Tint = new Color( 0.45f, 0.38f, 0.3f ), WorldSize = new( 1.4f, 1f, 1.1f ),
			SpriteWorldHeight = 1.7f, RequiresScan = true
		},
		new()
		{
			Id = "locked_vault", DisplayName = "Locked Vault", Description = "Needs a harpoon breach.",
			Rarity = CollectibleRarity.Epic, BaseValue = 320f, MinDepth = 120f, MaxDepth = 360f,
			SpawnWeight = 0.25f, Tint = new Color( 0.3f, 0.35f, 0.45f ), WorldSize = new( 1.6f, 1.2f, 1.3f ),
			SpriteWorldHeight = 2.0f, RequiresScan = true, RequiredTool = ToolKind.Harpoon
		},
		new()
		{
			Id = "hadal_pearl", DisplayName = "Hadal Pearl", Description = "Cold light from the trench.",
			Rarity = CollectibleRarity.Epic, BaseValue = 360f, MinDepth = 280f, MaxDepth = 420f,
			SpawnWeight = 0.18f, Tint = new Color( 0.45f, 0.75f, 0.95f ), WorldSize = new( 0.7f, 0.7f, 0.7f ),
			SpriteWorldHeight = 1.1f
		},
	];

	public static CollectibleDefinition Get( string id ) =>
		All.FirstOrDefault( c => c.Id == id );
}
