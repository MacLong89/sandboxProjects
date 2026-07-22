namespace Dynasty.Core;

using Dynasty.Bootstrap;
using Dynasty.Domain.League;
using Dynasty.UI.Management;

/// <summary>Goal-gated center tips — Got it soft-dismisses; tips advance on shell actions.</summary>
public static class DynastyTutorialTipsController
{
	public static TutorialTipDef ActiveTip { get; private set; }

	static int _softDismissedGoal = -1;
	static TimeUntil _tipCooldown;

	public static void Tick( LeagueState state, string activeTab )
	{
		Refresh( state, activeTab );
	}

	public static void Refresh( LeagueState state, string activeTab )
	{
		if ( state is null
		     || !FtueHelper.IsFtueActive( state )
		     || !TutorialTips.ShouldRun()
		     || DynastyApp.Session.Screen != GameScreen.InGame )
		{
			ActiveTip = null;
			CloseTipWindow();
			return;
		}

		if ( IsBlockedByOtherModal() )
		{
			ActiveTip = null;
			CloseTipWindow();
			return;
		}

		var goal = TutorialTips.CurrentGoalIndex();
		if ( goal > TutorialTips.MaxGoalIndex )
		{
			ActiveTip = null;
			CloseTipWindow();
			return;
		}

		if ( !CanShowGoal( goal, activeTab ) )
		{
			ActiveTip = null;
			CloseTipWindow();
			return;
		}

		if ( goal == _softDismissedGoal )
		{
			ActiveTip = null;
			CloseTipWindow();
			return;
		}

		if ( DynastyUiManager.Instance.IsWindowOpen( UiWindowType.TutorialTip ) )
		{
			if ( ActiveTip is not null && ActiveTip.GoalIndex == goal )
				return;

			CloseTipWindow();
		}

		if ( ActiveTip is not null || !_tipCooldown )
			return;

		var tip = TutorialTips.TipForGoal( goal );
		if ( tip is null )
			return;

		ActiveTip = tip;
		DynastyUiManager.Instance.ProcessRequest( UiRequest.Open( UiWindowType.TutorialTip, tip ) );
	}

	public static void NotifyInboxOpened()
	{
		TryAdvanceAfterGoal( 0 );
	}

	public static void NotifyTodoOrNextAction()
	{
		TryAdvanceAfterGoal( 1 );
	}

	public static void NotifyWeekAdvanced()
	{
		TryAdvanceAfterGoal( 2 );
	}

	public static void NotifyWeekFlowAcknowledged()
	{
		TryAdvanceAfterGoal( 3 );
	}

	public static void Dismiss( bool hideAll = false )
	{
		if ( hideAll )
		{
			ActiveTip = null;
			CloseTipWindow();
			TutorialTips.HideAllTips();
			DynastyUiManager.Instance.EnqueueNotification( "Tips hidden — press H to show again" );
			_softDismissedGoal = -1;
			_tipCooldown = 0f;
			return;
		}

		if ( ActiveTip is null && !DynastyUiManager.Instance.IsWindowOpen( UiWindowType.TutorialTip ) )
			return;

		if ( ActiveTip?.GoalIndex == TutorialTips.MaxGoalIndex )
			TutorialTips.MarkTipsForGoal( TutorialTips.MaxGoalIndex );

		_softDismissedGoal = ActiveTip?.GoalIndex ?? TutorialTips.CurrentGoalIndex();
		ActiveTip = null;
		CloseTipWindow();
		_tipCooldown = 0f;
	}

	public static void ToggleHidden()
	{
		DynastyClientSettings.Current.HideTutorialTips = !DynastyClientSettings.Current.HideTutorialTips;
		DynastyClientSettings.Save();

		if ( DynastyClientSettings.Current.HideTutorialTips )
		{
			ActiveTip = null;
			CloseTipWindow();
			DynastyUiManager.Instance.EnqueueNotification( "Tips hidden — press H to show again" );
		}
		else
		{
			DynastyUiManager.Instance.EnqueueNotification( "Tips enabled" );
			_softDismissedGoal = -1;
		}

		_tipCooldown = 0.55f;
	}

	static void TryAdvanceAfterGoal( int completedGoalIndex )
	{
		if ( TutorialTips.CurrentGoalIndex() != completedGoalIndex )
			return;

		TutorialTips.MarkTipsForGoal( completedGoalIndex );
		ActiveTip = null;
		CloseTipWindow();
		_softDismissedGoal = -1;
		_tipCooldown = 0.55f;
	}

	static bool CanShowGoal( int goalIndex, string activeTab ) => goalIndex switch
	{
		0 => activeTab == "home",
		1 => activeTab == "inbox",
		2 => true,
		3 => true,
		4 => true,
		_ => false
	};

	static bool IsBlockedByOtherModal()
	{
		var ui = DynastyUiManager.Instance;
		if ( !ui.HasModalOpen )
			return false;

		return !ui.IsWindowOpen( UiWindowType.TutorialTip );
	}

	static void CloseTipWindow()
	{
		if ( DynastyUiManager.Instance.IsWindowOpen( UiWindowType.TutorialTip ) )
			DynastyUiManager.Instance.ProcessRequest( UiRequest.Close( UiWindowType.TutorialTip ) );
	}
}
