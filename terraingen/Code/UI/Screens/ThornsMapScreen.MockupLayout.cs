namespace Terraingen.UI.Screens;

using Sandbox.UI;
using Terraingen.UI;

public sealed partial class ThornsMapScreen
{
	Panel _contentRow;
	Panel _mapColumn;

	void BuildMapLayout()
	{
		AddClass( "map-screen map-screen-concept" );
		Style.FlexDirection = FlexDirection.Column;
		Style.FlexGrow = 1;
		Style.MinHeight = Length.Pixels( 0 );
		Style.Width = Length.Percent( 100 );
		Style.BackgroundColor = Color.Transparent;

		_contentRow = ThornsUiFactory.AddPanel( this, "map-content-row" );
		_contentRow.Style.FlexDirection = FlexDirection.Row;
		_contentRow.Style.FlexGrow = 1;
		_contentRow.Style.FlexShrink = 1;
		_contentRow.Style.MinHeight = Length.Pixels( 0 );
		_contentRow.Style.Width = Length.Percent( 100 );

		_mapColumn = ThornsTheme.CreateMenuSectionWindow( _contentRow,
			"thorns-col-center map-main-column map-main-column-concept", flexWeight: 72f );
		_mapColumn.Style.FlexDirection = FlexDirection.Column;
		_mapColumn.Style.MinHeight = Length.Pixels( 0 );
		_mapColumn.Style.MinWidth = Length.Pixels( 0 );
		_mapColumn.Style.Padding = Length.Pixels( 8 );

		_mapViewport = ThornsUiFactory.AddPanel( _mapColumn, "map-viewport map-viewport-parchment map-viewport-concept" );
		_mapViewport.Style.FlexGrow = 1;
		_mapViewport.Style.FlexShrink = 1;
		_mapViewport.Style.MinHeight = Length.Pixels( 0 );
		_mapViewport.Style.MinWidth = Length.Pixels( 0 );
		_mapViewport.Style.Position = PositionMode.Relative;
		_mapViewport.Style.Overflow = OverflowMode.Hidden;

		var gridOverlay = ThornsUiFactory.AddPanel( _mapViewport, "map-grid-overlay" );
		gridOverlay.Style.Position = PositionMode.Absolute;
		gridOverlay.Style.Left = Length.Pixels( 0 );
		gridOverlay.Style.Top = Length.Pixels( 0 );
		gridOverlay.Style.Width = Length.Percent( 100 );
		gridOverlay.Style.Height = Length.Percent( 100 );
		gridOverlay.Style.PointerEvents = PointerEvents.None;

		_mapView = ThornsMapView.Create( _mapViewport, includePlayerBlip: true, scrollZoomEnabled: true );
		if ( _mapView.Square is ThornsMapViewportPanel viewport )
			viewport.MapView = _mapView;

		var compass = ThornsUiFactory.AddPanel( _mapViewport, "map-compass-rose map-compass-rose-concept" );
		compass.Style.Position = PositionMode.Absolute;
		compass.Style.Left = Length.Pixels( 14 );
		compass.Style.Bottom = Length.Pixels( 14 );
		compass.Style.PointerEvents = PointerEvents.None;
		ThornsUiFactory.AddPassiveLabel( compass, "N", "map-compass-n map-compass-north" );
		ThornsUiFactory.AddPassiveLabel( compass, "E", "map-compass-e map-compass-east" );
		ThornsUiFactory.AddPassiveLabel( compass, "S", "map-compass-s map-compass-south" );
		ThornsUiFactory.AddPassiveLabel( compass, "W", "map-compass-w map-compass-west" );

		ThornsTheme.CreateWoodColumnDivider( _contentRow );

		_legend = ThornsTheme.CreateMenuSectionWindow( _contentRow,
			"thorns-col-right map-legend-column map-legend-column-concept", flexWeight: 28f );
		_legend.Style.FlexDirection = FlexDirection.Column;
		_legend.Style.MinHeight = Length.Pixels( 0 );
		_legend.Style.MinWidth = Length.Pixels( 0 );
		_legend.Style.Padding = Length.Pixels( 10 );

		var legendHeader = ThornsUiFactory.AddPanel( _legend, "map-legend-header map-legend-header-concept" );
		ThornsTheme.CreateSectionHeader( legendHeader, "MAP LEGEND" );

		_legendEntries = ThornsUiFactory.AddPanel( _legend, "map-legend-entries map-legend-entries-concept" );
		_legendEntries.Style.FlexDirection = FlexDirection.Column;
		_legendEntries.Style.FlexGrow = 1;
		_legendEntries.Style.MinHeight = Length.Pixels( 0 );
		_legendEntries.Style.Overflow = OverflowMode.Scroll;
	}

}
