namespace Fauna2;

public enum FaunaSeason { Spring, Summer, Autumn, Winter }
public enum FaunaWeather { Clear, Rain, Heatwave, Snow, Festival }
public enum StaffRole { Keeper, Cleaner, Guide, Vet }
public enum ResearchNode { HabitatCare, AnimalCare, GuestComfort, FieldTools, DecorationDesign }
public enum SanctuaryDailyKind { ClearObstacles, AttractGuests, EarnIncome, BreedAnimals, CatchWildlife, PlaceDecor }
public enum MomentumMilestone { Habitat, GuestAccess, FirstAnimal, FirstSpecies, TenGuests, FirstDaily }

public readonly struct SanctuaryDailyGoal
{
	public string Title { get; init; }
	public string Detail { get; init; }
	public string Icon { get; init; }
	public int Progress { get; init; }
	public int Target { get; init; }
	public bool Complete => Target > 0 && Progress >= Target;
	public float Fraction => Target <= 0 ? 0f : MathX.Clamp( Progress / (float)Target, 0f, 1f );
}

public readonly struct AnimalLineageSummary
{
	public IReadOnlyList<AnimalComponent> Parents { get; init; }
	public IReadOnlyList<AnimalComponent> Siblings { get; init; }
	public IReadOnlyList<AnimalComponent> Offspring { get; init; }
	public IReadOnlyList<AnimalComponent> GrandOffspring { get; init; }
}

public readonly struct MomentumGoal
{
	public string Title { get; init; }
	public string Detail { get; init; }
	public string Icon { get; init; }
	public float Progress { get; init; }
	public string Label { get; init; }
}

public sealed class SanctuaryMomentumSystem : Component
{
	public static SanctuaryMomentumSystem Instance { get; private set; }

	[Sync( SyncFlags.FromHost )] public int CompletedMask { get; set; }
	[Sync( SyncFlags.FromHost )] public int MomentumPoints { get; set; }
	[Sync( SyncFlags.FromHost )] public bool MomentumEventGranted { get; set; }

	private TimeUntil _nextCheck;

	protected override void OnAwake() => Instance = this;
	protected override void OnDestroy() { if ( Instance == this ) Instance = null; }
	protected override void OnStart() => _nextCheck = 1f;

	protected override void OnFixedUpdate()
	{
		if ( IsProxy || !_nextCheck || GameManager.Instance?.GameStarted != true ) return;
		_nextCheck = 1.5f;
		CheckMilestones();
	}

	public MomentumGoal CurrentGoal
	{
		get
		{
			if ( !IsComplete( MomentumMilestone.Habitat ) )
				return new() { Icon = "fence", Title = "First Habitat", Detail = "Place any habitat to make the zoo feel real immediately.", Progress = HabitatRegistry.Count > 0 ? 1f : 0f, Label = $"{HabitatRegistry.Count}/1" };
			if ( !IsComplete( MomentumMilestone.GuestAccess ) )
				return new() { Icon = "follow_the_signs", Title = "Open The Gates", Detail = "Build an entrance and path access so guests can arrive.", Progress = PathNetwork.HasGuestAccess ? 1f : PathNetwork.HasEntrance ? 0.5f : 0f, Label = PathNetwork.HasGuestAccess ? "Open" : PathNetwork.HasEntrance ? "Need paths" : "Need entrance" };
			if ( !IsComplete( MomentumMilestone.FirstAnimal ) )
				return new() { Icon = "pets", Title = "First Resident", Detail = "Adopt or release one animal into a habitat.", Progress = AnimalRegistry.Count > 0 ? 1f : 0f, Label = $"{AnimalRegistry.Count}/1" };
			if ( !IsComplete( MomentumMilestone.TenGuests ) )
				return new() { Icon = "groups", Title = "First Crowd", Detail = "Reach 10 guests at once for your first popularity burst.", Progress = (GuestSystem.Instance?.GuestCount ?? 0) / 10f, Label = $"{GuestSystem.Instance?.GuestCount ?? 0}/10" };
			if ( !IsComplete( MomentumMilestone.FirstDaily ) )
				return new() { Icon = "redeem", Title = "Daily Spark", Detail = "Complete any daily sanctuary objective for a bonus event.", Progress = DailyCompletedCount() > 0 ? 1f : 0f, Label = $"{DailyCompletedCount()}/1" };
			return default;
		}
	}

	private void CheckMilestones()
	{
		TryComplete( MomentumMilestone.Habitat, HabitatRegistry.Count > 0, "First habitat built" );
		TryComplete( MomentumMilestone.GuestAccess, PathNetwork.HasGuestAccess, "Zoo opened to guests" );
		TryComplete( MomentumMilestone.FirstAnimal, AnimalRegistry.Count > 0, "First animal joined" );
		TryComplete( MomentumMilestone.FirstSpecies, (CollectionSystem.Instance?.DiscoveredSpeciesCount ?? 0) > 0, "First species discovered" );
		TryComplete( MomentumMilestone.TenGuests, (GuestSystem.Instance?.GuestCount ?? 0) >= 10, "First crowd arrived" );
		TryComplete( MomentumMilestone.FirstDaily, DailyCompletedCount() > 0, "First daily goal completed" );

		if ( !MomentumEventGranted && MomentumPoints >= 4 )
		{
			MomentumEventGranted = true;
			SanctuaryEventSystem.Instance?.GrantMomentumEvent();
			ZooState.Instance?.Notify( "Momentum bonus: a special visitor event was triggered.", "bolt" );
			SaveSystem.Instance?.RequestSave();
		}
	}

	private void TryComplete( MomentumMilestone milestone, bool condition, string title )
	{
		if ( !condition || IsComplete( milestone ) ) return;

		CompletedMask |= 1 << (int)milestone;
		MomentumPoints++;

		var reward = 150 + MomentumPoints * 75;
		ZooState.Instance?.AddMoney( reward );
		ZooState.Instance?.AddXp( 25 + MomentumPoints * 10 );
		ZooState.Instance?.Notify( $"Momentum: {title} (+${reward:n0})", "bolt" );
		SaveSystem.Instance?.RequestSave();
	}

	public bool IsComplete( MomentumMilestone milestone ) =>
		(CompletedMask & (1 << (int)milestone)) != 0;

	private static int DailyCompletedCount()
	{
		var daily = DailySanctuarySystem.Instance;
		if ( daily is null ) return 0;
		var count = 0;
		for ( var i = 0; i < 8; i++ )
			if ( (daily.CompletedMask & (1 << i)) != 0 )
				count++;
		return count;
	}

	public void Restore( int completedMask, int points, bool eventGranted )
	{
		if ( !Networking.IsHost ) return;
		CompletedMask = completedMask;
		MomentumPoints = Math.Max( 0, points );
		MomentumEventGranted = eventGranted;
		_nextCheck = 1f;
	}
}

public static class AnimalLineage
{
	public static AnimalLineageSummary For( AnimalComponent animal )
	{
		if ( animal is null )
			return default;

		var parents = AnimalRegistry.All
			.Where( a => a.AnimalId == animal.Genome.ParentAId || a.AnimalId == animal.Genome.ParentBId )
			.OrderBy( a => a.AnimalName )
			.ToList();

		var parentIds = new[] { animal.Genome.ParentAId, animal.Genome.ParentBId }
			.Where( id => !string.IsNullOrEmpty( id ) )
			.ToHashSet();

		var siblings = AnimalRegistry.All
			.Where( a => a != animal && parentIds.Count > 0 )
			.Where( a => parentIds.Contains( a.Genome.ParentAId ) || parentIds.Contains( a.Genome.ParentBId ) )
			.OrderBy( a => a.AnimalName )
			.ToList();

		var offspring = AnimalRegistry.All
			.Where( a => a.Genome.ParentAId == animal.AnimalId || a.Genome.ParentBId == animal.AnimalId )
			.OrderBy( a => a.AnimalName )
			.ToList();

		var childIds = offspring.Select( a => a.AnimalId ).ToHashSet();
		var grandOffspring = AnimalRegistry.All
			.Where( a => childIds.Contains( a.Genome.ParentAId ) || childIds.Contains( a.Genome.ParentBId ) )
			.OrderBy( a => a.AnimalName )
			.ToList();

		return new AnimalLineageSummary
		{
			Parents = parents,
			Siblings = siblings,
			Offspring = offspring,
			GrandOffspring = grandOffspring,
		};
	}
}

public static class BiomeIdentity
{
	public static string Label( Biome biome ) => biome switch
	{
		Biome.Forest => "Forest",
		Biome.Rainforest => "Rainforest",
		Biome.Grassland => "Grassland",
		Biome.Desert => "Desert",
		Biome.Arctic => "Arctic",
		Biome.Swamp => "Swamp",
		Biome.Alpine => "Alpine",
		Biome.Coastal => "Coastal",
		_ => biome.ToString()
	};

	public static float HabitatScoreBonus( Biome biome, FaunaSeason season, FaunaWeather weather )
	{
		var bonus = biome switch
		{
			Biome.Grassland => season == FaunaSeason.Spring ? 6f : 0f,
			Biome.Forest => season == FaunaSeason.Autumn ? 6f : 0f,
			Biome.Rainforest or Biome.Swamp => weather == FaunaWeather.Rain ? 8f : 0f,
			Biome.Arctic or Biome.Alpine => season == FaunaSeason.Winter || weather == FaunaWeather.Snow ? 8f : -2f,
			Biome.Desert => weather == FaunaWeather.Heatwave ? 4f : 0f,
			Biome.Coastal => season == FaunaSeason.Summer ? 5f : 0f,
			_ => 0f,
		};

		if ( weather == FaunaWeather.Heatwave && biome is not Biome.Swamp and not Biome.Coastal and not Biome.Desert )
			bonus -= 5f;
		if ( weather == FaunaWeather.Rain && biome == Biome.Grassland )
			bonus += 3f;

		return bonus;
	}

	public static float AnimalHappinessBonus( Biome biome, FaunaSeason season, FaunaWeather weather )
	{
		return biome switch
		{
			Biome.Grassland => season == FaunaSeason.Spring ? 5f : 0f,
			Biome.Forest => season == FaunaSeason.Autumn ? 4f : 0f,
			Biome.Rainforest or Biome.Swamp => weather == FaunaWeather.Rain ? 8f : 0f,
			Biome.Arctic or Biome.Alpine => weather == FaunaWeather.Snow ? 10f : season == FaunaSeason.Summer ? -6f : 0f,
			Biome.Desert => weather == FaunaWeather.Heatwave ? 5f : weather == FaunaWeather.Snow ? -8f : 0f,
			Biome.Coastal => season == FaunaSeason.Summer ? 4f : 0f,
			_ => 0f,
		};
	}

	public static float GuestAppealBonus( Biome biome, FaunaSeason season, FaunaWeather weather )
	{
		if ( weather == FaunaWeather.Festival )
			return 12f;

		return biome switch
		{
			Biome.Grassland => season == FaunaSeason.Spring ? 8f : 0f,
			Biome.Forest => season == FaunaSeason.Autumn ? 10f : 0f,
			Biome.Rainforest or Biome.Swamp => weather == FaunaWeather.Rain ? 6f : 0f,
			Biome.Arctic or Biome.Alpine => weather == FaunaWeather.Snow ? 10f : 0f,
			Biome.Desert => weather == FaunaWeather.Heatwave ? 6f : 0f,
			Biome.Coastal => season == FaunaSeason.Summer ? 7f : 0f,
			_ => 0f,
		};
	}
}

public sealed class WeatherSeasonSystem : Component
{
	public static WeatherSeasonSystem Instance { get; private set; }

	[Sync( SyncFlags.FromHost )] public int Day { get; set; } = 1;
	[Sync( SyncFlags.FromHost )] public FaunaSeason Season { get; set; } = FaunaSeason.Spring;
	[Sync( SyncFlags.FromHost )] public FaunaWeather Weather { get; set; } = FaunaWeather.Clear;

	private TimeUntil _nextDay;
	private TimeSince _dayStarted;

	public string Summary => $"{Season} day {Day} - {Weather}";
	public float DayProgress => ((float)_dayStarted / GameConstants.SanctuaryDayDuration).Clamp( 0f, 1f );
	public string TimeOfDayLabel => DayProgress switch
	{
		< 0.25f => "Morning",
		< 0.55f => "Afternoon",
		< 0.78f => "Evening",
		_ => "Night",
	};
	public string ClockLabel
	{
		get
		{
			var minutes = (int)(6 * 60 + DayProgress * 18 * 60);
			var hour24 = (minutes / 60) % 24;
			var minute = minutes % 60;
			var suffix = hour24 >= 12 ? "PM" : "AM";
			var hour12 = hour24 % 12;
			if ( hour12 == 0 ) hour12 = 12;
			return $"{hour12}:{minute:00} {suffix}";
		}
	}
	public float GuestModifier => Weather switch
	{
		FaunaWeather.Clear => 1f,
		FaunaWeather.Rain => 0.92f,
		FaunaWeather.Heatwave => 0.84f,
		FaunaWeather.Snow => 0.88f,
		FaunaWeather.Festival => 1.25f,
		_ => 1f,
	};

	protected override void OnAwake() => Instance = this;
	protected override void OnDestroy() { if ( Instance == this ) Instance = null; }
	protected override void OnStart()
	{
		_nextDay = GameConstants.SanctuaryDayDuration;
		_dayStarted = 0f;
	}

	protected override void OnFixedUpdate()
	{
		if ( IsProxy || !_nextDay || GameManager.Instance?.GameStarted != true ) return;
		_nextDay = GameConstants.SanctuaryDayDuration;
		AdvanceDay();
	}

	public void AdvanceDay()
	{
		if ( !Networking.IsHost ) return;

		Day++;
		_dayStarted = 0f;
		Season = (FaunaSeason)(((Day - 1) / GameConstants.SeasonLengthDays) % 4);
		Weather = RollWeather();
		ZooState.Instance?.Notify( $"{Summary}. Animal moods and guest turnout shifted.", WeatherIcon );
		DailySanctuarySystem.Instance?.RollDailyGoals();
		SanctuaryEventSystem.Instance?.RollDailyEvent();
		SaveSystem.Instance?.RequestSave();
	}

	private FaunaWeather RollWeather()
	{
		var roll = Game.Random.Float();
		if ( roll < 0.08f ) return FaunaWeather.Festival;
		if ( Season == FaunaSeason.Winter && roll < 0.42f ) return FaunaWeather.Snow;
		if ( Season == FaunaSeason.Summer && roll < 0.35f ) return FaunaWeather.Heatwave;
		if ( roll < 0.38f ) return FaunaWeather.Rain;
		return FaunaWeather.Clear;
	}

	public string WeatherIcon => Weather switch
	{
		FaunaWeather.Rain => "rainy",
		FaunaWeather.Heatwave => "local_fire_department",
		FaunaWeather.Snow => "ac_unit",
		FaunaWeather.Festival => "festival",
		_ => "wb_sunny",
	};

	public void Restore( int day, FaunaSeason season, FaunaWeather weather )
	{
		if ( !Networking.IsHost ) return;
		Day = Math.Max( 1, day );
		Season = season;
		Weather = weather;
		_nextDay = GameConstants.SanctuaryDayDuration;
		_dayStarted = 0f;
	}
}

public sealed class SanctuaryEventSystem : Component
{
	public static SanctuaryEventSystem Instance { get; private set; }

	[Sync( SyncFlags.FromHost )] public string ActiveEventTitle { get; set; } = "";
	[Sync( SyncFlags.FromHost )] public string ActiveEventDetail { get; set; } = "";
	[Sync( SyncFlags.FromHost )] public string ActiveEventIcon { get; set; } = "event";
	[Sync( SyncFlags.FromHost )] public string RareSightingSpeciesId { get; set; } = "";
	[Sync( SyncFlags.FromHost )] public float GuestAppealBonus { get; set; }
	[Sync( SyncFlags.FromHost )] public float IncomeMultiplier { get; set; } = 1f;
	[Sync( SyncFlags.FromHost )] public float RareSpawnMultiplier { get; set; } = 1f;
	[Sync( SyncFlags.FromHost )] public float BuildCostMultiplier { get; set; } = 1f;

	protected override void OnAwake() => Instance = this;
	protected override void OnDestroy() { if ( Instance == this ) Instance = null; }

	public void RollDailyEvent()
	{
		if ( !Networking.IsHost ) return;

		GuestAppealBonus = 0f;
		IncomeMultiplier = 1f;
		RareSpawnMultiplier = 1f;
		BuildCostMultiplier = 1f;
		RareSightingSpeciesId = "";

		var roll = Game.Random.Int( 0, 5 );
		switch ( roll )
		{
			case 0:
				ActiveEventTitle = "Reporter Visit";
				ActiveEventDetail = "Keep ratings high today for extra attendance.";
				ActiveEventIcon = "photo_camera";
				GuestAppealBonus = 18f;
				break;
			case 1:
				ActiveEventTitle = "Celebrity Patron";
				ActiveEventDetail = "Guest spending is boosted while the celebrity tours.";
				ActiveEventIcon = "stars";
				IncomeMultiplier = 1.22f;
				break;
			case 2:
				var rare = Defs.Animals
					.Where( a => a.Rarity >= AnimalRarity.Rare && a.UnlockLevel <= Math.Max( 3, ZooState.Instance?.Level ?? 1 ) + 2 )
					.OrderBy( _ => Game.Random.Int( 0, 9999 ) )
					.FirstOrDefault();
				ActiveEventTitle = "Rare Sighting";
				ActiveEventDetail = rare is null ? "Scouts report unusual tracks near the wilderness." : $"{rare.DisplayName} tracks were seen near the border.";
				ActiveEventIcon = "travel_explore";
				RareSightingSpeciesId = rare?.ResourceName ?? "";
				RareSpawnMultiplier = 3f;
				WildernessSpawner.Instance?.SpawnRareSightingNow( rare );
				break;
			case 3:
				ActiveEventTitle = "Traveling Merchant";
				ActiveEventDetail = "Build prices are 20% lower and guests love themed decor today.";
				ActiveEventIcon = "storefront";
				GuestAppealBonus = 10f;
				BuildCostMultiplier = 0.8f;
				break;
			case 4:
				ActiveEventTitle = "School Field Trip";
				ActiveEventDetail = "Education decor and guides dramatically improve guest satisfaction.";
				ActiveEventIcon = "school";
				GuestAppealBonus = PlaceableRegistry.TotalEducation() * 1.5f + (StaffSystem.Instance?.GuideGuestBonus ?? 0f);
				break;
			default:
				ActiveEventTitle = "Quiet Sanctuary Day";
				ActiveEventDetail = "A peaceful day for breeding, decorating, and slow growth.";
				ActiveEventIcon = "park";
				break;
		}

		ZooState.Instance?.Notify( $"{ActiveEventTitle}: {ActiveEventDetail}", ActiveEventIcon );
		SaveSystem.Instance?.RequestSave();
	}

	public bool IsRareSighting( AnimalDefinition def ) =>
		def is not null && !string.IsNullOrEmpty( RareSightingSpeciesId ) && def.ResourceName == RareSightingSpeciesId;

	public void GrantMomentumEvent()
	{
		if ( !Networking.IsHost ) return;

		ActiveEventTitle = "Opening Week Buzz";
		ActiveEventDetail = "Early momentum is drawing extra guests and improving spending today.";
		ActiveEventIcon = "bolt";
		RareSightingSpeciesId = "";
		GuestAppealBonus = 16f;
		IncomeMultiplier = 1.12f;
		RareSpawnMultiplier = 1.25f;
		BuildCostMultiplier = Math.Min( BuildCostMultiplier <= 0f ? 1f : BuildCostMultiplier, 0.9f );
		SaveSystem.Instance?.RequestSave();
	}

	public void Restore( string title, string detail, string icon, string speciesId, float appealBonus, float incomeMultiplier, float rareSpawnMultiplier, float buildCostMultiplier = 1f )
	{
		if ( !Networking.IsHost ) return;
		ActiveEventTitle = title ?? "";
		ActiveEventDetail = detail ?? "";
		ActiveEventIcon = string.IsNullOrEmpty( icon ) ? "event" : icon;
		RareSightingSpeciesId = speciesId ?? "";
		GuestAppealBonus = appealBonus;
		IncomeMultiplier = incomeMultiplier <= 0f ? 1f : incomeMultiplier;
		RareSpawnMultiplier = rareSpawnMultiplier <= 0f ? 1f : rareSpawnMultiplier;
		BuildCostMultiplier = buildCostMultiplier <= 0f ? 1f : buildCostMultiplier;
	}
}

public sealed class DailySanctuarySystem : Component
{
	public static DailySanctuarySystem Instance { get; private set; }

	[Sync( SyncFlags.FromHost )] public int DailySeed { get; set; }
	[Sync( SyncFlags.FromHost )] public int CompletedMask { get; set; }
	[Sync( SyncFlags.FromHost )] public int StartingCleared { get; set; }
	[Sync( SyncFlags.FromHost )] public int StartingGuests { get; set; }
	[Sync( SyncFlags.FromHost )] public long StartingEarned { get; set; }
	[Sync( SyncFlags.FromHost )] public int StartingBred { get; set; }
	[Sync( SyncFlags.FromHost )] public int StartingCaught { get; set; }
	[Sync( SyncFlags.FromHost )] public int StartingPlaceables { get; set; }

	private TimeUntil _nextCheck;

	protected override void OnAwake() => Instance = this;
	protected override void OnDestroy() { if ( Instance == this ) Instance = null; }
	protected override void OnStart()
	{
		_nextCheck = 2f;
		if ( Networking.IsHost && DailySeed == 0 )
			RollDailyGoals();
	}

	protected override void OnFixedUpdate()
	{
		if ( IsProxy || !_nextCheck || GameManager.Instance?.GameStarted != true ) return;
		_nextCheck = 2f;
		CheckCompletions();
	}

	public void RollDailyGoals()
	{
		if ( !Networking.IsHost ) return;
		var state = ZooState.Instance;
		DailySeed = (WeatherSeasonSystem.Instance?.Day ?? 1) * 17 + (int)(state?.Prestige ?? 0);
		CompletedMask = 0;
		StartingCleared = TerrainObstacleSystem.Instance?.TotalCleared ?? 0;
		StartingGuests = GuestSystem.Instance?.GuestCount ?? 0;
		StartingEarned = state?.TotalEarned ?? 0;
		StartingBred = state?.TotalAnimalsBred ?? 0;
		StartingCaught = state?.TotalAnimalsCaught ?? 0;
		StartingPlaceables = PlaceableRegistry.Count;
	}

	public IReadOnlyList<SanctuaryDailyGoal> Goals()
	{
		var kinds = KindsForToday();
		return kinds.Select( BuildGoal ).ToList();
	}

	private IEnumerable<SanctuaryDailyKind> KindsForToday()
	{
		var all = Enum.GetValues( typeof( SanctuaryDailyKind ) ).Cast<SanctuaryDailyKind>().ToList();
		var seed = DailySeed == 0 ? 1 : DailySeed;
		return all.OrderBy( k => (((int)k + 3) * 97 + seed * 31) % 997 ).Take( 3 );
	}

	private SanctuaryDailyGoal BuildGoal( SanctuaryDailyKind kind )
	{
		var state = ZooState.Instance;
		var level = Math.Max( 1, state?.Level ?? 1 );

		return kind switch
		{
			SanctuaryDailyKind.ClearObstacles => new()
			{
				Icon = "forest",
				Title = "Fresh Paths",
				Detail = "Clear land to make room for guests and habitats.",
				Progress = (TerrainObstacleSystem.Instance?.TotalCleared ?? 0) - StartingCleared,
				Target = 2 + level / 4,
			},
			SanctuaryDailyKind.AttractGuests => new()
			{
				Icon = "groups",
				Title = "Crowd Builder",
				Detail = "Raise today's live guest count.",
				Progress = Math.Max( 0, (GuestSystem.Instance?.GuestCount ?? 0) - StartingGuests ),
				Target = 15 + level * 4,
			},
			SanctuaryDailyKind.EarnIncome => new()
			{
				Icon = "paid",
				Title = "Healthy Till",
				Detail = "Earn visitor income today.",
				Progress = (int)Math.Max( 0, (state?.TotalEarned ?? 0) - StartingEarned ),
				Target = 500 + level * 180,
			},
			SanctuaryDailyKind.BreedAnimals => new()
			{
				Icon = "favorite",
				Title = "New Bloodline",
				Detail = "Breed animals and grow your family trees.",
				Progress = Math.Max( 0, (state?.TotalAnimalsBred ?? 0) - StartingBred ),
				Target = 1,
			},
			SanctuaryDailyKind.CatchWildlife => new()
			{
				Icon = "travel_explore",
				Title = "Field Rescue",
				Detail = "Catch wildlife from the surrounding wilderness.",
				Progress = Math.Max( 0, (state?.TotalAnimalsCaught ?? 0) - StartingCaught ),
				Target = 1,
			},
			_ => new()
			{
				Icon = "yard",
				Title = "Decor Day",
				Detail = "Place decor, nature, or education pieces.",
				Progress = Math.Max( 0, PlaceableRegistry.Count - StartingPlaceables ),
				Target = 3,
			},
		};
	}

	private void CheckCompletions()
	{
		var goals = Goals();
		for ( var i = 0; i < goals.Count; i++ )
		{
			if ( (CompletedMask & (1 << i)) != 0 || !goals[i].Complete ) continue;
			CompletedMask |= 1 << i;

			var reward = 350 + (ZooState.Instance?.Level ?? 1) * 90;
			ZooState.Instance?.AddMoney( reward );
			ZooState.Instance?.AddXp( 50 );
			ZooState.Instance?.Notify( $"Daily goal complete: {goals[i].Title} (+${reward:n0})", goals[i].Icon );
			SaveSystem.Instance?.RequestSave();
		}
	}

	public void Restore( int seed, int mask, int cleared, int guests, long earned, int bred, int caught, int placeables )
	{
		if ( !Networking.IsHost ) return;
		DailySeed = seed;
		CompletedMask = mask;
		StartingCleared = cleared;
		StartingGuests = guests;
		StartingEarned = earned;
		StartingBred = bred;
		StartingCaught = caught;
		StartingPlaceables = placeables;
		_nextCheck = 2f;
	}
}

public sealed class StaffSystem : Component
{
	public static StaffSystem Instance { get; private set; }

	[Sync( SyncFlags.FromHost )] public int Keepers { get; set; }
	[Sync( SyncFlags.FromHost )] public int Cleaners { get; set; }
	[Sync( SyncFlags.FromHost )] public int Guides { get; set; }
	[Sync( SyncFlags.FromHost )] public int Vets { get; set; }

	public float KeeperHabitatBonus => Keepers * 2.5f;
	public float CleanerRecoveryBonus => Cleaners * 0.55f;
	public float GuideGuestBonus => Guides * 4f;
	public float VetHealthBonus => Vets * 2f;
	public int PayrollPerMinute => (int)MathF.Round( (Keepers + Cleaners + Guides + Vets) * GameConstants.StaffCostPerMinute * GameConstants.GamePaceMultiplier );
	public int TotalStaff => Keepers + Cleaners + Guides + Vets;
	public int StaffCap => Math.Max( 2, (PlotSystem.Instance?.PlotCount ?? 1) * 2 + (FranchiseSystem.Instance?.FranchiseRank ?? 0) );

	protected override void OnAwake() => Instance = this;
	protected override void OnDestroy() { if ( Instance == this ) Instance = null; }

	[Rpc.Host]
	public void RequestHire( StaffRole role )
	{
		if ( !RpcAuthorization.IsOwnerCaller() ) return;

		var state = ZooState.Instance;
		if ( TotalStaff >= StaffCap )
		{
			state?.Notify( $"Staff cap reached ({TotalStaff}/{StaffCap}). Expand land or franchise rank.", "badge" );
			return;
		}

		var cost = HireCost( role );
		if ( state is null || !state.TrySpend( cost ) )
		{
			state?.Notify( "Not enough money to hire staff.", "block" );
			return;
		}

		switch ( role )
		{
			case StaffRole.Keeper: Keepers++; break;
			case StaffRole.Cleaner: Cleaners++; break;
			case StaffRole.Guide: Guides++; break;
			case StaffRole.Vet: Vets++; break;
		}

		state.Notify( $"Hired {role}. Payroll increased by ${GameConstants.StaffCostPerMinute}/min.", "badge" );
		SaveSystem.Instance?.RequestSave();
	}

	public int CountFor( StaffRole role ) => role switch
	{
		StaffRole.Keeper => Keepers,
		StaffRole.Cleaner => Cleaners,
		StaffRole.Guide => Guides,
		StaffRole.Vet => Vets,
		_ => 0,
	};

	public int HireCost( StaffRole role ) => GameConstants.StaffHireCost + CountFor( role ) * 450;

	public void Restore( int keepers, int cleaners, int guides, int vets )
	{
		if ( !Networking.IsHost ) return;
		Keepers = Math.Max( 0, keepers );
		Cleaners = Math.Max( 0, cleaners );
		Guides = Math.Max( 0, guides );
		Vets = Math.Max( 0, vets );
	}
}

public sealed class ResearchSystem : Component
{
	public static ResearchSystem Instance { get; private set; }

	[Sync( SyncFlags.FromHost )] public int HabitatCare { get; set; }
	[Sync( SyncFlags.FromHost )] public int AnimalCare { get; set; }
	[Sync( SyncFlags.FromHost )] public int GuestComfort { get; set; }
	[Sync( SyncFlags.FromHost )] public int FieldTools { get; set; }
	[Sync( SyncFlags.FromHost )] public int DecorationDesign { get; set; }

	public float HabitatScoreBonus => HabitatCare * 3f;
	public float AnimalCareBonus => AnimalCare * 3f;
	public float GuestComfortBonus => GuestComfort * 4f;
	public float FieldCatchBonus => FieldTools * 0.04f;
	public float DecorationMultiplier => 1f + DecorationDesign * 0.08f;

	protected override void OnAwake() => Instance = this;
	protected override void OnDestroy() { if ( Instance == this ) Instance = null; }

	public int LevelOf( ResearchNode node ) => node switch
	{
		ResearchNode.HabitatCare => HabitatCare,
		ResearchNode.AnimalCare => AnimalCare,
		ResearchNode.GuestComfort => GuestComfort,
		ResearchNode.FieldTools => FieldTools,
		ResearchNode.DecorationDesign => DecorationDesign,
		_ => 0,
	};

	[Rpc.Host]
	public void RequestResearch( ResearchNode node )
	{
		if ( !RpcAuthorization.IsOwnerCaller() ) return;

		var current = LevelOf( node );
		if ( current >= GameConstants.ResearchMaxRank )
		{
			ZooState.Instance?.Notify( $"{NodeTitle( node )} research is complete.", "science" );
			return;
		}

		if ( !CanResearch( node, out var requirement ) )
		{
			ZooState.Instance?.Notify( requirement, "lock" );
			return;
		}

		var cost = GameConstants.ResearchBaseCost + current * GameConstants.ResearchCostGrowth;
		var state = ZooState.Instance;
		if ( state is null || !state.TrySpend( cost ) )
		{
			state?.Notify( $"Research needs ${cost:n0}.", "block" );
			return;
		}

		switch ( node )
		{
			case ResearchNode.HabitatCare: HabitatCare++; break;
			case ResearchNode.AnimalCare: AnimalCare++; break;
			case ResearchNode.GuestComfort: GuestComfort++; break;
			case ResearchNode.FieldTools: FieldTools++; break;
			case ResearchNode.DecorationDesign: DecorationDesign++; break;
		}

		state.AddXp( 75 );
		state.Notify( $"Research complete: {NodeTitle( node )} rank {LevelOf( node )}", "science" );
		SaveSystem.Instance?.RequestSave();
	}

	public static string NodeTitle( ResearchNode node ) => node switch
	{
		ResearchNode.HabitatCare => "Habitat Care",
		ResearchNode.AnimalCare => "Animal Care",
		ResearchNode.GuestComfort => "Guest Comfort",
		ResearchNode.FieldTools => "Field Tools",
		ResearchNode.DecorationDesign => "Decoration Design",
		_ => node.ToString(),
	};

	public static string NodeEffect( ResearchNode node ) => node switch
	{
		ResearchNode.HabitatCare => "+3 habitat score per rank",
		ResearchNode.AnimalCare => "+3 animal mood/health support per rank",
		ResearchNode.GuestComfort => "+4 satisfaction per rank",
		ResearchNode.FieldTools => "easier catches and field work",
		ResearchNode.DecorationDesign => "stronger decor beauty/education/comfort",
		_ => "",
	};

	public bool CanResearch( ResearchNode node, out string requirement )
	{
		requirement = "";
		var current = LevelOf( node );
		var levelRequired = 1 + current * 2;
		if ( (ZooState.Instance?.Level ?? 1) < levelRequired )
		{
			requirement = $"{NodeTitle( node )} rank {current + 1} requires zoo level {levelRequired}.";
			return false;
		}

		if ( node == ResearchNode.FieldTools && AnimalCare <= 0 )
		{
			requirement = "Field Tools requires Animal Care rank 1.";
			return false;
		}

		if ( node == ResearchNode.DecorationDesign && GuestComfort <= 0 )
		{
			requirement = "Decoration Design requires Guest Comfort rank 1.";
			return false;
		}

		return true;
	}

	public string RequirementText( ResearchNode node )
	{
		if ( LevelOf( node ) >= GameConstants.ResearchMaxRank )
			return "Complete";
		return CanResearch( node, out var requirement ) ? "Available" : requirement;
	}

	public void Restore( int habitat, int animal, int guest, int field, int decor )
	{
		if ( !Networking.IsHost ) return;
		HabitatCare = habitat.Clamp( 0, GameConstants.ResearchMaxRank );
		AnimalCare = animal.Clamp( 0, GameConstants.ResearchMaxRank );
		GuestComfort = guest.Clamp( 0, GameConstants.ResearchMaxRank );
		FieldTools = field.Clamp( 0, GameConstants.ResearchMaxRank );
		DecorationDesign = decor.Clamp( 0, GameConstants.ResearchMaxRank );
	}
}

public sealed class FranchiseSystem : Component
{
	public static FranchiseSystem Instance { get; private set; }

	[Sync( SyncFlags.FromHost )] public int FranchiseRank { get; set; }
	[Sync( SyncFlags.FromHost )] public int LegacyTokens { get; set; }
	[Sync( SyncFlags.FromHost )] public int BranchExpansions { get; set; }

	public float LegacyGuestMultiplier => 1f + LegacyTokens * 0.02f;
	public float LegacyBreedingBonus => LegacyTokens * 0.01f;

	protected override void OnAwake() => Instance = this;
	protected override void OnDestroy() { if ( Instance == this ) Instance = null; }

	public bool CanPrestige()
	{
		var state = ZooState.Instance;
		return state is not null && state.Level >= GameConstants.MaxLevel
			&& (CollectionSystem.Instance?.CompletionPercent ?? 0f) >= 50f
			&& (GuestSystem.Instance?.ZooRating ?? 0f) >= 4f;
	}

	[Rpc.Host]
	public void RequestFranchisePrestige()
	{
		if ( !RpcAuthorization.IsOwnerCaller() ) return;

		if ( !CanPrestige() )
		{
			ZooState.Instance?.Notify( "Franchise prestige requires max level, 50% codex, and a 4-star zoo.", "lock" );
			return;
		}

		FranchiseRank++;
		LegacyTokens += 3 + Math.Max( 0, ZooState.Instance.Prestige / 20 );
		if ( PlotSystem.Instance?.TryGrantFreeExpansion() == true )
			BranchExpansions++;
		ZooState.Instance.AddPrestige( 10 );
		ZooState.Instance.Notify( $"Franchise rank {FranchiseRank}! Legacy tokens: {LegacyTokens}. A branch expansion grant was applied if land was available.", "workspace_premium" );
		SaveSystem.Instance?.RequestSave();
	}

	public void Restore( int rank, int tokens, int branches = 0 )
	{
		if ( !Networking.IsHost ) return;
		FranchiseRank = Math.Max( 0, rank );
		LegacyTokens = Math.Max( 0, tokens );
		BranchExpansions = Math.Max( 0, branches );
	}
}
