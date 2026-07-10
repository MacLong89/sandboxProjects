namespace Terraingen.TerrainGen;

using System.Runtime.CompilerServices;

/// <summary>
/// Grass (slopes) → thin dirt at treeline → stone → snow on gentle peaks (cliffs stay rock).
/// </summary>
public static class TerrainMaterialPainter
{
	static readonly ConditionalWeakTable<TerrainStorage, bool[]> DominantGrassMasks = new();
	static readonly ConditionalWeakTable<TerrainStorage, bool[]> GrassClutterAllowedMasks = new();
	static readonly ConditionalWeakTable<TerrainStorage, byte[]> DominantMaterialIndexMaps = new();

	public const byte MaterialGrass = 0;
	public const byte MaterialDirt = 1;
	public const byte MaterialRock = 2;
	public const byte MaterialSnow = 3;

	/// <summary>Grass clutter only on the grass terrain layer; blocked on dirt, rock, snow, and underwater.</summary>
	public static bool IsGrassClutterMaterial( byte dominantMaterial ) =>
		dominantMaterial == MaterialGrass;
	public static void InitializeDefaultControlMap( TerrainStorage storage )
	{
		if ( storage.ControlMap is null || storage.ControlMap.Length == 0 )
			return;

		var defaultMat = new CompactTerrainMaterial( 0, 0, 0, false );
		for ( int i = 0; i < storage.ControlMap.Length; i++ )
			storage.ControlMap[i] = defaultMat.Packed;
	}

	public static void PaintControlMap( TerrainStorage storage, HeightmapField field, ThornsTerrainConfig config )
	{
		var maxIndex = (byte)Math.Max( 0, storage.Materials.Count - 1 );
		var hasSnow = maxIndex >= 3;
		var hasDirt = maxIndex >= 1;
		var hasRock = maxIndex >= 2;

		if ( !hasSnow )
			Log.Warning( "[Thorns Terrain] Snow material missing (need 4 layers). Peaks will show rock only." );

		TerrainAnalysis.ComputeSlopeAndCurvature( field, out var slope, out _ );

		var sea = config.SeaLevelNormalized + 0.012f;
		var maxHeight = field.Heights.Max();
		var elevSpan = Math.Max( 0.001f, maxHeight - sea );

		var cliffSlope = TerrainAnalysis.Percentile( slope, 0.9f );
		var grassSlopeMax = config.GrassMaxSlope;

		var rockLine = sea + elevSpan * config.RockLowerRangeFraction;
		var grassLine = sea + elevSpan * config.GrassUpperRangeFraction;
		var dirtStart = grassLine - elevSpan * config.DirtBandRangeFraction;

		var snowLine = hasSnow ? sea + elevSpan * (1f - config.SnowUpperRangeFraction) : 1.1f;
		var snowBand = Math.Max( 0.001f, maxHeight - snowLine );

		if ( grassLine < rockLine + elevSpan * 0.04f )
			grassLine = rockLine + elevSpan * 0.04f;

		const byte grass = 0;
		const byte dirt = 1;
		const byte rock = 2;
		const byte snow = 3;

		var snowCount = 0;
		var grassCount = 0;
		var dominantGrassMask = new bool[field.Heights.Length];
		var grassClutterAllowedMask = new bool[field.Heights.Length];
		var dominantMaterialIndex = new byte[field.Heights.Length];

		for ( int i = 0; i < field.Heights.Length; i++ )
		{
			var h = field.Heights[i];
			var s = slope[i];

			byte baseMat = grass;
			byte overlayMat = grass;
			byte blend = 0;

			var aboveSea = h > sea;
			var isCliff = aboveSea && hasRock && s >= cliffSlope;
			var steep = s > grassSlopeMax;
			var isSteep = aboveSea && (steep || isCliff);
			var onPeak = hasSnow && h >= snowLine;
			var onGrassSlope = aboveSea && !steep && !isCliff && h < grassLine;

			if ( !aboveSea )
			{
				baseMat = grass;
				overlayMat = grass;
			}
			else if ( onPeak )
			{
				if ( isCliff || steep )
				{
					baseMat = rock;
					overlayMat = rock;
				}
				else
				{
					var snowT = Math.Clamp( (h - snowLine) / snowBand, 0f, 1f );
					baseMat = snow;
					overlayMat = snow;
					blend = 0;

					if ( snowT < 0.35f && hasRock )
					{
						overlayMat = rock;
						blend = (byte)Math.Clamp( (int)((0.35f - snowT) / 0.35f * 90f), 0, 90 );
					}

					snowCount++;
				}
			}
			else if ( isSteep && hasRock )
			{
				baseMat = rock;
				overlayMat = rock;
			}
			else if ( onGrassSlope )
			{
				baseMat = grass;
				overlayMat = grass;
				grassCount++;

				if ( hasDirt && h >= dirtStart )
				{
					overlayMat = dirt;
					var band = Math.Max( 0.001f, grassLine - dirtStart );
					blend = (byte)Math.Clamp( (int)((h - dirtStart) / band * 100f), 0, 100 );
				}
			}
			else if ( hasRock && h >= rockLine )
			{
				baseMat = rock;
				overlayMat = rock;
			}
			else
			{
				baseMat = grass;
				overlayMat = grass;
				grassCount++;
			}

			baseMat = (byte)Math.Min( baseMat, maxIndex );
			overlayMat = (byte)Math.Min( overlayMat, maxIndex );

			var dominantMat = blend >= 128 ? overlayMat : baseMat;
			dominantGrassMask[i] = dominantMat == grass;
			grassClutterAllowedMask[i] = aboveSea && IsGrassClutterMaterial( dominantMat );
			dominantMaterialIndex[i] = dominantMat;
			storage.ControlMap[i] = new CompactTerrainMaterial( baseMat, overlayMat, blend, false ).Packed;
		}

		DominantGrassMasks.Remove( storage );
		DominantGrassMasks.Add( storage, dominantGrassMask );
		GrassClutterAllowedMasks.Remove( storage );
		GrassClutterAllowedMasks.Add( storage, grassClutterAllowedMask );
		DominantMaterialIndexMaps.Remove( storage );
		DominantMaterialIndexMaps.Add( storage, dominantMaterialIndex );

		if ( hasSnow )
		{
			var snowPct = 100f * snowCount / field.Heights.Length;
			var grassPct = 100f * grassCount / field.Heights.Length;
			Log.Info( $"[Thorns Terrain] Materials: grass<{grassLine:F3} dirt band≥{dirtStart:F3} rock≥{rockLine:F3} snow≥{snowLine:F3} (top {config.SnowUpperRangeFraction * 100f:F0}% above sea) | grass {grassPct:F0}% snow {snowPct:F1}%" );
		}
	}

	public static int PaintDirtPathSegments(
		Terrain terrain,
		HeightmapField field,
		ThornsTerrainConfig config,
		IEnumerable<(Vector3 Start, Vector3 End, float Width)> segments )
	{
		if ( !terrain.IsValid() || terrain.Storage is null || field is null || segments is null )
			return 0;

		var storage = terrain.Storage;
		if ( storage.ControlMap is null || storage.ControlMap.Length == 0 || storage.Materials.Count <= MaterialDirt )
			return 0;

		var origin = terrain.GameObject.WorldPosition;
		var terrainSize = terrain.TerrainSize;
		if ( terrainSize <= 1f )
			return 0;

		var sea = (config?.SeaLevelNormalized ?? 0.06f) + 0.006f;
		var dirt = new CompactTerrainMaterial( MaterialDirt, MaterialDirt, 0, false ).Packed;
		var painted = 0;

		DominantGrassMasks.TryGetValue( storage, out var dominantGrassMask );
		GrassClutterAllowedMasks.TryGetValue( storage, out var grassClutterAllowedMask );
		DominantMaterialIndexMaps.TryGetValue( storage, out var dominantMaterialIndex );

		foreach ( var segment in segments )
		{
			var width = MathF.Max( 24f, segment.Width );
			var radius = width * 0.5f;
			var start = new Vector2( segment.Start.x, segment.Start.y );
			var end = new Vector2( segment.End.x, segment.End.y );
			var minX = MathF.Min( start.x, end.x ) - radius;
			var maxX = MathF.Max( start.x, end.x ) + radius;
			var minY = MathF.Min( start.y, end.y ) - radius;
			var maxY = MathF.Max( start.y, end.y ) + radius;

			var ix0 = WorldToFieldX( minX, origin.x, terrainSize, field.Width );
			var ix1 = WorldToFieldX( maxX, origin.x, terrainSize, field.Width );
			var iy0 = WorldToFieldY( minY, origin.y, terrainSize, field.Height );
			var iy1 = WorldToFieldY( maxY, origin.y, terrainSize, field.Height );
			var radiusSq = radius * radius;

			for ( var y = iy0; y <= iy1; y++ )
			{
				var worldY = origin.y + (y / MathF.Max( 1f, field.Height - 1f )) * terrainSize;
				for ( var x = ix0; x <= ix1; x++ )
				{
					var index = field.Index( x, y );
					if ( index < 0 || index >= storage.ControlMap.Length || field.Heights[index] <= sea )
						continue;

					var worldX = origin.x + (x / MathF.Max( 1f, field.Width - 1f )) * terrainSize;
					if ( DistanceToSegmentSquared( new Vector2( worldX, worldY ), start, end ) > radiusSq )
						continue;

					if ( storage.ControlMap[index] == dirt )
						continue;

					storage.ControlMap[index] = dirt;
					if ( index < field.Heights.Length )
					{
						if ( dominantGrassMask is not null )
							dominantGrassMask[index] = false;
						if ( grassClutterAllowedMask is not null )
							grassClutterAllowedMask[index] = false;
						if ( dominantMaterialIndex is not null )
							dominantMaterialIndex[index] = MaterialDirt;
					}
					painted++;
				}
			}
		}

		return painted;
	}

	public static int PaintDirtPathRoutes(
		Terrain terrain,
		HeightmapField field,
		ThornsTerrainConfig config,
		IEnumerable<(Vector3 Start, Vector3 End, float Width)> segments,
		int routeGridSize,
		float elevationCost,
		float highElevationCost,
		float searchPaddingInches )
	{
		if ( segments is null )
			return 0;

		var routedSegments = new List<(Vector3 Start, Vector3 End, float Width)>();
		var routing = CreateRouteContext( terrain, field, config, routeGridSize );
		if ( routing is null )
			return PaintDirtPathSegments( terrain, field, config, segments );

		foreach ( var segment in segments )
		{
			var route = FindLowElevationRoute(
				routing,
				segment.Start,
				segment.End,
				elevationCost,
				highElevationCost,
				searchPaddingInches );

			if ( route.Count < 2 )
			{
				routedSegments.Add( segment );
				continue;
			}

			for ( var i = 1; i < route.Count; i++ )
				routedSegments.Add( (route[i - 1], route[i], segment.Width) );
		}

		return PaintDirtPathSegments( terrain, field, config, routedSegments );
	}

	static int WorldToFieldX( float worldX, float originX, float terrainSize, int width )
	{
		var u = ((worldX - originX) / terrainSize).Clamp( 0f, 1f );
		return Math.Clamp( (int)MathF.Round( u * (width - 1) ), 0, width - 1 );
	}

	static int WorldToFieldY( float worldY, float originY, float terrainSize, int height )
	{
		var v = ((worldY - originY) / terrainSize).Clamp( 0f, 1f );
		return Math.Clamp( (int)MathF.Round( v * (height - 1) ), 0, height - 1 );
	}

	static float DistanceToSegmentSquared( Vector2 point, Vector2 start, Vector2 end )
	{
		var ab = end - start;
		var denom = MathF.Max( 0.001f, ab.LengthSquared );
		var ap = point - start;
		var t = (ap.x * ab.x + ap.y * ab.y) / denom;
		t = t.Clamp( 0f, 1f );
		var closest = start + ab * t;
		return (point - closest).LengthSquared;
	}

	static RouteContext CreateRouteContext( Terrain terrain, HeightmapField field, ThornsTerrainConfig config, int routeGridSize )
	{
		if ( !terrain.IsValid() || field is null )
			return null;

		var size = Math.Clamp( routeGridSize, 32, 192 );
		var origin = terrain.GameObject.WorldPosition;
		var terrainSize = terrain.TerrainSize;
		if ( terrainSize <= 1f )
			return null;

		var heights = new float[size * size];
		for ( var y = 0; y < size; y++ )
		{
			var v = y / MathF.Max( 1f, size - 1f );
			for ( var x = 0; x < size; x++ )
			{
				var u = x / MathF.Max( 1f, size - 1f );
				heights[x + y * size] = field.SampleBilinear( u, v );
			}
		}

		return new RouteContext(
			origin,
			terrainSize,
			size,
			heights,
			(config?.SeaLevelNormalized ?? 0.06f) + 0.006f );
	}

	static List<Vector3> FindLowElevationRoute(
		RouteContext ctx,
		Vector3 startWorld,
		Vector3 endWorld,
		float elevationCost,
		float highElevationCost,
		float searchPaddingInches )
	{
		var start = ctx.WorldToCell( startWorld );
		var goal = ctx.WorldToCell( endWorld );
		if ( start == goal )
			return new List<Vector3> { startWorld, endWorld };

		var paddingCells = Math.Clamp(
			(int)MathF.Ceiling( MathF.Max( 0f, searchPaddingInches ) / ctx.CellWorldSize ),
			4,
			ctx.Size );
		var minX = Math.Clamp( Math.Min( start.X, goal.X ) - paddingCells, 0, ctx.Size - 1 );
		var maxX = Math.Clamp( Math.Max( start.X, goal.X ) + paddingCells, 0, ctx.Size - 1 );
		var minY = Math.Clamp( Math.Min( start.Y, goal.Y ) - paddingCells, 0, ctx.Size - 1 );
		var maxY = Math.Clamp( Math.Max( start.Y, goal.Y ) + paddingCells, 0, ctx.Size - 1 );
		var count = ctx.Size * ctx.Size;
		var cost = new float[count];
		var cameFrom = new int[count];
		var closed = new bool[count];
		Array.Fill( cost, float.MaxValue );
		Array.Fill( cameFrom, -1 );

		var startIndex = ctx.Index( start.X, start.Y );
		var goalIndex = ctx.Index( goal.X, goal.Y );
		var open = new RoutePriorityQueue();
		cost[startIndex] = 0f;
		open.Enqueue( startIndex, Heuristic( ctx, start.X, start.Y, goal.X, goal.Y ) );

		while ( open.Count > 0 )
		{
			var current = open.Dequeue();
			if ( closed[current] )
				continue;
			if ( current == goalIndex )
				return ReconstructRoute( ctx, cameFrom, current, startWorld, endWorld );

			closed[current] = true;
			var cx = current % ctx.Size;
			var cy = current / ctx.Size;

			for ( var oy = -1; oy <= 1; oy++ )
			{
				for ( var ox = -1; ox <= 1; ox++ )
				{
					if ( ox == 0 && oy == 0 )
						continue;

					var nx = cx + ox;
					var ny = cy + oy;
					if ( nx < minX || nx > maxX || ny < minY || ny > maxY )
						continue;

					var next = ctx.Index( nx, ny );
					if ( closed[next] )
						continue;

					var step = ox != 0 && oy != 0 ? 1.4142135f : 1f;
					var moveCost = StepRouteCost( ctx, current, next, step, elevationCost, highElevationCost );
					var nextCost = cost[current] + moveCost;
					if ( nextCost >= cost[next] )
						continue;

					cost[next] = nextCost;
					cameFrom[next] = current;
					open.Enqueue( next, nextCost + Heuristic( ctx, nx, ny, goal.X, goal.Y ) );
				}
			}
		}

		return new List<Vector3>();
	}

	static float StepRouteCost(
		RouteContext ctx,
		int current,
		int next,
		float step,
		float elevationCost,
		float highElevationCost )
	{
		var h0 = ctx.Heights[current];
		var h1 = ctx.Heights[next];
		var climb = MathF.Abs( h1 - h0 );
		var high = MathF.Max( 0f, h1 - (ctx.SeaLevel + 0.18f) );
		var water = h1 <= ctx.SeaLevel ? 10000f : 0f;
		return step
		       * (1f
		          + climb * MathF.Max( 0f, elevationCost )
		          + high * MathF.Max( 0f, highElevationCost )
		          + water);
	}

	static List<Vector3> ReconstructRoute( RouteContext ctx, int[] cameFrom, int current, Vector3 startWorld, Vector3 endWorld )
	{
		var route = new List<Vector3>();
		while ( current >= 0 )
		{
			route.Add( ctx.CellToWorld( current ) );
			current = cameFrom[current];
		}

		route.Reverse();
		if ( route.Count == 0 )
			return route;

		route[0] = startWorld;
		route[^1] = endWorld;
		return SimplifyRoute( route );
	}

	static List<Vector3> SimplifyRoute( List<Vector3> route )
	{
		if ( route.Count <= 2 )
			return route;

		var simplified = new List<Vector3> { route[0] };
		var lastDir = Vector2.Zero;
		for ( var i = 1; i < route.Count; i++ )
		{
			var delta = new Vector2( route[i].x - route[i - 1].x, route[i].y - route[i - 1].y );
			var dir = delta.LengthSquared > 0.001f ? delta.Normal : Vector2.Zero;
			if ( i == 1 )
			{
				lastDir = dir;
				continue;
			}

			var dot = lastDir.x * dir.x + lastDir.y * dir.y;
			if ( dot < 0.985f )
			{
				simplified.Add( route[i - 1] );
				lastDir = dir;
			}
		}

		simplified.Add( route[^1] );
		return simplified;
	}

	static float Heuristic( RouteContext ctx, int x, int y, int gx, int gy )
	{
		var dx = x - gx;
		var dy = y - gy;
		return MathF.Sqrt( dx * dx + dy * dy );
	}

	sealed class RouteContext
	{
		public readonly Vector3 Origin;
		public readonly float TerrainSize;
		public readonly int Size;
		public readonly float[] Heights;
		public readonly float SeaLevel;
		public readonly float CellWorldSize;

		public RouteContext( Vector3 origin, float terrainSize, int size, float[] heights, float seaLevel )
		{
			Origin = origin;
			TerrainSize = terrainSize;
			Size = size;
			Heights = heights;
			SeaLevel = seaLevel;
			CellWorldSize = terrainSize / MathF.Max( 1f, size - 1f );
		}

		public int Index( int x, int y ) => x + y * Size;

		public RouteCell WorldToCell( Vector3 world )
		{
			var u = ((world.x - Origin.x) / TerrainSize).Clamp( 0f, 1f );
			var v = ((world.y - Origin.y) / TerrainSize).Clamp( 0f, 1f );
			return new RouteCell(
				Math.Clamp( (int)MathF.Round( u * (Size - 1) ), 0, Size - 1 ),
				Math.Clamp( (int)MathF.Round( v * (Size - 1) ), 0, Size - 1 ) );
		}

		public Vector3 CellToWorld( int index )
		{
			var x = index % Size;
			var y = index / Size;
			return new Vector3(
				Origin.x + (x / MathF.Max( 1f, Size - 1f )) * TerrainSize,
				Origin.y + (y / MathF.Max( 1f, Size - 1f )) * TerrainSize,
				0f );
		}
	}

	readonly struct RouteCell
	{
		public readonly int X;
		public readonly int Y;

		public RouteCell( int x, int y )
		{
			X = x;
			Y = y;
		}

		public override bool Equals( object obj ) => obj is RouteCell other && X == other.X && Y == other.Y;
		public override int GetHashCode() => HashCode.Combine( X, Y );
		public static bool operator ==( RouteCell a, RouteCell b ) => a.X == b.X && a.Y == b.Y;
		public static bool operator !=( RouteCell a, RouteCell b ) => !(a == b);
	}

	sealed class RoutePriorityQueue
	{
		readonly List<(int Node, float Priority)> _items = new();
		public int Count => _items.Count;

		public void Enqueue( int node, float priority )
		{
			_items.Add( (node, priority) );
			var i = _items.Count - 1;
			while ( i > 0 )
			{
				var parent = (i - 1) / 2;
				if ( _items[parent].Priority <= priority )
					break;

				_items[i] = _items[parent];
				i = parent;
			}

			_items[i] = (node, priority);
		}

		public int Dequeue()
		{
			var result = _items[0].Node;
			var last = _items[^1];
			_items.RemoveAt( _items.Count - 1 );
			if ( _items.Count == 0 )
				return result;

			var i = 0;
			while ( true )
			{
				var left = i * 2 + 1;
				if ( left >= _items.Count )
					break;

				var right = left + 1;
				var child = right < _items.Count && _items[right].Priority < _items[left].Priority ? right : left;
				if ( _items[child].Priority >= last.Priority )
					break;

				_items[i] = _items[child];
				i = child;
			}

			_items[i] = last;
			return result;
		}
	}

	public static bool TryGetDominantGrassMask( TerrainStorage storage, out bool[] mask )
	{
		mask = null;
		return storage is not null && DominantGrassMasks.TryGetValue( storage, out mask );
	}

	public static bool TryGetGrassClutterAllowedMask( TerrainStorage storage, out bool[] mask )
	{
		mask = null;
		return storage is not null && GrassClutterAllowedMasks.TryGetValue( storage, out mask );
	}

	public static bool TryGetDominantMaterialIndexMap( TerrainStorage storage, out byte[] materialIndex )
	{
		materialIndex = null;
		return storage is not null && DominantMaterialIndexMaps.TryGetValue( storage, out materialIndex );
	}

	public static bool IsMineralStoneMaterial( byte dominantMaterial ) =>
		dominantMaterial is MaterialRock or MaterialDirt
		|| dominantMaterial == MaterialGrass;

	public static bool IsMineralOreMaterial( byte dominantMaterial ) =>
		dominantMaterial is MaterialRock or MaterialSnow;
}
