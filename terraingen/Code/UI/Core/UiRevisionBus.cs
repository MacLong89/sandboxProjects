namespace Terraingen.UI;

/// <summary>Event-driven UI invalidation. Gameplay systems publish; UI reads revision only.</summary>
public enum UiRevisionChannel
{
	Inventory,
	Journal,
	Skills,
	Guild,
	Tames,
	TameFeedNotice,
	Map,
	Vitals,
	Settings,
	Menu,
	Craft,
	Hotbar,
	LootFeed,
	Milestones,
	WorldContainer,
	RadioShop,
	Research,
	Campfire,
	Workbench,
	Victory,
	Interaction,
	BuildMenu,
	Compass,
	Notifications,
	WorldEvents
}

public static class UiRevisionBus
{
	static readonly int[] Revisions = new int[Enum.GetValues<UiRevisionChannel>().Length];

	public static event Action<UiRevisionChannel, int> MenuRevisionChanged;

	/// <summary>Clears stale menu handlers after hotload (old panels may not unhook).</summary>
	public static void ResetMenuListeners() => MenuRevisionChanged = null;

	public static void Publish( UiRevisionChannel channel )
	{
		var i = (int)channel;
		Revisions[i]++;
		var revision = Revisions[i];
		var handlers = MenuRevisionChanged;
		if ( handlers is null )
			return;

		foreach ( var del in handlers.GetInvocationList() )
		{
			if ( del is not Action<UiRevisionChannel, int> handler )
				continue;

			try
			{
				handler( channel, revision );
			}
			catch ( Exception e )
			{
				Log.Warning( e, "[Thorns UI] Stale menu revision handler removed." );
				try
				{
					MenuRevisionChanged -= handler;
				}
				catch
				{
					MenuRevisionChanged = null;
				}
			}
		}
	}

	public static int GetRevision( UiRevisionChannel channel ) => Revisions[(int)channel];
}
