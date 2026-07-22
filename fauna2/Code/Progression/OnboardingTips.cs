namespace Fauna2;

/// <summary>Coach-mark tip shown during early zoo onboarding (Final Outpost-style).</summary>
public sealed class OnboardingTipDef
{
	public string Id { get; init; }
	public string Title { get; init; }
	public string Body { get; init; }
	public string Icon { get; init; } = "tips_and_updates";
	public int GoalIndex { get; init; }
	public ObjectiveAction OpenAction { get; init; } = ObjectiveAction.None;
	/// <summary>Label for the secondary action button when <see cref="OpenAction"/> is set.</summary>
	public string ActionLabel { get; init; } = "Open tool";
}

/// <summary>
/// One large center tip per tutorial goal (0–10). The tip always matches
/// <see cref="ObjectiveSystem.CurrentIndex"/> — completing a goal swaps the card.
/// </summary>
public static class OnboardingTips
{
	public const int MaxTutorialGoalIndex = ObjectiveSystem.TutorialGoalCount - 1;

	/// <summary>
	/// Built each call so s&amp;box hotload cannot leave a stale tip list in memory.
	/// </summary>
	public static IReadOnlyList<OnboardingTipDef> All => BuildTips();

	private static List<OnboardingTipDef> BuildTips() => new()
	{
		new()
		{
			Id = "catch_prep",
			GoalIndex = 0,
			Icon = "shopping_bag",
			Title = "Buy a Tranquilizer, then catch",
			Body = "Buy a Tranquilizer: press Animals (N) → Catch Tools, or tap Buy tranquilizer below. "
				+ "Grab one now — you'll need it for tougher animals.\n\n"
				+ "Catch an animal: walk into the wilderness, get close to wildlife, press E, "
				+ "and click when the marker hits the green zone. You already have a Catch Net.",
			OpenAction = ObjectiveAction.CatchHelp,
			ActionLabel = "Buy tranquilizer",
		},
		new()
		{
			Id = "catch_animal",
			GoalIndex = 1,
			Icon = "pets",
			Title = "Catch it!",
			Body = "Press E near the animal and time your catch on the green zone. "
				+ "If a fight starts, use your Tranquilizer from Catch Tools. Keep the net ready.",
			OpenAction = ObjectiveAction.CatchHelp,
			ActionLabel = "Open Catch Tools",
		},
		new()
		{
			Id = "build_entrance",
			GoalIndex = 2,
			Icon = "door_front",
			Title = "Build an entrance",
			Body = "Guests need a gate. Open Build and place an entrance on the edge of your land.",
			OpenAction = ObjectiveAction.BuildEntrance,
			ActionLabel = "Place entrance",
		},
		new()
		{
			Id = "clear_land",
			GoalIndex = 3,
			Icon = "forest",
			Title = "Clear trees and boulders",
			Body = "Your plot has trees and rocks in the way. Click one on your land, then press Clear "
				+ "and wait for it to finish — you'll earn a small cash reward and open space to build.",
			OpenAction = ObjectiveAction.ClearLand,
			ActionLabel = "How to clear",
		},
		new()
		{
			Id = "build_path",
			GoalIndex = 4,
			Icon = "route",
			Title = "Connect a path",
			Body = "Lay path tiles from your entrance so guests can walk in. "
				+ "At least one path must touch the entrance.",
			OpenAction = ObjectiveAction.BuildPaths,
			ActionLabel = "Place path",
		},
		new()
		{
			Id = "build_habitat",
			GoalIndex = 5,
			Icon = "fence",
			Title = "Build a habitat",
			Body = "Place a habitat for your catch. Open Build and pick one that matches their biome "
				+ "when you can — any habitat works for this step.",
			OpenAction = ObjectiveAction.BuildHabitats,
			ActionLabel = "Place habitat",
		},
		new()
		{
			Id = "place_animal",
			GoalIndex = 6,
			Icon = "home",
			Title = "House your animal",
			Body = "Walk to your habitat and press E to release your catch. "
				+ "That's the loop — catch, build, house. Next you'll add guest amenities.",
			OpenAction = ObjectiveAction.PlaceAnimal,
			ActionLabel = "How to house",
		},
		new()
		{
			Id = "build_restroom",
			GoalIndex = 7,
			Icon = "wc",
			Title = "Build a restroom",
			Body = "Guests get unhappy without facilities. Open Build → Utility and place a restroom "
				+ "next to your path so visitors can reach it.",
			OpenAction = ObjectiveAction.BuildRestroom,
			ActionLabel = "Place restroom",
		},
		new()
		{
			Id = "build_restaurant",
			GoalIndex = 8,
			Icon = "restaurant",
			Title = "Build a food stand",
			Body = "Hungry guests leave early. Place a restaurant or food stand along your path "
				+ "— you'll also earn income when you collect from it.",
			OpenAction = ObjectiveAction.BuildRestaurant,
			ActionLabel = "Place food stand",
		},
		new()
		{
			Id = "build_shop",
			GoalIndex = 9,
			Icon = "storefront",
			Title = "Build a gift shop",
			Body = "A shop near the path sells souvenirs and boosts income. "
				+ "Open Build → Utility and place a Sanctuary Shop where guests walk by.",
			OpenAction = ObjectiveAction.BuildShop,
			ActionLabel = "Place shop",
		},
		new()
		{
			Id = "check_ratings",
			GoalIndex = 10,
			Icon = "star",
			Title = "Check ratings & guest wants",
			Body = "Open Stats to see your zoo rating and what guests want next — "
				+ "restrooms, food, variety, cleanliness, and more. Fix the red items to climb stars.",
			OpenAction = ObjectiveAction.StatsOverview,
			ActionLabel = "Open Stats",
		},
	};

	public static bool ShouldRun()
	{
		if ( GameSettings.Current.HideOnboardingTips )
			return false;
		if ( GameManager.Instance?.GameStarted != true )
			return false;

		var index = ObjectiveSystem.Instance?.CurrentIndex ?? 0;
		return index >= 0 && index <= MaxTutorialGoalIndex;
	}

	public static OnboardingTipDef TipForGoal( int goalIndex )
	{
		foreach ( var tip in All )
		{
			if ( tip.GoalIndex == goalIndex )
				return tip;
		}

		return null;
	}

	public static OnboardingTipDef TipForCurrentGoal()
	{
		if ( !ShouldRun() )
			return null;

		var index = ObjectiveSystem.Instance?.CurrentIndex ?? 0;
		return TipForGoal( index );
	}

	public static OnboardingTipDef PickNext() => TipForCurrentGoal();

	public static void MarkShown( string tipId )
	{
		if ( string.IsNullOrEmpty( tipId ) )
			return;

		GameSettings.Current.OnboardingTipsShown ??= new List<string>();
		if ( !GameSettings.Current.OnboardingTipsShown.Contains( tipId ) )
			GameSettings.Current.OnboardingTipsShown.Add( tipId );
		GameSettings.Save();
	}

	public static void MarkTipsForGoal( int goalIndex )
	{
		var tip = TipForGoal( goalIndex );
		if ( tip is not null )
			MarkShown( tip.Id );
	}

	public static void HideAllTips()
	{
		GameSettings.Current.HideOnboardingTips = true;
		GameSettings.Save();
	}
}
