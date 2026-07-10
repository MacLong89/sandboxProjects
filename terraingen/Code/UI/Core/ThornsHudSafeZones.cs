namespace Terraingen.UI.Core;

using Terraingen.UI;

/// <summary>Responsive HUD safe zones — prevents corner collisions across resolutions.</summary>
public static class ThornsHudSafeZones
{
	public const int MinEdgePx = 16;
	public const int StandardEdgePx = 24;
	public const int WideEdgePx = 32;

	public static int ViewportWidth
	{
		get
		{
			var w = Screen.Width;
			return w > 0 ? (int)w : 1920;
		}
	}

	public static int ViewportHeight
	{
		get
		{
			var h = Screen.Height;
			return h > 0 ? (int)h : 1080;
		}
	}

	public static float ScaleFactor
	{
		get
		{
			var w = ViewportWidth;
			if ( w >= 3440 ) return 1.15f;
			if ( w >= 2560 ) return 1.08f;
			if ( w <= 1280 ) return 0.88f;
			return 1f;
		}
	}

	public static int EdgeInset =>
		ViewportWidth >= 2560 ? WideEdgePx : StandardEdgePx;

	public static int Scaled( int designPx ) =>
		Math.Max( MinEdgePx, (int)MathF.Round( designPx * ScaleFactor * ThornsLocalSettings.Current.UiScale ) );

	/// <summary>Left-column status toasts — below vitals and the level-up slot.</summary>
	public static int LevelUpToastTopPx => Scaled( ThornsHudTheme.LevelUpToastTopPx );

	public static int LevelUpToastLeftPx => Scaled( ThornsHudTheme.VitalsClusterLeftPx );

	public static int LevelUpToastWidthPx => Scaled( ThornsHudTheme.VitalsClusterWidthPx );

	public static int StatusToastTopPx => Scaled( ThornsHudTheme.StatusToastTopPx );

	public static int StatusToastLeftPx => Scaled( ThornsHudTheme.VitalsClusterLeftPx );

	public static int StatusToastWidthPx => Scaled( ThornsHudTheme.VitalsClusterWidthPx );

	/// <summary>Middle-left column — loot gather lines and compact status toasts.</summary>
	public static int LeftMiddleColumnLeftPx => Scaled( ThornsHudTheme.ClassicLeftColumnLeftPx );

	public static int LeftMiddleColumnWidthPx => Scaled( ThornsHudTheme.LootFeedMaxWidthPx );

	static int LeftMiddleAnchorTopPx => Math.Max( Scaled( 180 ), ViewportHeight / 2 - Scaled( 96 ) );

	/// <summary>Gather feed — stacked upward from just above screen center.</summary>
	public static int LeftMiddleLootFeedTopPx => LeftMiddleAnchorTopPx - Scaled( 72 );

	/// <summary>Status toasts — stacked downward from just below the gather feed.</summary>
	public static int LeftMiddleStatusToastTopPx => LeftMiddleAnchorTopPx + Scaled( 12 );

	/// <summary>Combat-safe zone — avoid covering crosshair center.</summary>
	public static bool IsInCrosshairZone( int x, int y, int margin = 120 )
	{
		var cx = ViewportWidth / 2;
		var cy = ViewportHeight / 2;
		return Math.Abs( x - cx ) < margin && Math.Abs( y - cy ) < margin;
	}
}
