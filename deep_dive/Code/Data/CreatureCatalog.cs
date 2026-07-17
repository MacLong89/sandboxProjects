namespace DeepDive;

public enum CreatureDisposition
{
	Ambient,
	Hostile
}

public sealed class CreatureDefinition
{
	public string Id { get; init; }
	public string DisplayName { get; init; }
	public string Description { get; init; }
	public CreatureDisposition Disposition { get; init; }
	public float MinDepth { get; init; }
	public float MaxDepth { get; init; } = 9999f;
	public string TexturePath { get; init; }
	public float SpriteWorldHeight { get; init; } = 2f;
	public HazardKind? LinkedHazard { get; init; }
}

public static class CreatureCatalog
{
	public static IReadOnlyList<CreatureDefinition> All { get; } =
	[
		new()
		{
			Id = "reef_fish", DisplayName = "Reef Fish", Description = "Bright schooling fish of the shallows.",
			Disposition = CreatureDisposition.Ambient, MinDepth = 5f, MaxDepth = 90f,
			TexturePath = "textures/creatures/reef_fish.png", SpriteWorldHeight = 1.6f
		},
		new()
		{
			Id = "lantern_fry", DisplayName = "Lantern Fry", Description = "Tiny glowing baitfish.",
			Disposition = CreatureDisposition.Ambient, MinDepth = 80f, MaxDepth = 220f,
			TexturePath = "textures/creatures/reef_fish.png", SpriteWorldHeight = 1.1f
		},
		new()
		{
			Id = "drift_jelly", DisplayName = "Drift Jelly", Description = "Docile jellies that ignore divers.",
			Disposition = CreatureDisposition.Ambient, MinDepth = 20f, MaxDepth = 140f,
			TexturePath = "textures/creatures/jellyfish.png", SpriteWorldHeight = 2.2f
		},
		new()
		{
			Id = "abyss_glow", DisplayName = "Abyss Glowfin", Description = "Soft lights in the midnight column.",
			Disposition = CreatureDisposition.Ambient, MinDepth = 180f, MaxDepth = 360f,
			TexturePath = "textures/creatures/reef_fish.png", SpriteWorldHeight = 1.4f
		},
		new()
		{
			Id = "jelly_hazard", DisplayName = "Stinging Jellyfish", Description = "Contact delivers a painful sting.",
			Disposition = CreatureDisposition.Hostile, MinDepth = 15f, MaxDepth = 120f,
			TexturePath = "textures/creatures/jellyfish.png", SpriteWorldHeight = 2.8f,
			LinkedHazard = HazardKind.Jellyfish
		},
		new()
		{
			Id = "puffer_hazard", DisplayName = "Spined Puffer", Description = "Inflates and lashes with spines.",
			Disposition = CreatureDisposition.Hostile, MinDepth = 30f, MaxDepth = 160f,
			TexturePath = "textures/creatures/puffer.png", SpriteWorldHeight = 2.4f,
			LinkedHazard = HazardKind.Puffer
		},
		new()
		{
			Id = "mine_hazard", DisplayName = "Drifting Mine", Description = "Old ordinance still armed.",
			Disposition = CreatureDisposition.Hostile, MinDepth = 50f, MaxDepth = 320f,
			TexturePath = "textures/creatures/mine.png", SpriteWorldHeight = 2.2f,
			LinkedHazard = HazardKind.Mine
		},
		new()
		{
			Id = "angler_hazard", DisplayName = "Angler", Description = "A lure in the dark — and teeth.",
			Disposition = CreatureDisposition.Hostile, MinDepth = 110f, MaxDepth = 380f,
			TexturePath = "textures/creatures/angler.png", SpriteWorldHeight = 3.6f,
			LinkedHazard = HazardKind.Angler
		},
	];

	public static CreatureDefinition Get( string id ) =>
		All.FirstOrDefault( c => c.Id == id );

	public static CreatureDefinition FromHazard( HazardKind kind ) =>
		All.FirstOrDefault( c => c.LinkedHazard == kind );

	public static int TotalCount => All.Count;
}
