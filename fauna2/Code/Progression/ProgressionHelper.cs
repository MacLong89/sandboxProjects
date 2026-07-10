namespace Fauna2;

/// <summary>A single unlockable shown on the progression page.</summary>
public readonly struct UnlockEntry
{
	public string Name { get; init; }
	public string Kind { get; init; }
	public int Level { get; init; }
	public int Prestige { get; init; }
	public bool Unlocked { get; init; }
}

/// <summary>
/// Read-only helpers over the data-driven unlock tree. Everything is derived
/// from definitions — adding content with an UnlockLevel automatically slots
/// it into the progression page.
/// </summary>
public static class ProgressionHelper
{
	public static IEnumerable<UnlockEntry> AllUnlocks()
	{
		var state = ZooState.Instance;
		var level = state?.Level ?? 1;
		var prestige = state?.Prestige ?? 0;

		foreach ( var a in Defs.Animals.OrderBy( a => a.UnlockLevel ).ThenBy( a => a.RequiredPrestige ) )
		{
			yield return new UnlockEntry
			{
				Name = a.DisplayName,
				Kind = "Animal",
				Level = a.UnlockLevel,
				Prestige = a.RequiredPrestige,
				Unlocked = level >= a.UnlockLevel && prestige >= a.RequiredPrestige,
			};
		}

		foreach ( var p in Defs.Placeables.Where( p => p.UnlockLevel > 1 || p.RequiredPrestige > 0 ).OrderBy( p => p.UnlockLevel ).ThenBy( p => p.RequiredPrestige ) )
		{
			yield return new UnlockEntry
			{
				Name = p.DisplayName,
				Kind = p.ProvidesShop ? "Shop" : p.ProvidesRestaurant ? "Dining" : p.Category.ToString(),
				Level = p.UnlockLevel,
				Prestige = p.RequiredPrestige,
				Unlocked = level >= p.UnlockLevel && prestige >= p.RequiredPrestige,
			};
		}
	}

	/// <summary>The next few locked things — used as a teaser on the HUD/progression page.</summary>
	public static IEnumerable<UnlockEntry> UpcomingUnlocks( int count = 3 ) =>
		AllUnlocks().Where( u => !u.Unlocked ).OrderBy( u => u.Level ).Take( count );
}
