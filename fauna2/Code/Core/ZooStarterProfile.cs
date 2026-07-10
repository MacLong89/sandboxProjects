namespace Fauna2;

/// <summary>Land-size tier for a new zoo — affects starting costs and guest capacity feel.</summary>
public enum StarterPlotTier
{
	Small,
	Medium,
	Large,
}

/// <summary>
/// A new-game starter pack: biome theme, plot tier, economy and gameplay modifiers.
/// </summary>
public sealed class ZooStarterProfile
{
	public string Id { get; init; }
	public string DisplayName { get; init; }
	public string Description { get; init; }
	public Biome Biome { get; init; }
	public StarterPlotTier PlotTier { get; init; }

	public int StartingMoney { get; init; }
	/// <summary>Up-front land prep / licensing fee deducted from starting money.</summary>
	public int SetupCost { get; init; }

	/// <summary>Flat bonus added to aggregate guest appeal.</summary>
	public float GuestAppealBonus { get; init; }
	/// <summary>Extra happiness for animals whose species biome matches this starter.</summary>
	public float NativeAnimalHappinessBonus { get; init; }
	/// <summary>Guest appeal multiplier for animals matching the starter biome (display + slight boost).</summary>
	public float NativeGuestAppealBonus { get; init; }

	public int GuestCapBonus { get; init; }

	public int NetStartingMoney => Math.Max( 0, StartingMoney - SetupCost );

	/// <summary>Starter bonuses — plot size is always one cell until expansion.</summary>
	public string PlotTierLabel => PlotTier switch
	{
		StarterPlotTier.Small => "Balanced bonuses",
		StarterPlotTier.Medium => "Coastal bonuses",
		StarterPlotTier.Large => "Premium guest bonuses",
		_ => "Starter bonuses",
	};

	public Color AccentColor => HabitatComponent.BiomeColor( Biome );
}

/// <summary>All selectable new-game starter packs.</summary>
public static class ZooStarterProfiles
{
	public static IReadOnlyList<ZooStarterProfile> All { get; } = new List<ZooStarterProfile>
	{
		new()
		{
			Id = "grassland_homestead",
			DisplayName = "Grassland Homestead",
			Description = "A gentle beginning on open grassland. Cheap to start, grassland animals love it here, and families enjoy the relaxed scenery.",
			Biome = Biome.Grassland,
			PlotTier = StarterPlotTier.Small,
			StartingMoney = 9_200,
			SetupCost = 1_800,
			GuestAppealBonus = 6f,
			NativeAnimalHappinessBonus = 0.18f,
			NativeGuestAppealBonus = 0.12f,
			GuestCapBonus = 0,
		},
		new()
		{
			Id = "forest_retreat",
			DisplayName = "Forest Retreat",
			Description = "Shaded forest with strong visitor appeal. Forest species thrive; setup costs a little more before you open the gates.",
			Biome = Biome.Forest,
			PlotTier = StarterPlotTier.Small,
			StartingMoney = 8_800,
			SetupCost = 2_100,
			GuestAppealBonus = 14f,
			NativeAnimalHappinessBonus = 0.16f,
			NativeGuestAppealBonus = 0.10f,
			GuestCapBonus = 25,
		},
		new()
		{
			Id = "coastal_sanctuary",
			DisplayName = "Coastal Sanctuary",
			Description = "Coastal land suited to water-loving species. Guests find it niche but rewarding — coastal animals are exceptionally content.",
			Biome = Biome.Coastal,
			PlotTier = StarterPlotTier.Medium,
			StartingMoney = 8_500,
			SetupCost = 2_600,
			GuestAppealBonus = 5f,
			NativeAnimalHappinessBonus = 0.24f,
			NativeGuestAppealBonus = 0.08f,
			GuestCapBonus = 50,
		},
		new()
		{
			Id = "grassland_estate",
			DisplayName = "Grassland Estate",
			Description = "A larger grassland parcel with premium guest draw. High setup costs mean you start leaner but can grow into a crowd-pleaser.",
			Biome = Biome.Grassland,
			PlotTier = StarterPlotTier.Large,
			StartingMoney = 8_000,
			SetupCost = 3_400,
			GuestAppealBonus = 20f,
			NativeAnimalHappinessBonus = 0.12f,
			NativeGuestAppealBonus = 0.14f,
			GuestCapBonus = 75,
		},
		new()
		{
			Id = "arctic_outpost",
			DisplayName = "Arctic Outpost",
			Description = "Remote cold-climate land with generous grant funding. Exotic appeal draws curious guests; arctic specialists flourish.",
			Biome = Biome.Arctic,
			PlotTier = StarterPlotTier.Large,
			StartingMoney = 10_500,
			SetupCost = 4_200,
			GuestAppealBonus = 18f,
			NativeAnimalHappinessBonus = 0.22f,
			NativeGuestAppealBonus = 0.15f,
			GuestCapBonus = 50,
		},
		new()
		{
			Id = "forest_sanctuary",
			DisplayName = "Forest Sanctuary",
			Description = "Expansive old-growth grounds. The highest guest appeal and a huge habitat, but the steepest opening costs.",
			Biome = Biome.Forest,
			PlotTier = StarterPlotTier.Large,
			StartingMoney = 7_500,
			SetupCost = 4_800,
			GuestAppealBonus = 24f,
			NativeAnimalHappinessBonus = 0.14f,
			NativeGuestAppealBonus = 0.16f,
			GuestCapBonus = 100,
		},
	};

	public static ZooStarterProfile Find( string id ) =>
		All.FirstOrDefault( p => p.Id == id ) ?? All[0];

	public static ZooStarterProfile Default => All[0];
}
