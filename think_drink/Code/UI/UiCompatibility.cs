namespace ThinkDrink.UI;

/// <summary>Global mutual-exclusivity and coexistence rules — no window decides this locally.</summary>
public static class UiCompatibility
{
	public static bool CanCoexist( UiWindowId a, UiWindowId b )
	{
		if ( a == UiWindowId.None || b == UiWindowId.None || a == b )
			return true;

		var defA = UiWindowRegistry.Get( a );
		var defB = UiWindowRegistry.Get( b );

		if ( defA.Group == UiWindowGroup.ForcedModal || defB.Group == UiWindowGroup.ForcedModal )
		{
			var forced = defA.Group == UiWindowGroup.ForcedModal ? a : b;
			var other = forced == a ? b : a;
			return other is UiWindowId.FlashFeedback or UiWindowId.LevelUpBanner;
		}

		if ( defA.Group == UiWindowGroup.OverlayPanel && defB.Group == UiWindowGroup.OverlayPanel )
			return false;

		if ( defA.Group == UiWindowGroup.DevTool && defB.Group == UiWindowGroup.OverlayPanel )
			return false;
		if ( defB.Group == UiWindowGroup.DevTool && defA.Group == UiWindowGroup.OverlayPanel )
			return false;

		if ( defA.Group == UiWindowGroup.DevTool && defB.Group == UiWindowGroup.ForcedModal )
			return false;
		if ( defB.Group == UiWindowGroup.DevTool && defA.Group == UiWindowGroup.ForcedModal )
			return false;

		return true;
	}

	public static bool CanOpen( UiWindowId target, IReadOnlyCollection<UiWindowId> active )
	{
		if ( target == UiWindowId.None )
			return false;

		foreach ( var open in active )
		{
			if ( !CanCoexist( target, open ) )
				return false;
		}

		return true;
	}

	public static IEnumerable<UiWindowId> GetConflicts( UiWindowId target, IReadOnlyCollection<UiWindowId> active )
	{
		foreach ( var open in active )
		{
			if ( !CanCoexist( target, open ) )
				yield return open;
		}
	}
}
