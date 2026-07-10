namespace Terraingen.UI.Hud;

using Sandbox.UI;
using Terraingen.UI;

/// <summary>Right HUD stack: minimap (Classic: bottom-right) and objectives (non-Classic).</summary>
public sealed class ThornsRightHudColumn
{
	readonly Panel _root;
	readonly bool _classicLayout;

	public ThornsMinimapHud Minimap { get; }
	public ThornsObjectivesHud Objectives { get; }

	public ThornsRightHudColumn( Panel hudLayer, bool includeObjectives = true )
	{
		_classicLayout = ThornsHudClassicChrome.IsActive;

		_root = ThornsUiFactory.AddPanel( hudLayer, _classicLayout ? "right-hud-column right-hud-column-classic" : "right-hud-column" );
		_root.Style.Position = PositionMode.Absolute;
		if ( _classicLayout )
		{
			_root.Style.Top = Length.Auto;
			_root.Style.Bottom = Length.Pixels( ThornsHudTheme.ClassicMinimapBottomPx );
			_root.Style.Right = Length.Pixels( ThornsHudTheme.ClassicMinimapRightPx );
			_root.Style.Left = Length.Auto;
			_root.Style.Width = Length.Pixels( ThornsHudTheme.ClassicMinimapSizePx + 24 );
		}
		else
		{
			_root.Style.Top = Length.Pixels( ThornsHudTheme.RightHudColumnTopPx );
			_root.Style.Right = Length.Pixels( ThornsHudTheme.RightHudColumnRightPx );
			_root.Style.Width = Length.Pixels( ThornsHudTheme.RightHudColumnWidthPx );
		}

		_root.Style.FlexDirection = FlexDirection.Column;
		_root.Style.AlignItems = Align.Stretch;
		_root.Style.PointerEvents = PointerEvents.None;

		Minimap = new ThornsMinimapHud( _root, includeTimeRow: true );

		if ( includeObjectives && !_classicLayout )
			Objectives = new ThornsObjectivesHud( _root );
	}

	public void RefreshMinimap( bool force = false ) => Minimap.Refresh( force );

	public void RefreshMinimapBlip( bool force = false ) => Minimap.RefreshBlip( force );

	public void RefreshObjectives() => Objectives?.Refresh();

	public void Dispose()
	{
	}
}
