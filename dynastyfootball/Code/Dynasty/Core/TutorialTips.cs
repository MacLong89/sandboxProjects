namespace Dynasty.Core;

public sealed class TutorialTipDef
{
	public string Id { get; init; }
	public string Title { get; init; }
	public string Body { get; init; }
	public string Icon { get; init; } = "tips_and_updates";
	public int GoalIndex { get; init; }
}

/// <summary>UI chrome coach tips — one center card per onboarding goal (Final Outpost-style).</summary>
public static class TutorialTips
{
	public const int MaxGoalIndex = 4;

	public static readonly IReadOnlyList<TutorialTipDef> All = new List<TutorialTipDef>
	{
		new()
		{
			Id = "welcome", GoalIndex = 0, Icon = "sports_football",
			Title = "Welcome to Sunday Dynasty",
			Body = "You're the GM. This quick tour covers the shell — your Inbox and To-Do list handle the actual steps."
		},
		new()
		{
			Id = "inbox_todo", GoalIndex = 1, Icon = "inbox",
			Title = "Inbox and To-Do",
			Body = "Open Inbox for league mail. The To-Do sidebar lists what needs action — click an item to jump there."
		},
		new()
		{
			Id = "next_bar", GoalIndex = 2, Icon = "arrow_forward",
			Title = "Next action bar",
			Body = "The Next bar at the top highlights your highest-priority task. Use it when you're not sure where to go."
		},
		new()
		{
			Id = "week_flow", GoalIndex = 3, Icon = "calendar_month",
			Title = "Draft and week flow",
			Body = "Tabs unlock as the season progresses. Follow Inbox steps through draft, lineup, and sim — then advance the week."
		},
		new()
		{
			Id = "done", GoalIndex = 4, Icon = "check_circle",
			Title = "You're set",
			Body = "Follow your Inbox from here. Press H any time to hide these tips."
		},
	};

	public static bool ShouldRun() => !DynastyClientSettings.Current.HideTutorialTips;

	public static int CurrentGoalIndex()
	{
		if ( !ShouldRun() )
			return MaxGoalIndex + 1;

		var shown = DynastyClientSettings.Current.TutorialTipsShown;
		for ( var i = 0; i <= MaxGoalIndex; i++ )
		{
			var tip = TipForGoal( i );
			if ( tip is null || !shown.Contains( tip.Id ) )
				return i;
		}

		return MaxGoalIndex + 1;
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

	public static TutorialTipDef TipForCurrentGoal()
	{
		if ( !ShouldRun() )
			return null;

		return TipForGoal( CurrentGoalIndex() );
	}

	public static void MarkTipsForGoal( int goalIndex )
	{
		var tip = TipForGoal( goalIndex );
		if ( tip is not null )
			MarkShown( tip.Id );
	}

	public static void MarkShown( string id )
	{
		if ( string.IsNullOrEmpty( id ) )
			return;

		DynastyClientSettings.MarkTipShown( id );
	}

	public static void HideAllTips()
	{
		DynastyClientSettings.Current.HideTutorialTips = true;
		DynastyClientSettings.Save();
	}
}
