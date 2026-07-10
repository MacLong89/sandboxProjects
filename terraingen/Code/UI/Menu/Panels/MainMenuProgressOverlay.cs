namespace Terraingen.UI.Menu.Panels;

using Sandbox.UI;
using Terraingen.Clutter;
using Terraingen.UI;
using Terraingen.UI.Core;
using Terraingen.UI.Menu;

/// <summary>Full-screen dim + centered card for join/load progress (main menu server browser and in-game streaming).</summary>
public sealed class MainMenuProgressOverlay
{
	readonly Panel _overlay;
	readonly Label _title;
	readonly Label _hint;
	int _lastTipIndex = -1;

	public MainMenuProgressOverlay( Panel parent )
	{
		_overlay = ThornsUiFactory.AddPanel( parent, "mainmenu-progress-overlay" );
		_overlay.Style.Position = PositionMode.Absolute;
		_overlay.Style.Left = Length.Pixels( 0 );
		_overlay.Style.Top = Length.Pixels( 0 );
		_overlay.Style.Width = Length.Percent( 100 );
		_overlay.Style.Height = Length.Percent( 100 );
		_overlay.Style.JustifyContent = Justify.Center;
		_overlay.Style.AlignItems = Align.Center;
		_overlay.Style.PointerEvents = PointerEvents.All;
		ThornsUiLayer.ApplyModalSurface( _overlay, ThornsUiPriority.FullscreenMenu );
		_overlay.Style.Display = DisplayMode.None;

		var card = ThornsUiFactory.AddPanel( _overlay, "mainmenu-progress-card thorns-glass" );
		card.Style.FlexDirection = FlexDirection.Column;
		card.Style.AlignItems = Align.Center;
		card.Style.Padding = Length.Pixels( 36 );
		card.Style.MinWidth = Length.Pixels( 360 );
		card.Style.MaxWidth = Length.Pixels( 520 );

		_ = ThornsUiFactory.AddPanel( card, "mainmenu-progress-spinner" );

		_title = ThornsUiFactory.AddLabel( card, "", "mainmenu-progress-title" );
		_title.Style.TextAlign = TextAlign.Center;

		_hint = ThornsUiFactory.AddLabel( card, "Please wait", "mainmenu-progress-hint" );
		_hint.Style.TextAlign = TextAlign.Center;
		_hint.Style.MarginTop = Length.Pixels( 10 );

		Refresh();
	}

	public void Tick()
	{
		if ( _overlay is null || !_overlay.IsValid || _overlay.Style.Display == DisplayMode.None )
			return;

		if ( !IsOverlayVisible() || _hint is null || !_hint.IsValid )
			return;

		var index = ThornsLoadingTips.TipIndexAt( Time.Now );
		if ( index == _lastTipIndex )
			return;

		_lastTipIndex = index;
		_hint.Text = FormatLoadingTip( ThornsLoadingTips.PickTipAt( Time.Now ) );
	}

	static bool IsOverlayVisible() =>
		ThornsMenuJoinFlow.IsProgressVisible || ThornsNearbyCosmeticsReadiness.IsWaiting;

	static string FormatLoadingTip( string tip ) => string.IsNullOrWhiteSpace( tip ) ? tip : $"TIP: {tip}";

	public void Refresh()
	{
		if ( _overlay is null || !_overlay.IsValid )
			return;

		var message = ResolveProgressMessage();
		var visible = IsOverlayVisible();

		_overlay.Style.Display = visible ? DisplayMode.Flex : DisplayMode.None;
		_overlay.SetClass( "visible", visible );

		if ( _title is not null && _title.IsValid )
			_title.Text = message ?? "";

		if ( _hint is not null && _hint.IsValid )
		{
			if ( visible )
			{
				_lastTipIndex = ThornsLoadingTips.TipIndexAt( Time.Now );
				_hint.Text = FormatLoadingTip( ThornsLoadingTips.PickTipAt( Time.Now ) );
			}
			else
			{
				_lastTipIndex = -1;
				_hint.Text = "Please wait";
			}
		}
	}

	static string ResolveProgressMessage()
	{
		if ( ThornsNearbyCosmeticsReadiness.IsWaiting && string.IsNullOrWhiteSpace( ThornsMenuJoinFlow.ProgressMessage ) )
			return "Loading world around you...";

		return ThornsMenuJoinFlow.ProgressMessage;
	}
}
