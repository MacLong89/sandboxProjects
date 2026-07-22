namespace SkyEmpire;

public sealed class TutorialTipDef
{
	public string Id { get; init; }
	public string Title { get; init; }
	public string Body { get; init; }
	public string Icon { get; init; } = "tips_and_updates";
	public int GoalIndex { get; init; }
}

/// <summary>
/// One coach tip per early milestone index — matches <see cref="SaveData.MilestoneIndex"/>.
/// </summary>
public static class TutorialTips
{
	public const int MaxTutorialGoalIndex = 3;

	/// <summary>Built each call so s&amp;box hotload cannot leave a stale tip list in memory.</summary>
	public static IReadOnlyList<TutorialTipDef> All => BuildTips();

	static List<TutorialTipDef> BuildTips() => new()
	{
		new()
		{
			Id = "first_dropper",
			GoalIndex = 0,
			Icon = "cloud",
			Title = "Place your first dropper",
			Body = "Step on the glowing FREE pad on your island to place your first dropper."
		},
		new()
		{
			Id = "orbs",
			GoalIndex = 1,
			Icon = "payments",
			Title = "Orbs are cash",
			Body = "Orbs roll into your furnace — every orb pays, even while you're away."
		},
		new()
		{
			Id = "grow",
			GoalIndex = 2,
			Icon = "trending_up",
			Title = "Grow your island",
			Body = "Buy pads, raise floors, and rebirth for permanent power. Visit friends for a +25% boost."
		},
		new()
		{
			Id = "done",
			GoalIndex = 3,
			Icon = "check_circle",
			Title = "You're on your own",
			Body = "Follow the milestone chip for your next goal. Press H any time to hide these tips."
		},
	};

	public static bool ShouldRun( SaveData save )
	{
		if ( save is null || save.HideTutorialTips )
			return false;

		return save.MilestoneIndex >= 0 && save.MilestoneIndex <= MaxTutorialGoalIndex;
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

	public static TutorialTipDef TipForCurrentGoal( SaveData save )
	{
		if ( !ShouldRun( save ) )
			return null;

		return TipForGoal( save.MilestoneIndex );
	}

	/// <summary>Returns the tip for the current milestone when not soft-dismissed for that goal.</summary>
	public static TutorialTipDef PickNext( SaveData save, int dismissedGoalIndex = -1 )
	{
		var tip = TipForCurrentGoal( save );
		if ( tip is null )
			return null;

		if ( tip.GoalIndex == dismissedGoalIndex )
			return null;

		return tip;
	}

	public static void MarkShown( SaveData save, int goalIndex )
	{
		if ( save is null || goalIndex < 0 )
			return;

		var tip = TipForGoal( goalIndex );
		if ( tip is null )
			return;

		save.TutorialTipsShown ??= new List<string>();
		if ( !save.TutorialTipsShown.Contains( tip.Id ) )
			save.TutorialTipsShown.Add( tip.Id );
	}

	public static void HideAllTips( SaveData save )
	{
		if ( save is null )
			return;

		save.HideTutorialTips = true;
	}
}