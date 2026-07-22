namespace Fauna2;

public enum HudGoalNav
{
	None,
	Goal,
}

/// <summary>Passive HUD goal row — click the active goal row to jump to the right tool.</summary>
public readonly struct HudGoal
{
	public string Icon { get; init; }
	public string Category { get; init; }
	public string Title { get; init; }
	public string Detail { get; init; }
	public float Progress { get; init; }
	public string ProgressLabel { get; init; }
	public int SortOrder { get; init; }
	public HudGoalNav Nav { get; init; }

	public int ProgressPercent => (int)(MathX.Clamp( Progress, 0f, 1f ) * 100f);
	public bool Clickable => Nav != HudGoalNav.None;
}

/// <summary>
/// Aggregates sequential goals, codex tiers, guest milestones,
/// and other long-term goals into passive HUD progress rows.
/// </summary>
public static class GoalBoard
{
	/// <summary>Passive HUD — current sequential goal only.</summary>
	public static IReadOnlyList<HudGoal> HudGoals()
	{
		// Center coach tips cover the early tutorial; don't duplicate them top-right.
		var objectives = ObjectiveSystem.Instance;
		if ( objectives is not null
			&& !objectives.AllComplete
			&& objectives.CurrentIndex >= 0
			&& objectives.CurrentIndex < ObjectiveSystem.TutorialGoalCount )
			return Array.Empty<HudGoal>();

		var goal = SequentialGoals().FirstOrDefault();
		if ( goal.Nav != HudGoalNav.Goal )
			return Array.Empty<HudGoal>();

		return new[] { goal };
	}

	/// <summary>Full goal list for menus and progression panels.</summary>
	public static IReadOnlyList<HudGoal> ActiveGoals( int maxCount = 4 )
	{
		var sequentialIndex = ObjectiveSystem.Instance?.CurrentIndex ?? 0;
		return CollectGoals( sequentialIndex )
			.OrderBy( g => g.SortOrder )
			.ThenByDescending( g => g.Progress )
			.Take( maxCount )
			.ToList();
	}

	private static IEnumerable<HudGoal> CollectGoals( int sequentialIndex )
	{
		foreach ( var goal in SequentialGoals() ) yield return goal;
		foreach ( var goal in MomentumGoals() ) yield return goal;
		if ( ShouldSkipPassiveGoals( sequentialIndex ) )
			yield break;

		foreach ( var goal in CodexGoals() ) yield return goal;
		foreach ( var goal in GuestGoals() ) yield return goal;
		foreach ( var goal in CollectionGoals() ) yield return goal;
		foreach ( var goal in ZooGrowthGoals() ) yield return goal;
		foreach ( var goal in EventGoals() ) yield return goal;
		foreach ( var goal in DailyGoals() ) yield return goal;
	}

	private static IEnumerable<HudGoal> MomentumGoals()
	{
		var momentum = SanctuaryMomentumSystem.Instance;
		if ( momentum is null )
			yield break;

		var goal = momentum.CurrentGoal;
		if ( string.IsNullOrEmpty( goal.Title ) )
			yield break;

		yield return new HudGoal
		{
			Icon = goal.Icon,
			Category = "Momentum",
			Title = goal.Title,
			Detail = goal.Detail,
			Progress = goal.Progress,
			ProgressLabel = goal.Label,
			SortOrder = 1,
		};
	}

	private static bool ShouldSkipPassiveGoals( int sequentialIndex )
	{
		// While early tutorial goals are active, avoid duplicate milestone rows in the panel.
		return sequentialIndex >= 0 && sequentialIndex < ObjectiveSystem.TutorialGoalCount;
	}

	private static IEnumerable<HudGoal> SequentialGoals()
	{
		var objectives = ObjectiveSystem.Instance;
		if ( objectives is null || objectives.AllComplete )
			yield break;

		var current = objectives.Current;
		if ( current is null )
			yield break;

		yield return new HudGoal
		{
			Icon = objectives.CurrentIndex < ObjectiveSystem.TutorialGoalCount ? "flag" : "emoji_events",
			Category = $"Goal {objectives.CurrentIndex + 1}/{ObjectiveSystem.Objectives.Count}",
			Title = objectives.GetGoalTitle(),
			Detail = objectives.GetDescriptionWithProgress(),
			Progress = objectives.GetProgressFraction(),
			ProgressLabel = objectives.GetProgressLabel(),
			SortOrder = 0,
			Nav = HudGoalNav.Goal,
		};
	}

	private static IEnumerable<HudGoal> CodexGoals()
	{
		var collection = CollectionSystem.Instance;
		if ( collection is null )
			yield break;

		var pct = collection.CompletionPercent;
		var nextTier = NextCodexTier( pct );
		if ( nextTier is null )
			yield break;

		yield return new HudGoal
		{
			Icon = "menu_book",
			Category = "Codex",
			Title = nextTier.Value.Title,
			Detail = "Discover species and rare variants to fill your codex.",
			Progress = pct / nextTier.Value.TargetPercent,
			ProgressLabel = $"{pct:0}% / {nextTier.Value.TargetPercent:0}%",
			SortOrder = 2,
		};
	}

	private static IEnumerable<HudGoal> GuestGoals()
	{
		var guests = GuestSystem.Instance?.GuestCount ?? 0;
		var next = NextGuestMilestone( guests );
		if ( next is null )
			yield break;

		yield return new HudGoal
		{
			Icon = "groups",
			Category = "Guests",
			Title = next.Value.Title,
			Detail = "Paths, habitats, and amenities attract more visitors.",
			Progress = guests / (float)next.Value.Target,
			ProgressLabel = $"{guests} / {next.Value.Target}",
			SortOrder = 3,
		};
	}

	private static IEnumerable<HudGoal> CollectionGoals()
	{
		var collection = CollectionSystem.Instance;
		if ( collection is null )
			yield break;

		var discovered = collection.DiscoveredSpeciesCount;
		var total = Defs.Animals.Count();
		if ( total == 0 || discovered >= total )
			yield break;

		var nextTarget = NextSpeciesTarget( discovered );
		yield return new HudGoal
		{
			Icon = "pets",
			Category = "Species",
			Title = $"Discover {nextTarget} species",
			Detail = "Buy at the market or catch wildlife in the wilderness.",
			Progress = discovered / (float)nextTarget,
			ProgressLabel = $"{discovered} / {nextTarget}",
			SortOrder = 4,
		};
	}

	private static IEnumerable<HudGoal> ZooGrowthGoals()
	{
		var state = ZooState.Instance;
		if ( state is null )
			yield break;

		if ( state.Level < GameConstants.MaxLevel )
		{
			var needed = ZooState.XpForLevel( state.Level );
			var progress = needed <= 0 ? 1f : state.Xp / (float)needed;
			yield return new HudGoal
			{
				Icon = "military_tech",
				Category = "Level",
				Title = $"Reach level {state.Level + 1}",
				Detail = "Earn XP from building, guests, discoveries, and milestones.",
				Progress = progress,
				ProgressLabel = $"{state.Xp:n0} / {needed:n0} XP",
				SortOrder = 5,
			};
		}

		var rating = GuestSystem.Instance?.ZooRating ?? 0f;
		if ( rating < 5f )
		{
			yield return new HudGoal
			{
				Icon = "star",
				Category = "Rating",
				Title = rating < 4f ? "Reach a 4-star zoo" : "Reach a 5-star zoo",
				Detail = "Happy animals, clean paths, and amenities raise your rating.",
				Progress = rating / (rating < 4f ? 4f : 5f),
				ProgressLabel = $"{rating:0.0} / {(rating < 4f ? 4f : 5f):0} stars",
				SortOrder = 6,
			};
		}
	}

	private static IEnumerable<HudGoal> DailyGoals()
	{
		var sanctuary = DailySanctuarySystem.Instance;
		if ( sanctuary is not null )
		{
			foreach ( var goal in sanctuary.Goals().Where( g => !g.Complete ).Take( 2 ) )
			{
				yield return new HudGoal
				{
					Icon = goal.Icon,
					Category = "Daily mission",
					Title = goal.Title,
					Detail = goal.Detail,
					Progress = goal.Fraction,
					ProgressLabel = $"{Math.Min( goal.Progress, goal.Target )} / {goal.Target}",
					SortOrder = 7,
				};
			}
		}

		var daily = DailyBonusSystem.Instance;
		if ( daily is null || daily.LoginStreak <= 0 )
			yield break;

		var countdown = DailyBonusSystem.NextBonusCountdown();
		if ( countdown == "Available now" )
			yield break;

		yield return new HudGoal
		{
			Icon = "redeem",
			Category = $"Day {daily.LoginStreak} streak",
			Title = "Daily bonus",
			Detail = "Come back tomorrow for escalating login rewards.",
			Progress = 0f,
			ProgressLabel = countdown,
			SortOrder = 8,
		};
	}

	private static IEnumerable<HudGoal> EventGoals()
	{
		var weather = WeatherSeasonSystem.Instance;
		if ( weather is not null )
		{
			yield return new HudGoal
			{
				Icon = weather.WeatherIcon,
				Category = "Sanctuary",
				Title = weather.Summary,
				Detail = "Weather and seasons now affect animals, habitats, and guests.",
				Progress = 0f,
				ProgressLabel = "",
				SortOrder = 6,
			};
		}

		var events = SanctuaryEventSystem.Instance;
		if ( events is not null && !string.IsNullOrWhiteSpace( events.ActiveEventTitle ) )
		{
			yield return new HudGoal
			{
				Icon = events.ActiveEventIcon,
				Category = "Event",
				Title = events.ActiveEventTitle,
				Detail = events.ActiveEventDetail,
				Progress = 0f,
				ProgressLabel = "",
				SortOrder = 6,
			};
		}
	}

	private static (string Title, float TargetPercent)? NextCodexTier( float pct )
	{
		if ( pct < 25f ) return ("Codex scholar (25%)", 25f);
		if ( pct < 50f ) return ("Half the codex (50%)", 50f);
		if ( pct < 75f ) return ("Codex expert (75%)", 75f);
		if ( pct < 99f ) return ("Complete the codex", 99f);
		return null;
	}

	private static (string Title, int Target)? NextGuestMilestone( int guests )
	{
		if ( guests < 25 ) return ("Welcome 25 guests", 25);
		if ( guests < 50 ) return ("Host 50 guests", 50);
		if ( guests < 100 ) return ("Crowd of 100 guests", 100);
		if ( guests < 200 ) return ("Mega zoo (200 guests)", 200);
		return null;
	}

	private static int NextSpeciesTarget( int discovered )
	{
		int[] targets = [3, 5, 10, 15, 20, 30];
		foreach ( var target in targets )
		{
			if ( discovered < target )
				return target;
		}

		return Math.Max( discovered + 1, 30 );
	}
}
