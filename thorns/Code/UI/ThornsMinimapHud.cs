using System;
using System.Buffers;
using System.Collections.Generic;
using Sandbox.UI;

namespace Sandbox;

/// <summary>
/// Corner minimap driven by host-replicated <see cref="ThornsPoiAuthority"/> in sessions, or scene composition when not networked (THORNS map / minimap UI).
/// </summary>
[Title( "Thorns — Minimap HUD" )]
[Category( "Thorns/UI" )]
[Icon( "map" )]
public sealed class ThornsMinimapHud : PanelComponent, Component.INetworkSpawn
{
	// Square minimap edge length in pixels; pinned goal card matches this width (stacked below the map).
	[Property] public float MapSizePixels { get; set; } = 238f;

	[Property] public float MarginRightPixels { get; set; } = 16f;

	// Top edge of the minimap (goal card sits below with StackGapPixels; keep in sync with ApplyCornerStackLayout).
	[Property] public float MarginTopPixels { get; set; } = 32f;

	[Property] public float StackGapPixels { get; set; } = 8f;
	[Property] public float UiUpdateIntervalSeconds { get; set; } = 0.12f;

	[Property] public float DynamicBlipUpdateIntervalSeconds { get; set; } = 0.55f;

	// Square raster used for the procedural terrain overview (lower = faster UI rebuild).
	[Property] public int TerrainOverviewResolution { get; set; } = 128;

	/// <summary>Centered world-map square edge as a fraction of the shorter viewport side.</summary>
	[Property] public float WorldMapScreenFraction { get; set; } = 0.73f;

	/// <summary>Extra shrink for POI / player icons on the fullscreen map (smaller = tinier blips).</summary>
	[Property] public float WorldMapIconScale { get; set; } = 0.88f;

	static readonly HashSet<int> BossBlipActiveScratch = new();
	static readonly HashSet<string> GuildBlipActiveScratch = new( StringComparer.Ordinal );
	static readonly List<int> BossBlipStaleScratch = new();
	static readonly List<string> GuildBlipStaleScratch = new();

	static bool _forceNextPoiHydrate;
	static ThornsMinimapHud _localInstance;

	/// <summary>Called when world boot finishes — forces a POI resync after procedural buildings publish markers.</summary>
	public static void NotifyWorldBootComplete()
	{
		_forceNextPoiHydrate = true;
		_localInstance?.RequestPoiHydrate( force: true );
	}

	public void RequestPoiHydrate( bool force )
	{
		if ( !_treeReady )
			return;

		HydrateDatasetIfNeeded( force );
		LayoutPlayerAndPois();
		if ( _worldMapOpen )
			LayoutWorldMapMarkers();
	}

	ScreenPanel _screen;
	bool _worldMapOpen;
	Panel _worldMapOverlay;
	Panel _worldMapCluster;
	Panel _worldMapLegend;
	Panel _worldMapLegendBody;
	Panel _worldMapFrame;
	Panel _worldMapTerrain;
	Panel _worldMapPoiLayer;
	Panel _worldMapBossLayer;
	Panel _worldMapPlayerDot;
	Panel _worldMapDeathMarker;
	Panel _worldMapBedMarker;
	Label _worldMapHint;
	Panel _mapFrame;
	Panel _terrainBg;
	Panel _poiLayer;
	Panel _playerDot;
	Panel _deathXMarker;
	Panel _ownedBedMarker;
	Panel _bossWildlifeBlipLayer;
	Panel _guildMateBlipLayer;
	Panel _worldMapGuildMateLayer;

	[Sync( SyncFlags.FromHost )] public bool HasOwnedBedMinimapBlip { get; private set; }
	[Sync( SyncFlags.FromHost )] public float OwnedBedMinimapWorldX { get; private set; }
	[Sync( SyncFlags.FromHost )] public float OwnedBedMinimapWorldY { get; private set; }
	bool _hasRecentDeathWorldXy;
	Vector2 _recentDeathWorldXy;
	Texture _terrainOverviewTex;
	bool _terrainOverviewTexAlive;
	int _lastTerrainOverviewBoundsHash = int.MinValue;
	long _lastTerrainOverviewContentToken = long.MinValue;
	bool _treeReady;
	string _lastHydratedJsonKey = "";
	List<ThornsPoiAuthority.PoiClientRecord> _poiCache = new();
	float _minX;
	float _maxX;
	float _minY;
	float _maxY;
	int _lastDatasetVersion = int.MinValue;
	long _lastPoiDatasetToken = long.MinValue;
	double _nextUiTick;
	double _nextDynamicBlipTick;
	int _offlineMarkerSignature = int.MinValue;
	double _nextEmptyPoiRetry;

	readonly Dictionary<int, Panel> _bossBlipsCorner = new();
	readonly Dictionary<int, Panel> _bossBlipsWorld = new();
	readonly Dictionary<string, Panel> _guildBlipsCorner = new();
	readonly Dictionary<string, Panel> _guildBlipsWorld = new();

	Panel _pinnedGoalWrap;
	Label _pinnedGoalTitle;
	Label _pinnedGoalHint;
	Label _pinnedGoalProgressMeta;
	ThornsUiProgressBar _pinnedGoalProgress;
	string _lastPinnedGoalUiKey = "";

	public void OnNetworkSpawn( Connection owner ) => TryBootstrapLocal();

	protected override void OnStart() => TryBootstrapLocal();

	void TryBootstrapLocal()
	{
		if ( _treeReady )
			return;

		if ( !ThornsPawn.IsLocalConnectionOwner( this ) )
			return;

		if ( ThornsWorldBootGate.BlocksLocalOwnerPresentation )
			return;

		if ( !Components.Get<ScreenPanel>( FindMode.EnabledInSelf ).IsValid() )
			_screen = Components.Create<ScreenPanel>();

		_screen = Components.Get<ScreenPanel>( FindMode.EnabledInSelf );
		if ( _screen.IsValid() )
		{
			_screen.AutoScreenScale = true;
			_screen.ZIndex = 54;
		}

		if ( Panel is not null && Panel.IsValid )
		{
			Panel.AddClass( "thorns-minimap-hud" );
			Panel.Style.Width = Length.Fraction( 1f );
			Panel.Style.Height = Length.Fraction( 1f );
			Panel.Style.PointerEvents = PointerEvents.None;

			var stackW = Math.Max( 96f, MapSizePixels );
			var goalTop = MarginTopPixels + stackW + Math.Max( 0f, StackGapPixels );

			_pinnedGoalWrap = ThornsUiPanelAdd.AddChildPanel( Panel, "thorns-minimap-pinned-goal-wrap" );
			_pinnedGoalWrap.Style.Position = PositionMode.Absolute;
			_pinnedGoalWrap.Style.Right = Length.Pixels( MarginRightPixels );
			_pinnedGoalWrap.Style.Top = Length.Pixels( goalTop );
			_pinnedGoalWrap.Style.Width = Length.Pixels( stackW );
			_pinnedGoalWrap.Style.ZIndex = 11;
			_pinnedGoalWrap.Style.FlexDirection = FlexDirection.Column;
			_pinnedGoalWrap.Style.PointerEvents = PointerEvents.None;
			_pinnedGoalWrap.Style.Overflow = OverflowMode.Visible;

			_pinnedGoalWrap.AddChild( new Label( "GOAL", "thorns-minimap-pinned-goal-cap" ) )
				.Style.PointerEvents = PointerEvents.None;
			_pinnedGoalTitle = _pinnedGoalWrap.AddChild( new Label( "", "thorns-minimap-pinned-goal-title" ) );
			_pinnedGoalTitle.Style.PointerEvents = PointerEvents.None;
			_pinnedGoalTitle.Style.WhiteSpace = WhiteSpace.Normal;
			_pinnedGoalHint = _pinnedGoalWrap.AddChild( new Label( "", "thorns-minimap-pinned-goal-hint" ) );
			_pinnedGoalHint.Style.PointerEvents = PointerEvents.None;
			_pinnedGoalHint.Style.WhiteSpace = WhiteSpace.Normal;
			_pinnedGoalProgressMeta = _pinnedGoalWrap.AddChild( new Label( "", "thorns-minimap-pinned-goal-meta" ) );
			_pinnedGoalProgressMeta.Style.PointerEvents = PointerEvents.None;
			_pinnedGoalProgressMeta.Style.WhiteSpace = WhiteSpace.Normal;

			_pinnedGoalProgress = _pinnedGoalWrap.AddChild( new ThornsUiProgressBar() );
			_pinnedGoalProgress.Style.PointerEvents = PointerEvents.None;
			_pinnedGoalProgress.Style.FlexGrow = 0;
			_pinnedGoalProgress.Style.FlexShrink = 0;

			_mapFrame = ThornsUiPanelAdd.AddChildPanel( Panel, "thorns-minimap-frame" );
			_mapFrame.Style.Position = PositionMode.Absolute;
			_mapFrame.Style.Right = Length.Pixels( MarginRightPixels );
			_mapFrame.Style.Top = Length.Pixels( MarginTopPixels );
			_mapFrame.Style.Width = Length.Pixels( stackW );
			_mapFrame.Style.Height = Length.Pixels( stackW );
			_mapFrame.Style.ZIndex = 12;

			_terrainBg = ThornsUiPanelAdd.AddChildPanel( _mapFrame, "thorns-minimap-terrain" );
			_terrainBg.Style.Position = PositionMode.Absolute;
			_terrainBg.Style.Left = 6;
			_terrainBg.Style.Top = 6;
			_terrainBg.Style.Right = 6;
			_terrainBg.Style.Bottom = 6;
			_terrainBg.Style.ZIndex = 0;

			_poiLayer = ThornsUiPanelAdd.AddChildPanel( _mapFrame, "thorns-minimap-pois" );
			_poiLayer.Style.Position = PositionMode.Absolute;
			_poiLayer.Style.Left = 6;
			_poiLayer.Style.Top = 6;
			_poiLayer.Style.Right = 6;
			_poiLayer.Style.Bottom = 6;
			_poiLayer.Style.Overflow = OverflowMode.Hidden;
			_poiLayer.Style.ZIndex = 2;

			_bossWildlifeBlipLayer = ThornsUiPanelAdd.AddChildPanel( _mapFrame, "thorns-minimap-boss-wildlife" );
			_bossWildlifeBlipLayer.Style.Position = PositionMode.Absolute;
			_bossWildlifeBlipLayer.Style.Left = 6;
			_bossWildlifeBlipLayer.Style.Top = 6;
			_bossWildlifeBlipLayer.Style.Right = 6;
			_bossWildlifeBlipLayer.Style.Bottom = 6;
			_bossWildlifeBlipLayer.Style.Overflow = OverflowMode.Hidden;
			_bossWildlifeBlipLayer.Style.ZIndex = 4;

			_guildMateBlipLayer = ThornsUiPanelAdd.AddChildPanel( _mapFrame, "thorns-minimap-guild-mates" );
			_guildMateBlipLayer.Style.Position = PositionMode.Absolute;
			_guildMateBlipLayer.Style.Left = 6;
			_guildMateBlipLayer.Style.Top = 6;
			_guildMateBlipLayer.Style.Right = 6;
			_guildMateBlipLayer.Style.Bottom = 6;
			_guildMateBlipLayer.Style.Overflow = OverflowMode.Hidden;
			_guildMateBlipLayer.Style.ZIndex = 3;

			_playerDot = ThornsUiPanelAdd.AddChildPanel( _poiLayer, "thorns-minimap-player" );
			_playerDot.Style.Position = PositionMode.Absolute;
			_playerDot.Style.Width = 8;
			_playerDot.Style.Height = 8;
			_playerDot.Style.ZIndex = 5;

			_mapFrame.SetClass( "thorns-minimap-frame--terrain-ready", false );

			BuildWorldMapOverlay();

			_treeReady = true;
			if ( ThornsPawn.IsLocalConnectionOwner( this ) )
				_localInstance = this;
			HydrateDatasetIfNeeded( force: true );
			EnsureDeathMarkerPanel();
			EnsureWorldMapMarkerPanels();
			Log.Info( "[Thorns] Minimap HUD ready (local owner)" );
		}
	}

	void BuildWorldMapOverlay()
	{
		_worldMapOverlay = ThornsUiPanelAdd.AddChildPanel( Panel, "thorns-world-map-overlay" );
		_worldMapOverlay.Style.Position = PositionMode.Absolute;
		_worldMapOverlay.Style.Left = 0;
		_worldMapOverlay.Style.Top = 0;
		_worldMapOverlay.Style.Right = 0;
		_worldMapOverlay.Style.Bottom = 0;
		_worldMapOverlay.Style.ZIndex = 80;
		_worldMapOverlay.Style.PointerEvents = PointerEvents.None;
		_worldMapOverlay.SetClass( "thorns-world-map-overlay--open", false );

		_worldMapCluster = ThornsUiPanelAdd.AddChildPanel( _worldMapOverlay, "thorns-world-map-cluster" );
		_worldMapCluster.Style.Position = PositionMode.Absolute;
		_worldMapCluster.Style.ZIndex = 1;
		_worldMapCluster.Style.Display = DisplayMode.Flex;
		_worldMapCluster.Style.FlexDirection = FlexDirection.Row;
		_worldMapCluster.Style.AlignItems = Align.Stretch;
		_worldMapCluster.Style.JustifyContent = Justify.FlexStart;
		_worldMapCluster.Style.Overflow = OverflowMode.Visible;

		_worldMapFrame = ThornsUiPanelAdd.AddChildPanel( _worldMapCluster, "thorns-world-map-frame" );
		_worldMapFrame.Style.Position = PositionMode.Relative;
		_worldMapFrame.Style.FlexShrink = 0;
		_worldMapFrame.Style.Overflow = OverflowMode.Hidden;

		_worldMapLegend = ThornsUiPanelAdd.AddChildPanel( _worldMapCluster, "thorns-world-map-legend" );
		_worldMapLegend.Style.FlexShrink = 0;
		_worldMapLegend.Style.FlexDirection = FlexDirection.Column;
		_worldMapLegend.AddChild( new Label( "MAP LEGEND", "thorns-world-map-legend-cap" ) );
		_worldMapLegendBody = ThornsUiPanelAdd.AddChildPanel( _worldMapLegend, "thorns-world-map-legend-body" );
		_worldMapLegendBody.Style.FlexDirection = FlexDirection.Column;
		_worldMapLegendBody.Style.FlexGrow = 1;
		_worldMapLegendBody.Style.Overflow = OverflowMode.Scroll;

		ApplyWorldMapLayout();

		_worldMapTerrain = ThornsUiPanelAdd.AddChildPanel( _worldMapFrame, "thorns-minimap-terrain" );
		_worldMapTerrain.Style.Position = PositionMode.Absolute;
		_worldMapTerrain.Style.Left = 8;
		_worldMapTerrain.Style.Top = 8;
		_worldMapTerrain.Style.Right = 8;
		_worldMapTerrain.Style.Bottom = 8;
		_worldMapTerrain.Style.ZIndex = 0;

		_worldMapPoiLayer = ThornsUiPanelAdd.AddChildPanel( _worldMapFrame, "thorns-minimap-pois" );
		_worldMapPoiLayer.Style.Position = PositionMode.Absolute;
		_worldMapPoiLayer.Style.Left = 8;
		_worldMapPoiLayer.Style.Top = 8;
		_worldMapPoiLayer.Style.Right = 8;
		_worldMapPoiLayer.Style.Bottom = 8;
		_worldMapPoiLayer.Style.Overflow = OverflowMode.Hidden;
		_worldMapPoiLayer.Style.ZIndex = 2;

		_worldMapBossLayer = ThornsUiPanelAdd.AddChildPanel( _worldMapFrame, "thorns-minimap-boss-wildlife" );
		_worldMapBossLayer.Style.Position = PositionMode.Absolute;
		_worldMapBossLayer.Style.Left = 8;
		_worldMapBossLayer.Style.Top = 8;
		_worldMapBossLayer.Style.Right = 8;
		_worldMapBossLayer.Style.Bottom = 8;
		_worldMapBossLayer.Style.Overflow = OverflowMode.Hidden;
		_worldMapBossLayer.Style.ZIndex = 4;

		_worldMapGuildMateLayer = ThornsUiPanelAdd.AddChildPanel( _worldMapFrame, "thorns-minimap-guild-mates" );
		_worldMapGuildMateLayer.Style.Position = PositionMode.Absolute;
		_worldMapGuildMateLayer.Style.Left = 8;
		_worldMapGuildMateLayer.Style.Top = 8;
		_worldMapGuildMateLayer.Style.Right = 8;
		_worldMapGuildMateLayer.Style.Bottom = 8;
		_worldMapGuildMateLayer.Style.Overflow = OverflowMode.Hidden;
		_worldMapGuildMateLayer.Style.ZIndex = 3;

		_worldMapPlayerDot = ThornsUiPanelAdd.AddChildPanel( _worldMapPoiLayer, "thorns-minimap-player" );
		_worldMapPlayerDot.Style.Position = PositionMode.Absolute;
		_worldMapPlayerDot.Style.Width = 6;
		_worldMapPlayerDot.Style.Height = 6;
		_worldMapPlayerDot.Style.ZIndex = 5;

		_worldMapHint = _worldMapOverlay.AddChild( new Label( "WORLD MAP  ·  M or Esc to close", "thorns-world-map-hint" ) );
		_worldMapHint.Style.PointerEvents = PointerEvents.None;
	}

	(float viewW, float viewH) GetUiViewportSize()
	{
		if ( Panel.IsValid )
		{
			var w = Panel.Box.RectInner.Width;
			var h = Panel.Box.RectInner.Height;
			if ( w > 64f && h > 64f )
				return (w, h);
		}

		return (Screen.Width, Screen.Height);
	}

	float ComputeWorldMapEdgePixels()
	{
		var (sw, sh) = GetUiViewportSize();
		if ( sw < 64f || sh < 64f )
			return Math.Max( MapSizePixels * 2.2f, 360f );

		var frac = Math.Clamp( WorldMapScreenFraction, 0.4f, 0.82f );
		return Math.Clamp( Math.Min( sw, sh ) * frac, 360f, 880f );
	}

	float WorldMapIconScaleMul( float mapEdgePx ) =>
		Math.Clamp( MapSizePixels / Math.Max( 64f, mapEdgePx ) * WorldMapIconScale, 0.22f, 1f );

	static float MapInnerPixels( float mapEdgePx ) => Math.Max( 64f, mapEdgePx - 24f );

	public bool WorldMapOpen => _worldMapOpen;

	void SetWorldMapOpen( bool open )
	{
		if ( _worldMapOpen == open )
			return;

		_worldMapOpen = open;
		if ( _worldMapOverlay.IsValid )
			_worldMapOverlay.SetClass( "thorns-world-map-overlay--open", open );

		if ( _mapFrame.IsValid )
			_mapFrame.SetClass( "thorns-minimap--world-map-active", open );

		if ( _pinnedGoalWrap.IsValid )
			_pinnedGoalWrap.SetClass( "thorns-minimap--world-map-active", open );

		if ( open )
		{
			ApplyWorldMapLayout();
			HydrateDatasetIfNeeded( force: false );
			TryRebuildTerrainOverview();
			RebuildWorldMapPoiPanels();
			RebuildWorldMapLegend();
			LayoutWorldMapMarkers();
			LayoutWorldMapBossWildlifeBlips();
		}
	}

	const float WorldMapLegendWidthPixels = 296f;
	const float WorldMapClusterGapPixels = 18f;

	void ApplyWorldMapLayout()
	{
		if ( !_worldMapCluster.IsValid() || !_worldMapFrame.IsValid )
			return;

		var edge = ComputeWorldMapEdgePixels();
		var legendW = WorldMapLegendWidthPixels;
		var gap = WorldMapClusterGapPixels;
		var clusterW = edge + gap + legendW;

		_worldMapCluster.Style.Width = Length.Pixels( clusterW );
		_worldMapCluster.Style.Height = Length.Pixels( edge );
		_worldMapCluster.Style.Left = Length.Fraction( 0.5f );
		_worldMapCluster.Style.Top = Length.Fraction( 0.5f );
		_worldMapCluster.Style.MarginLeft = Length.Pixels( -clusterW * 0.5f );
		_worldMapCluster.Style.MarginTop = Length.Pixels( -edge * 0.5f );

		_worldMapFrame.Style.Width = Length.Pixels( edge );
		_worldMapFrame.Style.Height = Length.Pixels( edge );

		if ( _worldMapLegend.IsValid )
		{
			_worldMapLegend.Style.Width = Length.Pixels( legendW );
			_worldMapLegend.Style.Height = Length.Pixels( edge );
			_worldMapLegend.Style.MarginLeft = Length.Pixels( gap );
		}
	}

	void RebuildWorldMapLegend()
	{
		if ( !_worldMapLegendBody.IsValid )
			return;

		foreach ( var ch in _worldMapLegendBody.Children.ToArray() )
			ch.Delete();

		var useTypeColors = ThornsMinimapLegend.ResolveUseBuildingTypeColors( GameObject.Scene );
		foreach ( var entry in ThornsMinimapLegend.BuildEntries( useTypeColors, _poiCache ) )
			AddWorldMapLegendRow( entry );
	}

	void AddWorldMapLegendRow( ThornsMinimapLegend.Entry entry )
	{
		var row = ThornsUiPanelAdd.AddChildPanel( _worldMapLegendBody, "thorns-world-map-legend-row" );
		row.Style.FlexDirection = FlexDirection.Row;
		row.Style.AlignItems = Align.Center;
		row.Style.FlexShrink = 0;

		var swatchWrap = ThornsUiPanelAdd.AddChildPanel( row, "thorns-world-map-legend-swatch-wrap" );
		swatchWrap.Style.Width = 28;
		swatchWrap.Style.Height = 28;
		swatchWrap.Style.FlexShrink = 0;
		swatchWrap.Style.JustifyContent = Justify.Center;
		swatchWrap.Style.AlignItems = Align.Center;

		if ( entry.Shape == ThornsMinimapLegend.SwatchShape.DeathCross )
		{
			var cross = swatchWrap.AddChild( new Label( "\u2715", "thorns-world-map-legend-cross" ) );
			cross.Style.PointerEvents = PointerEvents.None;
		}
		else
		{
			var swatch = ThornsUiPanelAdd.AddChildPanel( swatchWrap, "thorns-world-map-legend-swatch" );
			var d = Math.Clamp( entry.SizePx * 1.3f, 7f, 18f );
			swatch.Style.Width = Length.Pixels( d );
			swatch.Style.Height = Length.Pixels( d );
			swatch.Style.BackgroundColor = entry.Color;
			swatch.Style.FlexShrink = 0;
			swatch.SetClass(
				"thorns-world-map-legend-swatch--square",
				entry.Shape == ThornsMinimapLegend.SwatchShape.Square );
		}

		var label = row.AddChild( new Label( entry.Label, "thorns-world-map-legend-label" ) );
		label.Style.PointerEvents = PointerEvents.None;
		label.Style.WhiteSpace = WhiteSpace.Normal;
	}

	bool CanToggleWorldMap( ThornsGameShell shell ) =>
		shell is not { BlocksGameplayShellOverlay: true };

	void TryHandleWorldMapInput( ThornsGameShell shell )
	{
		if ( !CanToggleWorldMap( shell ) )
		{
			if ( _worldMapOpen )
				SetWorldMapOpen( false );
			return;
		}

		if ( Input.Pressed( "WorldMap" ) )
			SetWorldMapOpen( !_worldMapOpen );
		else if ( _worldMapOpen && Input.Keyboard.Pressed( "Escape" ) )
			SetWorldMapOpen( false );
	}

	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying || !_treeReady )
		{
			if ( !_treeReady )
				TryBootstrapLocal();
			return;
		}

		if ( !ThornsPawn.IsLocalConnectionOwner( this ) )
			return;

		var shell = GameObject.Components.Get<ThornsGameShell>();
		var hideMm = shell is { IsValid: true } && shell.Enabled && shell.MenuOpen;
		if ( _mapFrame.IsValid )
			_mapFrame.SetClass( "thorns-minimap--hidden-under-shell", hideMm );

		if ( _pinnedGoalWrap.IsValid )
			_pinnedGoalWrap.SetClass( "thorns-minimap--hidden-under-shell", hideMm );

		TryHandleWorldMapInput( shell );
		RefreshPinnedGoalChip( shell, hideMm );

		if ( Time.Now < _nextUiTick && !_worldMapOpen )
			return;

		_nextUiTick = Time.Now + Math.Max( 0.03f, UiUpdateIntervalSeconds );
		HydrateDatasetIfNeeded( force: _forceNextPoiHydrate );
		if ( _forceNextPoiHydrate )
			_forceNextPoiHydrate = false;
		if ( _poiCache.Count == 0 && Time.Now >= _nextEmptyPoiRetry )
		{
			_nextEmptyPoiRetry = Time.Now + 0.45;
			HydrateDatasetIfNeeded( force: true );
		}
		TryRebuildTerrainOverview();
		LayoutPlayerAndPois();

		if ( Time.Now >= _nextDynamicBlipTick || _worldMapOpen )
		{
			_nextDynamicBlipTick = Time.Now + Math.Max( 0.15f, DynamicBlipUpdateIntervalSeconds );
			LayoutBossWildlifeBlips();
			LayoutGuildMateBlips();
		}

		if ( _worldMapOpen )
		{
			ApplyWorldMapLayout();
			LayoutWorldMapMarkers();
			LayoutWorldMapBossWildlifeBlips();
			LayoutWorldMapGuildMateBlips();
		}
	}

	void LayoutBossWildlifeBlips() =>
		LayoutBossWildlifeBlipsOnLayer( _bossWildlifeBlipLayer, MapSizePixels, 1f );

	void LayoutGuildMateBlips() =>
		LayoutGuildMateBlipsOnLayer( _guildMateBlipLayer, MapSizePixels, 1f );

	void LayoutWorldMapGuildMateBlips()
	{
		if ( !_worldMapOpen || !_worldMapGuildMateLayer.IsValid )
			return;

		LayoutGuildMateBlipsOnLayer(
			_worldMapGuildMateLayer,
			ComputeWorldMapEdgePixels(),
			WorldMapIconScaleMul( ComputeWorldMapEdgePixels() ) );
	}

	void LayoutWorldMapBossWildlifeBlips()
	{
		if ( !_worldMapOpen || !_worldMapBossLayer.IsValid )
			return;

		LayoutBossWildlifeBlipsOnLayer(
			_worldMapBossLayer,
			ComputeWorldMapEdgePixels(),
			WorldMapIconScaleMul( ComputeWorldMapEdgePixels() ) );
	}

	void LayoutBossWildlifeBlipsOnLayer( Panel layer, float mapEdgePx, float iconScale )
	{
		if ( !layer.IsValid )
			return;

		var pool = ReferenceEquals( layer, _worldMapBossLayer ) ? _bossBlipsWorld : _bossBlipsCorner;
		var scene = GameObject.Scene;
		if ( scene is null || !scene.IsValid() )
			return;

		var bossD = Math.Clamp( 10f * iconScale, 4f, 14f );
		var bossHalf = bossD * 0.5f;
		var active = BossBlipActiveScratch;
		active.Clear();

		foreach ( var brain in ThornsPopulationDirector.HostWildlifeBrainsReadOnly )
		{
			if ( !brain.IsValid() )
				continue;

			var id = brain.Components.Get<ThornsWildlifeIdentity>();
			if ( !id.IsValid() || !id.IsBossWildlifeSync || id.HostIsTamed )
				continue;

			var hp = brain.Components.Get<ThornsHealth>();
			if ( !hp.IsValid() || !hp.IsAlive || hp.IsDeadState )
				continue;

			var blipKey = brain.GameObject.GetHashCode();
			active.Add( blipKey );

			if ( !pool.TryGetValue( blipKey, out var blip ) || !blip.IsValid )
			{
				blip = ThornsUiPanelAdd.AddChildPanel( layer, "thorns-minimap-boss-wildlife-blip" );
				blip.Style.Width = Length.Pixels( bossD );
				blip.Style.Height = Length.Pixels( bossD );
				blip.Style.Position = PositionMode.Absolute;
				blip.Style.BackgroundColor = new Color( 0.96f, 0.12f, 0.08f, 0.98f );
				blip.Style.ZIndex = 2;
				pool[blipKey] = blip;
			}

			var wf = brain.GameObject.WorldPosition;
			PlaceOnMap( blip, new Vector2( wf.x, wf.y ), bossHalf, bossHalf, mapEdgePx );
		}

		PruneBlipPool( pool, active );
	}

	void LayoutGuildMateBlipsOnLayer( Panel layer, float mapEdgePx, float iconScale )
	{
		if ( !layer.IsValid )
			return;

		var pool = ReferenceEquals( layer, _worldMapGuildMateLayer ) ? _guildBlipsWorld : _guildBlipsCorner;
		var roster = GameObject.Components.Get<ThornsGuildRoster>();
		if ( !roster.IsValid() || roster.MemberCount == 0 )
		{
			GuildBlipActiveScratch.Clear();
			PruneGuildBlipPool( pool, GuildBlipActiveScratch );
			return;
		}

		var scene = GameObject.Scene;
		if ( scene is null || !scene.IsValid() )
			return;

		ThornsGuildRoster.TryGetAccountKeyForPawnRoot( GameObject, out var selfKey );

		var d = Math.Clamp( 5f * iconScale, 3f, 7f );
		var half = d * 0.5f;
		var active = GuildBlipActiveScratch;
		active.Clear();

		foreach ( var session in scene.GetAllComponents<ThornsPlayer>() )
		{
			if ( !session.IsValid() )
				continue;

			var key = session.HostPersistenceAccountKey;
			if ( string.IsNullOrWhiteSpace( key ) && session.OwnerConnection is not null )
				key = ThornsPersistenceIdentity.GetStableAccountKey( session.OwnerConnection );
			key = ThornsGuildRoster.NormalizeAccountKey( key );
			if ( string.IsNullOrEmpty( key ) || !roster.ContainsAccountKey( key ) )
				continue;

			if ( !string.IsNullOrEmpty( selfKey ) && string.Equals( key, selfKey, StringComparison.Ordinal ) )
				continue;

			var pawn = session.ControlledPawn;
			if ( !pawn.IsValid() || !pawn.GameObject.IsValid() )
				continue;

			var hp = pawn.GameObject.Components.GetInDescendantsOrSelf<ThornsHealth>( true );
			if ( hp.IsValid() && ( !hp.IsAlive || hp.IsDeadState ) )
				continue;

			active.Add( key );

			if ( !pool.TryGetValue( key, out var blip ) || !blip.IsValid )
			{
				blip = ThornsUiPanelAdd.AddChildPanel( layer, "thorns-minimap-guild-mate-blip" );
				blip.Style.Width = Length.Pixels( d );
				blip.Style.Height = Length.Pixels( d );
				blip.Style.Position = PositionMode.Absolute;
				blip.Style.ZIndex = 2;
				pool[key] = blip;
			}

			var wf = pawn.GameObject.WorldPosition;
			PlaceOnMap( blip, new Vector2( wf.x, wf.y ), half, half, mapEdgePx );
		}

		PruneGuildBlipPool( pool, active );
	}

	static void PruneBlipPool( Dictionary<int, Panel> pool, HashSet<int> active )
	{
		BossBlipStaleScratch.Clear();
		foreach ( var kv in pool )
		{
			if ( active.Contains( kv.Key ) )
				continue;
			if ( kv.Value.IsValid )
				kv.Value.Delete();
			BossBlipStaleScratch.Add( kv.Key );
		}

		foreach ( var key in BossBlipStaleScratch )
			pool.Remove( key );
	}

	static void PruneGuildBlipPool( Dictionary<string, Panel> pool, HashSet<string> active )
	{
		GuildBlipStaleScratch.Clear();
		foreach ( var kv in pool )
		{
			if ( active.Contains( kv.Key ) )
				continue;
			if ( kv.Value.IsValid )
				kv.Value.Delete();
			GuildBlipStaleScratch.Add( kv.Key );
		}

		foreach ( var key in GuildBlipStaleScratch )
			pool.Remove( key );
	}

	void RefreshPinnedGoalChip( ThornsGameShell shell, bool hideForMenu )
	{
		if ( _pinnedGoalWrap is null || !_pinnedGoalWrap.IsValid )
			return;

		var hide = hideForMenu || shell is not { IsValid: true, Enabled: true }
		                    || string.IsNullOrWhiteSpace( shell.ClientPinnedJournalGoalId );
		if ( hide )
		{
			_lastPinnedGoalUiKey = "";
			_pinnedGoalWrap.SetClass( "thorns-minimap-pinned-goal-wrap--hidden", true );
			return;
		}

		if ( !ThornsMilestoneDefinitions.TryGetById( shell.ClientPinnedJournalGoalId, out var idx, out var def ) )
		{
			_lastPinnedGoalUiKey = "";
			_pinnedGoalWrap.SetClass( "thorns-minimap-pinned-goal-wrap--hidden", true );
			return;
		}

		var ms = GameObject.Components.Get<ThornsPlayerMilestones>();
		var pr = ms.IsValid() ? ms.GetGoalProgressSnapshot() : Array.Empty<int>();
		var p = idx >= 0 && idx < pr.Length ? pr[idx] : 0;
		var done = p >= def.TargetValue;
		var cur = Math.Min( p, def.TargetValue );
		var meta = done
			? $"{def.TargetValue} / {def.TargetValue} · Done · +{def.RewardXp} XP"
			: $"{cur} / {def.TargetValue} · +{def.RewardXp} XP";
		var frac01 = done
			? 1f
			: def.TargetValue > 0
				? Math.Clamp( p / (float)def.TargetValue, 0f, 1f )
				: 1f;

		var key =
			$"{shell.ClientPinnedJournalGoalId}|{p}|{def.TargetValue}|{def.Title}|{def.ShortHint}|{meta}|{hideForMenu}";
		if ( string.Equals( key, _lastPinnedGoalUiKey, StringComparison.Ordinal ) )
		{
			_pinnedGoalWrap.SetClass( "thorns-minimap-pinned-goal-wrap--hidden", false );
			return;
		}

		_lastPinnedGoalUiKey = key;
		_pinnedGoalWrap.SetClass( "thorns-minimap-pinned-goal-wrap--hidden", false );
		_pinnedGoalTitle.Text = def.Title;
		_pinnedGoalHint.Text = def.ShortHint ?? "";
		_pinnedGoalProgressMeta.Text = meta;
		if ( _pinnedGoalProgress is not null && _pinnedGoalProgress.IsValid )
			_pinnedGoalProgress.SetFraction01( frac01 );
	}

	/// <summary>Host: show the owner's latest placed bed as a green square on the local minimap.</summary>
	public void HostSetOwnedBedMinimapBlip( Vector2 worldXy )
	{
		if ( !Networking.IsHost )
			return;

		HasOwnedBedMinimapBlip = true;
		OwnedBedMinimapWorldX = worldXy.x;
		OwnedBedMinimapWorldY = worldXy.y;
	}

	/// <summary>Host: hide the bed minimap blip when the owner has no active respawn bed.</summary>
	public void HostClearOwnedBedMinimapBlip()
	{
		if ( !Networking.IsHost )
			return;

		HasOwnedBedMinimapBlip = false;
	}

	/// <summary>Local owner: remember last death horizontal position for the red ✕ on the minimap (called from <see cref="ThornsHealth.RpcDeathNotify"/>).</summary>
	public void NotifyMostRecentDeathForMinimap( Vector3 worldPosition )
	{
		if ( !ThornsPawn.IsLocalConnectionOwner( this ) )
			return;

		_recentDeathWorldXy = new Vector2( worldPosition.x, worldPosition.y );
		_hasRecentDeathWorldXy = true;
	}

	void EnsureOwnedBedMarkerPanel()
	{
		if ( _ownedBedMarker is { IsValid: true } || !_poiLayer.IsValid )
			return;

		_ownedBedMarker = ThornsUiPanelAdd.AddChildPanel( _poiLayer, "thorns-minimap-bed-blip" );
		_ownedBedMarker.Style.Position = PositionMode.Absolute;
		_ownedBedMarker.Style.Width = 11;
		_ownedBedMarker.Style.Height = 11;
		_ownedBedMarker.Style.ZIndex = 3;
		_ownedBedMarker.Style.Opacity = 0f;
	}

	void EnsureDeathMarkerPanel()
	{
		if ( _deathXMarker is { IsValid: true } || !_poiLayer.IsValid )
			return;

		_deathXMarker = ThornsUiPanelAdd.AddChildPanel( _poiLayer, "thorns-minimap-death-x" );
		_deathXMarker.Style.Position = PositionMode.Absolute;
		_deathXMarker.Style.Width = 18;
		_deathXMarker.Style.Height = 18;
		_deathXMarker.Style.ZIndex = 4;
		_deathXMarker.Style.JustifyContent = Justify.Center;
		_deathXMarker.Style.AlignItems = Align.Center;
		_deathXMarker.Style.FlexDirection = FlexDirection.Row;
		_deathXMarker.Style.Opacity = 0f;
		var glyph = _deathXMarker.AddChild( new Label( "\u2715", "thorns-minimap-death-x-glyph" ) );
		glyph.Style.PointerEvents = PointerEvents.None;
	}

	void HydrateDatasetIfNeeded( bool force )
	{
		if ( _forceNextPoiHydrate )
			force = true;

		var networked = Networking.IsActive;

		if ( networked && ThornsPoiAuthority.Instance?.IsValid == true )
		{
			var auth = ThornsPoiAuthority.Instance;
			var poiTok = HashCode.Combine(
				auth.DatasetVersion,
				auth.PoiDescriptorVersion,
				auth.PoiContentDescriptorHash,
				auth.PoiDatasetPayloadV1Base64?.GetHashCode( StringComparison.Ordinal ) ?? 0,
				auth.PoiDatasetJson?.GetHashCode( StringComparison.Ordinal ) ?? 0,
				HashCode.Combine( auth.MapHorizMinX, auth.MapHorizMaxX, auth.MapHorizMinY, auth.MapHorizMaxY ) );
			if ( !force && poiTok == _lastPoiDatasetToken )
				return;

			_lastPoiDatasetToken = poiTok;
			_lastDatasetVersion = auth.DatasetVersion;
			_minX = auth.MapHorizMinX;
			_maxX = auth.MapHorizMaxX;
			_minY = auth.MapHorizMinY;
			_maxY = auth.MapHorizMaxY;
			if ( !MapBoundsAreUsable() )
				ApplySceneSettingsBoundsFallback();
			InvalidateTerrainOverviewIfBoundsChanged();
			var t0 = Time.Now;
			_poiCache = ThornsPoiAuthority.GetClientRecordsForUi( auth );
			TryAugmentPoiCacheFromSceneMarkersIfEmpty( ref _minX, ref _maxX, ref _minY, ref _maxY );
			ThornsWorldReplicaMetrics.LastPoiParseMs = ( Time.Now - t0 ) * 1000.0;
			ThornsWorldReplicaMetrics.PoiDatasetClientHydrateCount++;
			ThornsWorldReplicaMetrics.LastPoiClientHydrateReason = force ? "force" : "replica_change";
			RebuildPoiPanels();
			return;
		}

		if ( networked && ThornsPoiAuthority.Instance?.IsValid != true )
		{
			ApplySceneSettingsBoundsFallback();
			InvalidateTerrainOverviewIfBoundsChanged();
			return;
		}

		var sig = ComputeOfflineMarkerSignature();
		if ( !force && sig == _offlineMarkerSignature && _lastHydratedJsonKey == "__offline_scene__" )
			return;

		_offlineMarkerSignature = sig;
		_lastHydratedJsonKey = "__offline_scene__";
		ThornsPoiAuthority.TryComposeSceneMarkersToDataset( GameObject.Scene, out _poiCache, out _minX, out _maxX, out _minY, out _maxY );
		_lastDatasetVersion++;
		InvalidateTerrainOverviewIfBoundsChanged();
		RebuildPoiPanels();
	}

	void TryAugmentPoiCacheFromSceneMarkersIfEmpty( ref float minX, ref float maxX, ref float minY, ref float maxY )
	{
		if ( _poiCache.Count > 0 )
			return;

		var scene = GameObject.Scene;
		if ( scene is null || !scene.IsValid() )
			return;

		if ( !ThornsPoiAuthority.TryComposeSceneMarkersToDataset( scene, out var sceneList, out var sx0, out var sx1, out var sy0, out var sy1 ) )
			return;

		if ( sceneList.Count == 0 )
			return;

		_poiCache = sceneList;
		if ( MapBoundsAreUsable( sx0, sx1, sy0, sy1 ) )
		{
			minX = sx0;
			maxX = sx1;
			minY = sy0;
			maxY = sy1;
		}
	}

	int _lastHydratedBoundsHash = int.MinValue;

	void InvalidateTerrainOverviewIfBoundsChanged()
	{
		var hash = HashCode.Combine( _minX, _maxX, _minY, _maxY );
		if ( hash == _lastHydratedBoundsHash )
			return;

		_lastHydratedBoundsHash = hash;
		_lastTerrainOverviewBoundsHash = int.MinValue;
	}

	static bool MapBoundsAreUsable( float minX, float maxX, float minY, float maxY ) =>
		maxX > minX + 32f
		&& maxY > minY + 32f
		&& ( MathF.Abs( minX ) + MathF.Abs( maxX ) + MathF.Abs( minY ) + MathF.Abs( maxY ) ) > 48f;

	bool MapBoundsAreUsable() => MapBoundsAreUsable( _minX, _maxX, _minY, _maxY );

	bool TryEnsureMapBoundsForRaster()
	{
		if ( MapBoundsAreUsable() )
			return true;

		if ( Networking.IsActive && ThornsPoiAuthority.Instance?.IsValid == true )
		{
			var auth = ThornsPoiAuthority.Instance;
			if ( MapBoundsAreUsable( auth.MapHorizMinX, auth.MapHorizMaxX, auth.MapHorizMinY, auth.MapHorizMaxY ) )
			{
				_minX = auth.MapHorizMinX;
				_maxX = auth.MapHorizMaxX;
				_minY = auth.MapHorizMinY;
				_maxY = auth.MapHorizMaxY;
				InvalidateTerrainOverviewIfBoundsChanged();
				return true;
			}
		}

		ApplySceneSettingsBoundsFallback();
		InvalidateTerrainOverviewIfBoundsChanged();
		return MapBoundsAreUsable();
	}

	int ComputeOfflineMarkerSignature()
	{
		var sig = 0;
		var scene = GameObject.Scene;
		if ( scene is null || !scene.IsValid() )
			return 0;

		foreach ( var m in scene.GetAllComponents<ThornsPoiMarker>() )
		{
			if ( !m.IsValid() || !m.Enabled || !m.ShowOnMinimap )
				continue;
			sig = HashCode.Combine( sig, m.StableId.GetHashCode(), m.GameObject.WorldPosition.GetHashCode() );
		}

		return sig;
	}

	void ApplySceneSettingsBoundsFallback()
	{
		var scene = GameObject.Scene;
		if ( scene is null || !scene.IsValid() )
			return;

		if ( ThornsPoiMapBounds.TryGetTerrainPlayableBounds( scene, out _minX, out _maxX, out _minY, out _maxY ) )
			return;

		foreach ( var st in scene.GetAllComponents<ThornsPoiSceneSettings>() )
		{
			if ( !st.IsValid() )
				continue;

			if ( !st.DeriveHorizontalBoundsFromPois )
			{
				_minX = st.ManualHorizontalMin.x;
				_minY = st.ManualHorizontalMin.y;
				_maxX = st.ManualHorizontalMax.x;
				_maxY = st.ManualHorizontalMax.y;
				return;
			}

			var fe = st.EmptyMapFallbackHalfExtent;
			_minX = -fe.x;
			_minY = -fe.y;
			_maxX = fe.x;
			_maxY = fe.y;
			return;
		}

		_minX = -9000f;
		_minY = -9000f;
		_maxX = 9000f;
		_maxY = 9000f;
	}

	void RebuildPoiPanels()
	{
		RebuildPoiPanelsOnLayer( _poiLayer, 1f );
		if ( _worldMapOpen )
			RebuildWorldMapPoiPanels();
	}

	void RebuildWorldMapPoiPanels() =>
		RebuildPoiPanelsOnLayer( _worldMapPoiLayer, WorldMapIconScaleMul( ComputeWorldMapEdgePixels() ) );

	void RebuildPoiPanelsOnLayer( Panel layer, float iconScale )
	{
		if ( !layer.IsValid )
			return;

		Panel playerDot = null;
		Panel deathMarker = null;
		Panel bedMarker = null;
		if ( layer == _poiLayer )
		{
			playerDot = _playerDot;
			deathMarker = _deathXMarker;
			bedMarker = _ownedBedMarker;
		}
		else if ( layer == _worldMapPoiLayer )
		{
			playerDot = _worldMapPlayerDot;
			deathMarker = _worldMapDeathMarker;
			bedMarker = _worldMapBedMarker;
		}

		foreach ( var ch in layer.Children.ToArray() )
		{
			if ( ch == playerDot || ch == deathMarker || ch == bedMarker )
				continue;
			ch.Delete();
		}

		foreach ( var p in _poiCache )
		{
			var blip = ThornsUiPanelAdd.AddChildPanel( layer, "thorns-minimap-blip" );
			var rawD = p.BlipDiameterPx <= 0.5f ? 9f : p.BlipDiameterPx;
			var d = Math.Clamp( rawD * iconScale, 3f, 28f );
			blip.Style.Width = Length.Pixels( d );
			blip.Style.Height = Length.Pixels( d );
			blip.Style.Position = PositionMode.Absolute;
			blip.Style.BackgroundColor = ThornsPoiAuthority.UnpackRgba( p.Rgba );
		}
	}

	void LayoutPlayerAndPois()
	{
		EnsureDeathMarkerPanel();
		EnsureOwnedBedMarkerPanel();
		LayoutMapMarkers(
			_poiLayer,
			_playerDot,
			_deathXMarker,
			_ownedBedMarker,
			MapSizePixels,
			1f );
	}

	void LayoutWorldMapMarkers()
	{
		if ( !_worldMapOpen || !_worldMapPoiLayer.IsValid )
			return;

		EnsureWorldMapMarkerPanels();
		LayoutMapMarkers(
			_worldMapPoiLayer,
			_worldMapPlayerDot,
			_worldMapDeathMarker,
			_worldMapBedMarker,
			ComputeWorldMapEdgePixels(),
			WorldMapIconScaleMul( ComputeWorldMapEdgePixels() ) );
	}

	void EnsureWorldMapMarkerPanels()
	{
		if ( _worldMapDeathMarker is { IsValid: true } && _worldMapBedMarker is { IsValid: true } )
			return;

		if ( !_worldMapPoiLayer.IsValid )
			return;

		if ( _worldMapDeathMarker is not { IsValid: true } )
		{
			_worldMapDeathMarker = ThornsUiPanelAdd.AddChildPanel( _worldMapPoiLayer, "thorns-minimap-death-x" );
			_worldMapDeathMarker.Style.Position = PositionMode.Absolute;
			_worldMapDeathMarker.Style.Width = 14;
			_worldMapDeathMarker.Style.Height = 14;
			_worldMapDeathMarker.Style.ZIndex = 4;
			_worldMapDeathMarker.Style.JustifyContent = Justify.Center;
			_worldMapDeathMarker.Style.AlignItems = Align.Center;
			_worldMapDeathMarker.Style.Opacity = 0f;
			var glyph = _worldMapDeathMarker.AddChild( new Label( "\u2715", "thorns-minimap-death-x-glyph" ) );
			glyph.Style.PointerEvents = PointerEvents.None;
		}

		if ( _worldMapBedMarker is not { IsValid: true } )
		{
			_worldMapBedMarker = ThornsUiPanelAdd.AddChildPanel( _worldMapPoiLayer, "thorns-minimap-bed-blip" );
			_worldMapBedMarker.Style.Position = PositionMode.Absolute;
			_worldMapBedMarker.Style.Width = 8;
			_worldMapBedMarker.Style.Height = 8;
			_worldMapBedMarker.Style.ZIndex = 3;
			_worldMapBedMarker.Style.Opacity = 0f;
		}
	}

	void LayoutMapMarkers(
		Panel poiLayer,
		Panel playerDot,
		Panel deathMarker,
		Panel bedMarker,
		float mapEdgePx,
		float iconScale )
	{
		if ( !poiLayer.IsValid || !playerDot.IsValid )
			return;

		var my = GameObject.WorldPosition;
		var playerFlat = new Vector2( my.x, my.y );
		var playerHalf = Math.Clamp( 4f * iconScale, 2.5f, 5f );
		PlaceOnMap( playerDot, playerFlat, playerHalf, playerHalf, mapEdgePx );

		var pi = 0;
		foreach ( var ch in poiLayer.Children )
		{
			if ( ch == playerDot || ch == deathMarker || ch == bedMarker || ch is not Panel pan )
				continue;

			if ( pi >= _poiCache.Count )
				break;

			var p = _poiCache[pi];
			var rawD = p.BlipDiameterPx <= 0.5f ? 9f : p.BlipDiameterPx;
			var d = Math.Clamp( rawD * iconScale, 3f, 28f );
			PlaceOnMap( pan, new Vector2( p.X, p.Y ), d * 0.5f, d * 0.5f, mapEdgePx );
			pan.Style.ZIndex = 1;
			pi++;
		}

		if ( deathMarker is { IsValid: true } )
		{
			if ( _hasRecentDeathWorldXy )
			{
				var deathHalf = Math.Clamp( 9f * iconScale, 5f, 10f );
				PlaceOnMap( deathMarker, _recentDeathWorldXy, deathHalf, deathHalf, mapEdgePx );
				deathMarker.Style.Opacity = 1f;
			}
			else
				deathMarker.Style.Opacity = 0f;
		}

		if ( bedMarker is { IsValid: true } )
		{
			if ( HasOwnedBedMinimapBlip )
			{
				var bedHalf = Math.Clamp( 5.5f * iconScale, 3f, 6f );
				PlaceOnMap(
					bedMarker,
					new Vector2( OwnedBedMinimapWorldX, OwnedBedMinimapWorldY ),
					bedHalf,
					bedHalf,
					mapEdgePx );
				bedMarker.Style.Opacity = 1f;
			}
			else
				bedMarker.Style.Opacity = 0f;
		}
	}

	void PlaceOnMap( Panel mark, Vector2 worldXY, float halfW, float halfH, float mapEdgePx )
	{
		var u = (worldXY.x - _minX) / Math.Max( 1f, _maxX - _minX );
		var v = (worldXY.y - _minY) / Math.Max( 1f, _maxY - _minY );
		u = Math.Clamp( u, 0f, 1f );
		v = Math.Clamp( v, 0f, 1f );

		var innerPx = MapInnerPixels( mapEdgePx );

		mark.Style.Left = Length.Pixels( u * innerPx - halfW );
		mark.Style.Top = Length.Pixels( (1f - v) * innerPx - halfH );
	}

	static ThornsTerrainChunk FindTerrainChunkIn( Scene scene )
	{
		if ( scene is null || !scene.IsValid() )
			return default;

		foreach ( var c in scene.GetAllComponents<ThornsTerrainChunk>() )
		{
			if ( c.IsValid() )
				return c;
		}

		return default;
	}

	void TryRebuildTerrainOverview()
	{
		if ( !_terrainBg.IsValid() )
			return;

		if ( !TryEnsureMapBoundsForRaster() )
			return;

		var chunk = FindTerrainChunkIn( GameObject.Scene );
		if ( !chunk.IsValid() || !chunk.GameObject.IsValid() || !chunk.HasReplicatedTerrainSpec() )
		{
			// Terrain spec can arrive a tick after POI bounds — keep the last overview instead of flashing blank.
			return;
		}

		var boundsHash = HashCode.Combine( _minX, _maxX, _minY, _maxY );
		var terrainTok = HashCode.Combine( chunk.TerrainSpecDescriptorVersion, chunk.TerrainSpecContentHash );
		if ( terrainTok == _lastTerrainOverviewContentToken
		     && boundsHash == _lastTerrainOverviewBoundsHash
		     && _terrainOverviewTexAlive )
			return;

		if ( !TryRasterTerrainOverview( chunk, out var bmp ) )
			return;

		var newTex = bmp.ToTexture( false );
		if ( ReferenceEquals( newTex, Texture.Invalid )
		     || ReferenceEquals( newTex, Texture.Transparent ) )
			return;

		SafeDisposeTerrainOverviewTexture();
		_terrainOverviewTex = newTex;

		_lastTerrainOverviewContentToken = terrainTok;
		_lastTerrainOverviewBoundsHash = boundsHash;
		_terrainOverviewTexAlive = true;
		_terrainBg.Style.BackgroundImage = _terrainOverviewTex;
		if ( _worldMapTerrain.IsValid )
			_worldMapTerrain.Style.BackgroundImage = _terrainOverviewTex;
		_mapFrame.SetClass( "thorns-minimap-frame--terrain-ready", true );
		if ( _worldMapFrame.IsValid )
			_worldMapFrame.SetClass( "thorns-minimap-frame--terrain-ready", true );
	}

	void ClearTerrainOverview()
	{
		_lastTerrainOverviewContentToken = long.MinValue;
		_lastTerrainOverviewBoundsHash = int.MinValue;
		if ( _terrainBg.IsValid() )
			_terrainBg.Style.BackgroundImage = null;
		if ( _worldMapTerrain.IsValid() )
			_worldMapTerrain.Style.BackgroundImage = null;
		SafeDisposeTerrainOverviewTexture();
		if ( _mapFrame.IsValid() )
			_mapFrame.SetClass( "thorns-minimap-frame--terrain-ready", false );
		if ( _worldMapFrame.IsValid() )
			_worldMapFrame.SetClass( "thorns-minimap-frame--terrain-ready", false );
	}

	void SafeDisposeTerrainOverviewTexture()
	{
		if ( !_terrainOverviewTexAlive )
			return;

		_terrainOverviewTexAlive = false;
		try
		{
			_terrainOverviewTex.Dispose();
		}
		catch
		{
			// Ignore dispose failures on partially-built textures (Interop edge cases).
		}

		_terrainOverviewTex = default;
	}

	bool TryRasterTerrainOverview( ThornsTerrainChunk chunk, out Bitmap bitmap )
	{
		bitmap = default;
		if ( !chunk.GameObject.IsValid() )
			return false;

		if ( !chunk.TryGetResolvedNetSpec( out var fullSpec ) )
			return false;

		var dim = Math.Clamp( TerrainOverviewResolution, 48, 256 );
		var prevRx = fullSpec.HeightmapResolutionX;
		var prevRz = fullSpec.HeightmapResolutionZ;
		var savedPads = fullSpec.ProcBuildingTerrainPads;
		var rx = Math.Max( 2, dim );
		var rz = Math.Max( 2, dim );
		var count = rx * rz;
		var heights = ArrayPool<float>.Shared.Rent( count );
		try
		{
			if ( !ThornsHeightmapBakeCache.TryDownsample( in fullSpec, rx, rz, heights.AsSpan( 0, count ) ) )
			{
				fullSpec.HeightmapResolutionX = dim;
				fullSpec.HeightmapResolutionZ = dim;
				if ( savedPads is { Count: > 0 } )
					fullSpec.ProcBuildingTerrainPads = new List<ThornsTerrainProcBuildingPad>();

				var worldSeed = fullSpec.TerraingenWorldSeed != 0 ? fullSpec.TerraingenWorldSeed : fullSpec.Seed;
				ThornsTerraingenTerrainRuntime.TryBindConfigsFromScene( GameObject.Scene );
				var field = ThornsTerraingenTerrainRuntime.GetOrGenerateField( worldSeed );
				ThornsTerraingenTerrainRuntime.FillHeightmapBase( fullSpec, heights, field );
			}

			var spec = fullSpec;
			spec.HeightmapResolutionX = rx;
			spec.HeightmapResolutionZ = rz;

			var ww = Math.Max( 64f, spec.WorldWidth );
			var wd = Math.Max( 64f, spec.WorldDepth );

			float hMax = float.NegativeInfinity;
			for ( var i = 0; i < count; i++ )
			{
				var zh = heights[i];
				if ( zh > hMax )
					hMax = zh;
			}

			if ( !float.IsFinite( hMax ) )
				hMax = spec.WaterLevelWorldZ + 800f;

			var wPx = Math.Clamp( dim, 32, 256 );
			var hPx = wPx;
			var spanX = Math.Max( 1f, _maxX - _minX );
			var spanY = Math.Max( 1f, _maxY - _minY );
			var wxSample = spanX / Math.Max( 8f, wPx ) * 2.5f;

			bitmap = new Bitmap( wPx, hPx, false );
			var px = new Color[wPx * hPx];

			for ( var py = 0; py < hPx; py++ )
			{
				var vNorm = 1f - (py + 0.5f) / hPx;
				var worldY = _minY + vNorm * spanY;

				for ( var pxX = 0; pxX < wPx; pxX++ )
				{
					var uNorm = (pxX + 0.5f) / wPx;
					var worldX = _minX + uNorm * spanX;

					var span = heights.AsSpan( 0, count );
					var z = SampleTerrainHeightWorld( chunk, in spec, span, rx, rz, ww, wd, worldX, worldY );
					var zxp = SampleTerrainHeightWorld( chunk, in spec, span, rx, rz, ww, wd, worldX + wxSample, worldY );
					var zxm = SampleTerrainHeightWorld( chunk, in spec, span, rx, rz, ww, wd, worldX - wxSample, worldY );
					var zyp = SampleTerrainHeightWorld( chunk, in spec, span, rx, rz, ww, wd, worldX, worldY + wxSample );
					var zym = SampleTerrainHeightWorld( chunk, in spec, span, rx, rz, ww, wd, worldX, worldY - wxSample );
					var dx = zxp - zxm;
					var dy = zyp - zym;
					var shade = Math.Clamp( 0.68f + dx * 0.00085f + dy * 0.00085f, 0.38f, 1.05f );

					var c = TerrainPixelColor( z, hMax, spec.WaterLevelWorldZ, shade );
					px[py * wPx + pxX] = c;
				}
			}

			bitmap.SetPixels( px );
			return true;
		}
		finally
		{
			ArrayPool<float>.Shared.Return( heights );
			fullSpec.HeightmapResolutionX = prevRx;
			fullSpec.HeightmapResolutionZ = prevRz;
			fullSpec.ProcBuildingTerrainPads = savedPads;
		}
	}

	static float SampleTerrainHeightWorld(
		ThornsTerrainChunk chunk,
		in ThornsTerrainNetSpec spec,
		ReadOnlySpan<float> heights,
		int rx,
		int rz,
		float ww,
		float wd,
		float worldX,
		float worldY )
	{
		if ( !chunk.IsValid() || !chunk.GameObject.IsValid() )
			return spec.WaterLevelWorldZ;

		var origin = chunk.GameObject.WorldPosition;
		var delta = new Vector3( worldX, worldY, origin.z ) - origin;
		var local = chunk.GameObject.WorldRotation.Inverse * delta;
		return ThornsTerrainGeometry.SampleHeightLocalZUp(
			heights,
			rx,
			rz,
			ww,
			wd,
			spec.CenterOnWorldOrigin,
			local.x,
			local.y );
	}

	static Color TerrainPixelColor( float z, float hMax, float waterZ, float shade )
	{
		var deep = new Color( 0.11f, 0.22f, 0.42f, 1f );
		var shallow = new Color( 0.18f, 0.42f, 0.58f, 1f );
		var lowLand = new Color( 0.18f, 0.34f, 0.16f, 1f );
		var highLand = new Color( 0.42f, 0.38f, 0.30f, 1f );

		Color baseCol;
		if ( z < waterZ - 2f )
			baseCol = Color.Lerp( deep, shallow, Math.Clamp( (z - (waterZ - 180f)) / 180f, 0f, 1f ) );
		else if ( z < waterZ + 28f )
			baseCol = Color.Lerp( shallow, lowLand, Math.Clamp( (z - (waterZ - 2f)) / 30f, 0f, 1f ) );
		else
		{
			var t = Math.Clamp( (z - waterZ) / Math.Max( 120f, hMax - waterZ ), 0f, 1f );
			baseCol = Color.Lerp( lowLand, highLand, t );
		}

		var lit = baseCol * shade;
		lit.a = 1f;
		return lit;
	}

	protected override void OnDestroy()
	{
		if ( _localInstance == this )
			_localInstance = null;
	}
}
