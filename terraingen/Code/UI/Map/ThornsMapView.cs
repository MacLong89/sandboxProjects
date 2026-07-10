using Sandbox.UI;
using Terraingen.GameData;
using Terraingen.Player;
using Terraingen.TerrainGen;
using Terraingen.UI.Core;

namespace Terraingen.UI;

/// <summary>Shared square map surface + marker layer used by the world map screen and minimap HUD.</summary>
public sealed class ThornsMapView
{
	public const float MinimapZoom = 1.3f;
	public const float MinimapPlayerBlipSizePx = ThornsMapMarkerStyle.MinimapPlayerBlipSizePx;
	public const float WorldMapPlayerBlipSizePx = ThornsMapMarkerStyle.WorldMapPlayerBlipSizePx;
	public const float MinUserZoom = 1f;
	public const float MaxUserZoom = 5f;
	const float ScrollZoomStep = 1.12f;

	public Panel Square { get; }
	public Panel Surface { get; }
	public Panel MarkerLayer { get; }
	public Panel PlayerBlip { get; }
	public Panel PlayerBlipArrow { get; }
	public bool PlayerCenteredMinimap { get; }
	public bool ScrollZoomEnabled { get; }

	readonly Panel _content;
	float _userZoom = 1f;
	float _viewLeft;
	float _viewTop;
	float _minimapViewportSizePx;

	ThornsMapView(
		Panel square,
		Panel content,
		Panel surface,
		Panel markerLayer,
		Panel playerBlip,
		Panel playerBlipArrow,
		bool playerCenteredMinimap,
		bool scrollZoomEnabled )
	{
		Square = square;
		_content = content;
		Surface = surface;
		MarkerLayer = markerLayer;
		PlayerBlip = playerBlip;
		PlayerBlipArrow = playerBlipArrow;
		PlayerCenteredMinimap = playerCenteredMinimap;
		ScrollZoomEnabled = scrollZoomEnabled;
	}

	public static ThornsMapView Create( Panel parent, bool includePlayerBlip, bool playerCenteredMinimap = false, bool scrollZoomEnabled = false )
	{
		Panel square;
		if ( scrollZoomEnabled )
		{
			var viewport = new ThornsMapViewportPanel( parent, "map-square" );
			square = viewport;
		}
		else
		{
			square = ThornsUiFactory.AddPanel( parent, "map-square" );
		}

		square.Style.Position = PositionMode.Relative;
		square.Style.Width = Length.Percent( 100 );
		square.Style.Height = Length.Percent( 100 );

		if ( playerCenteredMinimap || scrollZoomEnabled )
			square.Style.Overflow = OverflowMode.Hidden;

		if ( playerCenteredMinimap )
		{
			var backdrop = ThornsUiFactory.AddPanel( square, "minimap-backdrop" );
			backdrop.Style.Position = PositionMode.Absolute;
			backdrop.Style.Left = Length.Pixels( 0 );
			backdrop.Style.Top = Length.Pixels( 0 );
			backdrop.Style.Width = Length.Percent( 100 );
			backdrop.Style.Height = Length.Percent( 100 );
			backdrop.Style.BackgroundColor = ThornsMapProjection.MapOceanColor;
		}

		var host = square;
		Panel content = null;
		if ( scrollZoomEnabled || playerCenteredMinimap )
		{
			content = ThornsUiFactory.AddPanel( square, "map-content" );
			content.Style.Position = PositionMode.Absolute;
			content.Style.Left = Length.Pixels( 0 );
			if ( scrollZoomEnabled )
			{
				content.Style.Top = Length.Pixels( 0 );
				content.Style.Width = Length.Percent( 100 );
				content.Style.Height = Length.Percent( 100 );
			}
			else if ( playerCenteredMinimap )
			{
				content.Style.Bottom = Length.Pixels( 0 );
				content.Style.Width = Length.Fraction( MinimapZoom );
				content.Style.Height = Length.Fraction( MinimapZoom );
			}

			host = content;
		}

		var surfaceClass = "map-surface map-cover";
		var surface = ThornsUiFactory.AddPanel( host, surfaceClass );
		surface.Style.Position = PositionMode.Absolute;
		surface.Style.Left = Length.Pixels( 0 );
		surface.Style.Top = Length.Pixels( 0 );
		surface.Style.Width = Length.Percent( 100 );
		surface.Style.Height = Length.Percent( 100 );
		if ( playerCenteredMinimap )
			surface.Style.BackgroundColor = ThornsMapProjection.MapOceanColor;
		else if ( scrollZoomEnabled )
			surface.Style.BackgroundColor = new Color( 0.91f, 0.85f, 0.75f );

		var markerLayer = ThornsUiFactory.AddPanel( host, "map-marker-layer" );
		markerLayer.Style.Position = PositionMode.Absolute;
		markerLayer.Style.Left = Length.Pixels( 0 );
		markerLayer.Style.Top = Length.Pixels( 0 );
		markerLayer.Style.Width = Length.Percent( 100 );
		markerLayer.Style.Height = Length.Percent( 100 );

		Panel playerBlip = null;
		Panel playerBlipArrow = null;
		if ( includePlayerBlip )
		{
			var blipHost = playerCenteredMinimap ? square : host;
			var blipSizePx = playerCenteredMinimap ? MinimapPlayerBlipSizePx : WorldMapPlayerBlipSizePx;
			playerBlip = ThornsUiFactory.AddPanel( blipHost, "map-player-blip" );
			playerBlip.Style.Position = PositionMode.Absolute;
			playerBlip.Style.PointerEvents = PointerEvents.None;
			playerBlip.Style.BackgroundColor = new Color( 0f, 0f, 0f, 0f );
			if ( playerCenteredMinimap )
			{
				playerBlip.AddClass( "minimap-player-blip" );
				ThornsUiLayer.ApplyLocalOrder( playerBlip, 5 );
			}

			playerBlipArrow = ThornsUiFactory.AddPanel( playerBlip, "map-player-blip-arrow" );
			playerBlipArrow.Style.Position = PositionMode.Absolute;
			playerBlipArrow.Style.Left = Length.Pixels( 0 );
			playerBlipArrow.Style.Top = Length.Pixels( 0 );
			StylePlayerBlipPanel( playerBlip, playerBlipArrow, blipSizePx );
		}

		return new ThornsMapView( square, content, surface, markerLayer, playerBlip, playerBlipArrow, playerCenteredMinimap, scrollZoomEnabled );
	}

	/// <summary>Locks the minimap viewport to a known square size so pan math can resolve before layout.</summary>
	public void ConfigureMinimapViewport( float sizePx )
	{
		if ( !PlayerCenteredMinimap || sizePx <= 1f )
			return;

		_minimapViewportSizePx = sizePx;
		if ( !Square.IsValid() )
			return;

		Square.Style.Width = Length.Pixels( sizePx );
		Square.Style.Height = Length.Pixels( sizePx );
		Square.Style.MinWidth = Length.Pixels( sizePx );
		Square.Style.MinHeight = Length.Pixels( sizePx );
		Square.Style.MaxWidth = Length.Pixels( sizePx );
		Square.Style.MaxHeight = Length.Pixels( sizePx );
		Square.Style.FlexShrink = 0;
	}

	public void ResetUserViewport()
	{
		_userZoom = 1f;
		_viewLeft = 0f;
		_viewTop = 0f;
		ApplyUserViewportStyles();
	}

	public void ZoomBy( float factor )
	{
		if ( !ScrollZoomEnabled || _content is null || !_content.IsValid || !Square.IsValid )
			return;

		var inner = Square.Box.RectInner;
		var width = inner.Width;
		var height = inner.Height;
		if ( width <= 1f || height <= 1f )
			return;

		var mx = 0.5f;
		var my = 0.5f;
		var mapU = (mx - _viewLeft) / _userZoom;
		var mapV = (my - _viewTop) / _userZoom;

		var newZoom = Math.Clamp( _userZoom * factor, MinUserZoom, MaxUserZoom );
		if ( MathF.Abs( newZoom - _userZoom ) < 0.001f )
			return;

		_userZoom = newZoom;
		var minOffset = 1f - _userZoom;
		_viewLeft = Math.Clamp( mx - mapU * _userZoom, minOffset, 0f );
		_viewTop = Math.Clamp( my - mapV * _userZoom, minOffset, 0f );
		ApplyUserViewportStyles();
	}

	public void CenterOnPlayer( ThornsMapSnapshotDto snap )
	{
		if ( !ScrollZoomEnabled || _content is null || !_content.IsValid )
			return;

		var player = ThornsPlayerGameplay.Local;
		if ( !player.IsValid() || snap is null )
			return;

		if ( !ThornsMapProjection.TryWorldToMap01( snap, player.GameObject.WorldPosition.x, player.GameObject.WorldPosition.y, out var u, out var v ) )
			return;

		var minOffset = 1f - _userZoom;
		_viewLeft = Math.Clamp( 0.5f - u * _userZoom, minOffset, 0f );
		_viewTop = Math.Clamp( 0.5f - v * _userZoom, minOffset, 0f );
		ApplyUserViewportStyles();
	}

	public void ApplyScrollZoom( float wheelDelta, Vector2 localMousePos )
	{
		if ( !ScrollZoomEnabled || _content is null || !_content.IsValid || !Square.IsValid() )
			return;

		if ( MathF.Abs( wheelDelta ) < 0.01f )
			return;

		var inner = Square.Box.RectInner;
		var width = inner.Width;
		var height = inner.Height;
		if ( width <= 1f || height <= 1f )
			return;

		var mx = Math.Clamp( localMousePos.x / width, 0f, 1f );
		var my = Math.Clamp( localMousePos.y / height, 0f, 1f );
		var mapU = (mx - _viewLeft) / _userZoom;
		var mapV = (my - _viewTop) / _userZoom;

		var step = wheelDelta > 0f ? ScrollZoomStep : 1f / ScrollZoomStep;
		var newZoom = Math.Clamp( _userZoom * step, MinUserZoom, MaxUserZoom );
		if ( MathF.Abs( newZoom - _userZoom ) < 0.001f )
			return;

		_userZoom = newZoom;
		var minOffset = 1f - _userZoom;
		_viewLeft = Math.Clamp( mx - mapU * _userZoom, minOffset, 0f );
		_viewTop = Math.Clamp( my - mapV * _userZoom, minOffset, 0f );
		ApplyUserViewportStyles();
	}

	void ApplyUserViewportStyles()
	{
		if ( _content is null || !_content.IsValid )
			return;

		_content.Style.Width = Length.Fraction( _userZoom );
		_content.Style.Height = Length.Fraction( _userZoom );
		_content.Style.Left = Length.Fraction( _viewLeft );
		_content.Style.Top = Length.Fraction( _viewTop );
	}

	public void SetTexture( Texture texture )
	{
		if ( !Surface.IsValid )
			return;

		Surface.Style.BackgroundImage = texture is not null && texture.IsValid ? texture : null;
	}

	public void UpdatePlayerBlip( ThornsMapSnapshotDto snap )
	{
		if ( PlayerBlip is null || !PlayerBlip.IsValid )
			return;

		var player = ThornsPlayerGameplay.Local;
		if ( !player.IsValid() || snap is null || !ThornsMapProjection.TryWorldToMap01( snap, player.GameObject.WorldPosition.x, player.GameObject.WorldPosition.y, out var u, out var v ) )
		{
			PlayerBlip.Style.Display = DisplayMode.None;
			return;
		}

		PlayerBlip.Style.Display = DisplayMode.Flex;
		var pos = player.GameObject.WorldPosition;

		if ( PlayerCenteredMinimap )
		{
			var zoom = MathF.Max( 1f, MinimapZoom );
			var panLeft = 0.5f - u * zoom;
			var panTop = 0.5f - v * zoom;

			ApplyMinimapContentPan( panLeft, panTop, zoom );
			ApplyMinimapPlayerBlipPosition();

			var rectW = 0f;
			var rectH = 0f;
			if ( Square.IsValid() )
			{
				var inner = Square.Box.RectInner;
				rectW = inner.Width;
				rectH = inner.Height;
			}

			ThornsMapProjection.TryResolveActiveBounds( snap, out var boundsMinX, out var boundsMinY, out var boundsMaxX, out var boundsMaxY );
			ThornsMinimapDiagnostics.LogUpdate(
				pos,
				boundsMinX,
				boundsMinY,
				boundsMaxX,
				boundsMaxY,
				u,
				v,
				panLeft,
				panTop,
				_minimapViewportSizePx,
				rectW,
				rectH );
		}
		else
		{
			ThornsMapMarkerStyle.ApplyMarkerPosition( PlayerBlip, u, v );
		}

		if ( PlayerBlipArrow is not null && PlayerBlipArrow.IsValid
		     && ThornsMapMarkerStyle.TryResolvePlayerMapFacingDegrees( player.GameObject, out var facingDeg ) )
			ThornsMapMarkerStyle.ApplyPlayerBlipRotation( PlayerBlipArrow, facingDeg );
	}

	void ApplyMinimapPlayerBlipPosition()
	{
		if ( PlayerBlip is null || !PlayerBlip.IsValid )
			return;

		var half = MinimapPlayerBlipSizePx * 0.5f;
		PlayerBlip.Style.Left = Length.Fraction( 0.5f );
		PlayerBlip.Style.Top = Length.Fraction( 0.5f );
		PlayerBlip.Style.MarginLeft = Length.Pixels( -half );
		PlayerBlip.Style.MarginTop = Length.Pixels( -half );
	}

	static void StylePlayerBlipPanel( Panel blipHost, Panel arrow, float sizePx )
	{
		if ( blipHost is null || !blipHost.IsValid || arrow is null || !arrow.IsValid )
			return;

		var half = sizePx * 0.5f;
		blipHost.Style.Width = Length.Pixels( sizePx );
		blipHost.Style.Height = Length.Pixels( sizePx );
		blipHost.Style.MarginLeft = Length.Pixels( -half );
		blipHost.Style.MarginTop = Length.Pixels( -half );
		arrow.Style.Width = Length.Percent( 100 );
		arrow.Style.Height = Length.Percent( 100 );
		ThornsMapMarkerStyle.StylePlayerBlipPanel( arrow );
	}

	void ApplyMinimapContentPan( float panLeft, float panTop, float zoom )
	{
		if ( _content is null || !_content.IsValid )
			return;

		// s&box UI reliably applies Left + Bottom on absolute panels; Top often ignores updates in flex minimap layout.
		_content.Style.Width = Length.Fraction( zoom );
		_content.Style.Height = Length.Fraction( zoom );
		_content.Style.Left = Length.Fraction( panLeft );
		_content.Style.Top = Length.Auto;
		_content.Style.Bottom = Length.Fraction( 1f - panTop - zoom );
	}

	float ResolveMinimapViewportPx()
	{
		if ( _minimapViewportSizePx > 1f )
			return _minimapViewportSizePx;

		if ( Square.IsValid() )
		{
			var inner = Square.Box.RectInner;
			var size = MathF.Max( inner.Width, inner.Height );
			if ( size > 1f )
				return size;
		}

		return ThornsHudTheme.RightHudColumnWidthPx - 6f;
	}

	static bool ResolvePlayerMapUv( ThornsMapSnapshotDto snap, float worldX, float worldY, out float u, out float v ) =>
		ThornsMapProjection.TryWorldToMap01( snap, worldX, worldY, out u, out v );

	public void RebuildMarkers(
		ThornsMapSnapshotDto snap,
		bool useLivePlayerBlip,
		Action<ThornsMapMarkerDto> onMarkerClick = null,
		ThornsMapMarkerDto selectedMarker = null,
		bool detailedTooltips = false )
	{
		if ( !MarkerLayer.IsValid )
			return;

		MarkerLayer.DeleteChildren( true );

		if ( snap?.Markers is null || snap.Markers.Count == 0 )
			return;

		foreach ( var marker in snap.Markers )
		{
			if ( useLivePlayerBlip && ThornsMapMarkerStyle.UseLivePlayerBlip( marker.Kind ) )
				continue;

			RenderMarker( snap, marker, onMarkerClick, selectedMarker, detailedTooltips );
		}
	}

	void RenderMarker(
		ThornsMapSnapshotDto snap,
		ThornsMapMarkerDto marker,
		Action<ThornsMapMarkerDto> onMarkerClick,
		ThornsMapMarkerDto selectedMarker,
		bool detailedTooltips )
	{
		if ( !ResolvePlayerMapUv( snap, marker.WorldX, marker.WorldY, out var mapU, out var mapV ) )
			return;

		Panel dot;
		if ( onMarkerClick is not null )
		{
			var captured = marker;
			dot = ThornsUiFactory.AddClickable( MarkerLayer, "map-marker-hit", () => onMarkerClick( captured ) );
		}
		else
		{
			dot = ThornsUiFactory.AddPanel( MarkerLayer, "map-marker-hit" );
		}

		dot.Style.Position = PositionMode.Absolute;
		ThornsMapMarkerStyle.ApplyMarkerPosition( dot, mapU, mapV );

		Panel iconHost = dot;
		if ( ScrollZoomEnabled )
		{
			var badge = ThornsUiFactory.AddPanel( dot, "map-marker-badge" );
			badge.Style.PointerEvents = PointerEvents.None;
			iconHost = badge;
		}

		var glyph = ThornsUiFactory.AddPanel( iconHost, "map-marker-glyph" );
		ThornsMapMarkerStyle.StyleMapMarkerPanel( glyph, marker.Kind );
		glyph.Style.PointerEvents = PointerEvents.None;

		if ( selectedMarker is not null )
			dot.SetClass( "selected", string.Equals( selectedMarker.Id, marker.Id, StringComparison.Ordinal ) );

		ThornsTooltip.Attach( dot, BuildMarkerTooltip( marker, detailedTooltips ) );
	}

	static string BuildMarkerTooltip( ThornsMapMarkerDto marker, bool detailed )
	{
		var title = string.IsNullOrWhiteSpace( marker.Label )
			? ThornsMapMarkerStyle.GetLegendTitle( marker.Kind )
			: marker.Label;

		return detailed
			? $"{title} ({ThornsMapMarkerStyle.GetLegendTitle( marker.Kind )})"
			: title;
	}
}
