using Sandbox.UI;
using Terraingen.GameData;
using Terraingen.Player;
using Terraingen.UI;
using Terraingen.UI.Core;
using Terraingen.Core;
using Terraingen.World.Environment;

namespace Terraingen.UI.Hud;

public sealed class ThornsMinimapHud
{
	readonly ThornsMapView _mapView;
	readonly Label _time;
	string _markerSignature = "";
	string _lastTimeText = "";
	float _timeRefreshTimer;
	float _blipRefreshTimer;
	float _markerRefreshTimer;
	string _cachedMapTextureKey = "";

	public ThornsMinimapHud( Panel parent, bool includeTimeRow = true )
	{
		var classic = ThornsHudClassicChrome.IsActive;

		var root = ThornsUiFactory.AddPanel( parent, classic ? "minimap-hud minimap-hud-classic" : "minimap-hud" );
		root.Style.FlexDirection = FlexDirection.Column;
		root.Style.AlignItems = classic ? Align.Center : Align.FlexEnd;
		root.Style.Width = Length.Percent( 100 );

		var mapSize = classic ? ThornsHudTheme.ClassicMinimapSizePx : ThornsHudTheme.RightHudColumnWidthPx;
		var mapInnerPx = mapSize - 6;
		var frame = ThornsUiFactory.AddPanel( root, "minimap-frame minimap-frame-concept" );
		frame.Style.Width = Length.Pixels( mapSize );
		frame.Style.Height = Length.Pixels( mapSize );

		var ring = ThornsUiFactory.AddPanel( frame, "minimap-ring minimap-ring-concept" );
		ring.Style.Width = Length.Pixels( mapInnerPx );
		ring.Style.Height = Length.Pixels( mapInnerPx );
		ring.Style.BackgroundColor = ThornsMapProjection.MapOceanColor;

		_mapView = ThornsMapView.Create( ring, includePlayerBlip: true, playerCenteredMinimap: true );
		_mapView.ConfigureMinimapViewport( mapInnerPx );

		if ( includeTimeRow )
		{
			var timeRow = ThornsUiFactory.AddPanel( root, "hud-time-row hud-time-row-minimap" );
			timeRow.Style.FlexDirection = FlexDirection.Row;
			timeRow.Style.AlignItems = Align.Center;
			timeRow.Style.JustifyContent = Justify.Center;
			timeRow.Style.AlignSelf = Align.Center;
			timeRow.Style.MarginTop = Length.Pixels( 8 );
			ThornsUiFactory.AddLabel( timeRow, "☀", "hud-time-icon" );
			_time = ThornsUiFactory.AddLabel( timeRow, "12:00 PM", "hud-time-label" );
		}
		else
		{
			_time = null;
		}
	}

	public void Refresh( bool force = false ) => Refresh( force, blipOnly: false );

	public void RefreshBlip( bool force = false ) => Refresh( force, blipOnly: true );

	public void Refresh( bool force, bool blipOnly )
	{
		if ( !_mapView.Surface.IsValid )
			return;

		if ( !blipOnly )
		{
			var tex = ThornsIconCache.GetMapTexture();
			var texKey = tex is not null && tex.IsValid ? tex.ResourcePath : "";
			if ( force || !string.Equals( _cachedMapTextureKey, texKey, StringComparison.Ordinal ) )
			{
				_cachedMapTextureKey = texKey;
				_mapView.SetTexture( tex );
			}

			_timeRefreshTimer -= Time.Delta;
			if ( force || _timeRefreshTimer <= 0f )
			{
				_timeRefreshTimer = 0.5f;
				UpdateTime();
			}

			_markerRefreshTimer -= Time.Delta;
			if ( force || _markerRefreshTimer <= 0f )
			{
				_markerRefreshTimer = ThornsHudTickRates.MinimapMarkersSeconds;
				UpdateMarkers();
			}
		}

		_blipRefreshTimer -= Time.Delta;
		if ( force || blipOnly || _blipRefreshTimer <= 0f )
		{
			_blipRefreshTimer = ThornsHudTickRates.MinimapBlipSeconds;
			UpdateBlip();
		}
	}

	void UpdateTime()
	{
		if ( _time is null || !_time.IsValid )
			return;

		var scene = Game.ActiveScene;
		if ( scene is null || !ThornsTimeOfDaySystem.TryGet( scene, out var time ) || !time.IsValid() )
		{
			_time.Text = DateTime.Now.ToString( "h:mm tt" );
			return;
		}

		var hours = time.ResolvedHours;
		var h = (int)hours % 24;
		var m = (int)((hours - h) * 60f);
		var dt = DateTime.Today.AddHours( h ).AddMinutes( m );
		var text = dt.ToString( "h:mm tt" );
		if ( text == _lastTimeText )
			return;

		_lastTimeText = text;
		_time.Text = text;
	}

	void UpdateBlip()
	{
		_mapView.UpdatePlayerBlip( ThornsUiClientState.Snapshot?.Map );
	}

	void UpdateMarkers()
	{
		var map = ThornsUiClientState.Snapshot?.Map;
		if ( map?.Markers is null )
		{
			if ( string.IsNullOrEmpty( _markerSignature ) )
				return;

			_markerSignature = "";
			_mapView.MarkerLayer.DeleteChildren( true );
			return;
		}

		var signature = BuildMarkerSignature( map );
		if ( string.Equals( _markerSignature, signature, StringComparison.Ordinal ) )
			return;

		_markerSignature = signature;
		_mapView.RebuildMarkers( map, useLivePlayerBlip: true );
	}

	static string BuildMarkerSignature( ThornsMapSnapshotDto map )
	{
		var hash = new HashCode();
		hash.Add( MathF.Round( map.WorldMinX ) );
		hash.Add( MathF.Round( map.WorldMinY ) );
		hash.Add( MathF.Round( map.WorldMaxX ) );
		hash.Add( MathF.Round( map.WorldMaxY ) );
		foreach ( var marker in map.Markers )
		{
			if ( ThornsMapMarkerStyle.UseLivePlayerBlip( marker.Kind ) )
				continue;

			hash.Add( marker.Kind );
			hash.Add( MathF.Round( marker.WorldX ) );
			hash.Add( MathF.Round( marker.WorldY ) );
			hash.Add( marker.Label ?? "" );
		}

		return hash.ToHashCode().ToString();
	}
}
