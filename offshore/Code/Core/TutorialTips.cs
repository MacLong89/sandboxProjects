namespace Offshore;

public sealed class TutorialTipDef
{
	public string Id { get; init; }
	public string Title { get; init; }
	public string Body { get; init; }
	public string Icon { get; init; } = "tips_and_updates";
	public int GoalIndex { get; init; }
	/// <summary>Optional next tip shown immediately after "Got it" (e.g. cast → hook/reel).</summary>
	public string FollowUpId { get; init; }
	/// <summary>Only offer this tip once current objective progress reaches this value.</summary>
	public int MinObjectiveProgress { get; init; }
	/// <summary>Skip until the player has at least one fish in storage.</summary>
	public bool RequiresStoredCatch { get; init; }
	/// <summary>Skip this tip if another tip id was already shown (duplicate lessons).</summary>
	public string SkipIfShownId { get; init; }
}

/// <summary>
/// Coach tips keyed to early objective indices — matches <see cref="SaveData.ObjectiveIndex"/>.
/// Tips may chain via <see cref="TutorialTipDef.FollowUpId"/> so related lessons appear back to back.
/// </summary>
public static class TutorialTips
{
	/// <summary>Done tip shows once all intro objectives are complete.</summary>
	public const int DoneGoalIndex = 10;

	/// <summary>Built each call so s&amp;box hotload cannot leave a stale tip list in memory.</summary>
	public static IReadOnlyList<TutorialTipDef> All => BuildTips();

	static List<TutorialTipDef> BuildTips() => new()
	{
		new()
		{
			Id = "shop",
			GoalIndex = 0,
			Icon = "storefront",
			Title = "Visit the shop",
			Body = "Press E at the bait shop on the pier. Buy worms and check your tackle before you cast."
		},
		new()
		{
			Id = "cast",
			GoalIndex = 2,
			Icon = "sailing",
			Title = "Cast from the dock",
			Body = "Walk the pier, hold Left Click to charge a cast, release to send your line.",
			FollowUpId = "reel"
		},
		new()
		{
			Id = "reel",
			GoalIndex = 3,
			Icon = "phishing",
			Title = "Hook and reel",
			Body = "When you see the bite flash, press Left Click quickly to set the hook. A vertical bar appears: hold Left Click to raise the green zone, release to let it fall. Keep that zone over the fish to fill the catch meter — if the meter empties, the fish escapes."
		},
		new()
		{
			Id = "sell_shop",
			GoalIndex = 3,
			MinObjectiveProgress = 1,
			RequiresStoredCatch = true,
			Icon = "sell",
			Title = "Sell your catch",
			Body = "Nice haul! Go to the bait shop and sell your fish — open the shop and press SELL FISH in the top left for coins.",
			FollowUpId = "earn_dinghy"
		},
		new()
		{
			Id = "earn_dinghy",
			GoalIndex = 3,
			Icon = "directions_boat",
			Title = "Save for a Dinghy",
			Body = "Buy and sell fish until you get up to $450. Then come back to the shop and buy a Dinghy. This will allow you to travel out into the ocean."
		},
		// Same lesson if the player reaches the buy-boat goal without seeing it after the first catch.
		new()
		{
			Id = "earn_dinghy_boat_goal",
			GoalIndex = 4,
			SkipIfShownId = "earn_dinghy",
			Icon = "directions_boat",
			Title = "Save for a Dinghy",
			Body = "Buy and sell fish until you get up to $450. Then come back to the shop and buy a Dinghy. This will allow you to travel out into the ocean."
		},
		new()
		{
			Id = "board_dinghy",
			GoalIndex = 5,
			Icon = "directions_boat",
			Title = "Find your Dinghy",
			Body = "Walk right along the pier to the berth at the end — your Dinghy is moored there. Press E to climb aboard. Use A/D to sail (right leaves the dock, left returns). Press E at the dock to hop off."
		},
		new()
		{
			Id = "freedom",
			GoalIndex = 6,
			Icon = "check_circle",
			Title = "You're on your own",
			Body = "Upgrade rods, equipment, and boats to travel further into the ocean and catch rarer, more valuable fish. There are also dangers that await, so stay on your toes. You're on your own now!"
		},
		new()
		{
			Id = "sell",
			GoalIndex = 8,
			Icon = "sell",
			Title = "Sell your catch",
			Body = "Back at the pier, open the bait shop and hit SELL FISH to cash in — fund better rods, bait, and upgrades."
		},
	};

	public static bool ShouldRun( SaveData save )
	{
		if ( save is null || save.HideTutorialTips )
			return false;

		var index = save.ObjectiveIndex;
		return index >= 0 && (index < Catalog.Objectives.Count || index == DoneGoalIndex);
	}

	public static int NormalizedGoalIndex( SaveData save )
	{
		if ( save is null )
			return -1;

		var index = save.ObjectiveIndex;
		if ( index >= Catalog.Objectives.Count )
			return DoneGoalIndex;
		return index;
	}

	public static TutorialTipDef TipById( string id )
	{
		if ( string.IsNullOrEmpty( id ) )
			return null;

		foreach ( var tip in All )
		{
			if ( tip.Id == id )
				return tip;
		}

		return null;
	}

	public static TutorialTipDef TipForGoal( int goalIndex )
	{
		foreach ( var tip in All )
		{
			if ( tip.GoalIndex == goalIndex )
				return tip;
		}

		return null;
	}

	public static bool IsShown( SaveData save, string tipId )
	{
		if ( save is null || string.IsNullOrEmpty( tipId ) )
			return false;

		return save.TutorialTipsShown is not null && save.TutorialTipsShown.Contains( tipId );
	}

	static bool TipReady( SaveData save, TutorialTipDef tip )
	{
		if ( tip is null || IsShown( save, tip.Id ) )
			return false;
		if ( !string.IsNullOrEmpty( tip.SkipIfShownId ) && IsShown( save, tip.SkipIfShownId ) )
			return false;
		if ( save.ObjectiveProgress < tip.MinObjectiveProgress )
			return false;
		if ( tip.RequiresStoredCatch && (save.Storage is null || save.Storage.Count == 0) )
			return false;
		return true;
	}

	/// <summary>
	/// Next unshown tip for the current goal, walking <see cref="TutorialTipDef.FollowUpId"/> chains
	/// (so cast → hook/reel can appear back to back before the catch objective).
	/// </summary>
	public static TutorialTipDef PickNext( SaveData save, int dismissedGoalIndex = -1 )
	{
		if ( !ShouldRun( save ) )
			return null;

		var index = NormalizedGoalIndex( save );
		if ( index == dismissedGoalIndex )
			return null;

		var tip = TipForGoal( index );
		var walked = new HashSet<string>();
		while ( tip is not null && walked.Add( tip.Id ) )
		{
			if ( TipReady( save, tip ) )
				return tip;

			// Waiting on progress for this tip — don't skip ahead in the chain.
			if ( !IsShown( save, tip.Id ) && save.ObjectiveProgress < tip.MinObjectiveProgress )
				return null;

			tip = TipById( tip.FollowUpId );
		}

		// Extra tips keyed to this goal (e.g. sell after first kept fish → save for dinghy).
		foreach ( var extra in All )
		{
			if ( extra.GoalIndex != index || walked.Contains( extra.Id ) )
				continue;
			if ( TipReady( save, extra ) )
				return extra;
		}

		return null;
	}

	public static void MarkShown( SaveData save, TutorialTipDef tip )
	{
		if ( save is null || tip is null )
			return;

		save.TutorialTipsShown ??= new HashSet<string>();
		save.TutorialTipsShown.Add( tip.Id );
	}

	public static void MarkShown( SaveData save, int goalIndex )
	{
		if ( save is null || goalIndex < 0 )
			return;

		MarkShown( save, TipForGoal( goalIndex ) );
	}

	public static void HideAllTips( SaveData save )
	{
		if ( save is null )
			return;

		save.HideTutorialTips = true;
	}
}
