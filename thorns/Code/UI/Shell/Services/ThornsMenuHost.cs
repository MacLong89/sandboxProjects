namespace Sandbox;

/// <summary>TAB menu and tab routing state — view layer applies panel visibility.</summary>
public sealed class ThornsMenuHost : IThornsMenuHost
{
	public bool MenuOpen { get; private set; }
	public ThornsMainUiTab ActiveTab { get; private set; } = ThornsMainUiTab.Inventory;
	public string ClientPinnedJournalGoalId { get; private set; } = "";
	public bool ClientJournalHudPinExplicit { get; private set; }

	public void Toggle( Action onOpened, Action onClosed, Action<ThornsMainUiTab> onTabSelected )
	{
		MenuOpen = !MenuOpen;
		if ( MenuOpen )
		{
			SetActiveTab( ThornsMainUiTab.Inventory, onTabSelected );
			onOpened?.Invoke();
		}
		else
			onClosed?.Invoke();

		Log.Info( $"[Thorns] UI: Tab menu={(MenuOpen ? "open" : "closed")}" );
		if ( MenuOpen )
			Log.Info( "[Thorns][Shell DnD] Active inventory UI path: ThornsGameShell + ThornsUiGridSlot (not legacy InventorySlotPanel overlay)." );
	}

	public void Close( Action onClosed )
	{
		if ( !MenuOpen )
			return;

		MenuOpen = false;
		onClosed?.Invoke();
		Log.Info( "[Thorns] UI: Tab menu=closed" );
	}

	public void SetActiveTab( ThornsMainUiTab tab, Action<ThornsMainUiTab> onTabSelected )
	{
		ActiveTab = tab;
		onTabSelected?.Invoke( tab );
	}

	public void ClientApplyJournalHudPin( string goalIdOrEmpty, bool pinExplicit )
	{
		ClientJournalHudPinExplicit = pinExplicit;
		ClientPinnedJournalGoalId = goalIdOrEmpty ?? "";
	}

	public void ForceCloseForDestroy() => MenuOpen = false;

	public void ClientEnsureDefaultPinnedJournalGoal( ThornsPlayerMilestones milestones )
	{
		if ( ClientJournalHudPinExplicit )
			return;

		if ( !milestones.IsValid() )
		{
			ClientPinnedJournalGoalId = "";
			return;
		}

		var idx = milestones.ClientFirstIncompleteGoalIndex();
		if ( idx < 0 )
		{
			ClientPinnedJournalGoalId = "";
			return;
		}

		if ( ThornsMilestoneDefinitions.TryGet( idx, out var def ) )
			ClientPinnedJournalGoalId = def.Id;
	}
}
