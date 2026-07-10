namespace Terraingen.UI.Menu;

/// <summary>Rotating hints shown on the join/load overlay.</summary>
public static class ThornsLoadingTips
{
	const double TipCycleSeconds = 3.0;

	static readonly string[] Tips =
	{
		"Hold RMB on food or water to eat and drink.",
		"Press I, J, K, M, G for inventory, journal, skills, map, and guild.",
		"Follow the pinned goal on the right — it is your survival guide.",
		"Punch trees and stone nodes with empty hands to gather wood and stone.",
		"Craft a stone hatchet once you have enough wood and stone.",
		"Place a bed to set your respawn point before you wander far.",
		"Press B to enter build mode once you have materials.",
		"Shift-click items to move them quickly between containers.",
		"Hunger and thirst bars flash red below 35% — refuel soon."
	};

	public static string PickTip() => PickTipAt( Time.Now );

	public static string PickTipAt( double time )
	{
		if ( Tips.Length == 0 )
			return "Please wait";

		return Tips[TipIndexAt( time )];
	}

	public static int TipIndexAt( double time )
	{
		if ( Tips.Length == 0 )
			return 0;

		return (int)(time / TipCycleSeconds) % Tips.Length;
	}
}
