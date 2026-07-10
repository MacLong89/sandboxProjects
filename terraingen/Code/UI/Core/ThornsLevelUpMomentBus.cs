namespace Terraingen.UI;

using Terraingen.UI.Core;

/// <summary>Routes level-up moments to the HUD overlay — deferred during combat.</summary>
public static class ThornsLevelUpMomentBus
{
	public static int PendingLevel { get; private set; }
	public static int Revision { get; private set; }

	public static void Show( int level )
	{
		PendingLevel = Math.Max( PendingLevel, Math.Max( 1, level ) );
		Revision++;
		UiRevisionBus.Publish( UiRevisionChannel.Vitals );
	}

	public static int ConsumePendingLevel()
	{
		if ( PendingLevel <= 0 )
			return 0;

		if ( ThornsUiGameplayState.ShouldSuppressAchievements )
			return 0;

		var level = PendingLevel;
		PendingLevel = 0;
		return level;
	}
}
