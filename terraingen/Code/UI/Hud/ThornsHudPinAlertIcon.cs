namespace Terraingen.UI.Hud;

using Sandbox.UI;
using Terraingen.GameData;
using Terraingen.UI;
using Terraingen.UI.Core;

/// <summary>Notification PNG for pinned survival log goals and interaction prompts.</summary>
public static class ThornsHudPinAlertIcon
{
	public static Panel CreateNotificationGraphic( Panel parent, out Panel iconPanel, int sizePx = 0 )
	{
		if ( sizePx <= 0 )
			sizePx = ThornsUiMetrics.HudPinnedGoalNotificationIcon;

		iconPanel = ThornsUiFactory.AddPanel( parent, "objectives-pin-alert-icon objectives-pin-notification-icon" );
		iconPanel.Style.Width = Length.Pixels( sizePx );
		iconPanel.Style.Height = Length.Pixels( sizePx );
		iconPanel.Style.FlexShrink = 0;
		iconPanel.Style.JustifyContent = Justify.Center;
		iconPanel.Style.AlignItems = Align.Center;
		iconPanel.Style.Overflow = OverflowMode.Visible;

		var graphic = ThornsUiFactory.AddPanel( iconPanel, "objectives-pin-notification-graphic slot-icon" );
		graphic.Style.Width = Length.Percent( 100 );
		graphic.Style.Height = Length.Percent( 100 );
		graphic.Style.FlexShrink = 0;
		graphic.Style.PointerEvents = PointerEvents.None;
		ThornsIconCache.ApplyToPanel( graphic, ThornsIconRegistry.Hud( "notification" ) );
		return graphic;
	}
}
