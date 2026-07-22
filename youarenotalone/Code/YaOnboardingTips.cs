namespace Sandbox;

public sealed class YaOnboardingTipDef
{
	public string Id { get; init; }
	public string Title { get; init; }
	public string Body { get; init; }
	public string Icon { get; init; } = "tips_and_updates";
	public int StepIndex { get; init; }
}

/// <summary>First-session coach tips — goal-gated (Got it soft-dismisses; next tip after the goal completes).</summary>
public static class YaOnboardingTips
{
	public static readonly IReadOnlyList<YaOnboardingTipDef> All = new List<YaOnboardingTipDef>
	{
		new()
		{
			Id = "welcome", StepIndex = 0, Icon = "groups",
			Title = "You Are Not Alone",
			Body = "One player is the hidden Alone. Everyone else hunts as Not Alone. Eliminate the monster or survive until time runs out."
		},
		new()
		{
			Id = "move_look", StepIndex = 1, Icon = "sports_esports",
			Title = "Move and look",
			Body = "WASD moves, mouse looks, Space jumps. Sprint with Shift. Tab opens the scoreboard."
		},
		new()
		{
			Id = "objectives", StepIndex = 2, Icon = "flag",
			Title = "Round goals",
			Body = "Not Alone: find and kill the Alone before the timer hits zero. Alone: pick off hunters or outlast the clock."
		},
		new()
		{
			Id = "controls_key", StepIndex = 3, Icon = "keyboard",
			Title = "Full controls",
			Body = "Hold C anytime for weapon keys, abilities, and role-specific actions — hunters and Alone use different kits."
		},
		new()
		{
			Id = "done", StepIndex = 4, Icon = "check_circle",
			Title = "Good hunting",
			Body = "You're set. Press H any time to hide these tips."
		},
	};

	public static bool ShouldRun() =>
		!YaClientPrefs.HideTutorialTips && !YaClientPrefs.HasSeenControlsTutorial;

	public static int MaxStepIndex => All.Max( t => t.StepIndex );

	public static YaOnboardingTipDef TipForStep( int stepIndex )
	{
		foreach ( var tip in All )
		{
			if ( tip.StepIndex == stepIndex )
				return tip;
		}

		return null;
	}

	/// <summary>
	/// Returns the active tip when prior goals are complete and this tip's goal is not.
	/// Soft-dismiss hides the card until the same tip's goal completes.
	/// </summary>
	public static YaOnboardingTipDef PickNext( IReadOnlyCollection<string> completedGoals, string softDismissedId )
	{
		if ( !ShouldRun() || completedGoals is null )
			return null;

		foreach ( var tip in All.OrderBy( t => t.StepIndex ) )
		{
			if ( !ArePriorGoalsComplete( tip.StepIndex, completedGoals ) )
				return null;

			if ( completedGoals.Contains( tip.Id ) )
				continue;

			if ( string.Equals( tip.Id, softDismissedId, StringComparison.Ordinal ) )
				return null;

			return tip;
		}

		return null;
	}

	public static bool ArePriorGoalsComplete( int stepIndex, IReadOnlyCollection<string> completedGoals )
	{
		if ( completedGoals is null )
			return false;

		foreach ( var tip in All )
		{
			if ( tip.StepIndex >= stepIndex )
				continue;

			if ( !completedGoals.Contains( tip.Id ) )
				return false;
		}

		return true;
	}

	public static bool AllGoalsComplete( IReadOnlyCollection<string> completedGoals )
	{
		if ( completedGoals is null )
			return false;

		foreach ( var tip in All )
		{
			if ( !completedGoals.Contains( tip.Id ) )
				return false;
		}

		return true;
	}

	public static void MarkGoalComplete( ICollection<string> completedGoals, string goalId )
	{
		if ( completedGoals is null || string.IsNullOrEmpty( goalId ) )
			return;

		if ( !completedGoals.Contains( goalId ) )
			completedGoals.Add( goalId );
	}
}
