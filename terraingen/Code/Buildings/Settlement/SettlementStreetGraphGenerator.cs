namespace Terraingen.Buildings.Settlement;

using Terraingen.TerrainGen;

/// <summary>Generates terrain-aware street graphs before building placement.</summary>
public static class SettlementStreetGraphGenerator
{
	const int MaxPolylineSamples = 14;

	public static bool TryGenerate(
		Terrain terrain,
		ThornsTerrainConfig terrainConfig,
		SettlementSitePlan plan,
		int settlementIndex,
		int worldSeed,
		SettlementLayoutMode mode,
		float primaryPathWidth,
		float secondaryPathWidth,
		float alleyPathWidth,
		Random rng,
		List<SettlementRoadSegment> roadScratch,
		out SettlementLayout layout )
	{
		layout = null;
		roadScratch.Clear();

		if ( mode == SettlementLayoutMode.Scatter || !terrain.IsValid() || terrainConfig is null )
			return false;

		var primaryLength = ResolvePrimaryLength( plan.TargetBuildingCount, mode, rng );
		var orientation = rng.NextSingle() * MathF.PI * 2f;
		var curvature = mode == SettlementLayoutMode.LargeSettlement
			? rng.NextSingle() * 0.08f + 0.02f
			: rng.NextSingle() * 0.05f + 0.01f;

		if ( !TrySamplePolylineRoad(
			     terrain,
			     terrainConfig,
			     plan.Center,
			     orientation,
			     primaryLength,
			     curvature,
			     rng,
			     out var primaryPoints ) )
			return false;

		AppendPolylineSegments( roadScratch, primaryPoints, primaryPathWidth, SettlementRoadType.Primary );

		if ( mode == SettlementLayoutMode.SmallTown && rng.NextSingle() < 0.55f )
		{
			var branchT = 0.35f + rng.NextSingle() * 0.3f;
			var branchOrigin = SamplePolylineAt( primaryPoints, branchT );
			var branchAngle = orientation + (rng.NextSingle() < 0.5f ? 1.57f : -1.57f) + rng.NextSingle() * 0.35f - 0.175f;
			var branchLen = primaryLength * (0.35f + rng.NextSingle() * 0.25f);
			if ( TrySamplePolylineRoad(
				     terrain,
				     terrainConfig,
				     branchOrigin,
				     branchAngle,
				     branchLen,
				     curvature * 0.6f,
				     rng,
				     out var branchPoints ) )
			{
				AppendPolylineSegments(
					roadScratch,
					branchPoints,
					secondaryPathWidth,
					SettlementRoadType.Secondary );
			}
		}

		if ( mode == SettlementLayoutMode.LargeSettlement )
			AppendLargeSettlementBranches(
				terrain,
				terrainConfig,
				primaryPoints,
				orientation,
				primaryLength,
				curvature,
				primaryPathWidth,
				secondaryPathWidth,
				alleyPathWidth,
				plan.TargetBuildingCount,
				rng,
				roadScratch );

		if ( roadScratch.Count == 0 )
			return false;

		layout = new SettlementLayout
		{
			SettlementIndex = settlementIndex,
			Center = plan.Center,
			Identity = plan.Identity,
			Mode = mode,
			TargetBuildingCount = plan.TargetBuildingCount,
			BoundsRadius = plan.RadiusInches
		};
		layout.Roads.AddRange( roadScratch );
		return true;
	}

	static void AppendLargeSettlementBranches(
		Terrain terrain,
		ThornsTerrainConfig terrainConfig,
		List<Vector3> primaryPoints,
		float primaryOrientation,
		float primaryLength,
		float curvature,
		float primaryPathWidth,
		float secondaryPathWidth,
		float alleyPathWidth,
		int targetBuildingCount,
		Random rng,
		List<SettlementRoadSegment> roads )
	{
		var secondaryCount = targetBuildingCount >= 14 ? 2 : 1;
		for ( var s = 0; s < secondaryCount; s++ )
		{
			var branchT = 0.22f + (s + 1) / (float)(secondaryCount + 1) * 0.55f + rng.NextSingle() * 0.08f - 0.04f;
			branchT = Math.Clamp( branchT, 0.15f, 0.85f );
			var branchOrigin = SamplePolylineAt( primaryPoints, branchT );
			var side = s == 0 ? 1f : -1f;
			var branchAngle = primaryOrientation + side * (1.35f + rng.NextSingle() * 0.5f);
			var branchLen = primaryLength * (0.45f + rng.NextSingle() * 0.35f);
			if ( !TrySamplePolylineRoad(
				     terrain,
				     terrainConfig,
				     branchOrigin,
				     branchAngle,
				     branchLen,
				     curvature * 0.75f,
				     rng,
				     out var branchPoints ) )
				continue;

			AppendPolylineSegments( roads, branchPoints, secondaryPathWidth, SettlementRoadType.Secondary );

			if ( roads.Count >= 6 || branchPoints.Count < 3 )
				continue;

			var connectorT = 0.55f + rng.NextSingle() * 0.25f;
			var connectorStart = SamplePolylineAt( branchPoints, connectorT );
			var connectorAngle = branchAngle + side * (0.85f + rng.NextSingle() * 0.35f);
			var connectorLen = branchLen * 0.35f;
			if ( TrySamplePolylineRoad(
				     terrain,
				     terrainConfig,
				     connectorStart,
				     connectorAngle,
				     connectorLen,
				     curvature * 0.4f,
				     rng,
				     out var connectorPoints ) )
			{
				AppendPolylineSegments(
					roads,
					connectorPoints,
					alleyPathWidth,
					rng.NextSingle() < 0.5f ? SettlementRoadType.Alley : SettlementRoadType.Connector );
			}
		}

		if ( roads.Count < 6 && targetBuildingCount >= 12 && rng.NextSingle() < 0.65f )
		{
			var alleyT = 0.4f + rng.NextSingle() * 0.2f;
			var alleyStart = SamplePolylineAt( primaryPoints, alleyT );
			var alleyAngle = primaryOrientation + (rng.NextSingle() < 0.5f ? 1.57f : -1.57f);
			var alleyLen = primaryLength * 0.28f;
			if ( TrySamplePolylineRoad(
				     terrain,
				     terrainConfig,
				     alleyStart,
				     alleyAngle,
				     alleyLen,
				     curvature * 0.35f,
				     rng,
				     out var alleyPoints ) )
			{
				AppendPolylineSegments( roads, alleyPoints, alleyPathWidth, SettlementRoadType.Alley );
			}
		}

		_ = primaryPathWidth;
	}

	static float ResolvePrimaryLength( int buildingCount, SettlementLayoutMode mode, Random rng )
	{
		if ( mode == SettlementLayoutMode.SmallTown )
		{
			var scale = MathF.Sqrt( Math.Clamp( buildingCount, 4, 7 ) / 5f );
			return (500f + rng.NextSingle() * 700f) * scale;
		}

		var t = Math.Clamp( (buildingCount - 8) / 12f, 0f, 1f );
		return (1000f + t * 2000f) * (0.85f + rng.NextSingle() * 0.3f);
	}

	static void AppendPolylineSegments(
		List<SettlementRoadSegment> roads,
		List<Vector3> points,
		float width,
		SettlementRoadType type )
	{
		for ( var i = 1; i < points.Count; i++ )
		{
			var a = points[i - 1];
			var b = points[i];
			if ( (b - a).LengthSquared < 64f )
				continue;

			roads.Add( new SettlementRoadSegment( a, b, width, type ) );
		}
	}

	static Vector3 SamplePolylineAt( List<Vector3> points, float t )
	{
		if ( points is null || points.Count == 0 )
			return default;

		if ( points.Count == 1 )
			return points[0];

		t = Math.Clamp( t, 0f, 1f );
		var total = 0f;
		for ( var i = 1; i < points.Count; i++ )
			total += (points[i] - points[i - 1]).Length;

		if ( total <= 1f )
			return points[0];

		var target = total * t;
		var walked = 0f;
		for ( var i = 1; i < points.Count; i++ )
		{
			var segLen = (points[i] - points[i - 1]).Length;
			if ( walked + segLen >= target )
			{
				var u = (target - walked) / MathF.Max( segLen, 1f );
				return Vector3.Lerp( points[i - 1], points[i], u );
			}

			walked += segLen;
		}

		return points[^1];
	}

	static bool TrySamplePolylineRoad(
		Terrain terrain,
		ThornsTerrainConfig terrainConfig,
		Vector3 origin,
		float orientationRad,
		float length,
		float curvature,
		Random rng,
		out List<Vector3> points )
	{
		points = new List<Vector3>( MaxPolylineSamples );
		var half = length * 0.5f;
		var dir = new Vector3( MathF.Cos( orientationRad ), MathF.Sin( orientationRad ), 0f );
		var perp = new Vector3( -dir.y, dir.x, 0f );
		var samples = Math.Clamp( (int)(length / 180f) + 3, 5, MaxPolylineSamples );
		var valid = 0;

		for ( var i = 0; i < samples; i++ )
		{
			var t = i / (float)(samples - 1);
			var along = -half + t * length;
			var wave = MathF.Sin( t * MathF.PI ) * curvature * length;
			var jitter = (rng.NextSingle() - 0.5f) * 40f;
			var probe = origin + dir * along + perp * (wave + jitter);
			if ( !TrySampleRoadPoint( terrain, terrainConfig, probe, out var ground ) )
				continue;

			if ( points.Count > 0 && (ground - points[^1]).LengthSquared < 36f )
				continue;

			points.Add( ground );
			valid++;
		}

		return valid >= 3 && points.Count >= 3;
	}

	static bool TrySampleRoadPoint(
		Terrain terrain,
		ThornsTerrainConfig terrainConfig,
		Vector3 probe,
		out Vector3 ground )
	{
		ground = default;
		if ( !ThornsTerrainSurface.TrySnapToTerrain( terrain, probe, out ground ) )
			return false;

		if ( !ThornsProcBuildingTerrainUtil.IsWithinLowlandElevation( terrain, terrainConfig, ground.z ) )
			return false;

		return true;
	}
}

/// <summary>Places frontage lots along an existing street graph.</summary>
public static class SettlementFrontageLotGenerator
{
	static readonly float[] SetbackRetries = { 0f, 40f, 80f, -40f };

	public static void Generate(
		SettlementLayout layout,
		SettlementSitePlan plan,
		int worldSeed,
		Random rng,
		List<SettlementFrontageLot> lotScratch )
	{
		lotScratch.Clear();
		if ( layout?.Roads is null || layout.Roads.Count == 0 )
			return;

		var def = ThornsPoiIdentityCatalog.Get( plan.Identity );
		var spacing = MathF.Max( 120f, def.MinLotSpacingInches );
		var padHalf = ThornsBuildingModule.ProcTownScatterExclusionHalfExtent;
		var targetLots = plan.TargetBuildingCount * 3;

		for ( var roadIndex = 0; roadIndex < layout.Roads.Count; roadIndex++ )
		{
			var road = layout.Roads[roadIndex];
			var segLen = (road.End - road.Start).Length;
			if ( segLen < spacing * 0.5f )
				continue;

			var dir = road.Direction;
			var perp = new Vector3( -dir.y, dir.x, 0f );
			var steps = Math.Clamp( (int)(segLen / spacing), 1, 24 );
			var stepDist = segLen / (steps + 1);

			for ( var step = 1; step <= steps; step++ )
			{
				if ( lotScratch.Count >= targetLots )
					return;

				var along = step * stepDist;
				var centerLine = road.Start + dir * along;
				PlaceSideLot( lotScratch, centerLine, perp, roadIndex, road, plan, spacing, padHalf, worldSeed, rng, side: 1 );
				PlaceSideLot( lotScratch, centerLine, perp, roadIndex, road, plan, spacing, padHalf, worldSeed, rng, side: -1 );
			}
		}
	}

	static void PlaceSideLot(
		List<SettlementFrontageLot> lots,
		Vector3 centerLine,
		Vector3 perp,
		int roadIndex,
		SettlementRoadSegment road,
		SettlementSitePlan plan,
		float spacing,
		float padHalf,
		int worldSeed,
		Random rng,
		int side )
	{
		var def = ThornsPoiIdentityCatalog.Get( plan.Identity );
		var baseSetback = road.Width * 0.5f + padHalf + 24f;
		var slotSalt = HashCode.Combine( worldSeed, plan.Identity, roadIndex, centerLine.x, centerLine.y, side );

		for ( var retry = 0; retry < SetbackRetries.Length; retry++ )
		{
			var setback = baseSetback + SetbackRetries[retry];
			var pos = centerLine + perp * side * setback;
			pos.z = centerLine.z;

			var faceRoad = side > 0 ? -perp : perp;
			var yaw = MathF.Atan2( faceRoad.y, faceRoad.x ) * 180f / MathF.PI;
			yaw = MathF.Round( yaw / 90f ) * 90f;

			if ( IsTooNearExisting( lots, pos, spacing * 0.85f ) )
				continue;

			lots.Add( new SettlementFrontageLot
			{
				Position = pos,
				Rotation = Rotation.FromYaw( yaw ),
				RoadIndex = roadIndex,
				FrontageWidth = spacing,
				Identity = plan.Identity,
				RoadType = road.Type
			} );
			return;
		}

		_ = rng;
		_ = slotSalt;
		_ = def;
	}

	static bool IsTooNearExisting( List<SettlementFrontageLot> lots, Vector3 p, float minSpacing )
	{
		var minSqr = minSpacing * minSpacing;
		for ( var i = 0; i < lots.Count; i++ )
		{
			var d = lots[i].Position - p;
			if ( new Vector2( d.x, d.y ).LengthSquared < minSqr )
				return true;
		}

		return false;
	}
}
