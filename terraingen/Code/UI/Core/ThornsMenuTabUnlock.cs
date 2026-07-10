namespace Terraingen.UI.Core;

/// <summary>Tab menu availability — all core tabs are available from session start.</summary>
public static class ThornsMenuTabUnlock
{
	static readonly string[] AllTabs =
	{
		"Inventory", "Journal", "Tames", "Skills", "Map", "Guild", "Settings"
	};

	static bool _tabPulseActive;
	static double _tabPulseUntil;

	public static void ResetSession()
	{
		_tabPulseActive = false;
		_tabPulseUntil = 0;
	}

	public static bool IsTabPulseActive => _tabPulseActive && Time.Now < _tabPulseUntil;

	public static void NotifyMilestoneCompleted()
	{
		_tabPulseActive = true;
		_tabPulseUntil = Time.Now + 2.5;
		UiRevisionBus.Publish( UiRevisionChannel.Journal );
	}

	public static IReadOnlyList<string> GetUnlockedTabs() => AllTabs;

	public static bool IsTabUnlocked( string tabId )
	{
		foreach ( var tab in AllTabs )
		{
			if ( string.Equals( tab, tabId, StringComparison.OrdinalIgnoreCase ) )
				return true;
		}

		return false;
	}
}
