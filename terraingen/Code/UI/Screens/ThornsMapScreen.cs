namespace Terraingen.UI.Screens;

using Sandbox.UI;
using Terraingen.GameData;
using Terraingen.Player;
using Terraingen.UI;
using Terraingen.UI.Core;

public sealed partial class ThornsMapScreen : ThornsScreenBase
{
	Panel _mapViewport;
	ThornsMapView _mapView;
	Panel _legend;
	Panel _legendEntries;
	ThornsMapMarkerDto _selectedMarker;

	public ThornsMapScreen( ThornsMenuHost host, Panel parent ) : base( host, parent ) { }

	protected override void Build()
	{
		BuildMapLayout();
	}

	protected override void OnRevision( UiRevisionChannel channel, int revision )
	{
		_ = revision;
		if ( channel == UiRevisionChannel.Map )
			Rebuild();
	}

	public override void OnShown()
	{
		_mapView?.ResetUserViewport();
		base.OnShown();
	}

	public override void Rebuild()
	{
		RebuildLegend();
		RebuildMap();
	}

	public void RefreshBlip() => _mapView?.UpdatePlayerBlip( GetMapSnapshot() );

	ThornsMapSnapshotDto GetMapSnapshot() =>
		ThornsUiClientState.Snapshot?.Map ?? new ThornsMapSnapshotDto();

	void RebuildLegend()
	{
		if ( _legendEntries is null || !_legendEntries.IsValid )
			return;

		_legendEntries.DeleteChildren( true );

		foreach ( var kind in ThornsMapMarkerStyle.GetClassicLegendKinds() )
		{
			if ( kind == ThornsMapMarkerKind.You && !ThornsPlayerGameplay.Local.IsValid() )
				continue;

			AddLegendRow( kind );
		}
	}

	void AddLegendRow( ThornsMapMarkerKind kind )
	{
		var row = ThornsUiFactory.AddPanel( _legendEntries, "map-legend-row map-legend-row-concept" );
		row.Style.FlexDirection = FlexDirection.Row;
		row.Style.AlignItems = Align.Center;

		var iconWrap = ThornsUiFactory.AddPanel( row, "map-legend-icon-wrap" );
		var icon = ThornsUiFactory.AddPanel( iconWrap, "map-legend-icon" );
		icon.Style.FlexShrink = 0;
		ThornsMapMarkerStyle.StyleLegendIconPanel( icon, kind );

		ThornsUiFactory.AddPassiveLabel( row, ThornsMapMarkerStyle.GetClassicLegendTitle( kind ), "map-legend-label" );
	}

	void RebuildMap()
	{
		if ( _mapView?.Surface is null || !_mapView.Surface.IsValid )
			return;

		var tex = ThornsIconCache.GetMapTexture();
		_mapView.SetTexture( tex );
		_mapView.Surface.DeleteChildren( true );
		if ( _mapView.MarkerLayer?.IsValid == true )
			_mapView.MarkerLayer.DeleteChildren( true );

		if ( tex is null || !tex.IsValid )
		{
			ThornsTheme.CreateMuted( _mapView.Surface, "Generating world map…" );
			return;
		}

		var snap = GetMapSnapshot();
		if ( snap.Markers is null || snap.Markers.Count == 0 )
		{
			ThornsTheme.CreateMuted( _mapView.MarkerLayer, "No markers on the map yet." );
			_mapView.UpdatePlayerBlip( snap );
			return;
		}

		_mapView.RebuildMarkers(
			snap,
			useLivePlayerBlip: true,
			onMarkerClick: SelectMarker,
			selectedMarker: _selectedMarker,
			detailedTooltips: true );
		_mapView.UpdatePlayerBlip( snap );
	}

	void SelectMarker( ThornsMapMarkerDto marker )
	{
		_selectedMarker = marker;
		RebuildMap();
	}
}
