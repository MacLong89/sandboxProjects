namespace Terraingen.UI.Hud;

using Sandbox.UI;
using Terraingen.GameData;
using Terraingen.Player;
using Terraingen.UI;
using Terraingen.UI.Core;
using Terraingen.UI.Presenters;
using Terraingen.Victory;

/// <summary>One-time victory path picker after first town visit.</summary>
public sealed class ThornsVictoryPathIntroHud
{
	readonly Panel _backdrop;
	readonly Panel _shell;
	bool _dismissed;

	public bool IsOpen => _backdrop.IsValid() && _backdrop.Style.Display == DisplayMode.Flex;

	public Panel Backdrop => _backdrop;

	public ThornsVictoryPathIntroHud( Panel parent )
	{
		_backdrop = ThornsUiFactory.AddPanel( parent, "victory-intro-backdrop" );
		_backdrop.Style.Position = PositionMode.Absolute;
		_backdrop.Style.Left = Length.Pixels( 0 );
		_backdrop.Style.Top = Length.Pixels( 0 );
		_backdrop.Style.Width = Length.Percent( 100 );
		_backdrop.Style.Height = Length.Percent( 100 );
		_backdrop.Style.Display = DisplayMode.None;
		_backdrop.Style.FlexDirection = FlexDirection.Column;
		_backdrop.Style.JustifyContent = Justify.Center;
		_backdrop.Style.AlignItems = Align.Center;
		_backdrop.Style.PointerEvents = PointerEvents.All;
		ThornsUiLayer.ApplyModalSurface( _backdrop, ThornsUiPriority.CriticalPopup );
		ThornsMenuChrome.ApplyMenuOverlay( _backdrop );

		_shell = ThornsUiFactory.AddPanel( _backdrop, "victory-intro-shell" );
		ThornsTheme.ApplyOpaquePanel( _shell );
		_shell.Style.BorderColor = ThornsHudTheme.Gold.WithAlpha( 0.34f );
		_shell.Style.Width = Length.Pixels( 920 );
		_shell.Style.MaxWidth = Length.Percent( 94 );
		_shell.Style.MaxHeight = Length.Percent( 92 );
		_shell.Style.FlexDirection = FlexDirection.Column;
		_shell.Style.PaddingTop = Length.Pixels( 22 );
		_shell.Style.PaddingRight = Length.Pixels( 24 );
		_shell.Style.PaddingBottom = Length.Pixels( 20 );
		_shell.Style.PaddingLeft = Length.Pixels( 24 );
		_shell.Style.PointerEvents = PointerEvents.All;
		_shell.Style.Overflow = OverflowMode.Hidden;
		_shell.Style.FlexShrink = 0;

		var header = ThornsUiFactory.AddPanel( _shell, "victory-intro-header" );
		header.Style.FlexDirection = FlexDirection.Row;
		header.Style.AlignItems = Align.FlexStart;
		header.Style.JustifyContent = Justify.SpaceBetween;
		header.Style.FlexShrink = 0;
		header.Style.Width = Length.Percent( 100 );
		header.Style.MarginBottom = Length.Pixels( 14 );
		header.Style.PaddingBottom = Length.Pixels( 12 );
		header.Style.BorderBottomWidth = Length.Pixels( 1 );
		header.Style.BorderBottomColor = ThornsHudTheme.Gold.WithAlpha( 0.22f );

		var headerMain = ThornsUiFactory.AddPanel( header, "victory-intro-header-main" );
		headerMain.Style.FlexDirection = FlexDirection.Column;
		headerMain.Style.FlexGrow = 1;
		headerMain.Style.Width = Length.Percent( 100 );
		headerMain.Style.PaddingRight = Length.Pixels( 16 );

		var title = ThornsUiFactory.AddLabel( headerMain, "SERVER VICTORY PATHS", "victory-intro-title thorns-header" );
		title.Style.Width = Length.Percent( 100 );
		title.Style.WhiteSpace = WhiteSpace.Normal;

		var blurb = ThornsUiFactory.AddPassiveLabel(
			headerMain,
			"Your server competes on four long-term paths. Pick one to track on your HUD, or choose later in the Guild tab.",
			"victory-intro-hint thorns-muted" );
		blurb.Style.Width = Length.Percent( 100 );
		blurb.Style.WhiteSpace = WhiteSpace.Normal;
		blurb.Style.LineHeight = Length.Pixels( 22 );
		blurb.Style.MarginTop = Length.Pixels( 8 );

		var closeBtn = ThornsUiFactory.AddClickable( header, "close victory-intro-close", "×", Dismiss );
		closeBtn.Style.FlexShrink = 0;

		var pathsScroll = ThornsUiFactory.AddPanel( _shell, "victory-intro-paths" );
		pathsScroll.Style.FlexDirection = FlexDirection.Row;
		pathsScroll.Style.FlexWrap = Wrap.Wrap;
		pathsScroll.Style.FlexGrow = 1;
		pathsScroll.Style.FlexShrink = 1;
		pathsScroll.Style.Width = Length.Percent( 100 );
		pathsScroll.Style.MinHeight = Length.Pixels( 0 );
		pathsScroll.Style.Overflow = OverflowMode.Scroll;
		pathsScroll.Style.PaddingRight = Length.Pixels( 2 );

		foreach ( var path in ThornsVictoryPathCatalog.All )
		{
			var pathId = path.PathId;
			var accent = ThornsVictoryUiPresenter.PathAccentColor( pathId );

			var card = ThornsUiFactory.AddClickable( pathsScroll, "victory-intro-path-card", () => SelectPath( pathId ) );
			card.AddClass( ThornsVictoryUiPresenter.PathCssClass( pathId ) );
			card.Style.FlexDirection = FlexDirection.Column;
			card.Style.FlexGrow = 1;
			card.Style.FlexShrink = 0;
			card.Style.FlexBasis = Length.Pixels( 420 );
			card.Style.MinWidth = Length.Pixels( 280 );
			card.Style.MaxWidth = Length.Percent( 49 );
			card.Style.MarginRight = Length.Pixels( 12 );
			card.Style.MarginBottom = Length.Pixels( 12 );
			card.Style.Overflow = OverflowMode.Hidden;

			var icon = ThornsUiFactory.AddPanel( card, "victory-intro-path-icon" );
			icon.Style.Width = Length.Percent( 100 );
			icon.Style.Height = Length.Pixels( 96 );
			icon.Style.FlexShrink = 0;
			if ( !ThornsIconCache.ApplyToPanel( icon, path.IconPath ) )
				icon.Style.BackgroundColor = accent.WithAlpha( 0.35f );

			var textCol = ThornsUiFactory.AddPanel( card, "victory-intro-path-text" );
			textCol.Style.FlexDirection = FlexDirection.Column;
			textCol.Style.Width = Length.Percent( 100 );
			textCol.Style.FlexShrink = 0;
			textCol.Style.FlexGrow = 1;

			var pathTitle = ThornsUiFactory.AddPassiveLabel( textCol, path.DisplayName.ToUpperInvariant(), "victory-intro-path-title" );
			pathTitle.Style.Width = Length.Percent( 100 );
			pathTitle.Style.WhiteSpace = WhiteSpace.Normal;
			pathTitle.Style.LineHeight = Length.Pixels( 20 );

			var summary = ThornsUiFactory.AddPassiveLabel( textCol, path.Summary, "victory-intro-path-summary thorns-muted" );
			summary.Style.Width = Length.Percent( 100 );
			summary.Style.WhiteSpace = WhiteSpace.Normal;
			summary.Style.LineHeight = Length.Pixels( 20 );
		}

		var footer = ThornsUiFactory.AddPanel( _shell, "victory-intro-footer" );
		footer.Style.FlexShrink = 0;
		footer.Style.Width = Length.Percent( 100 );
		footer.Style.MarginTop = Length.Pixels( 14 );
		footer.Style.PaddingTop = Length.Pixels( 12 );
		footer.Style.BorderTopWidth = Length.Pixels( 1 );
		footer.Style.BorderTopColor = ThornsHudTheme.Gold.WithAlpha( 0.18f );

		var okayBtn = ThornsUiFactory.AddClickable( footer, "victory-intro-okay thorns-btn-primary", "OKAY — CHOOSE LATER", Dismiss );
		okayBtn.Style.Width = Length.Percent( 100 );
		okayBtn.Style.JustifyContent = Justify.Center;

		_dismissed = ThornsLocalSettings.Current.VictoryPathIntroDismissed;
		if ( _dismissed )
			_backdrop.Style.Display = DisplayMode.None;
	}

	public void Tick()
	{
		if ( _dismissed || ThornsLocalSettings.Current.VictoryPathIntroDismissed )
		{
			Hide();
			return;
		}

		if ( !ThornsUiClientState.HasSnapshot )
			return;

		var goal = ThornsUiClientState.Snapshot.Journal?.Goals?.FirstOrDefault( g =>
			string.Equals( g.GoalId, "goal_place_workbench", StringComparison.OrdinalIgnoreCase ) );
		if ( goal?.State != ThornsGoalState.Completed )
			return;

		Show();
	}

	public void Dismiss() => Hide( persist: true );

	void Show()
	{
		if ( _dismissed || ThornsLocalSettings.Current.VictoryPathIntroDismissed )
			return;

		if ( !_backdrop.IsValid() || IsOpen )
			return;

		_backdrop.Style.Display = DisplayMode.Flex;
	}

	void SelectPath( string pathId )
	{
		ThornsPlayerGameplay.Local?.SetVictoryUiState( pathId );
		Dismiss();
	}

	void Hide( bool persist = false )
	{
		if ( persist )
		{
			_dismissed = true;
			ThornsLocalSettings.Current.VictoryPathIntroDismissed = true;
			ThornsLocalSettings.Save();
		}

		if ( _backdrop.IsValid() )
			_backdrop.Style.Display = DisplayMode.None;
	}
}
