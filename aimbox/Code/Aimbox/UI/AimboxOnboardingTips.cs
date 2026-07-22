namespace Sandbox;

public sealed class AimboxTutorialTipDef
{
	public string Id { get; init; }
	public string Title { get; init; }
	public string Body { get; init; }
	public string Icon { get; init; } = "tips_and_updates";
	public int GoalIndex { get; init; }
}

/// <summary>Lobby coach tips — one card per menu goal (Got it soft-dismisses until the goal fires).</summary>
public static class AimboxOnboardingTips
{
	public const int MaxGoalIndex = 3;

	public static readonly IReadOnlyList<AimboxTutorialTipDef> All = new List<AimboxTutorialTipDef>
	{
		new()
		{
			Id = "welcome", GoalIndex = 0, Icon = "gps_fixed",
			Title = "Welcome to Aimbox",
			Body = "Pick a mode in the lobby, manage loadouts, then fight. This primer covers the basics — press Got it when you're ready."
		},
		new()
		{
			Id = "move_shoot", GoalIndex = 1, Icon = "sports_esports",
			Title = "Move and shoot",
			Body = "WASD moves, mouse looks, Left Click fires, R reloads. Shift sprints."
		},
		new()
		{
			Id = "loadouts", GoalIndex = 2, Icon = "inventory_2",
			Title = "Loadouts matter",
			Body = "Open Manage Loadouts from the menu to swap primaries and unlock attachments as you rank up."
		},
		new()
		{
			Id = "done", GoalIndex = 3, Icon = "check_circle",
			Title = "You're ready",
			Body = "Queue a match when you're set. Press H any time to hide these tips."
		},
	};

	public static bool ShouldRun() => !AimboxClientSettings.HideTutorialTips;

	public static int CurrentGoalIndex()
	{
		if ( !ShouldRun() )
			return MaxGoalIndex + 1;

		var shown = AimboxClientSettings.TutorialTipsShown;
		for ( var i = 0; i <= MaxGoalIndex; i++ )
		{
			var tip = TipForGoal( i );
			if ( tip is null || !shown.Contains( tip.Id ) )
				return i;
		}

		return MaxGoalIndex + 1;
	}

	public static AimboxTutorialTipDef TipForGoal( int goalIndex )
	{
		foreach ( var tip in All )
		{
			if ( tip.GoalIndex == goalIndex )
				return tip;
		}

		return null;
	}

	public static AimboxTutorialTipDef TipForCurrentGoal()
	{
		if ( !ShouldRun() )
			return null;

		return TipForGoal( CurrentGoalIndex() );
	}

	public static bool CanShowGoal( int goalIndex )
	{
		if ( !ShouldRun() || goalIndex > MaxGoalIndex )
			return false;

		return goalIndex switch
		{
			0 => true,
			1 => AimboxClientSettings.VisitedPlayLobby,
			2 => AimboxClientSettings.VisitedLoadouts,
			3 => AimboxClientSettings.VisitedPlayLobby && AimboxClientSettings.VisitedLoadouts
				|| AimboxClientSettings.StartedMatchFromOnboarding,
			_ => false
		};
	}

	public static void NotifyPlayLobbyVisited()
	{
		AimboxClientSettings.VisitedPlayLobby = true;
		TryAdvanceAfterGoal( 0 );
	}

	public static void NotifyLoadoutsVisited()
	{
		AimboxClientSettings.VisitedLoadouts = true;
		TryAdvanceAfterGoal( 1 );
	}

	public static void NotifyMatchStarted()
	{
		AimboxClientSettings.StartedMatchFromOnboarding = true;
		if ( !AimboxClientSettings.VisitedPlayLobby )
			AimboxClientSettings.VisitedPlayLobby = true;
		if ( !AimboxClientSettings.VisitedLoadouts )
			AimboxClientSettings.VisitedLoadouts = true;
		TryAdvancePendingGoals();
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

		AimboxClientSettings.MarkTipShown( id );
	}

	public static void HideAllTips()
	{
		AimboxClientSettings.HideTutorialTips = true;
	}

	static void TryAdvanceAfterGoal( int completedGoalIndex )
	{
		if ( CurrentGoalIndex() != completedGoalIndex )
			return;

		MarkTipsForGoal( completedGoalIndex );
	}

	static bool IsGoalComplete( int goalIndex ) => goalIndex switch
	{
		0 => AimboxClientSettings.VisitedPlayLobby,
		1 => AimboxClientSettings.VisitedLoadouts,
		2 => (AimboxClientSettings.VisitedPlayLobby && AimboxClientSettings.VisitedLoadouts)
			|| AimboxClientSettings.StartedMatchFromOnboarding,
		_ => false
	};

	public static void TryAdvancePendingGoals()
	{
		var goal = CurrentGoalIndex();
		while ( goal <= MaxGoalIndex && IsGoalComplete( goal ) )
		{
			TryAdvanceAfterGoal( goal );
			goal = CurrentGoalIndex();
		}
	}
}
