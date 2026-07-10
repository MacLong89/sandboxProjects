namespace Terraingen.UI.Core;

using Terraingen.UI;

/// <summary>Protected screen regions — prevents corner overcrowding and overlap.</summary>
public static class ThornsHudRegions
{
	public enum Region
	{
		TopLeft,
		TopCenter,
		TopRight,
		BottomLeft,
		BottomCenter,
		BottomRight,
		Center
	}

	public static int EdgeLeft => ThornsHudSafeZones.Scaled( ThornsHudTheme.RightHudColumnRightPx > 0 ? 24 : 24 );
	public static int EdgeRight => ThornsHudSafeZones.Scaled( ThornsHudTheme.RightHudColumnRightPx );

	/// <summary>Bottom-left stack: join announcements sit above loot feed.</summary>
	public static int JoinAnnouncementBottomPx => ThornsHudSafeZones.Scaled( 24 );

	public static int LootFeedBottomPx => ThornsHudTheme.LootFeedBottomPx;

	/// <summary>Join announcements anchor above the loot feed column.</summary>
	public static int JoinAnnouncementStackBottomPx
	{
		get
		{
			var lootTop = LootFeedBottomPx + EstimateLootFeedHeight();
			return lootTop + ThornsHudSafeZones.Scaled( 12 );
		}
	}

	static int EstimateLootFeedHeight()
	{
		var rows = Math.Min( ThornsLootFeedBus.Active.Count, 6 );
		if ( rows <= 0 )
			return ThornsHudSafeZones.Scaled( 36 );

		return rows * ThornsHudSafeZones.Scaled( 34 ) + ThornsHudSafeZones.Scaled( 8 );
	}

	public static bool RegionAllows( Region region, Region occupant ) =>
		region != occupant || region is Region.Center;
}
