namespace Sandbox;

/// <summary>TAB menu state and tab routing — shell owns panel visibility.</summary>
public interface IThornsMenuHost
{
	bool MenuOpen { get; }
	ThornsMainUiTab ActiveTab { get; }
	string ClientPinnedJournalGoalId { get; }
	bool ClientJournalHudPinExplicit { get; }

	void Toggle( Action onOpened, Action onClosed, Action<ThornsMainUiTab> onTabSelected );
	void Close( Action onClosed );
	void SetActiveTab( ThornsMainUiTab tab, Action<ThornsMainUiTab> onTabSelected );
	void ClientApplyJournalHudPin( string goalIdOrEmpty, bool pinExplicit );
	void ClientEnsureDefaultPinnedJournalGoal( ThornsPlayerMilestones milestones );
}
