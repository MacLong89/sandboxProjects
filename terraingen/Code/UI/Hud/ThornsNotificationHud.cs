namespace Terraingen.UI.Hud;

using Sandbox.UI;
using Terraingen.UI;
using Terraingen.UI.Core;

/// <summary>Player status toasts under the vitals cluster — never on minimap/objectives.</summary>
public sealed class ThornsNotificationHud
{
	readonly Panel _root;
	Action<UiRevisionChannel, int> _onRevision;

	public ThornsNotificationHud( Panel parent )
	{
		_root = ThornsUiFactory.AddPanel( parent, "notification-hud" );
		_root.Style.Position = PositionMode.Absolute;
		_root.Style.FlexDirection = FlexDirection.Column;
		_root.Style.Width = Length.Pixels(
			ThornsHudClassicChrome.IsActive
				? ThornsHudSafeZones.LeftMiddleColumnWidthPx
				: ThornsHudSafeZones.StatusToastWidthPx );
		_root.Style.MaxWidth = Length.Pixels(
			ThornsHudClassicChrome.IsActive
				? ThornsHudSafeZones.LeftMiddleColumnWidthPx
				: ThornsHudSafeZones.StatusToastWidthPx );
		ThornsUiLayer.ApplyPassive( _root, ThornsUiPriority.Toast );
		ApplySafePosition();

		_onRevision = (channel, _) =>
		{
			if ( channel is UiRevisionChannel.Notifications or UiRevisionChannel.Vitals )
				Refresh();
		};
		UiRevisionBus.MenuRevisionChanged += _onRevision;
		Refresh();
	}

	public void Dispose() => UiRevisionBus.MenuRevisionChanged -= _onRevision;

	void ApplySafePosition()
	{
		_root.Style.Top = Length.Pixels( ThornsHudSafeZones.LeftMiddleStatusToastTopPx );
		_root.Style.Left = Length.Pixels( ThornsHudSafeZones.LeftMiddleColumnLeftPx );
		_root.Style.Right = Length.Auto;
		_root.Style.AlignItems = Align.FlexStart;
	}

	public void Refresh()
	{
		if ( !_root.IsValid )
			return;

		ApplySafePosition();
		_root.DeleteChildren( true );

		foreach ( var entry in ThornsNotificationBus.Active )
		{
			var row = ThornsUiFactory.AddPanel( _root, "notify-row" );
			row.SetClass( entry.Kind, true );
			if ( ThornsHudClassicChrome.IsActive )
				row.AddClass( "hud-toast-classic" );
			else
				ThornsHudTheme.ApplyObjectivesHudPanel( row );
			row.Style.MarginBottom = Length.Pixels( 8 );
			if ( !ThornsHudClassicChrome.IsActive )
			{
				row.Style.PaddingTop = Length.Pixels( 10 );
				row.Style.PaddingBottom = Length.Pixels( 10 );
				row.Style.PaddingLeft = Length.Pixels( 14 );
				row.Style.PaddingRight = Length.Pixels( 14 );
			}
			row.Style.Opacity = 1f;

			var label = ThornsUiFactory.AddLabel( row, entry.Message, "notify-label" );
			label.Style.WhiteSpace = WhiteSpace.Normal;
			label.Style.FontSize = Length.Pixels( 12 );
			label.Style.FontColor = ThornsHudTheme.TextWarm;
		}
	}
}
