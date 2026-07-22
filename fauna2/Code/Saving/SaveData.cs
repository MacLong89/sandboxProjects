namespace Fauna2;

/// <summary>
/// Versioned save format. Bump <see cref="CurrentVersion"/> and add a
/// migration step in SaveSystem.Migrate when the schema changes — old zoos
/// must always keep loading across updates.
/// </summary>
public sealed class SaveData
{
	public const int CurrentVersion = 14;

	public int Version { get; set; } = CurrentVersion;
	public long SavedAtUnix { get; set; }

	// ── Zoo state ───────────────────────────────────────────
	public string ZooName { get; set; } = "";
	public int Money { get; set; }
	public int Xp { get; set; }
	public int Level { get; set; } = 1;
	public int Prestige { get; set; }
	public long TotalEarned { get; set; }
	public long TotalSpent { get; set; }

	public string StarterProfileId { get; set; } = "";
	public int StarterBiome { get; set; }
	public float GuestAppealModifier { get; set; }
	public float NativeBiomeHappinessBonus { get; set; }
	public float NativeGuestAppealBonus { get; set; }
	public int GuestCapBonus { get; set; }
	public int SaveSlotId { get; set; } = 1;
	public bool TutorialAnimalClaimed { get; set; }
	public int TotalAnimalsBought { get; set; }
	public int TotalAnimalsBred { get; set; }
	public int TotalAnimalsCaught { get; set; }

	/// <summary>Zoo owner's carried animals and catch tools.</summary>
	public PlayerInventorySave OwnerInventory { get; set; } = new();

	/// <summary>Where the zoo owner was standing when this save was written.</summary>
	public bool HasOwnerPlayerPosition { get; set; }
	public SaveVector3 OwnerPlayerPosition { get; set; } = new();

	// ── Progression ─────────────────────────────────────────
	/// <summary>Unified sequential goal index (0-based; tutorial = indices 0–10).</summary>
	public int ObjectiveIndex { get; set; }
	/// <summary>Player opened Stats during the guest-ratings tutorial step.</summary>
	public bool GuestRatingsReviewed { get; set; }
	/// <summary>Deprecated — merged into ObjectiveIndex in save v10.</summary>
	public int ChallengeIndex { get; set; }
	public int LoginStreak { get; set; }
	public int GuestMilestoneFlags { get; set; }
	public int CodexTierFlags { get; set; }
	public int HabitatTierFlags { get; set; }
	public bool ProfitableNotified { get; set; }
	public bool EconomyTutorialShown { get; set; }
	public int AchievementFlags { get; set; }
	public Dictionary<string, int> CodexSpecies { get; set; } = new();
	public Dictionary<string, bool> CodexVariants { get; set; } = new();
	public List<BreedingRecord> BreedingHistory { get; set; } = new();

	// ── World ───────────────────────────────────────────────
	public List<string> Plots { get; set; } = new();
	public List<HabitatSave> Habitats { get; set; } = new();
	public List<PlaceableSave> Placeables { get; set; } = new();
	public List<AnimalSave> Animals { get; set; } = new();
	public List<TerrainObstacleSave> TerrainObstacles { get; set; } = new();
	public int TerrainObstaclesCleared { get; set; }
	public List<WildAnimalSave> WildAnimals { get; set; } = new();

	// ── Guests ──────────────────────────────────────────────
	public int GuestCount { get; set; }
	public int PeakGuests { get; set; }
	public float Cleanliness { get; set; } = 100f;
	public float GuestSatisfaction { get; set; } = 75f;

	// ── Social ──────────────────────────────────────────────
	public List<long> Likes { get; set; } = new();
	public List<long> SocialFavorites { get; set; } = new();
	public int TotalVisitors { get; set; }
	public Dictionary<string, int> VisitBonusDays { get; set; } = new();
	public long LastDailyBonusUnixDay { get; set; }
	public int WeeklyBestScore { get; set; }
	public string WeeklyTheme { get; set; } = "";

	// Retention systems
	public int WeatherDay { get; set; } = 1;
	public int Season { get; set; }
	public int Weather { get; set; }
	public string ActiveEventTitle { get; set; } = "";
	public string ActiveEventDetail { get; set; } = "";
	public string ActiveEventIcon { get; set; } = "event";
	public string RareSightingSpeciesId { get; set; } = "";
	public float EventGuestAppealBonus { get; set; }
	public float EventIncomeMultiplier { get; set; } = 1f;
	public float EventRareSpawnMultiplier { get; set; } = 1f;
	public float EventBuildCostMultiplier { get; set; } = 1f;
	public int DailySeed { get; set; }
	public int DailyCompletedMask { get; set; }
	public int DailyStartingCleared { get; set; }
	public int DailyStartingGuests { get; set; }
	public long DailyStartingEarned { get; set; }
	public int DailyStartingBred { get; set; }
	public int DailyStartingCaught { get; set; }
	public int DailyStartingPlaceables { get; set; }
	public int MomentumCompletedMask { get; set; }
	public int MomentumPoints { get; set; }
	public bool MomentumEventGranted { get; set; }
	public int StaffKeepers { get; set; }
	public int StaffCleaners { get; set; }
	public int StaffGuides { get; set; }
	public int StaffVets { get; set; }
	public int ResearchHabitatCare { get; set; }
	public int ResearchAnimalCare { get; set; }
	public int ResearchGuestComfort { get; set; }
	public int ResearchFieldTools { get; set; }
	public int ResearchDecorationDesign { get; set; }
	public int FranchiseRank { get; set; }
	public int LegacyTokens { get; set; }
	public int BranchExpansions { get; set; }
}

public sealed class PlayerInventorySave
{
	public int CarriedCount { get; set; }
	public string CarriedSpecies0 { get; set; } = "";
	public string CarriedSpecies1 { get; set; } = "";
	public bool HasNet { get; set; } = true;
	public int BaitCount { get; set; } = 3;
	public int TranquilizerCount { get; set; }

	public void ApplyTo( PlayerInventory inv )
	{
		if ( inv is null || !inv.IsValid() ) return;

		inv.CarriedSpecies0 = CarriedSpecies0 ?? "";
		inv.CarriedSpecies1 = CarriedSpecies1 ?? "";
		inv.HasNet = HasNet;
		inv.BaitCount = Math.Max( 0, BaitCount );
		inv.TranquilizerCount = Math.Max( 0, TranquilizerCount );
		inv.NormalizeCarried();
	}

	public static PlayerInventorySave From( PlayerInventory inv )
	{
		if ( inv is null || !inv.IsValid() )
			return new PlayerInventorySave();

		inv.NormalizeCarried();

		return new PlayerInventorySave
		{
			CarriedCount = inv.CarriedCount,
			CarriedSpecies0 = inv.CarriedSpecies0 ?? "",
			CarriedSpecies1 = inv.CarriedSpecies1 ?? "",
			HasNet = inv.HasNet,
			BaitCount = inv.BaitCount,
			TranquilizerCount = inv.TranquilizerCount,
		};
	}
}

public sealed class SaveVector3
{
	public float X { get; set; }
	public float Y { get; set; }
	public float Z { get; set; }

	public SaveVector3() { }

	public SaveVector3( Vector3 value )
	{
		X = value.x;
		Y = value.y;
		Z = value.z;
	}

	public Vector3 ToVector3() => new( X, Y, Z );
}

public sealed class HabitatSave
{
	public string HabitatId { get; set; } = "";
	public string DefinitionId { get; set; } = "";
	public SaveVector3 Position { get; set; } = new();
}

public sealed class PlaceableSave
{
	public string DefinitionId { get; set; } = "";
	public SaveVector3 Position { get; set; } = new();
	public float Yaw { get; set; }
	public float UncollectedRevenue { get; set; }
}

public sealed class AnimalSave
{
	public string AnimalId { get; set; } = "";
	public string DefinitionId { get; set; } = "";
	public string VariantId { get; set; } = "";
	public string Name { get; set; } = "";
	public string HabitatId { get; set; } = "";
	public SaveVector3 Position { get; set; } = new();
	public float Hunger { get; set; } = 80f;
	public float Happiness { get; set; } = 70f;
	public float Health { get; set; } = 100f;
	public float AgeSeconds { get; set; }
	public AnimalGenome Genome { get; set; } = new();
}

public sealed class WildAnimalSave
{
	public string WildId { get; set; } = "";
	public string SpeciesId { get; set; } = "";
	public SaveVector3 Position { get; set; } = new();
	public int PlotX { get; set; }
	public int PlotY { get; set; }
}
