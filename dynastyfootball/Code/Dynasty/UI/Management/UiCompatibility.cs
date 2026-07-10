namespace Dynasty.UI.Management;

/// <summary>
/// Global mutual exclusivity rules. No UI component may decide compatibility locally.
/// </summary>
public static class UiCompatibility
{
	public static bool CanCoexist( UiWindowType a, UiWindowType b )
	{
		if ( a == UiWindowType.None || b == UiWindowType.None || a == b )
			return a == b;

		var defA = UiWindowRegistry.Get( a );
		var defB = UiWindowRegistry.Get( b );
		if ( defA == null || defB == null )
			return false;

		if ( defA.Group == UiWindowGroup.CriticalTakeover || defB.Group == UiWindowGroup.CriticalTakeover )
			return false;

		if ( defA.Group == UiWindowGroup.FullscreenExperience && defB.Group != UiWindowGroup.None )
			return false;

		if ( defB.Group == UiWindowGroup.FullscreenExperience && defA.Group != UiWindowGroup.None )
			return false;

		if ( defA.Group == UiWindowGroup.StandardDialog && defB.Group == UiWindowGroup.StandardDialog )
			return false;

		if ( defA.Group == UiWindowGroup.StandardDialog && defB.Group == UiWindowGroup.FullscreenExperience )
			return false;

		if ( defB.Group == UiWindowGroup.StandardDialog && defA.Group == UiWindowGroup.FullscreenExperience )
			return false;

		return true;
	}

	public static bool ShouldCloseForIncoming( UiWindowType open, UiWindowType incoming )
	{
		if ( open == incoming )
			return true;

		return !CanCoexist( open, incoming );
	}
}
