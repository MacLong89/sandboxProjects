namespace Fauna2;

public enum ObjectiveAction
{
	None,
	ClearLand,
	BuildHabitats,
	BuildEntrance,
	BuildPaths,
	Market,
	StatsGuests,
	BuildUtility,
	BuildAmenities,
	BuildRestroom,
	BuildRestaurant,
	BuildShop,
	StatsFinances,
	Progression,
	BreedHelp,
	Codex,
	ExpandLand,
	StatsOverview,
	CatchHelp,
	PlaceAnimal,
	PrestigeHelp,
	FindWildlife,
}

public sealed class ObjectiveDef
{
	public string Title { get; init; }
	public string Description { get; init; }
	public int RewardMoney { get; init; }
	public int RewardXp { get; init; }
	public ObjectiveAction Action { get; init; }
}

/// <summary>Lambda-free goal checks — stored Func delegates fail in sbox hotload.</summary>
public static class ObjectiveRules
{
	public static bool IsComplete( int index ) => index switch
	{
		0 => StarterGoalGuide.HasSpottedWildAnimal(),
		1 => (ZooState.Instance?.TotalAnimalsCaught ?? 0) > 0,
		2 => PathNetwork.HasEntrance,
		3 => (TerrainObstacleSystem.Instance?.TotalCleared ?? 0) > 0,
		4 => PathNetwork.HasGuestAccess,
		5 => StarterGoalGuide.HasTutorialHabitat(),
		6 => StarterGoalGuide.HasAnimalInHabitat(),
		7 => PlaceableRegistry.RestroomCount > 0,
		8 => PlaceableRegistry.RestaurantCount > 0,
		9 => PlaceableRegistry.ShopCount > 0,
		10 => ObjectiveSystem.Instance?.GuestRatingsReviewed == true,
		11 => (GuestSystem.Instance?.GuestCount ?? 0) >= 10,
		12 => (ZooState.Instance?.TotalAnimalsBred ?? 0) > 0,
		13 => (PlotSystem.Instance?.PlotCount ?? 1) > 1,
		14 => (CollectionSystem.Instance?.DiscoveredSpeciesCount ?? 0) >= 5,
		15 => (CollectionSystem.Instance?.CompletionPercent ?? 0f) >= 50f,
		16 => (GuestSystem.Instance?.ZooRating ?? 0f) >= 4f,
		17 => (EconomySystem.Instance?.IncomePerMinute ?? 0f) > 0f,
		18 => (GuestSystem.Instance?.GuestCount ?? 0) >= 100,
		19 => (ZooState.Instance?.TotalAnimalsBred ?? 0) >= 5,
		20 => (PlotSystem.Instance?.PlotCount ?? 1) >= 4,
		21 => (CollectionSystem.Instance?.CompletionPercent ?? 0f) >= 99.5f,
		_ => false,
	};

	public static float ProgressFraction( int index ) => index switch
	{
		0 => StarterGoalGuide.HasSpottedWildAnimal() ? 1f : 0f,
		1 => (ZooState.Instance?.TotalAnimalsCaught ?? 0) > 0 ? 1f : 0f,
		2 => PathNetwork.HasEntrance ? 1f : 0f,
		3 => (TerrainObstacleSystem.Instance?.TotalCleared ?? 0) > 0 ? 1f : 0f,
		4 => PathNetwork.HasGuestAccess ? 1f : 0f,
		5 => StarterGoalGuide.HasTutorialHabitat() ? 1f : 0f,
		6 => StarterGoalGuide.HasAnimalInHabitat() ? 1f : 0f,
		7 => PlaceableRegistry.RestroomCount > 0 ? 1f : 0f,
		8 => PlaceableRegistry.RestaurantCount > 0 ? 1f : 0f,
		9 => PlaceableRegistry.ShopCount > 0 ? 1f : 0f,
		10 => ObjectiveSystem.Instance?.GuestRatingsReviewed == true ? 1f : 0f,
		11 => MathX.Clamp( (GuestSystem.Instance?.GuestCount ?? 0) / 10f, 0f, 1f ),
		12 => (ZooState.Instance?.TotalAnimalsBred ?? 0) > 0 ? 1f : 0f,
		13 => (PlotSystem.Instance?.PlotCount ?? 1) > 1 ? 1f : 0f,
		14 => MathX.Clamp( (CollectionSystem.Instance?.DiscoveredSpeciesCount ?? 0) / 5f, 0f, 1f ),
		15 => MathX.Clamp( (CollectionSystem.Instance?.CompletionPercent ?? 0f) / 50f, 0f, 1f ),
		16 => MathX.Clamp( (GuestSystem.Instance?.ZooRating ?? 0f) / 4f, 0f, 1f ),
		17 => (EconomySystem.Instance?.IncomePerMinute ?? 0f) > 0f ? 1f : 0f,
		18 => MathX.Clamp( (GuestSystem.Instance?.GuestCount ?? 0) / 100f, 0f, 1f ),
		19 => MathX.Clamp( (ZooState.Instance?.TotalAnimalsBred ?? 0) / 5f, 0f, 1f ),
		20 => MathX.Clamp( (PlotSystem.Instance?.PlotCount ?? 1) / 4f, 0f, 1f ),
		21 => MathX.Clamp( (CollectionSystem.Instance?.CompletionPercent ?? 0f) / 99.5f, 0f, 1f ),
		_ => 0f,
	};

	public static string ProgressLabel( int index ) => index switch
	{
		0 => StarterGoalGuide.HasSpottedWildAnimal() ? "Spotted" : "Not yet",
		1 => (ZooState.Instance?.TotalAnimalsCaught ?? 0) > 0 ? "Caught" : "Not yet",
		2 => PathNetwork.HasEntrance ? "Entrance built" : "Not placed",
		3 => (TerrainObstacleSystem.Instance?.TotalCleared ?? 0) > 0 ? "Cleared" : "Not yet",
		4 => PathNetwork.HasGuestAccess ? "Paths connected" : "Not connected",
		5 => StarterGoalGuide.HabitatProgressLabel(),
		6 => StarterGoalGuide.AnimalProgressLabel(),
		7 => PlaceableRegistry.RestroomCount > 0 ? "Restroom built" : "Not placed",
		8 => PlaceableRegistry.RestaurantCount > 0 ? "Food stand built" : "Not placed",
		9 => PlaceableRegistry.ShopCount > 0 ? "Shop built" : "Not placed",
		10 => ObjectiveSystem.Instance?.GuestRatingsReviewed == true ? "Reviewed" : "Not yet",
		11 => $"{GuestSystem.Instance?.GuestCount ?? 0}/10 guests",
		12 => (ZooState.Instance?.TotalAnimalsBred ?? 0) > 0 ? "Bred" : "Not yet",
		13 => (PlotSystem.Instance?.PlotCount ?? 1) > 1 ? "Expanded" : "1 plot",
		14 => $"{CollectionSystem.Instance?.DiscoveredSpeciesCount ?? 0}/5 species",
		15 => $"{CollectionSystem.Instance?.CompletionPercent ?? 0f:0}% / 50%",
		16 => $"{GuestSystem.Instance?.ZooRating ?? 0f:0.0} / 4.0 stars",
		17 => (EconomySystem.Instance?.IncomePerMinute ?? 0f) > 0f ? "Profitable" : "Still investing",
		18 => $"{GuestSystem.Instance?.GuestCount ?? 0}/100 guests",
		19 => $"{ZooState.Instance?.TotalAnimalsBred ?? 0}/5 bred",
		20 => $"{PlotSystem.Instance?.PlotCount ?? 1}/4 plots",
		21 => $"{CollectionSystem.Instance?.CompletionPercent ?? 0f:0}% / 100%",
		_ => "",
	};

	public static string DescriptionWithProgress( int index )
	{
		if ( index < 0 || index >= ObjectiveSystem.Objectives.Count )
			return "";

		var description = index switch
		{
			5 => StarterGoalGuide.HabitatGoalDescription(),
			_ => ObjectiveSystem.Objectives[index].Description,
		};

		var progress = ProgressLabel( index );
		if ( string.IsNullOrWhiteSpace( progress ) )
			return description;

		return $"{description} ({progress})";
	}
}

public sealed class ObjectiveSystem : Component
{
	public static ObjectiveSystem Instance { get; private set; }

	/// <summary>Core loop through amenities + ratings (indices 0–10).</summary>
	public const int TutorialGoalCount = 11;

	/// <summary>House-your-catch goal.</summary>
	public const int PlaceAnimalGoalIndex = 6;

	/// <summary>Open Stats / guest wants tip.</summary>
	public const int GuestRatingsGoalIndex = 10;

	[Sync( SyncFlags.FromHost )] public int CurrentIndex { get; set; }

	/// <summary>True once the player opens Stats during the ratings tutorial step.</summary>
	[Sync( SyncFlags.FromHost )] public bool GuestRatingsReviewed { get; set; }

	private TimeUntil _nextCheck;

	public static readonly List<ObjectiveDef> Objectives = new()
	{
		new()
		{
			Title = "Find a wild animal",
			Description = "Walk into the wilderness and get close to wildlife.",
			RewardMoney = 100, RewardXp = 15,
			Action = ObjectiveAction.FindWildlife,
		},
		new()
		{
			Title = "Catch a wild animal",
			Description = "Press E near wildlife and time your catch on the green zone.",
			RewardMoney = 400, RewardXp = 60,
			Action = ObjectiveAction.CatchHelp,
		},
		new()
		{
			Title = "Build your zoo entrance",
			Description = "Place an entrance on the edge of your land.",
			RewardMoney = 300, RewardXp = 25,
			Action = ObjectiveAction.BuildEntrance,
		},
		new()
		{
			Title = "Clear a tree or boulder",
			Description = "Click a tree or rock on your land, then press Clear to open space for paths and buildings.",
			RewardMoney = 150, RewardXp = 15,
			Action = ObjectiveAction.ClearLand,
		},
		new()
		{
			Title = "Connect a path",
			Description = "Lay path tiles from your entrance — at least one path must touch the entrance so guests can arrive.",
			RewardMoney = 200, RewardXp = 20,
			Action = ObjectiveAction.BuildPaths,
		},
		new()
		{
			Title = "Build a habitat",
			Description = "Place a habitat for your catch — match their biome when you can.",
			RewardMoney = 500, RewardXp = 30,
			Action = ObjectiveAction.BuildHabitats,
		},
		new()
		{
			Title = "House your animal",
			Description = "Walk to your habitat and press E to release your catch into it.",
			RewardMoney = 250, RewardXp = 40,
			Action = ObjectiveAction.PlaceAnimal,
		},
		new()
		{
			Title = "Build a restroom",
			Description = "Guests need a restroom before satisfaction stays high.",
			RewardMoney = 200, RewardXp = 20,
			Action = ObjectiveAction.BuildRestroom,
		},
		new()
		{
			Title = "Build a food stand",
			Description = "Place dining along your paths so guests can grab a bite.",
			RewardMoney = 200, RewardXp = 20,
			Action = ObjectiveAction.BuildRestaurant,
		},
		new()
		{
			Title = "Build a gift shop",
			Description = "Place a shop near your paths — guests love souvenirs and it earns extra income.",
			RewardMoney = 250, RewardXp = 25,
			Action = ObjectiveAction.BuildShop,
		},
		new()
		{
			Title = "Check guest ratings",
			Description = "Open Stats to see your rating and what guests want next.",
			RewardMoney = 150, RewardXp = 20,
			Action = ObjectiveAction.StatsOverview,
		},
		new()
		{
			Title = "Welcome 10 guests",
			Description = "Guests arrive through your entrance and bring ticket revenue.",
			RewardMoney = 500, RewardXp = 60,
			Action = ObjectiveAction.StatsGuests,
		},
		new()
		{
			Title = "Breed your first animal",
			Description = "Two happy adults in a good habitat can breed.",
			RewardMoney = 1000, RewardXp = 150,
			Action = ObjectiveAction.BreedHelp,
		},
		new()
		{
			Title = "Expand your sanctuary",
			Description = "Buy a neighbouring land plot.",
			RewardMoney = 500, RewardXp = 500,
			Action = ObjectiveAction.ExpandLand,
		},
		new()
		{
			Title = "Discover 5 species",
			Description = "Fill out your codex with five different animals.",
			RewardMoney = 600, RewardXp = 80,
			Action = ObjectiveAction.Codex,
		},
		new()
		{
			Title = "Half the codex",
			Description = "Discover at least half of all species and variants.",
			RewardMoney = 2000, RewardXp = 250,
			Action = ObjectiveAction.Codex,
		},
		new()
		{
			Title = "Four-star zoo",
			Description = "Raise your guest rating to 4.0 or higher.",
			RewardMoney = 1500, RewardXp = 200,
			Action = ObjectiveAction.StatsOverview,
		},
		new()
		{
			Title = "Profitable empire",
			Description = "Reach positive net cash flow.",
			RewardMoney = 1000, RewardXp = 150,
			Action = ObjectiveAction.StatsFinances,
		},
		new()
		{
			Title = "Crowd pleaser",
			Description = "Host 100 guests at once.",
			RewardMoney = 2500, RewardXp = 300,
			Action = ObjectiveAction.StatsGuests,
		},
		new()
		{
			Title = "Breeding program",
			Description = "Breed five animals in your habitats.",
			RewardMoney = 1800, RewardXp = 220,
			Action = ObjectiveAction.BreedHelp,
		},
		new()
		{
			Title = "Sanctuary sprawl",
			Description = "Own four or more land plots.",
			RewardMoney = 2200, RewardXp = 280,
			Action = ObjectiveAction.ExpandLand,
		},
		new()
		{
			Title = "Zoo legend",
			Description = "Complete the codex.",
			RewardMoney = 5000, RewardXp = 500,
			Action = ObjectiveAction.Codex,
		},
	};

	public ObjectiveDef Current =>
		CurrentIndex >= 0 && CurrentIndex < Objectives.Count ? Objectives[CurrentIndex] : null;

	public bool AllComplete => CurrentIndex >= Objectives.Count;

	public string GetProgressLabel() =>
		CurrentIndex >= 0 && CurrentIndex < Objectives.Count
			? ObjectiveRules.ProgressLabel( CurrentIndex )
			: "";

	public float GetProgressFraction() =>
		CurrentIndex >= 0 && CurrentIndex < Objectives.Count
			? ObjectiveRules.ProgressFraction( CurrentIndex )
			: 0f;

	public string GetGoalTitle() => GetGoalTitle( CurrentIndex );

	public string GetGoalTitle( int index ) => index switch
	{
		5 => StarterGoalGuide.HabitatGoalTitle(),
		_ => index >= 0 && index < Objectives.Count ? Objectives[index].Title : "",
	};

	public string GetDescriptionWithProgress() =>
		ObjectiveRules.DescriptionWithProgress( CurrentIndex );

	/// <summary>After load, rewind if the save index is ahead of world progress.</summary>
	public void RewindIndexIfBehindWorld()
	{
		if ( IsProxy || AllComplete ) return;

		for ( var i = 0; i < CurrentIndex && i < Objectives.Count; i++ )
		{
			if ( !ObjectiveRules.IsComplete( i ) )
			{
				CurrentIndex = i;
				return;
			}
		}
	}

	/// <summary>Award rewards for goals already satisfied when a save loads.</summary>
	public void CatchUpGoalsAfterLoad()
	{
		if ( IsProxy ) return;
		RewindIndexIfBehindWorld();
		TryAdvanceGoals( ignoreGameStarted: true );
	}

	protected override void OnAwake()
	{
		Instance = this;
		GameEvents.ZooModified += OnWorldChanged;
		GameEvents.HabitatPlaced += OnWorldChanged;
		GameEvents.AnimalSpawned += OnAnimalChanged;
		GameEvents.AnimalBred += OnAnimalChanged;
		GameEvents.PlotPurchased += OnWorldChanged;
		GameEvents.SpeciesDiscovered += OnWorldChangedId;
	}

	protected override void OnDestroy()
	{
		GameEvents.ZooModified -= OnWorldChanged;
		GameEvents.HabitatPlaced -= OnWorldChanged;
		GameEvents.AnimalSpawned -= OnAnimalChanged;
		GameEvents.AnimalBred -= OnAnimalChanged;
		GameEvents.PlotPurchased -= OnWorldChanged;
		GameEvents.SpeciesDiscovered -= OnWorldChangedId;

		if ( Instance == this ) Instance = null;
	}

	private void OnWorldChanged() => TryAdvanceGoals();
	private void OnWorldChangedId( string _ ) => TryAdvanceGoals();
	private void OnAnimalChanged( AnimalComponent _ ) => TryAdvanceGoals();

	protected override void OnFixedUpdate()
	{
		if ( IsProxy || !_nextCheck ) return;
		if ( GameManager.Instance is null || !GameManager.Instance.GameStarted ) return;
		// Find-wildlife checks player proximity — tick a bit faster during that goal.
		_nextCheck = CurrentIndex == 0 ? 0.2f : 0.5f;
		TryAdvanceGoals();
		UI.UiState.TryShowOnboardingTip();
	}

	private void TryAdvanceGoals( bool ignoreGameStarted = false )
	{
		if ( IsProxy ) return;
		if ( !ignoreGameStarted && (GameManager.Instance is null || !GameManager.Instance.GameStarted) )
			return;

		while ( CurrentIndex >= 0 && CurrentIndex < Objectives.Count && ObjectiveRules.IsComplete( CurrentIndex ) )
			CompleteCurrentGoal();
	}

	private void CompleteCurrentGoal()
	{
		if ( CurrentIndex < 0 || CurrentIndex >= Objectives.Count ) return;

		var completedIndex = CurrentIndex;
		var state = ZooState.Instance;
		if ( !state.IsValid() ) return;

		var rewardMoney = Objectives[completedIndex].RewardMoney;
		var rewardXp = Objectives[completedIndex].RewardXp;
		if ( rewardMoney > 0 ) state.AddMoney( rewardMoney );
		if ( rewardXp > 0 ) state.AddXp( rewardXp );

		if ( completedIndex == TutorialGoalCount - 1 )
			UI.UiState.ShowCelebration( "Tutorial complete!", "You know the loop — amenities, ratings, and growth from here.", "flag" );
		else
			ShowCompletionCelebration( completedIndex );

		CurrentIndex++;

		UI.UiState.NotifyGoalsChanged();
		// Dismiss the tip for this step and immediately show the next center card.
		UI.UiState.AdvanceOnboardingTipAfterGoal( completedIndex );

		// No bottom-right toasts for goal complete / next goal — too noisy when
		// rewards also grant XP (level-up toast + center celebration already cover wins).

		if ( AllComplete )
			UI.UiState.ShowCelebration( "All goals complete!", "You have mastered every milestone. Keep growing your sanctuary.", "emoji_events" );
	}

	private static void ShowCompletionCelebration( int completedIndex )
	{
		switch ( completedIndex )
		{
			case 1:
				UI.UiState.ShowCelebration( "First catch!", "Build an entrance, clear land, path, and habitat — then house them.", "pets" );
				break;
			case 3:
				UI.UiState.ShowCelebration( "Land cleared!", "Space opened — lay a path from your entrance next.", "forest" );
				break;
			case 4:
				UI.UiState.ShowCelebration( "Paths connected!", "Guests can reach your zoo once you have animals on show.", "follow_the_signs" );
				break;
			case 6:
				UI.UiState.ShowCelebration( "First resident!", "Next up: restrooms, food, and a shop for your guests.", "pets" );
				break;
			case 7:
				UI.UiState.ShowCelebration( "Restroom ready!", "Guests stay happier with facilities on the path.", "wc" );
				break;
			case 8:
				UI.UiState.ShowCelebration( "Food stand ready!", "Guests can grab a bite along your paths.", "restaurant" );
				break;
			case 9:
				UI.UiState.ShowCelebration( "Shop open!", "Souvenirs earn income — now check what guests want.", "storefront" );
				break;
		}
	}

	/// <summary>Called when the player opens Stats — completes the ratings tutorial goal.</summary>
	public void MarkGuestRatingsReviewed()
	{
		if ( IsProxy || GuestRatingsReviewed )
			return;

		GuestRatingsReviewed = true;
		TryAdvanceGoals();
	}
}
