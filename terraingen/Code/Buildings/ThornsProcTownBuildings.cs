namespace Terraingen.Buildings;

using Sandbox.Network;
using Terraingen;
using Terraingen.Buildings.Settlement;
using Terraingen.GameData;
using Terraingen.Multiplayer;
using Terraingen.Physics;
using Terraingen.Player;
using Terraingen.Rendering;
using Terraingen.TerrainGen;
using Terraingen.World;

/// <summary>Thorns building-piece dimensions, kept compatible with the source layout rules.</summary>
public static class ThornsBuildingModule
{
	public const float Cell = 100f;
	public const float FloorThickness = 5f;
	public const float WallHeight = 100f;
	public const float WallThickness = 5f;
	public const float StoryHeight = WallHeight + FloorThickness;
	public const float WindowHoleSize = 36f;
	public const float DoorWidth = 60f;
	public const float DoorHeight = 100f;

	/// <summary>Full-size door slab inset along local −X (inward from the +X jamb face).</summary>
	public const float DoorPanelDepthInset = 2.85f;

	/// <summary>Local Z of door-frame opening foot — matches procedural doorframe hole bottom.</summary>
	public static float DoorFrameHoleBottomLocalZ => -WallHeight * 0.5f;

	/// <summary>Bottom hinge corner on the inward jamb (local +Y spans the opening width).</summary>
	public static Vector3 DoorPanelHingeLocal =>
		new( -DoorPanelDepthInset, -DoorWidth * 0.5f, DoorFrameHoleBottomLocalZ );

	/// <summary>Panel pivot is mesh centre; offset from hinge places the slab in the opening.</summary>
	public static Vector3 DoorPanelOffsetFromHinge =>
		new( 0f, DoorWidth * 0.5f, DoorHeight * 0.5f );

	public const float TrimCornerSize = 24f;
	public const float TrimSeamRun = 16f;
	public const float TrimSeamDepth = 14f;
	public const float TrimBandHeight = 10f;
	public const float DevReferenceSize = 50f;

	/// <summary>One full storey: floor slab + wall band (thorns <c>StoryHeightWorld</c>).</summary>
	public static float StoryHeightWorld => StoryHeight;

	/// <summary>Procedural perimeter wall vertical span (multi-storey walls overlap half a slab).</summary>
	public static float ProcPerimeterWallSpanWorld( int stories ) =>
		stories <= 1 ? WallHeight : WallHeight + FloorThickness;

	/// <summary>Local Z centre for a procedural perimeter wall on storey <paramref name="storyIndex"/>.</summary>
	public static float ProcPerimeterWallCenterLocalZ( int storyIndex, int stories )
	{
		if ( stories <= 1 )
			return FloorThickness * 0.5f + WallHeight * 0.5f;

		var span = ProcPerimeterWallSpanWorld( stories );
		var bottom = storyIndex * StoryHeightWorld + (storyIndex == 0 ? FloorThickness * 0.5f : -FloorThickness * 0.5f );
		return bottom + span * 0.5f;
	}

	/// <summary>Full exterior shell height and centre Z for corner / seam trim columns.</summary>
	public static void ProcPerimeterShellFullExtent( int stories, out float centerLocalZ, out float spanWorld )
	{
		var perStorySpan = ProcPerimeterWallSpanWorld( stories );
		if ( stories <= 1 )
		{
			centerLocalZ = ProcPerimeterWallCenterLocalZ( 0, 1 );
			spanWorld = perStorySpan;
			return;
		}

		var zBottom = ProcPerimeterWallCenterLocalZ( 0, stories ) - perStorySpan * 0.5f;
		var zTop = ProcPerimeterWallCenterLocalZ( stories - 1, stories ) + perStorySpan * 0.5f;
		spanWorld = zTop - zBottom;
		centerLocalZ = ( zBottom + zTop ) * 0.5f;
	}

	/// <summary>Storey joint + roofline band trim centre Z values (no ground band).</summary>
	public static int CollectPerimeterBandTrimCenterZ( int stories, Span<float> into )
	{
		if ( stories < 1 || into.Length < 1 )
			return 0;

		var span = ProcPerimeterWallSpanWorld( stories );
		var n = 0;

		for ( var s = 1; s < stories; s++ )
		{
			if ( n >= into.Length )
				break;
			into[n++] = s * StoryHeightWorld;
		}

		if ( n < into.Length )
			into[n++] = ProcPerimeterWallCenterLocalZ( stories - 1, stories ) + span * 0.5f - TrimBandHeight * 0.5f;

		return n;
	}

	/// <summary>Horizontal band trim centre — flush with exterior wall face.</summary>
	public static Vector3 ProcPerimeterBandTrimLocalPosition(
		float alongLocal,
		float edgeLocal,
		float bandCenterZ,
		int side )
	{
		var push = WallThickness * 0.5f;
		return side switch
		{
			0 => new Vector3( alongLocal, edgeLocal - push, bandCenterZ ),
			2 => new Vector3( alongLocal, edgeLocal + push, bandCenterZ ),
			3 => new Vector3( edgeLocal - push, alongLocal, bandCenterZ ),
			1 => new Vector3( edgeLocal + push, alongLocal, bandCenterZ ),
			_ => new Vector3( alongLocal, edgeLocal, bandCenterZ )
		};
	}

	/// <summary>Half-extent of the 3×3 proc-town pad (walls + trim) for scatter exclusion.</summary>
	public static float ProcTownScatterExclusionHalfExtent =>
		(Cell * 3f + WallThickness * 2f + 20f) * 0.5f;

	/// <summary>Clearance beyond the building pad edge for trees and clutter.</summary>
	public const float ProcTownScatterExclusionMargin = Cell * 0.65f;

	/// <summary>Extra inset when testing whether two proc building pads intersect.</summary>
	public const float BuildingPlacementOverlapMarginInches = 0f;

	public static Vector3 ScaleBoxToWorldAxes( float x, float y, float z ) =>
		new( x / DevReferenceSize, y / DevReferenceSize, z / DevReferenceSize );

	/// <summary>Local position for a centered dev box so its bottom sits on the parent origin (ground contact).</summary>
	public static Vector3 BoxBottomAlignedLocalCenter( Vector3 worldSize ) =>
		new( 0f, 0f, worldSize.z * 0.5f );
}

public static class ThornsTownNodeRegistry
{
	[SkipHotload]
	static readonly List<ThornsTownNode> Nodes = new();

	public static int Version { get; private set; }
	public static IReadOnlyList<Vector3> TownCenters
	{
		get
		{
			TryRefreshFromScene();
			return Nodes.Select( n => n.Center ).ToList();
		}
	}
	public static IReadOnlyList<ThornsTownNode> TownNodes
	{
		get
		{
			TryRefreshFromScene();
			return Nodes;
		}
	}

	static void TryRefreshFromScene()
	{
		if ( Nodes.Count > 0 )
			return;

		var scene = Game.ActiveScene;
		if ( scene is null || !scene.IsValid() )
			return;

		foreach ( var generator in scene.GetAllComponents<ThornsWorldBuildingGenerator>() )
		{
			if ( !generator.IsValid() || generator.TownNodes.Count == 0 )
				continue;

			Publish( generator.TownNodes );
			return;
		}
	}

	public static void Publish( IEnumerable<ThornsTownNode> nodes )
	{
		Nodes.Clear();
		if ( nodes is not null )
		{
			foreach ( var node in nodes )
				Nodes.Add( node );
		}

		Version++;
		Log.Info( $"[Thorns Buildings] Published {Nodes.Count} POI marker node(s) for map snapshot version {Version}." );
	}

	public static void Clear()
	{
		if ( Nodes.Count == 0 )
			return;

		Nodes.Clear();
		Version++;
		Log.Info( $"[Thorns Buildings] Cleared POI marker nodes for map snapshot version {Version}." );
	}
}

public readonly struct ThornsTownNode
{
	public readonly int Index;
	public readonly Vector3 Center;
	public readonly ThornsPoiIdentity Identity;
	public readonly int TargetBuildingCount;
	public readonly int PlacedBuildingCount;

	public ThornsTownNode(
		int index,
		Vector3 center,
		ThornsPoiIdentity identity,
		int targetBuildingCount,
		int placedBuildingCount )
	{
		Index = index;
		Center = center;
		Identity = identity;
		TargetBuildingCount = targetBuildingCount;
		PlacedBuildingCount = placedBuildingCount;
	}
}

public sealed class ThornsProcBuildingConfig
{
	[Property] public bool Enabled { get; set; } = true;
	[Property] public int BuildingCount { get; set; } = 30;
	[Property] public int TownCount { get; set; } = 17;
	[Property] public float TownRadiusInches { get; set; } = 1150f;
	[Property] public float MinLotSpacingInches { get; set; } = 390f;
	[Property] public bool MicroTownsEnabled { get; set; } = true;
	[Property] public int MicroTownCount { get; set; } = 6;
	[Property] public int MicroTownMinBuildings { get; set; } = 1;
	[Property] public int MicroTownMaxBuildings { get; set; } = 3;
	[Property] public float MicroTownRadiusInches { get; set; } = 460f;
	[Property] public float MicroTownMinLotSpacingInches { get; set; } = 300f;
	[Property] public float MicroTownMinCenterSpacingInches { get; set; } = 2100f;
	[Property] public float MicroTownAvoidMajorTownRadiusInches { get; set; } = 1700f;
	[Property] public float MaxFootprintReliefInches { get; set; } = ThornsProcBuildingTerrainUtil.DefaultMaxFootprintReliefInches;
	[Property] public float FallbackMaxFootprintReliefInches { get; set; } = ThornsProcBuildingTerrainUtil.DefaultFallbackMaxFootprintReliefInches;
	[Property] public float EmergencyMaxFootprintReliefInches { get; set; } = ThornsProcBuildingTerrainUtil.DefaultEmergencyMaxFootprintReliefInches;
	[Property] public float QuotaFillMaxFootprintReliefInches { get; set; } = ThornsProcBuildingTerrainUtil.DefaultQuotaFillMaxFootprintReliefInches;
	[Property] public float MajorMinCenterSpacingInches { get; set; } = 900f;
	[Property] public float FoundationLiftInches { get; set; } = 6f;
	[Property] public bool PaintDirtPaths { get; set; } = true;
	[Property] public bool RouteDirtPathsByElevation { get; set; } = true;
	[Property] public float TownPathWidthInches { get; set; } = 190f;
	[Property] public float InterTownPathWidthInches { get; set; } = 240f;
	[Property] public bool ConnectMicroTownsWithPaths { get; set; }
	[Property] public int PathRouteGridSize { get; set; } = 128;
	[Property] public float PathRouteElevationCost { get; set; } = 220f;
	[Property] public float PathRouteHighElevationCost { get; set; } = 35f;
	[Property] public float PathRouteSearchPaddingInches { get; set; } = 9000f;
	[Property] public int ProcBuildingStories { get; set; } = ThornsProcBuildingSpawnDefaults.Stories;
	[Property] public int ProcBuildingLayoutVariant { get; set; } = ThornsProcBuildingSpawnDefaults.LayoutVariantIndex;
	[Property] public string DevBoxModel { get; set; } = "models/dev/box.vmdl";
	[Property] public bool DebugLogging { get; set; } = true;
	[Property] public int MaxDebugRejectLogs { get; set; } = 8;
	[Property] public int MaxFurnitureDebugLogs { get; set; } = 12;
	[Property] public bool StreetFirstSettlements { get; set; } = true;
	[Property] public bool SettlementDebugOverlay { get; set; }
}

[Title( "Thorns World Building Generator" )]
[Category( "Terrain/Buildings" )]
public sealed class ThornsWorldBuildingGenerator : Component
{
	[Property] public ThornsProcBuildingConfig Config { get; set; } = new();

	GameObject _root;
	ThornsProcBuildingShellSpawner _spawner;
	readonly List<Vector3> _townCenters = new();
	readonly List<TownCenterPlan> _townCenterPlans = new();
	readonly List<ThornsTownNode> _townNodes = new();
	int _spawnedFurniture;
	int _furnitureModelFallbacks;
	int _lastMajorTownCenterCount;
	int _lastMicroTownCenterCount;
	int _lastMicroTownBuildingTarget;
	readonly List<SettlementLayout> _settlementLayouts = new();
	readonly HashSet<int> _streetLayoutTownIndices = new();
	SettlementLayoutMetrics _layoutMetrics;

	public IReadOnlyList<Vector3> TownCenters => _townCenters;
	public IReadOnlyList<ThornsTownNode> TownNodes => _townNodes;

	public void Generate( Terrain terrain, HeightmapField field, ThornsTerrainConfig terrainConfig )
	{
		if ( !Config.Enabled || !terrain.IsValid() || field is null || terrainConfig is null )
		{
			Log.Info( $"[Thorns Buildings] Skipped: enabled={Config.Enabled} terrain={terrain.IsValid()} field={field is not null} config={terrainConfig is not null}." );
			return;
		}

		EnsureConfigDefaults();

		Clear();
		_spawner = new ThornsProcBuildingShellSpawner(
			Scene,
			Config.DevBoxModel,
			Config.DebugLogging,
			Config.MaxFurnitureDebugLogs );
		_spawner.ResetCounters();

		_root = Scene.CreateObject( true );
		_root.Name = "Thorns Town Buildings";
		_root.Parent = GameObject;

		var service = _root.Components.Create<ThornsBuildingLootWorldService>();
		service.Clear();
		_spawnedFurniture = _spawner.SpawnedFurniture;
		_furnitureModelFallbacks = _spawner.FurnitureModelFallbacks;

		var worldSeed = terrainConfig.WorldSeed;
		var rng = new Random( HashCode.Combine( worldSeed, 0xB01D1A6 ) );
		var placementRng = new Random( HashCode.Combine( worldSeed, 0xF01A1 ) );
		var layoutStopwatch = System.Diagnostics.Stopwatch.StartNew();
		_layoutMetrics = new SettlementLayoutMetrics();
		var lots = BuildTownLots( terrain, terrainConfig, rng, worldSeed );
		layoutStopwatch.Stop();
		_layoutMetrics.GenerationMs = layoutStopwatch.Elapsed.TotalMilliseconds;
		var majorBuildingTarget = _townCenterPlans.Where( plan => !plan.IsMicro ).Sum( plan => plan.TargetBuildingCount );
		var microBuildingTarget = _lastMicroTownBuildingTarget;
		var requestedBuildingCount = majorBuildingTarget + microBuildingTarget;
		var placed = 0;
		var placedMajor = 0;
		var placedMicro = 0;
		var placedByTownIndex = new Dictionary<int, int>();
		var snapRejects = 0;
		var reliefRejects = 0;
		var overlapRejects = 0;
		var debugRejectLogs = 0;
		var usedLots = new HashSet<int>();
		var placedLots = new List<Lot>( requestedBuildingCount );

		if ( Config.DebugLogging )
		{
			Log.Info(
				$"[Thorns Buildings] Begin: requested={requestedBuildingCount} (major={majorBuildingTarget}, micro={microBuildingTarget}), towns={Config.TownCount}, microTowns={_lastMicroTownCenterCount}, maxStories={Math.Clamp( Config.ProcBuildingStories, 1, ThornsProcBuildingSpawnDefaults.MaxStories )}, "
				+ $"layoutVariant={Config.ProcBuildingLayoutVariant}, lots={lots.Count}, terrainSize={terrain.TerrainSize:0}, "
				+ $"terrainHeight={terrain.TerrainHeight:0}, maxRelief={Config.MaxFootprintReliefInches:0}." );
		}

		for ( var li = 0; li < lots.Count; li++ )
		{
			var lot = lots[li];
			if ( ShouldSkipLotForQuota( lot, placedMajor, placedMicro, majorBuildingTarget, microBuildingTarget, placedByTownIndex ) )
				continue;

			if ( ThornsWorldScatterFootprintRegistry.WouldBuildingFootprintOverlapRegistered(
				     lot.Position,
				     lot.Rotation,
				     lot.WidthCells,
				     lot.DepthCells ) )
			{
				overlapRejects++;
				MarkFrontageLotRejected( lot );
				_layoutMetrics.RejectedLots++;

				if ( Config.DebugLogging && debugRejectLogs < Config.MaxDebugRejectLogs )
				{
					debugRejectLogs++;
					Log.Info(
						$"[Thorns Buildings] Reject lot {li}: reason=overlap pos={lot.Position} size={lot.WidthCells}x{lot.DepthCells} poi={lot.TownIndex}." );
				}

				continue;
			}

			if ( !TryResolveLotTerrain(
				     terrain,
				     terrainConfig,
				     lot,
				     Config.MaxFootprintReliefInches,
				     out var baseZ,
				     out var foundationDepth,
				     out var relief,
				     out var failedSnap ) )
			{
				if ( failedSnap )
					snapRejects++;
				else
					reliefRejects++;

				MarkFrontageLotRejected( lot );
				_layoutMetrics.RejectedLots++;

				if ( Config.DebugLogging && debugRejectLogs < Config.MaxDebugRejectLogs )
				{
					debugRejectLogs++;
					Log.Info(
						$"[Thorns Buildings] Reject lot {li}: reason={(failedSnap ? "terrain snap" : "relief")} pos={lot.Position} relief={relief:0.0}/{Config.MaxFootprintReliefInches:0.0}." );
				}
				continue;
			}

			lot.Position = new Vector3( lot.Position.x, lot.Position.y, baseZ );
			lots[li] = lot;
			MarkFrontageLotAccepted( lot );
			_layoutMetrics.AcceptedLots++;
			_layoutMetrics.ReliefSum += relief;
			_layoutMetrics.ReliefSamples++;
			SpawnBuilding( lot, foundationDepth, service, placed, worldSeed, placementRng );
			usedLots.Add( li );
			placedLots.Add( lot );
			if ( lot.IsMicro )
				placedMicro++;
			else
				placedMajor++;
			placedByTownIndex[lot.TownIndex] = placedByTownIndex.GetValueOrDefault( lot.TownIndex ) + 1;
			if ( Config.DebugLogging && placed < 3 )
			{
				Log.Info(
					$"[Thorns Buildings] Spawned building {placed:00}: pos={lot.Position}, foundationDepth={foundationDepth:0.0}, sampledRelief={relief:0.0}." );
			}
			placed++;
			if ( placedMajor >= majorBuildingTarget && placedMicro >= microBuildingTarget )
				break;
		}

		if ( placed < requestedBuildingCount && lots.Count > 0 && Config.FallbackMaxFootprintReliefInches > Config.MaxFootprintReliefInches )
		{
			Log.Warning(
				$"[Thorns Buildings] Only {placed}/{requestedBuildingCount} lots passed relief <= {Config.MaxFootprintReliefInches:0.0}; filling with fallback relief <= {Config.FallbackMaxFootprintReliefInches:0.0} on flatter lots only." );

			for ( var li = 0; li < lots.Count; li++ )
			{
				if ( usedLots.Contains( li ) )
					continue;

				var lot = lots[li];
				if ( ShouldSkipLotForQuota( lot, placedMajor, placedMicro, majorBuildingTarget, microBuildingTarget, placedByTownIndex ) )
					continue;

				if ( ThornsWorldScatterFootprintRegistry.WouldBuildingFootprintOverlapRegistered(
					     lot.Position,
					     lot.Rotation,
					     lot.WidthCells,
					     lot.DepthCells ) )
				{
					overlapRejects++;
					continue;
				}

				if ( !TryResolveLotTerrain(
					     terrain,
					     terrainConfig,
					     lot,
					     Config.FallbackMaxFootprintReliefInches,
					     out var baseZ,
					     out var foundationDepth,
					     out var relief,
					     out var failedSnap ) )
					continue;

				lot.Position = new Vector3( lot.Position.x, lot.Position.y, baseZ );
				lots[li] = lot;
				MarkFrontageLotAccepted( lot );
				_layoutMetrics.AcceptedLots++;
				_layoutMetrics.ReliefSum += relief;
				_layoutMetrics.ReliefSamples++;
				SpawnBuilding( lot, foundationDepth, service, placed, worldSeed, placementRng );
				usedLots.Add( li );
				placedLots.Add( lot );
				if ( lot.IsMicro )
					placedMicro++;
				else
					placedMajor++;
				placedByTownIndex[lot.TownIndex] = placedByTownIndex.GetValueOrDefault( lot.TownIndex ) + 1;
				if ( Config.DebugLogging && placed < 3 )
				{
					Log.Info(
						$"[Thorns Buildings] Fallback spawned building {placed:00}: pos={lot.Position}, foundationDepth={foundationDepth:0.0}, sampledRelief={relief:0.0}." );
				}

				placed++;
				if ( placedMajor >= majorBuildingTarget && placedMicro >= microBuildingTarget )
					break;
			}
		}

		if ( placed < requestedBuildingCount )
		{
			ExpandLotsForUnderQuotaPois( lots, placedByTownIndex, placementRng );
			FillBuildingQuotaAtRelief(
				terrain,
				terrainConfig,
				lots,
				usedLots,
				placedLots,
				placedByTownIndex,
				ref placed,
				ref placedMajor,
				ref placedMicro,
				majorBuildingTarget,
				microBuildingTarget,
				Config.EmergencyMaxFootprintReliefInches,
				service,
				worldSeed,
				placementRng,
				ref overlapRejects,
				isEmergency: true );
		}

		EnsureMinimumPoiBuildings(
			terrain,
			terrainConfig,
			lots,
			usedLots,
			placedLots,
			placedByTownIndex,
			ref placed,
			ref placedMajor,
			ref placedMicro,
			service,
			worldSeed,
			placementRng,
			ref overlapRejects );

		FillPerPoiBuildingQuotas(
			terrain,
			terrainConfig,
			lots,
			usedLots,
			placedLots,
			placedByTownIndex,
			ref placed,
			ref placedMajor,
			ref placedMicro,
			majorBuildingTarget,
			microBuildingTarget,
			service,
			worldSeed,
			placementRng,
			ref overlapRejects );

		service.HostSyncFurnitureContainers();
		var pathPixels = PaintTownPaths( terrain, field, terrainConfig, placedLots );

		PublishTownNodes( placedByTownIndex );
		ThornsTownNodeRegistry.Publish( _townNodes );
		_spawnedFurniture = _spawner?.SpawnedFurniture ?? 0;
		_furnitureModelFallbacks = _spawner?.FurnitureModelFallbacks ?? 0;
		Log.Info(
			$"[Thorns Buildings] Placed {placed}/{requestedBuildingCount} clustered buildings (major={placedMajor}/{majorBuildingTarget}, micro={placedMicro}/{microBuildingTarget}). lots={lots.Count}, townCenters={_townCenters.Count}, "
			+ $"maxStories={Math.Clamp( Config.ProcBuildingStories, 1, ThornsProcBuildingSpawnDefaults.MaxStories )}, layoutVariant={Config.ProcBuildingLayoutVariant}, "
			+ $"snapRejects={snapRejects}, reliefRejects={reliefRejects}, overlapRejects={overlapRejects}, furniture={_spawnedFurniture}, furnitureFallbackModels={_furnitureModelFallbacks}, "
			+ $"pathPixels={pathPixels}, scatterFootprints={ThornsProcBuildingFootprintRegistry.Count}." );
		if ( Config.StreetFirstSettlements )
			_layoutMetrics.LogSummary();
		if ( Config.SettlementDebugOverlay || SettlementDebugOverlay.Enabled )
			SettlementDebugOverlay.Publish( _settlementLayouts );
		ThornsMapWorldService.Instance?.NotifyWorldMarkersChanged();
		ThornsWorldVisualLodService.EnsureForScene( Scene )?.RegisterProcBuildingField( _root );
	}

	int PaintTownPaths( Terrain terrain, HeightmapField field, ThornsTerrainConfig terrainConfig, IReadOnlyList<Lot> placedLots )
	{
		if ( !Config.PaintDirtPaths || !terrain.IsValid() || terrain.Storage is null || field is null )
			return 0;

		var segmentCapacity = Math.Max( 16, (_settlementLayouts.Count * 6) + (placedLots?.Count ?? 0) );
		var gridSegments = new List<(Vector3 Start, Vector3 End, float Width)>( segmentCapacity );
		var routedSegments = new List<(Vector3 Start, Vector3 End, float Width)>( segmentCapacity );

		for ( var li = 0; li < _settlementLayouts.Count; li++ )
		{
			var layout = _settlementLayouts[li];
			for ( var ri = 0; ri < layout.Roads.Count; ri++ )
			{
				var road = layout.Roads[ri];
				gridSegments.Add( (road.Start, road.End, road.Width) );
			}
		}

		if ( placedLots is not null && placedLots.Count > 0 && _townCenters.Count > 0 )
		{
			for ( var i = 0; i < placedLots.Count; i++ )
			{
				var lot = placedLots[i];
				if ( _streetLayoutTownIndices.Contains( lot.TownIndex ) )
					continue;

				var centerIndex = Math.Clamp( lot.TownIndex, 0, Math.Max( 0, _townCenters.Count - 1 ) );
				routedSegments.Add( (_townCenters[centerIndex], lot.Position, Config.TownPathWidthInches) );
			}

			AddIntraTownPathSegments( routedSegments, placedLots, _streetLayoutTownIndices );
		}

		AddInterTownPathSegments( routedSegments );

		if ( gridSegments.Count == 0 && routedSegments.Count == 0 )
			return 0;

		var painted = 0;
		if ( gridSegments.Count > 0 )
		{
			painted += TerrainMaterialPainter.PaintDirtPathSegments(
				terrain,
				field,
				terrainConfig,
				gridSegments );
		}

		if ( routedSegments.Count > 0 )
		{
			painted += Config.RouteDirtPathsByElevation
				? TerrainMaterialPainter.PaintDirtPathRoutes(
					terrain,
					field,
					terrainConfig,
					routedSegments,
					Config.PathRouteGridSize,
					Config.PathRouteElevationCost,
					Config.PathRouteHighElevationCost,
					Config.PathRouteSearchPaddingInches )
				: TerrainMaterialPainter.PaintDirtPathSegments(
					terrain,
					field,
					terrainConfig,
					routedSegments );
		}

		if ( painted > 0 )
		{
			terrain.UpdateMaterialsBuffer();
			terrain.SyncGPUTexture();
		}

		if ( Config.DebugLogging )
		{
			Log.Info(
				$"[Thorns Buildings] Painted dirt paths: gridSegments={gridSegments.Count}, routedSegments={routedSegments.Count}, routed={Config.RouteDirtPathsByElevation}, controlPixels={painted}." );
		}

		return painted;
	}

	void AddIntraTownPathSegments(
		List<(Vector3 Start, Vector3 End, float Width)> segments,
		IReadOnlyList<Lot> placedLots,
		HashSet<int> skipTownIndices )
	{
		if ( _townCenters.Count == 0 || placedLots.Count < 2 )
			return;

		for ( var town = 0; town < _townCenters.Count; town++ )
		{
			if ( skipTownIndices is not null && skipTownIndices.Contains( town ) )
				continue;

			var center = _townCenters[town];
			var townLots = placedLots
				.Where( lot => lot.TownIndex == town )
				.OrderBy( lot => MathF.Atan2( lot.Position.y - center.y, lot.Position.x - center.x ) )
				.ToList();

			if ( townLots.Count < 2 )
				continue;

			for ( var i = 0; i < townLots.Count; i++ )
			{
				var next = (i + 1) % townLots.Count;
				segments.Add( (townLots[i].Position, townLots[next].Position, Config.TownPathWidthInches * 0.82f) );
			}
		}
	}

	void AddInterTownPathSegments( List<(Vector3 Start, Vector3 End, float Width)> segments )
	{
		var centerCount = Config.ConnectMicroTownsWithPaths
			? _townCenters.Count
			: Math.Min( _lastMajorTownCenterCount, _townCenters.Count );
		if ( centerCount < 2 )
			return;

		var connected = new HashSet<int> { 0 };
		while ( connected.Count < centerCount )
		{
			var bestA = -1;
			var bestB = -1;
			var bestDist = float.MaxValue;

			foreach ( var a in connected )
			{
				for ( var b = 0; b < centerCount; b++ )
				{
					if ( connected.Contains( b ) )
						continue;

					var d = (_townCenters[a] - _townCenters[b]).LengthSquared;
					if ( d >= bestDist )
						continue;

					bestDist = d;
					bestA = a;
					bestB = b;
				}
			}

			if ( bestA < 0 || bestB < 0 )
				break;

			segments.Add( (_townCenters[bestA], _townCenters[bestB], Config.InterTownPathWidthInches) );
			connected.Add( bestB );
		}
	}

	static bool ShouldSkipLotForQuota(
		Lot lot,
		int placedMajor,
		int placedMicro,
		int majorBuildingTarget,
		int microBuildingTarget,
		IReadOnlyDictionary<int, int> placedByTownIndex )
	{
		if ( placedByTownIndex is not null
		     && placedByTownIndex.TryGetValue( lot.TownIndex, out var placedForPoi )
		     && placedForPoi >= lot.TargetBuildingCount )
			return true;

		return lot.IsMicro
			? placedMicro >= microBuildingTarget
			: placedMajor >= majorBuildingTarget;
	}

	void Clear()
	{
		_townCenters.Clear();
		_townCenterPlans.Clear();
		_townNodes.Clear();
		_lastMajorTownCenterCount = 0;
		_lastMicroTownCenterCount = 0;
		_lastMicroTownBuildingTarget = 0;
		_settlementLayouts.Clear();
		_streetLayoutTownIndices.Clear();
		ThornsTownNodeRegistry.Clear();
		ThornsProcBuildingFootprintRegistry.Clear();
		if ( _root.IsValid() )
		{
			ThornsWorldVisualLodService.Instance?.ClearProcBuildings();
			_root.Components.Get<ThornsBuildingLootWorldService>()?.Clear();
			_root.Destroy();
		}
	}

	void PublishTownNodes( IReadOnlyDictionary<int, int> placedByTownIndex )
	{
		_townNodes.Clear();
		for ( var i = 0; i < _townCenterPlans.Count; i++ )
		{
			var plan = _townCenterPlans[i];
			var placed = 0;
			placedByTownIndex?.TryGetValue( i, out placed );
			if ( placed <= 0 )
				continue;

			_townNodes.Add( new ThornsTownNode(
				i,
				plan.Center,
				plan.Identity,
				plan.TargetBuildingCount,
				placed ) );
		}
	}

	static bool TryBuildSettlementTestGallery(
		Terrain terrain,
		List<Vector3> centers,
		List<TownCenterPlan> centerPlans )
	{
		if ( !ThornsSettlementTestSceneBootstrap.IsActive )
			return false;

		var identities = ThornsPoiIdentityCatalog.GalleryIdentities;
		var terrainSize = terrain.TerrainSize;
		var origin = terrain.GameObject.WorldPosition;
		var terrainCenter = origin + new Vector3( terrainSize * 0.5f, terrainSize * 0.5f, 0f );

		if ( !ThornsTerrainSurface.TrySnapToTerrain( terrain, terrainCenter, out var ground ) )
			ground = terrainCenter;

		var baseZ = ground.z;

		for ( var i = 0; i < identities.Count; i++ )
		{
			var identity = identities[i];
			var def = ThornsPoiIdentityCatalog.Get( identity );
			var offset = ThornsSettlementTestSceneBootstrap.GetGalleryOffset( i );
			var probe = new Vector3( terrainCenter.x + offset.x, terrainCenter.y + offset.y, ground.z );

			if ( !ThornsTerrainSurface.TrySnapToTerrain( terrain, probe, out var snapped ) )
				snapped = probe;

			var center = new Vector3( snapped.x, snapped.y, baseZ );
			centers.Add( center );
			centerPlans.Add( new TownCenterPlan(
				center,
				identity,
				def.GalleryBuildingCount,
				def.RadiusInches,
				def.MinLotSpacingInches,
				false ) );

			Log.Info(
				$"[Thorns Settlement Test] Gallery slot {i}: {ThornsSettlementTestSceneBootstrap.DescribeGallerySlot( i )} at {center}." );
		}

		return true;
	}

	List<Lot> BuildTownLots( Terrain terrain, ThornsTerrainConfig terrainConfig, Random rng, int worldSeed )
	{
		var result = new List<Lot>( Math.Max( Config.BuildingCount, Config.BuildingCount * 4 ) );
		var terrainSize = terrain.TerrainSize;
		var margin = ThornsSettlementTestSceneBootstrap.IsActive
			? Math.Max( 120f, Config.TownRadiusInches * 0.35f )
			: Math.Max( 900f, Config.TownRadiusInches + 420f );
		var min = terrain.GameObject.WorldPosition.x + margin;
		var max = terrain.GameObject.WorldPosition.x + terrainSize - margin;
		var minY = terrain.GameObject.WorldPosition.y + margin;
		var maxY = terrain.GameObject.WorldPosition.y + terrainSize - margin;
		var townCount = ThornsSettlementTestSceneBootstrap.IsActive
			? ThornsSettlementTestSceneBootstrap.GallerySettlementCount
			: Math.Clamp( Config.TownCount, 1, 20 );
		var centers = new List<Vector3>( townCount );
		var centerPlans = new List<TownCenterPlan>();
		var microCenters = new List<Vector3>();
		var microIdentities = new List<ThornsPoiIdentity>();
		var centerSnapRejects = 0;
		var centerSeaRejects = 0;
		var majorIdentityDeck = ThornsPoiIdentityCatalog.BuildMajorIdentityDeck( townCount, rng );

		if ( TryBuildSettlementTestGallery( terrain, centers, centerPlans ) )
		{
			// gallery grid on flat test terrain
		}
		else
		{
			EnsureMajorTownCenters(
				terrain,
				terrainConfig,
				rng,
				min,
				max,
				minY,
				maxY,
				townCount,
				majorIdentityDeck,
				centers,
				centerPlans,
				ref centerSnapRejects,
				ref centerSeaRejects );
		}

		if ( centers.Count == 0 )
		{
			var center = terrain.GameObject.WorldPosition + new Vector3( terrainSize * 0.5f, terrainSize * 0.5f, 0f );
			var identity = ThornsPoiIdentity.Town;
			var def = ThornsPoiIdentityCatalog.Get( identity );
			centers.Add( center );
			centerPlans.Add( new TownCenterPlan(
				center,
				identity,
				ThornsPoiIdentityCatalog.GetGalleryBuildingCount( identity ),
				def.RadiusInches,
				def.MinLotSpacingInches,
				false ) );
		}

		if ( Config.MicroTownsEnabled && Config.MicroTownCount > 0 && !ThornsSettlementTestSceneBootstrap.IsActive )
		{
			var microCount = Math.Clamp( Config.MicroTownCount, 0, 64 );
			var microIdentityDeck = ThornsPoiIdentityCatalog.BuildMicroIdentityDeck( microCount, rng );
			var microCenterRejects = EnsureMicroTownCenters(
				terrain,
				terrainConfig,
				rng,
				min,
				max,
				minY,
				maxY,
				microCount,
				microIdentityDeck,
				centers,
				microCenters,
				microIdentities,
				ref centerSnapRejects,
				ref centerSeaRejects );

			if ( Config.DebugLogging && microCenterRejects > 0 )
				Log.Info( $"[Thorns Buildings] Micro town centers rejected/missed={microCenterRejects}/{microCount}." );
		}

		_townCenters.Clear();
		_townCenters.AddRange( centers );
		_townCenters.AddRange( microCenters );
		_townCenterPlans.Clear();
		_townCenterPlans.AddRange( centerPlans );
		for ( var i = 0; i < microCenters.Count; i++ )
		{
			var identity = microIdentities[i];
			var def = ThornsPoiIdentityCatalog.Get( identity );
			var targetBuildings = ThornsPoiIdentityCatalog.GetGalleryBuildingCount( identity );

			_townCenterPlans.Add( new TownCenterPlan(
				microCenters[i],
				identity,
				targetBuildings,
				Math.Min( def.RadiusInches, Math.Max( 120f, Config.MicroTownRadiusInches ) ),
				Math.Min( def.MinLotSpacingInches, Math.Max( 120f, Config.MicroTownMinLotSpacingInches ) ),
				true ) );
		}
		_lastMajorTownCenterCount = centers.Count;
		_lastMicroTownCenterCount = microCenters.Count;
		_lastMicroTownBuildingTarget = 0;

		var spacingRejects = 0;
		var microSpacingRejects = 0;
		var footprintOverlapRejects = 0;

		for ( var planIndex = 0; planIndex < _townCenterPlans.Count; planIndex++ )
		{
			var plan = _townCenterPlans[planIndex];
			var layoutMode = SettlementLayoutClassifier.Classify(
				plan.Identity,
				plan.TargetBuildingCount,
				Config.StreetFirstSettlements );

			if ( layoutMode == SettlementLayoutMode.Scatter )
			{
				RecordScatterSettlement();
				ScatterPolarLotsForPlan( result, plan, planIndex, rng, ref spacingRejects, ref microSpacingRejects );
				continue;
			}

			RecordStreetSettlement( layoutMode );
			if ( TryBuildStreetLotsForPlan(
				    terrain,
				    terrainConfig,
				    plan,
				    planIndex,
				    worldSeed,
				    layoutMode,
				    result,
				    ref footprintOverlapRejects ) )
				continue;

			RecordScatterSettlement();
			ScatterPolarLotsForPlan( result, plan, planIndex, rng, ref spacingRejects, ref microSpacingRejects );
		}

		if ( Config.DebugLogging )
		{
			var majorTargetBuildings = centerPlans.Sum( plan => plan.TargetBuildingCount );
			Log.Info(
				$"[Thorns Buildings] Lot candidates: majorCenters={centers.Count}/{townCount}, microCenters={microCenters.Count}/{Config.MicroTownCount}, lots={result.Count}, majorTargetBuildings={majorTargetBuildings}, microTargetBuildings={_lastMicroTownBuildingTarget}, centerSnapRejects={centerSnapRejects}, centerSeaRejects={centerSeaRejects}, spacingRejects={spacingRejects}, microSpacingRejects={microSpacingRejects}, crossPoiOverlapRejects={footprintOverlapRejects}, streetLayouts={_settlementLayouts.Count}." );
		}

		return result;
	}

	bool TryBuildStreetLotsForPlan(
		Terrain terrain,
		ThornsTerrainConfig terrainConfig,
		TownCenterPlan plan,
		int planIndex,
		int worldSeed,
		SettlementLayoutMode layoutMode,
		List<Lot> result,
		ref int footprintOverlapRejects )
	{
		var site = ToSitePlan( plan );
		var planRng = new Random( HashCode.Combine( worldSeed, planIndex, 0x57AEE7 ) );
		if ( !SettlementGridPlanner.TryGenerate(
			     terrain,
			     terrainConfig,
			     site,
			     planIndex,
			     worldSeed,
			     layoutMode,
			     SettlementGridConstants.GridMainLanePathWidthInches,
			     SettlementGridConstants.GridSidePathWidthInches,
			     planRng,
			     out var layout ) )
			return false;

		_settlementLayouts.Add( layout );
		_streetLayoutTownIndices.Add( planIndex );
		_layoutMetrics.TotalRoads += layout.Roads.Count;
		_layoutMetrics.TotalFrontageLots += layout.Lots.Count;
		_layoutMetrics.TotalRoadLength += layout.TotalRoadLength;

		if ( planIndex >= 0 && planIndex < _townCenters.Count )
			_townCenters[planIndex] = layout.Center;
		if ( planIndex >= 0 && planIndex < _townCenterPlans.Count )
		{
			_townCenterPlans[planIndex] = new TownCenterPlan(
				layout.Center,
				plan.Identity,
				plan.TargetBuildingCount,
				plan.RadiusInches,
				plan.MinLotSpacingInches,
				plan.IsMicro );
		}

		if ( Config.DebugLogging )
		{
			Log.Info(
				$"[Thorns Settlement] {plan.Identity} #{planIndex}: mode={layoutMode}, roads={layout.Roads.Count}, gridLots={layout.Lots.Count}, roadLength={layout.TotalRoadLength:0}." );
		}

		var lotLimit = Math.Max( plan.TargetBuildingCount * 2, plan.TargetBuildingCount + 4 );
		var addedForPlan = 0;
		var overlapRejectsForPlan = 0;
		for ( var i = 0; i < layout.Lots.Count && i < lotLimit; i++ )
		{
			var frontage = layout.Lots[i];
			var lot = new Lot(
				frontage.Position,
				frontage.Rotation,
				planIndex,
				plan.IsMicro,
				plan.Identity,
				plan.TargetBuildingCount,
				true,
				frontage.RoadType,
				frontage.WidthCells,
				frontage.DepthCells,
				frontage.ForcedBuildingType );

			if ( WouldLotOverlapAny( lot, result, skipSameTown: true, out var otherTownIndex ) )
			{
				footprintOverlapRejects++;
				overlapRejectsForPlan++;
				if ( Config.DebugLogging && overlapRejectsForPlan <= 3 )
				{
					Log.Info(
						$"[Thorns Buildings] Cross-POI overlap reject: poi={planIndex} ({plan.Identity}) vs poi={otherTownIndex}, "
						+ $"pos={lot.Position}, size={lot.WidthCells}x{lot.DepthCells}." );
				}

				continue;
			}

			result.Add( lot );
			addedForPlan++;
		}

		if ( Config.DebugLogging && overlapRejectsForPlan > 0 )
		{
			Log.Info(
				$"[Thorns Buildings] POI #{planIndex} {plan.Identity}: street lots kept={addedForPlan}/{Math.Min( layout.Lots.Count, lotLimit )}, "
				+ $"crossPoiOverlapRejects={overlapRejectsForPlan}." );
		}

		if ( plan.IsMicro )
			_lastMicroTownBuildingTarget += Math.Min( layout.Lots.Count, lotLimit );

		return layout.Lots.Count > 0;
	}

	void ScatterPolarLotsForPlan(
		List<Lot> result,
		TownCenterPlan plan,
		int planIndex,
		Random rng,
		ref int spacingRejects,
		ref int microSpacingRejects )
	{
		if ( plan.IsMicro )
		{
			var accepted = 0;
			for ( var attempt = 0; attempt < plan.TargetBuildingCount * 40 && accepted < plan.TargetBuildingCount; attempt++ )
			{
				if ( TryAddPolarLot( result, plan, planIndex, rng, ref microSpacingRejects ) )
				{
					accepted++;
					_lastMicroTownBuildingTarget++;
				}
			}

			return;
		}

		var targetCandidateLots = Math.Max( plan.TargetBuildingCount, plan.TargetBuildingCount * 4 );
		var attemptsTotal = targetCandidateLots * 80;
		for ( var i = 0; i < attemptsTotal && CountLotsForTown( result, planIndex ) < targetCandidateLots; i++ )
		{
			TryAddPolarLot( result, plan, planIndex, rng, ref spacingRejects );
		}
	}

	bool TryAddPolarLot(
		List<Lot> result,
		TownCenterPlan plan,
		int planIndex,
		Random rng,
		ref int spacingRejects )
	{
		var center = plan.Center;
		var angle = rng.NextSingle() * MathF.PI * 2f;
		var radius = MathF.Sqrt( rng.NextSingle() ) * plan.RadiusInches;
		var p = center + new Vector3( MathF.Cos( angle ) * radius, MathF.Sin( angle ) * radius, 0f );
		var yaw = MathF.Round( rng.NextSingle() * 3f ) * 90f;
		var lot = new Lot( p, Rotation.FromYaw( yaw ), planIndex, plan.IsMicro, plan.Identity, plan.TargetBuildingCount );

		if ( WouldLotOverlapAny( lot, result, skipSameTown: false, out _ ) )
		{
			spacingRejects++;
			return false;
		}

		result.Add( lot );
		return true;
	}

	static bool WouldLotOverlapAny(
		Lot candidate,
		IReadOnlyList<Lot> lots,
		bool skipSameTown,
		out int otherTownIndex )
	{
		otherTownIndex = -1;
		if ( lots is null || lots.Count == 0 )
			return false;

		for ( var i = 0; i < lots.Count; i++ )
		{
			var other = lots[i];
			if ( skipSameTown && other.TownIndex == candidate.TownIndex )
				continue;

			if ( !ThornsWorldScatterFootprintRegistry.WouldBuildingPlacementsOverlap(
				     candidate.Position,
				     candidate.Rotation,
				     candidate.WidthCells,
				     candidate.DepthCells,
				     other.Position,
				     other.Rotation,
				     other.WidthCells,
				     other.DepthCells ) )
				continue;

			otherTownIndex = other.TownIndex;
			return true;
		}

		return false;
	}

	static int CountLotsForTown( List<Lot> lots, int townIndex )
	{
		var count = 0;
		for ( var i = 0; i < lots.Count; i++ )
		{
			if ( lots[i].TownIndex == townIndex )
				count++;
		}

		return count;
	}

	static SettlementSitePlan ToSitePlan( TownCenterPlan plan ) =>
		new(
			plan.Center,
			plan.Identity,
			plan.TargetBuildingCount,
			plan.RadiusInches,
			plan.MinLotSpacingInches,
			plan.IsMicro );

	void RecordScatterSettlement() => _layoutMetrics.ScatterSettlements++;

	void RecordStreetSettlement( SettlementLayoutMode mode )
	{
		if ( mode == SettlementLayoutMode.SmallTown )
			_layoutMetrics.SmallTownSettlements++;
		else if ( mode == SettlementLayoutMode.LargeSettlement )
			_layoutMetrics.LargeSettlements++;
	}

	void MarkFrontageLotAccepted( Lot lot )
	{
		if ( !lot.HasFrontageRoad )
			return;

		for ( var i = 0; i < _settlementLayouts.Count; i++ )
		{
			var layout = _settlementLayouts[i];
			if ( layout.SettlementIndex != lot.TownIndex )
				continue;

			for ( var li = 0; li < layout.Lots.Count; li++ )
			{
				var frontage = layout.Lots[li];
				if ( (frontage.Position - lot.Position).LengthSquared > 64f )
					continue;

				frontage.Accepted = true;
				return;
			}
		}
	}

	void MarkFrontageLotRejected( Lot lot )
	{
		if ( !lot.HasFrontageRoad )
			return;

		for ( var i = 0; i < _settlementLayouts.Count; i++ )
		{
			var layout = _settlementLayouts[i];
			if ( layout.SettlementIndex != lot.TownIndex )
				continue;

			for ( var li = 0; li < layout.Lots.Count; li++ )
			{
				var frontage = layout.Lots[li];
				if ( (frontage.Position - lot.Position).LengthSquared > 64f )
					continue;

				frontage.Accepted = false;
				return;
			}
		}
	}

	enum SettlementCenterPickTier
	{
		StrictLowland = 0,
		RelaxedLowland = 1,
		AboveSea = 2,
		Desperate = 3
	}

	void EnsureMajorTownCenters(
		Terrain terrain,
		ThornsTerrainConfig terrainConfig,
		Random rng,
		float min,
		float max,
		float minY,
		float maxY,
		int townCount,
		ThornsPoiIdentity[] majorIdentityDeck,
		List<Vector3> centers,
		List<TownCenterPlan> centerPlans,
		ref int centerSnapRejects,
		ref int centerSeaRejects )
	{
		while ( centers.Count < townCount )
		{
			var slot = centers.Count;
			var identity = slot < majorIdentityDeck.Length
				? majorIdentityDeck[slot]
				: ThornsPoiIdentity.Town;
			var def = ThornsPoiIdentityCatalog.Get( identity );
			var targetBuildings = ThornsPoiIdentityCatalog.GetGalleryBuildingCount( identity );

			if ( !TryPickSettlementCenter(
				     terrain,
				     terrainConfig,
				     rng,
				     min,
				     max,
				     minY,
				     maxY,
				     centers,
				     null,
				     Config.MajorMinCenterSpacingInches,
				     0f,
				     out var center,
				     ref centerSnapRejects,
				     ref centerSeaRejects )
			     && !TryGridSearchSettlementCenter(
				     terrain,
				     terrainConfig,
				     min,
				     max,
				     minY,
				     maxY,
				     centers,
				     null,
				     Config.MajorMinCenterSpacingInches * 0.5f,
				     0f,
				     slot,
				     out center ) )
			{
				if ( Config.DebugLogging )
				{
					Log.Warning(
						$"[Thorns Buildings] Could not place major POI center {slot + 1}/{townCount} ({identity}) after relaxed search." );
				}

				break;
			}

			centers.Add( center );
			centerPlans.Add( new TownCenterPlan(
				center,
				identity,
				targetBuildings,
				def.RadiusInches,
				def.MinLotSpacingInches,
				false ) );
		}

		if ( Config.DebugLogging && centers.Count < townCount )
		{
			Log.Warning( $"[Thorns Buildings] Major center shortfall: {centers.Count}/{townCount}." );
		}
	}

	int EnsureMicroTownCenters(
		Terrain terrain,
		ThornsTerrainConfig terrainConfig,
		Random rng,
		float min,
		float max,
		float minY,
		float maxY,
		int microCount,
		ThornsPoiIdentity[] microIdentityDeck,
		IReadOnlyList<Vector3> majorCenters,
		List<Vector3> microCenters,
		List<ThornsPoiIdentity> microIdentities,
		ref int centerSnapRejects,
		ref int centerSeaRejects )
	{
		var missedBefore = microCount;
		while ( microCenters.Count < microCount )
		{
			var slot = microCenters.Count;
			if ( !TryPickSettlementCenter(
				     terrain,
				     terrainConfig,
				     rng,
				     min,
				     max,
				     minY,
				     maxY,
				     majorCenters,
				     microCenters,
				     Config.MicroTownAvoidMajorTownRadiusInches,
				     Config.MicroTownMinCenterSpacingInches,
				     out var center,
				     ref centerSnapRejects,
				     ref centerSeaRejects )
			     && !TryGridSearchSettlementCenter(
				     terrain,
				     terrainConfig,
				     min,
				     max,
				     minY,
				     maxY,
				     majorCenters,
				     microCenters,
				     Config.MicroTownAvoidMajorTownRadiusInches * 0.5f,
				     Config.MicroTownMinCenterSpacingInches * 0.5f,
				     microCount + slot,
				     out center ) )
			{
				if ( Config.DebugLogging )
				{
					Log.Warning(
						$"[Thorns Buildings] Could not place micro POI center {slot + 1}/{microCount} after relaxed search." );
				}

				break;
			}

			var identity = slot < microIdentityDeck.Length
				? microIdentityDeck[slot]
				: ThornsPoiIdentityCatalog.PickMicroIdentity( rng );
			microCenters.Add( center );
			microIdentities.Add( identity );
		}

		return missedBefore - microCenters.Count;
	}

	bool TryPickSettlementCenter(
		Terrain terrain,
		ThornsTerrainConfig terrainConfig,
		Random rng,
		float min,
		float max,
		float minY,
		float maxY,
		IReadOnlyList<Vector3> majorCenters,
		IReadOnlyList<Vector3> microCenters,
		float majorSpacing,
		float microSpacing,
		out Vector3 center,
		ref int centerSnapRejects,
		ref int centerSeaRejects )
	{
		center = default;

		for ( var tierIndex = 0; tierIndex <= (int)SettlementCenterPickTier.Desperate; tierIndex++ )
		{
			var tier = (SettlementCenterPickTier)tierIndex;
			var attempts = tier == SettlementCenterPickTier.StrictLowland ? 90 : 160;
			var maxRelief = tier switch
			{
				SettlementCenterPickTier.StrictLowland => ThornsProcBuildingTerrainUtil.TownCenterMaxReliefInches,
				SettlementCenterPickTier.RelaxedLowland => ThornsProcBuildingTerrainUtil.TownCenterMaxReliefInches * 1.5f,
				SettlementCenterPickTier.AboveSea => ThornsProcBuildingTerrainUtil.TownCenterMaxReliefInches * 2f,
				_ => ThornsProcBuildingTerrainUtil.TownCenterMaxReliefInches * 3f
			};
			var spacingScale = tier == SettlementCenterPickTier.StrictLowland ? 1f : 0.55f;

			for ( var attempt = 0; attempt < attempts; attempt++ )
			{
				var p = new Vector3( Lerp( min, max, rng.NextSingle() ), Lerp( minY, maxY, rng.NextSingle() ), 0f );
				if ( !ThornsTerrainSurface.TrySnapToTerrain( terrain, p, out var ground ) )
				{
					centerSnapRejects++;
					continue;
				}

				var elevationOk = tier is SettlementCenterPickTier.StrictLowland or SettlementCenterPickTier.RelaxedLowland
					? ThornsProcBuildingTerrainUtil.IsWithinLowlandElevation( terrain, terrainConfig, ground.z )
					: ThornsProcBuildingTerrainUtil.IsAboveSeaLevel( terrain, terrainConfig, ground.z );
				if ( !elevationOk )
				{
					centerSeaRejects++;
					continue;
				}

				if ( IsTooNearCenters( majorCenters, ground, majorSpacing * spacingScale )
				     || IsTooNearCenters( microCenters, ground, microSpacing * spacingScale ) )
					continue;

				var centerYaw = rng.NextSingle() * 360f;
				if ( !ThornsProcBuildingTerrainUtil.TryResolveLotBase(
					     terrain,
					     terrainConfig,
					     ground,
					     Rotation.FromYaw( centerYaw ),
					     maxRelief,
					     out var baseZ,
					     out _,
					     out _,
					     out _ ) )
					continue;

				center = new Vector3( ground.x, ground.y, baseZ );
				return true;
			}
		}

		return false;
	}

	bool TryGridSearchSettlementCenter(
		Terrain terrain,
		ThornsTerrainConfig terrainConfig,
		float min,
		float max,
		float minY,
		float maxY,
		IReadOnlyList<Vector3> majorCenters,
		IReadOnlyList<Vector3> microCenters,
		float majorSpacing,
		float microSpacing,
		int slotSeed,
		out Vector3 center )
	{
		center = default;
		const int grid = 32;
		var stride = Math.Max( 1, (grid * grid) / 48 );
		var maxRelief = ThornsProcBuildingTerrainUtil.TownCenterMaxReliefInches * 3f;

		for ( var cell = slotSeed * stride; cell < grid * grid; cell += stride )
		{
			var gx = cell % grid;
			var gy = cell / grid;
			var p = new Vector3(
				Lerp( min, max, (gx + 0.5f) / grid ),
				Lerp( minY, maxY, (gy + 0.5f) / grid ),
				0f );

			if ( !ThornsTerrainSurface.TrySnapToTerrain( terrain, p, out var ground ) )
				continue;

			if ( !ThornsProcBuildingTerrainUtil.IsAboveSeaLevel( terrain, terrainConfig, ground.z ) )
				continue;

			if ( IsTooNearCenters( majorCenters, ground, majorSpacing * 0.45f )
			     || IsTooNearCenters( microCenters, ground, microSpacing * 0.45f ) )
				continue;

			if ( !ThornsProcBuildingTerrainUtil.TryResolveLotBase(
				     terrain,
				     terrainConfig,
				     ground,
				     Rotation.FromYaw( (slotSeed * 37 + cell * 11) % 360 ),
				     maxRelief,
				     out var baseZ,
				     out _,
				     out _,
				     out _ ) )
				continue;

			center = new Vector3( ground.x, ground.y, baseZ );
			return true;
		}

		return false;
	}

	void ExpandLotsForUnderQuotaPois(
		List<Lot> lots,
		IReadOnlyDictionary<int, int> placedByTownIndex,
		Random rng )
	{
		var spacingRejects = 0;

		for ( var planIndex = 0; planIndex < _townCenterPlans.Count; planIndex++ )
		{
			var plan = _townCenterPlans[planIndex];
			var placed = placedByTownIndex?.GetValueOrDefault( planIndex ) ?? 0;
			if ( placed >= plan.TargetBuildingCount )
				continue;

			var needed = plan.TargetBuildingCount - placed;
			var targetCandidates = CountLotsForTown( lots, planIndex ) + Math.Max( needed * 8, 8 );
			var guard = 0;
			while ( CountLotsForTown( lots, planIndex ) < targetCandidates && guard++ < needed * 120 )
			{
				if ( !TryAddPolarLot( lots, plan, planIndex, rng, ref spacingRejects ) )
					break;
			}
		}
	}

	void FillBuildingQuotaAtRelief(
		Terrain terrain,
		ThornsTerrainConfig terrainConfig,
		List<Lot> lots,
		HashSet<int> usedLots,
		List<Lot> placedLots,
		Dictionary<int, int> placedByTownIndex,
		ref int placed,
		ref int placedMajor,
		ref int placedMicro,
		int majorBuildingTarget,
		int microBuildingTarget,
		float maxAllowedRelief,
		ThornsBuildingLootWorldService service,
		int worldSeed,
		Random placementRng,
		ref int overlapRejects,
		bool isEmergency )
	{
		if ( isEmergency && Config.DebugLogging )
		{
			Log.Warning(
				$"[Thorns Buildings] Emergency fill pass: relief <= {maxAllowedRelief:0.0}, placed={placed}." );
		}

		for ( var li = 0; li < lots.Count; li++ )
		{
			if ( usedLots.Contains( li ) )
				continue;

			var lot = lots[li];
			if ( ShouldSkipLotForQuota( lot, placedMajor, placedMicro, majorBuildingTarget, microBuildingTarget, placedByTownIndex ) )
				continue;

			if ( !TryPlaceBuildingLot(
				     terrain,
				     terrainConfig,
				     lots,
				     li,
				     lot,
				     maxAllowedRelief,
				     usedLots,
				     placedLots,
				     placedByTownIndex,
				     ref placed,
				     ref placedMajor,
				     ref placedMicro,
				     service,
				     worldSeed,
				     placementRng,
				     ref overlapRejects,
				     clampFoundationDepth: isEmergency ) )
				continue;

			if ( placedMajor >= majorBuildingTarget && placedMicro >= microBuildingTarget )
				break;
		}
	}

	void EnsureMinimumPoiBuildings(
		Terrain terrain,
		ThornsTerrainConfig terrainConfig,
		List<Lot> lots,
		HashSet<int> usedLots,
		List<Lot> placedLots,
		Dictionary<int, int> placedByTownIndex,
		ref int placed,
		ref int placedMajor,
		ref int placedMicro,
		ThornsBuildingLootWorldService service,
		int worldSeed,
		Random placementRng,
		ref int overlapRejects )
	{
		for ( var planIndex = 0; planIndex < _townCenterPlans.Count; planIndex++ )
		{
			if ( placedByTownIndex.GetValueOrDefault( planIndex ) > 0 )
				continue;

			var plan = _townCenterPlans[planIndex];
			var lot = new Lot(
				plan.Center,
				Rotation.FromYaw( (planIndex * 53) % 360 ),
				planIndex,
				plan.IsMicro,
				plan.Identity,
				plan.TargetBuildingCount );
			var lotIndex = lots.Count;
			lots.Add( lot );

			if ( TryPlaceBuildingLot(
				     terrain,
				     terrainConfig,
				     lots,
				     lotIndex,
				     lot,
				     Config.EmergencyMaxFootprintReliefInches,
				     usedLots,
				     placedLots,
				     placedByTownIndex,
				     ref placed,
				     ref placedMajor,
				     ref placedMicro,
				     service,
				     worldSeed,
				     placementRng,
				     ref overlapRejects,
				     clampFoundationDepth: true ) )
				continue;

			if ( Config.DebugLogging )
			{
				Log.Warning(
					$"[Thorns Buildings] POI #{planIndex} {plan.Identity} has no buildings — could not anchor at center." );
			}
		}
	}

	void FillPerPoiBuildingQuotas(
		Terrain terrain,
		ThornsTerrainConfig terrainConfig,
		List<Lot> lots,
		HashSet<int> usedLots,
		List<Lot> placedLots,
		Dictionary<int, int> placedByTownIndex,
		ref int placed,
		ref int placedMajor,
		ref int placedMicro,
		int majorBuildingTarget,
		int microBuildingTarget,
		ThornsBuildingLootWorldService service,
		int worldSeed,
		Random placementRng,
		ref int overlapRejects )
	{
		var spacingRejects = 0;
		var shortfallBefore = 0;
		for ( var planIndex = 0; planIndex < _townCenterPlans.Count; planIndex++ )
		{
			var plan = _townCenterPlans[planIndex];
			shortfallBefore += Math.Max( 0, plan.TargetBuildingCount - placedByTownIndex.GetValueOrDefault( planIndex ) );
		}

		if ( shortfallBefore <= 0 )
			return;

		if ( Config.DebugLogging )
		{
			Log.Warning(
				$"[Thorns Buildings] Quota fill pass: {shortfallBefore} building(s) short across POIs; relief <= {Config.QuotaFillMaxFootprintReliefInches:0.0}, clamped foundations." );
		}

		for ( var planIndex = 0; planIndex < _townCenterPlans.Count; planIndex++ )
		{
			var plan = _townCenterPlans[planIndex];
			var guard = 0;
			while ( placedByTownIndex.GetValueOrDefault( planIndex ) < plan.TargetBuildingCount
			        && guard++ < plan.TargetBuildingCount * 220 )
			{
				var placedForPoi = false;

				for ( var li = 0; li < lots.Count; li++ )
				{
					if ( usedLots.Contains( li ) || lots[li].TownIndex != planIndex )
						continue;

					if ( TryPlaceBuildingLot(
						     terrain,
						     terrainConfig,
						     lots,
						     li,
						     lots[li],
						     Config.QuotaFillMaxFootprintReliefInches,
						     usedLots,
						     placedLots,
						     placedByTownIndex,
						     ref placed,
						     ref placedMajor,
						     ref placedMicro,
						     service,
						     worldSeed,
						     placementRng,
						     ref overlapRejects,
						     clampFoundationDepth: true ) )
					{
						placedForPoi = true;
						break;
					}
				}

				if ( placedForPoi )
					continue;

				if ( !TryAddPolarLot( lots, plan, planIndex, placementRng, ref spacingRejects ) )
					break;

				var newIndex = lots.Count - 1;
				if ( TryPlaceBuildingLot(
					     terrain,
					     terrainConfig,
					     lots,
					     newIndex,
					     lots[newIndex],
					     Config.QuotaFillMaxFootprintReliefInches,
					     usedLots,
					     placedLots,
					     placedByTownIndex,
					     ref placed,
					     ref placedMajor,
					     ref placedMicro,
					     service,
					     worldSeed,
					     placementRng,
					     ref overlapRejects,
					     clampFoundationDepth: true ) )
					continue;

				if ( guard > plan.TargetBuildingCount * 60 )
					break;
			}
		}
	}

	bool TryPlaceBuildingLot(
		Terrain terrain,
		ThornsTerrainConfig terrainConfig,
		List<Lot> lots,
		int lotIndex,
		Lot lot,
		float maxAllowedRelief,
		HashSet<int> usedLots,
		List<Lot> placedLots,
		Dictionary<int, int> placedByTownIndex,
		ref int placed,
		ref int placedMajor,
		ref int placedMicro,
		ThornsBuildingLootWorldService service,
		int worldSeed,
		Random placementRng,
		ref int overlapRejects,
		bool clampFoundationDepth = false )
	{
		if ( usedLots.Contains( lotIndex ) )
			return false;

		if ( ThornsWorldScatterFootprintRegistry.WouldBuildingFootprintOverlapRegistered(
			     lot.Position,
			     lot.Rotation,
			     lot.WidthCells,
			     lot.DepthCells ) )
		{
			overlapRejects++;
			MarkFrontageLotRejected( lot );
			_layoutMetrics.RejectedLots++;
			return false;
		}

		if ( !TryResolveLotTerrain(
			     terrain,
			     terrainConfig,
			     lot,
			     maxAllowedRelief,
			     out var baseZ,
			     out var foundationDepth,
			     out var relief,
			     out _,
			     clampFoundationDepth ) )
		{
			MarkFrontageLotRejected( lot );
			_layoutMetrics.RejectedLots++;
			return false;
		}

		lot.Position = new Vector3( lot.Position.x, lot.Position.y, baseZ );
		lots[lotIndex] = lot;
		MarkFrontageLotAccepted( lot );
		_layoutMetrics.AcceptedLots++;
		_layoutMetrics.ReliefSum += relief;
		_layoutMetrics.ReliefSamples++;
		SpawnBuilding( lot, foundationDepth, service, placed, worldSeed, placementRng );
		usedLots.Add( lotIndex );
		placedLots.Add( lot );
		if ( lot.IsMicro )
			placedMicro++;
		else
			placedMajor++;
		placedByTownIndex[lot.TownIndex] = placedByTownIndex.GetValueOrDefault( lot.TownIndex ) + 1;
		placed++;
		return true;
	}

	static bool IsTooNearCenters( IReadOnlyList<Vector3> centers, Vector3 p, float minSpacing )
	{
		if ( centers is null || centers.Count == 0 || minSpacing <= 0f )
			return false;

		var minSqr = minSpacing * minSpacing;
		foreach ( var center in centers )
		{
			var d = center - p;
			if ( new Vector2( d.x, d.y ).LengthSquared < minSqr )
				return true;
		}

		return false;
	}

	void SpawnBuilding(
		Lot lot,
		float foundationDepth,
		ThornsBuildingLootWorldService service,
		int index,
		int worldSeed,
		Random placementRng )
	{
		var buildingType = lot.ForcedBuildingType
		                   ?? ThornsPoiIdentityCatalog.PickBuildingType(
			                   lot.Identity,
			                   placementRng,
			                   index,
			                   lot.HasFrontageRoad ? lot.FrontageRoadType : null );
		var variantIndex = ResolveLayoutVariant( buildingType, index, worldSeed );
		var stories = ResolveStoryCount( buildingType, lot, index, worldSeed, placementRng );

		_spawner.Spawn(
			new ThornsProcBuildingShellSpawner.Request(
				lot.Position,
				lot.Rotation,
				_root,
				buildingType,
				variantIndex,
				stories,
				index,
				foundationDepth,
				$"Thorns Building {index:00} ({buildingType}, {stories}F, {lot.WidthCells}x{lot.DepthCells})",
				WidthCells: lot.WidthCells,
				DepthCells: lot.DepthCells ),
			service,
			placementRng );

		_spawnedFurniture = _spawner.SpawnedFurniture;
		_furnitureModelFallbacks = _spawner.FurnitureModelFallbacks;
	}

	int ResolveStoryCount(
		ThornsProcBuildingType buildingType,
		Lot lot,
		int buildingIndex,
		int worldSeed,
		Random rng )
	{
		var maxStories = Math.Clamp( Config.ProcBuildingStories, 1, ThornsProcBuildingSpawnDefaults.MaxStories );
		maxStories = Math.Min( maxStories, ThornsPoiIdentityCatalog.GetMaxStories( lot.Identity ) );
		if ( ThornsProcBuildingFootprintCatalog.IsMegastructureType( buildingType )
		     || lot.WidthCells > ThornsProcBuildingInterior.GridCells
		     || lot.DepthCells > ThornsProcBuildingInterior.GridCells )
			maxStories = Math.Min( maxStories, 2 );
		if ( maxStories <= 1 )
			return 1;

		var sampleRng = rng ?? new Random( HashCode.Combine( worldSeed, buildingIndex, buildingType, lot.Identity, 0x57A11 ) );
		var weights = StoryWeights( buildingType, lot.Identity );

		if ( lot.HasFrontageRoad )
		{
			weights = lot.FrontageRoadType switch
			{
				SettlementRoadType.Primary => BoostTallStories( weights, 2 ),
				SettlementRoadType.Secondary => BoostTallStories( weights, 1 ),
				SettlementRoadType.Alley or SettlementRoadType.Connector => BoostShortStories( weights ),
				_ => weights
			};
		}

		var total = 0;
		for ( var story = 1; story <= maxStories; story++ )
			total += weights[story - 1];

		if ( total <= 0 )
			return 1;

		var roll = sampleRng.Next( total );
		for ( var story = 1; story <= maxStories; story++ )
		{
			roll -= weights[story - 1];
			if ( roll < 0 )
				return story;
		}

		return maxStories;
	}

	static int[] StoryWeights( ThornsProcBuildingType buildingType, ThornsPoiIdentity identity )
	{
		var weights = buildingType switch
		{
			ThornsProcBuildingType.Skyscraper => W( 1, 1, 3, 6, 8, 10 ),
			ThornsProcBuildingType.ApartmentTower => W( 1, 2, 4, 5, 7, 8 ),
			ThornsProcBuildingType.OfficeBuilding => W( 1, 2, 4, 5, 6, 7 ),
			ThornsProcBuildingType.Apartment => W( 2, 4, 5, 4, 3, 2 ),
			ThornsProcBuildingType.MilitaryComplex => W( 2, 3, 4, 3, 2, 1 ),
			ThornsProcBuildingType.Factory => W( 3, 5, 4, 3, 2, 1 ),
			ThornsProcBuildingType.Warehouse => W( 3, 5, 3, 2, 1, 0 ),
			ThornsProcBuildingType.Store => W( 4, 5, 3, 2, 1, 0 ),
			ThornsProcBuildingType.House => W( 5, 5, 3, 2, 1, 0 ),
			ThornsProcBuildingType.Ruin => W( 5, 4, 2, 1, 1, 0 ),
			ThornsProcBuildingType.RadioOutpost => W( 3, 4, 3, 2, 1, 0 ),
			ThornsProcBuildingType.Cabin => W( 8, 4, 2, 1, 0, 0 ),
			ThornsProcBuildingType.Barn => W( 6, 4, 2, 1, 0, 0 ),
			_ => W( 4, 4, 3, 2, 1, 0 )
		};

		return identity switch
		{
			ThornsPoiIdentity.Metropolis => BoostTallStories( weights, 2 ),
			ThornsPoiIdentity.City => BoostTallStories( weights, 1 ),
			ThornsPoiIdentity.Rural or ThornsPoiIdentity.CabinSite or ThornsPoiIdentity.Farmstead => BoostShortStories( weights ),
			_ => weights
		};
	}

	static int[] W( params int[] weights )
	{
		var max = ThornsProcBuildingSpawnDefaults.MaxStories;
		var padded = new int[max];
		for ( var i = 0; i < max; i++ )
			padded[i] = i < weights.Length ? weights[i] : 0;
		return padded;
	}

	static int TallStoryBoost( int storyIndex, int amount ) => storyIndex switch
	{
		< 2 => 0,
		2 => amount,
		3 => amount * 2,
		4 => amount * 3,
		_ => amount * 4
	};

	static int[] BoostTallStories( int[] weights, int amount )
	{
		var result = new int[weights.Length];
		for ( var i = 0; i < weights.Length; i++ )
			result[i] = weights[i] + TallStoryBoost( i, amount );
		return result;
	}

	static int[] BoostShortStories( int[] weights )
	{
		var result = new int[weights.Length];
		for ( var i = 0; i < weights.Length; i++ )
		{
			result[i] = i switch
			{
				0 => weights[0] + 3,
				1 => weights[1] + 1,
				2 => Math.Max( 0, weights[2] - 1 ),
				3 => Math.Max( 0, weights[3] - 1 ),
				_ => Math.Max( 0, weights[i] - 2 )
			};
		}

		return result;
	}

	int ResolveLayoutVariant( ThornsProcBuildingType buildingType, int buildingIndex, int worldSeed )
	{
		if ( Config.ProcBuildingLayoutVariant >= 0 )
		{
			var count = ThornsInteriorFurnitureAsciiLayouts.VariantCount( buildingType );
			return count <= 0 ? 0 : Math.Clamp( Config.ProcBuildingLayoutVariant, 0, count - 1 );
		}

		var rng = new Random( HashCode.Combine( worldSeed, buildingIndex, 0x4A21 ) );
		return ThornsProcBuildingTypePicker.PickVariantIndex( rng, buildingType, buildingIndex );
	}

	static float Lerp( float a, float b, float t ) => a + (b - a) * t;

	void EnsureConfigDefaults()
	{
		Config ??= new ThornsProcBuildingConfig();
		if ( Config.ProcBuildingStories < 1 )
			Config.ProcBuildingStories = ThornsProcBuildingSpawnDefaults.MaxStories;
	}

	bool TryResolveLotTerrain(
		Terrain terrain,
		ThornsTerrainConfig terrainConfig,
		Lot lot,
		float maxAllowedRelief,
		out float baseZ,
		out float foundationDepth,
		out float relief,
		out bool failedSnap,
		bool clampFoundationDepth = false )
	{
		var halfX = ThornsProcBuildingFootprintCatalog.TerrainHalfExtentXInches( lot.WidthCells );
		var halfY = ThornsProcBuildingFootprintCatalog.TerrainHalfExtentYInches( lot.DepthCells );
		return ThornsProcBuildingTerrainUtil.TryResolveLotBase(
			terrain,
			terrainConfig,
			lot.Position,
			lot.Rotation,
			halfX,
			halfY,
			maxAllowedRelief,
			out baseZ,
			out foundationDepth,
			out relief,
			out failedSnap,
			Config.FoundationLiftInches,
			clampFoundationDepth );
	}

	struct Lot
	{
		public Vector3 Position;
		public Rotation Rotation;
		public int TownIndex;
		public bool IsMicro;
		public ThornsPoiIdentity Identity;
		public int TargetBuildingCount;
		public bool HasFrontageRoad;
		public SettlementRoadType FrontageRoadType;
		public int WidthCells;
		public int DepthCells;
		public ThornsProcBuildingType? ForcedBuildingType;

		public Lot(
			Vector3 position,
			Rotation rotation,
			int townIndex,
			bool isMicro,
			ThornsPoiIdentity identity,
			int targetBuildingCount,
			bool hasFrontageRoad = false,
			SettlementRoadType frontageRoadType = SettlementRoadType.Connector,
			int widthCells = ThornsProcBuildingInterior.GridCells,
			int depthCells = ThornsProcBuildingInterior.GridCells,
			ThornsProcBuildingType? forcedBuildingType = null )
		{
			Position = position;
			Rotation = rotation;
			TownIndex = townIndex;
			IsMicro = isMicro;
			Identity = identity;
			TargetBuildingCount = targetBuildingCount;
			HasFrontageRoad = hasFrontageRoad;
			FrontageRoadType = frontageRoadType;
			WidthCells = widthCells;
			DepthCells = depthCells;
			ForcedBuildingType = forcedBuildingType;
		}
	}

	readonly struct TownCenterPlan
	{
		public readonly Vector3 Center;
		public readonly ThornsPoiIdentity Identity;
		public readonly int TargetBuildingCount;
		public readonly float RadiusInches;
		public readonly float MinLotSpacingInches;
		public readonly bool IsMicro;

		public TownCenterPlan(
			Vector3 center,
			ThornsPoiIdentity identity,
			int targetBuildingCount,
			float radiusInches,
			float minLotSpacingInches,
			bool isMicro )
		{
			Center = center;
			Identity = identity;
			TargetBuildingCount = targetBuildingCount;
			RadiusInches = radiusInches;
			MinLotSpacingInches = minLotSpacingInches;
			IsMicro = isMicro;
		}
	}
}

[Title( "Thorns Building Loot World" )]
[Category( "Terrain/Buildings" )]
public sealed class ThornsBuildingLootWorldService : Component
{
	public static ThornsBuildingLootWorldService Instance { get; private set; }

	readonly Dictionary<int, LootFurniture> _furniture = new();
	int _nextId = 1;

	protected override void OnStart()
	{
		Instance = this;
	}

	protected override void OnDestroy()
	{
		if ( Instance == this )
			Instance = null;
	}

	public void Clear()
	{
		_furniture.Clear();
		_nextId = 1;
		Instance = this;
	}

	public void RegisterFurniture(
		GameObject obj,
		string structureDefId,
		ThornsProcBuildingType buildingType,
		Random rng )
	{
		if ( !obj.IsValid() )
			return;

		var id = _nextId++;
		var isContainer = ThornsFurnitureLootPolicy.ShouldSpawnProcLootContainer( structureDefId );
		var lootTable = isContainer
			? ThornsFurnitureLootPolicy.PickLootTable( structureDefId, buildingType, rng )
			: "";

		_furniture[id] = new LootFurniture( id, obj, structureDefId, lootTable, isContainer );

		var marker = obj.Components.Get<ThornsLootableFurniture>() ?? obj.Components.Create<ThornsLootableFurniture>();
		marker.FurnitureId = id;
		marker.StructureDefId = structureDefId ?? "";
		marker.LootTable = lootTable;
		marker.IsLootContainer = isContainer;

		if ( isContainer )
			Terraingen.World.ThornsWorldLootContainerService.Instance?.HostRegisterFurniture( id, lootTable );
	}

	/// <summary>Register all spawned furniture with the loot container service (after EnsureForScene).</summary>
	public void HostSyncFurnitureContainers()
	{
		if ( !ThornsMultiplayer.IsHostOrOffline )
			return;

		var containerService = Terraingen.World.ThornsWorldLootContainerService.Instance;
		if ( containerService is null || !containerService.IsValid() )
			return;

		foreach ( var (id, furniture) in _furniture )
		{
			if ( !furniture.IsLootContainer || !furniture.Object.IsValid() )
				continue;

			containerService.HostRegisterFurniture( id, furniture.LootTable );
		}
	}

	public void HostUnregisterFurniture( int furnitureId )
	{
		if ( furnitureId <= 0 )
			return;

		_furniture.Remove( furnitureId );
		Terraingen.World.ThornsWorldLootContainerService.Instance?.HostUnregister(
			Terraingen.World.ThornsWorldLootContainerService.FurnitureKey( furnitureId ) );
	}

	public bool TryGetFurnitureLootTable( int furnitureId, out string lootTable )
	{
		lootTable = "";
		if ( !_furniture.TryGetValue( furnitureId, out var furniture ) || !furniture.IsLootContainer )
			return false;

		lootTable = furniture.LootTable ?? "home_clutter";
		return true;
	}

	public bool TryGetFurnitureWorldPosition( int furnitureId, out Vector3 worldPos )
	{
		worldPos = default;
		if ( !_furniture.TryGetValue( furnitureId, out var furniture ) || !furniture.Object.IsValid() )
			return false;

		worldPos = furniture.Object.WorldPosition;
		return true;
	}

	public const float InteractRange = 260f;

	public bool HasTargetInFront( GameObject playerRoot ) =>
		TryPickAlongRay( playerRoot, out _ );

	public bool TryPickAlongRay( GameObject playerRoot, out int furnitureId )
	{
		furnitureId = 0;
		if ( !TryResolveAim( playerRoot, out var origin, out var forward ) )
			return false;

		return TryPickFurnitureAlongRay( origin, forward, InteractRange, playerRoot, out furnitureId );
	}

	public bool TryPickFurnitureInFront( GameObject playerRoot, out int furnitureId, out string containerKey )
	{
		furnitureId = 0;
		containerKey = "";
		if ( !TryPickAlongRay( playerRoot, out furnitureId ) || furnitureId <= 0 )
			return false;

		containerKey = Terraingen.World.ThornsWorldLootContainerService.FurnitureKey( furnitureId );
		return true;
	}

	public bool TryPickCraftStationFurnitureInFront( GameObject playerRoot, out ThornsCraftStationKind station )
	{
		station = ThornsCraftStationKind.Hand;
		if ( !TryResolveAim( playerRoot, out var origin, out var forward ) )
			return false;

		return TryPickCraftStationFurnitureAlongRay( origin, forward, InteractRange, playerRoot, out station );
	}

	public bool TryPickCraftStationFurnitureAlongRay(
		Vector3 origin,
		Vector3 direction,
		float maxRange,
		GameObject ignoreRoot,
		out ThornsCraftStationKind station )
	{
		station = ThornsCraftStationKind.Hand;
		var dir = direction.Normal;
		if ( dir.Length < 0.95f || ignoreRoot is null || !ignoreRoot.IsValid() )
			return false;

		var scene = ignoreRoot.Scene;
		if ( scene is null || !scene.IsValid() )
			return false;

		var end = origin + dir * maxRange;
		var trace = scene.Trace
			.Sphere( Terraingen.Combat.ThornsInteractAimPick.DefaultSphereTraceRadius, origin, end )
			.IgnoreGameObjectHierarchy( ignoreRoot )
			.Run();

		if ( trace.Hit && trace.GameObject.IsValid() )
		{
			var marker = trace.GameObject.Components.Get<ThornsLootableFurniture>( FindMode.EverythingInSelfAndParent );
			if ( TryAcceptCraftStationMarker( marker, out station ) )
				return true;
		}

		return TryPickCraftStationFromRegistry( origin, dir, maxRange, out station );
	}

	bool TryPickCraftStationFromRegistry( Vector3 origin, Vector3 dir, float maxRange, out ThornsCraftStationKind station )
	{
		station = ThornsCraftStationKind.Hand;
		var best = float.MaxValue;

		foreach ( var (_, furniture) in _furniture )
		{
			if ( !furniture.Object.IsValid() || !furniture.Object.Enabled )
				continue;

			if ( !ThornsPlacedStructureInteraction.TryGetCraftStationKind( furniture.StructureDefId, out var candidate )
			     || candidate == ThornsCraftStationKind.Hand )
				continue;

			var center = furniture.Object.WorldPosition;
			var pickRadius = 58f;
			if ( TryResolveFurniturePickBounds( furniture.Object, out var pickCenter, out var pickSize ) )
			{
				center = pickCenter;
				pickRadius = MathF.Max( pickRadius, pickSize * 0.65f );
			}
			else
			{
				center += Vector3.Up * 32f;
			}

			if ( !Terraingen.Combat.ThornsInteractAimPick.TryRaySphere( origin, dir, center, pickRadius, out var dist )
			     || dist > maxRange
			     || dist >= best )
				continue;

			best = dist;
			station = candidate;
		}

		return station != ThornsCraftStationKind.Hand;
	}

	static bool TryAcceptCraftStationMarker( ThornsLootableFurniture marker, out ThornsCraftStationKind station )
	{
		station = ThornsCraftStationKind.Hand;
		if ( !marker.IsValid() || string.IsNullOrWhiteSpace( marker.StructureDefId ) )
			return false;

		return ThornsPlacedStructureInteraction.TryGetCraftStationKind( marker.StructureDefId, out station )
		       && station != ThornsCraftStationKind.Hand;
	}

	public bool TryPickFurnitureAlongRay( Vector3 origin, Vector3 direction, float maxRange, GameObject ignoreRoot, out int furnitureId )
	{
		furnitureId = 0;
		var dir = direction.Normal;
		if ( dir.Length < 0.95f || ignoreRoot is null || !ignoreRoot.IsValid() )
			return false;

		var scene = ignoreRoot.Scene;
		if ( scene is null || !scene.IsValid() )
			return false;

		var end = origin + dir * maxRange;
		var trace = scene.Trace
			.Sphere( Terraingen.Combat.ThornsInteractAimPick.DefaultSphereTraceRadius, origin, end )
			.IgnoreGameObjectHierarchy( ignoreRoot )
			.Run();

		if ( trace.Hit && trace.GameObject.IsValid() )
		{
			var marker = trace.GameObject.Components.Get<ThornsLootableFurniture>( FindMode.EverythingInSelfAndParent );
			if ( TryAcceptFurnitureMarker( marker, out furnitureId ) )
				return true;
		}

		return TryPickFromRegistry( origin, dir, maxRange, out furnitureId );
	}

	bool TryPickFromRegistry( Vector3 origin, Vector3 dir, float maxRange, out int furnitureId )
	{
		furnitureId = 0;
		var best = float.MaxValue;

		foreach ( var (id, furniture) in _furniture )
		{
			if ( !furniture.IsLootContainer || !furniture.Object.IsValid() || !furniture.Object.Enabled )
				continue;

			var center = furniture.Object.WorldPosition;
			var pickRadius = 58f;
			if ( TryResolveFurniturePickBounds( furniture.Object, out var pickCenter, out var pickSize ) )
			{
				center = pickCenter;
				pickRadius = MathF.Max( pickRadius, pickSize * 0.5f );
			}
			else
			{
				center += Vector3.Up * 24f;
			}

			if ( !Terraingen.Combat.ThornsInteractAimPick.TryRaySphere( origin, dir, center, pickRadius, out var dist )
			     || dist > maxRange
			     || dist >= best )
				continue;

			best = dist;
			furnitureId = id;
		}

		return furnitureId > 0;
	}

	static bool TryAcceptFurnitureMarker( ThornsLootableFurniture marker, out int furnitureId )
	{
		furnitureId = 0;
		if ( !marker.IsValid() || marker.FurnitureId <= 0 || !marker.IsLootContainer )
			return false;

		if ( Instance?.TryGetFurnitureLootTable( marker.FurnitureId, out _ ) == true )
		{
			furnitureId = marker.FurnitureId;
			return true;
		}

		if ( !string.IsNullOrWhiteSpace( marker.LootTable ) )
		{
			Terraingen.World.ThornsWorldLootContainerService.Instance?.HostRegisterFurniture(
				marker.FurnitureId,
				marker.LootTable );
			furnitureId = marker.FurnitureId;
			return true;
		}

		return false;
	}

	static bool TryResolveAim( GameObject playerRoot, out Vector3 origin, out Vector3 forward )
		=> Terraingen.Combat.ThornsInteractAimPick.TryResolveCrosshairAimRay( playerRoot, out origin, out forward );

	static bool TryResolveFurniturePickBounds( GameObject obj, out Vector3 worldCenter, out float worldRadius )
	{
		worldCenter = default;
		worldRadius = 58f;
		if ( !obj.IsValid() )
			return false;

		foreach ( var collider in obj.Components.GetAll<Collider>( FindMode.EverythingInSelf ) )
		{
			if ( !collider.IsValid() || !collider.Enabled || collider.IsTrigger )
				continue;

			var bounds = collider.GetWorldBounds();
			worldCenter = ( bounds.Mins + bounds.Maxs ) * 0.5f;
			worldRadius = ( bounds.Maxs - bounds.Mins ).Length * 0.5f;
			return true;
		}

		var renderer = obj.Components.Get<ModelRenderer>();
		if ( !renderer.IsValid() || !renderer.Model.IsValid() )
			return false;

		var localBounds = TerraingenAnchoredPhysics.GetModelRenderBounds( renderer.Model );
		var scale = obj.WorldScale;
		var scaledHalf = new Vector3(
			localBounds.Size.x * MathF.Abs( scale.x ) * 0.5f,
			localBounds.Size.y * MathF.Abs( scale.y ) * 0.5f,
			localBounds.Size.z * MathF.Abs( scale.z ) * 0.5f );
		worldCenter = obj.WorldPosition + obj.WorldRotation * ( localBounds.Center * scale );
		worldRadius = MathF.Max( MathF.Max( scaledHalf.x, scaledHalf.y ), scaledHalf.z );
		return worldRadius > 1f;
	}

	readonly struct LootFurniture
	{
		public readonly int Id;
		public readonly GameObject Object;
		public readonly string StructureDefId;
		public readonly string LootTable;
		public readonly bool IsLootContainer;

		public LootFurniture( int id, GameObject obj, string structureDefId, string lootTable, bool isLootContainer )
		{
			Id = id;
			Object = obj;
			StructureDefId = structureDefId ?? "";
			LootTable = lootTable ?? "";
			IsLootContainer = isLootContainer;
		}
	}
}

public sealed class ThornsLootableFurniture : Component
{
	[Property] public int FurnitureId { get; set; }
	[Property] public string StructureDefId { get; set; } = "";
	[Property] public string LootTable { get; set; } = "home_clutter";
	[Property] public bool IsLootContainer { get; set; }
}
