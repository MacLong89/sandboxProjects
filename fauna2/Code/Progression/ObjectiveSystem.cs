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
	StatsFinances,
	Progression,
	BreedHelp,
	Codex,
	ExpandLand,
	StatsOverview,
	CatchHelp,
	PrestigeHelp,
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
		0 => (TerrainObstacleSystem.Instance?.TotalCleared ?? 0) >= 1,
		1 => PathNetwork.HasEntrance,
		2 => PathNetwork.HasGuestAccess,
		3 => StarterGoalGuide.HasStarterSmallHabitat(),
		4 => StarterGoalGuide.HasStarterAnimalPlaced(),
		5 => PlaceableRegistry.RestroomCount > 0,
		6 => PlaceableRegistry.RestaurantCount > 0,
		7 => (GuestSystem.Instance?.GuestCount ?? 0) >= 10,
		8 => (ZooState.Instance?.TotalAnimalsCaught ?? 0) > 0,
		9 => (ZooState.Instance?.TotalAnimalsBred ?? 0) > 0,
		10 => (PlotSystem.Instance?.PlotCount ?? 1) > 1,
		11 => (CollectionSystem.Instance?.DiscoveredSpeciesCount ?? 0) >= 5,
		12 => (CollectionSystem.Instance?.CompletionPercent ?? 0f) >= 50f,
		13 => (GuestSystem.Instance?.ZooRating ?? 0f) >= 4f,
		14 => (EconomySystem.Instance?.IncomePerMinute ?? 0f) > 0f,
		15 => (GuestSystem.Instance?.GuestCount ?? 0) >= 100,
		16 => (ZooState.Instance?.TotalAnimalsBred ?? 0) >= 5,
		17 => (PlotSystem.Instance?.PlotCount ?? 1) >= 4,
		18 => (CollectionSystem.Instance?.CompletionPercent ?? 0f) >= 99.5f,
		_ => false,
	};

	public static float ProgressFraction( int index ) => index switch
	{
		0 => (TerrainObstacleSystem.Instance?.TotalCleared ?? 0) >= 1 ? 1f : 0f,
		1 => PathNetwork.HasEntrance ? 1f : 0f,
		2 => PathNetwork.HasGuestAccess ? 1f : 0f,
		3 => StarterGoalGuide.HasStarterSmallHabitat() ? 1f : 0f,
		4 => StarterGoalGuide.HasStarterAnimalPlaced() ? 1f : 0f,
		5 => PlaceableRegistry.RestroomCount > 0 ? 1f : 0f,
		6 => PlaceableRegistry.RestaurantCount > 0 ? 1f : 0f,
		7 => MathX.Clamp( (GuestSystem.Instance?.GuestCount ?? 0) / 10f, 0f, 1f ),
		8 => (ZooState.Instance?.TotalAnimalsCaught ?? 0) > 0 ? 1f : 0f,
		9 => (ZooState.Instance?.TotalAnimalsBred ?? 0) > 0 ? 1f : 0f,
		10 => (PlotSystem.Instance?.PlotCount ?? 1) > 1 ? 1f : 0f,
		11 => MathX.Clamp( (CollectionSystem.Instance?.DiscoveredSpeciesCount ?? 0) / 5f, 0f, 1f ),
		12 => MathX.Clamp( (CollectionSystem.Instance?.CompletionPercent ?? 0f) / 50f, 0f, 1f ),
		13 => MathX.Clamp( (GuestSystem.Instance?.ZooRating ?? 0f) / 4f, 0f, 1f ),
		14 => (EconomySystem.Instance?.IncomePerMinute ?? 0f) > 0f ? 1f : 0f,
		15 => MathX.Clamp( (GuestSystem.Instance?.GuestCount ?? 0) / 100f, 0f, 1f ),
		16 => MathX.Clamp( (ZooState.Instance?.TotalAnimalsBred ?? 0) / 5f, 0f, 1f ),
		17 => MathX.Clamp( (PlotSystem.Instance?.PlotCount ?? 1) / 4f, 0f, 1f ),
		18 => MathX.Clamp( (CollectionSystem.Instance?.CompletionPercent ?? 0f) / 99.5f, 0f, 1f ),
		_ => 0f,
	};

	public static string ProgressLabel( int index ) => index switch
	{
		0 => (TerrainObstacleSystem.Instance?.TotalCleared ?? 0) > 0 ? "Cleared" : "0/1 cleared",
		1 => PathNetwork.HasEntrance ? "Entrance built" : "Not placed",
		2 => PathNetwork.HasGuestAccess ? "Paths connected" : "Not connected",
		3 => StarterGoalGuide.HabitatProgressLabel(),
		4 => StarterGoalGuide.AnimalProgressLabel(),
		5 => PlaceableRegistry.RestroomCount > 0 ? "Restroom built" : "Not placed",
		6 => PlaceableRegistry.RestaurantCount > 0 ? "Restaurant built" : "Not placed",
		7 => $"{GuestSystem.Instance?.GuestCount ?? 0}/10 guests",
		8 => (ZooState.Instance?.TotalAnimalsCaught ?? 0) > 0 ? "Caught" : "Not yet",
		9 => (ZooState.Instance?.TotalAnimalsBred ?? 0) > 0 ? "Bred" : "Not yet",
		10 => (PlotSystem.Instance?.PlotCount ?? 1) > 1 ? "Expanded" : "1 plot",
		11 => $"{CollectionSystem.Instance?.DiscoveredSpeciesCount ?? 0}/5 species",
		12 => $"{CollectionSystem.Instance?.CompletionPercent ?? 0f:0}% / 50%",
		13 => $"{GuestSystem.Instance?.ZooRating ?? 0f:0.0} / 4.0 stars",
		14 => (EconomySystem.Instance?.IncomePerMinute ?? 0f) > 0f ? "Profitable" : "Still investing",
		15 => $"{GuestSystem.Instance?.GuestCount ?? 0}/100 guests",
		16 => $"{ZooState.Instance?.TotalAnimalsBred ?? 0}/5 bred",
		17 => $"{PlotSystem.Instance?.PlotCount ?? 1}/4 plots",
		18 => $"{CollectionSystem.Instance?.CompletionPercent ?? 0f:0}% / 100%",
		_ => "",
	};

	public static string DescriptionWithProgress( int index )
	{
		if ( index < 0 || index >= ObjectiveSystem.Objectives.Count )
			return "";

		var description = index switch
		{
			3 => StarterGoalGuide.HabitatGoalDescription(),
			4 => StarterGoalGuide.AnimalGoalDescription(),
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

	/// <summary>Early goals teach core zoo setup; later goals extend into endgame.</summary>
	public const int TutorialGoalCount = 8;

	[Sync( SyncFlags.FromHost )] public int CurrentIndex { get; set; }

	private TimeUntil _nextCheck;

	public static readonly List<ObjectiveDef> Objectives = new()
	{
		new()
		{
			Title = "Clear an obstacle",
			Description = "Click a tree or rock, open its panel, and press Clear.",
			RewardMoney = 200, RewardXp = 25,
			Action = ObjectiveAction.ClearLand,
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
			Title = "Connect a path",
			Description = "Lay path tiles from your entrance — at least one path must touch the entrance so guests can arrive.",
			RewardMoney = 200, RewardXp = 20,
			Action = ObjectiveAction.BuildPaths,
		},
		new()
		{
			Title = "Build your first habitat",
			Description = "Place the small habitat that matches your starter biome.",
			RewardMoney = 500, RewardXp = 30,
			Action = ObjectiveAction.BuildHabitats,
		},
		new()
		{
			Title = "Adopt your first animal",
			Description = "Adopt a species that matches your habitat biome and size.",
			RewardMoney = 250, RewardXp = 40,
			Action = ObjectiveAction.Market,
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
			Title = "Welcome 10 guests",
			Description = "Guests arrive through your entrance and bring ticket revenue.",
			RewardMoney = 500, RewardXp = 60,
			Action = ObjectiveAction.StatsGuests,
		},
		new()
		{
			Title = "Catch a wild animal",
			Description = "Head into the wilderness, press E on wildlife, and time your catch.",
			RewardMoney = 400, RewardXp = 60,
			Action = ObjectiveAction.CatchHelp,
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
		3 => StarterGoalGuide.HabitatGoalTitle(),
		4 => StarterGoalGuide.AnimalGoalTitle(),
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
		_nextCheck = 0.5f;
		TryAdvanceGoals();
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
		var completedTitle = GetGoalTitle( completedIndex );
		var state = ZooState.Instance;
		if ( !state.IsValid() ) return;

		var rewardMoney = Objectives[completedIndex].RewardMoney;
		var rewardXp = Objectives[completedIndex].RewardXp;
		if ( rewardMoney > 0 ) state.AddMoney( rewardMoney );
		if ( rewardXp > 0 ) state.AddXp( rewardXp );

		var rewardText = rewardMoney > 0 ? $" (+${rewardMoney:n0})" : "";
		var completeIcon = completedIndex < TutorialGoalCount ? "task_alt" : "emoji_events";
		state.Notify( $"Goal complete: {completedTitle}{rewardText}", completeIcon );

		if ( completedIndex == TutorialGoalCount - 1 )
			UI.UiState.ShowCelebration( "Tutorial complete!", "Guests are visiting — catch wildlife, breed animals, and expand from here.", "flag" );
		else
			ShowCompletionCelebration( completedIndex );

		CurrentIndex++;
		UI.UiState.NotifyGoalsChanged();

		if ( CurrentIndex < Objectives.Count )
		{
			var nextIcon = CurrentIndex < TutorialGoalCount ? "flag" : "emoji_events";
			state.Notify( $"Next goal: {GetGoalTitle( CurrentIndex )}", nextIcon );
		}

		if ( AllComplete )
			UI.UiState.ShowCelebration( "All goals complete!", "You have mastered every milestone. Keep growing your sanctuary.", "emoji_events" );
	}

	private static void ShowCompletionCelebration( int completedIndex )
	{
		switch ( completedIndex )
		{
			case 2:
				UI.UiState.ShowCelebration( "Your zoo is open!", "Guests can arrive — ticket revenue starts now.", "follow_the_signs" );
				break;
			case 4:
				UI.UiState.ShowCelebration( "First resident!", "Your sanctuary has its first animal.", "pets" );
				break;
			case 6:
				UI.UiState.ShowCelebration( "Food stand ready!", "Guests can grab a bite along your paths.", "restaurant" );
				break;
			case 7:
				break;
		}
	}
}
