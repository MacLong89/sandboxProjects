namespace Fauna2;

public readonly struct AchievementEntry
{
	public string Title { get; init; }
	public string Description { get; init; }
	public bool Unlocked { get; init; }
}

public static class AchievementCatalog
{
	public static IReadOnlyList<AchievementEntry> All( int unlockedFlags ) =>
		Entries.Select( e => new AchievementEntry
		{
			Title = e.Title,
			Description = e.Description,
			Unlocked = (unlockedFlags & e.Bit) != 0,
		} ).ToList();

	private static readonly (int Bit, string Title, string Description)[] Entries =
	{
		(1, "First discovery", "Recorded your first species in the codex"),
		(2, "Collector", "Discovered 3 species"),
		(4, "Naturalist", "Discovered 10 species"),
		(8, "Variant hunter", "Found a rare color variant"),
		(16, "Color curator", "Discovered 5 rare variants"),
		(32, "Breeder", "Bred your first animal"),
		(64, "Dynasty builder", "Bred 5 animals"),
		(128, "Four stars", "Reached a 4-star zoo rating"),
		(256, "Five stars", "Reached a 5-star zoo rating"),
		(512, "Land owner", "Expanded to a second plot"),
		(1024, "Land baron", "Own 3 or more plots"),
		(2048, "Habitat architect", "Built 3 habitats"),
		(4096, "Busy sanctuary", "Care for 10 animals at once"),
		(8192, "Local favorite", "Hosted 50 guests at once"),
		(16384, "Codex master", "Completed the species codex"),
	};
}
