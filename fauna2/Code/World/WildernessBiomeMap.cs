namespace Fauna2;

/// <summary>
/// Fixed wilderness layout — eight solid macro regions; borders warp via domain noise only.
/// </summary>
public static class WildernessBiomeMap
{
	private const int WorldLayoutSeed = 31841;
	private const float CoastlineTileWidth = 3.5f;
	private const float CoastlineDeepWaterTiles = 0.85f;
	/// <summary>How far the shoreline wiggles inland/outward — matches biome border looseness.</summary>
	private const float CoastlineEdgeWobbleTiles = 2.25f;

	private struct MacroSeed
	{
		public Vector2 Position;
		public Biome Biome;
	}

	private static MacroSeed[] _macroSeeds;

	public static Biome BiomeForPlot( int px, int py, Biome starterBiome ) =>
		BiomeAtWorld( PlotSystem.PlotCenter( px, py ), starterBiome );

	public static Biome BiomeAtWorld( Vector3 position, Biome starterBiome )
	{
		var plotSize = GameConstants.PlotSize;
		var nx = position.x / plotSize;
		var ny = position.y / plotSize;
		return ComputeBiomeWeights( nx, ny ).Primary;
	}

	public static bool IsWildlifeHotspot( int px, int py )
	{
		var outer = GameConstants.PlotGridRadius + 1;
		return Math.Abs( px ) >= outer || Math.Abs( py ) >= outer;
	}

	/// <summary>Unowned land — solid voronoi regions; border squiggle comes from coordinate warp only.</summary>
	public static Texture GroundTileAtWorld( Vector3 position, Biome starterBiome )
	{
		var biome = BiomeAtWorld( position, starterBiome );
		return GroundTileForBiomeAtPoint( position, biome, starterBiome );
	}

	/// <summary>Grid lookup — same as <see cref="GroundTileAtWorld"/>.</summary>
	public static Texture GroundTileAtWorldGrid( Vector3 position, Biome starterBiome, bool multiSample = false ) =>
		GroundTileAtWorld( position, starterBiome );

	private static Texture GroundTileAtPoint( Vector3 position, Biome starterBiome )
	{
		var biome = BiomeAtWorld( position, starterBiome );
		return GroundTileForBiomeAtPoint( position, biome, starterBiome );
	}

	private static float BiomeEdgeBlendWidth() => GameConstants.WorldHalfExtent * 0.11f;

	private static (float X, float Y) BiomeBoundaryWarp( float x, float y )
	{
		var half = GameConstants.WorldHalfExtent;
		var tile = GameConstants.GroundRenderTileSize;
		const int seed = WorldLayoutSeed + 88050;

		var wx = (SmoothNoise( x, y, half * 0.28f, seed ) - 0.5f) * tile * 3.4f;
		var wy = (SmoothNoise( x + 900f, y - 700f, half * 0.16f, seed + 1 ) - 0.5f) * tile * 2.2f;
		var fx = (SmoothNoise( x - 400f, y + 1100f, tile * 8f, seed + 2 ) - 0.5f) * tile * 1.1f;
		var fy = (SmoothNoise( x + 1200f, y + 300f, tile * 8f, seed + 3 ) - 0.5f) * tile * 1.1f;

		return (x + wx + fx, y + wy + fy);
	}

	/// <summary>Biome blend overlays disabled — one texture per tile.</summary>
	public static bool TryGetGroundBlend(
		Vector2 center,
		float tileSize,
		Biome starterBiome,
		out Texture secondaryTexture,
		out float alpha )
	{
		secondaryTexture = null;
		alpha = 0f;
		return false;
	}

	private static Texture GroundTileForBiomeAtPoint( Vector3 position, Biome biome, Biome starterBiome )
	{
		var edgeWater = WorldEdgeWaterStrength( position.x, position.y );
		var dither = Hash01( position.x + 17f, position.y - 31f, WorldLayoutSeed + 23376 + 93 );

		if ( ShouldPaintShoreWater( edgeWater, dither ) )
			return PixelArt.TileWater;

		return GroundTile( biome );
	}

	/// <summary>Chunky land/water fringe — solid ocean near the rim, ±~2 tile dither inland.</summary>
	private static bool ShouldPaintShoreWater( float edgeWater, float dither )
	{
		if ( edgeWater >= 0.72f )
			return true;

		if ( edgeWater <= 0.28f )
			return false;

		return dither < edgeWater * 0.9f + 0.05f;
	}

	/// <summary>True when the ground renderer would draw open water here.</summary>
	public static bool IsWaterAt( Vector3 position, Biome starterBiome ) =>
		SampleGround( position, starterBiome ).Water;

	/// <summary>Terrestrial props and walkers should not spawn on open water.</summary>
	public static bool IsDryLandAt( Vector3 position, Biome starterBiome ) =>
		!SampleGround( position, starterBiome ).Water;

	private static GroundSample SampleGround( Vector3 position, Biome starterBiome )
	{
		var tile = GameConstants.GroundRenderTileSize;
		var seed = WorldLayoutSeed + 23376;
		var squiggle = SquiggleSampleOffset( position.x, position.y );
		var jitterX = (Hash01( position.x, position.y, seed + 91 ) - 0.5f) * tile * 0.42f;
		var jitterY = (Hash01( position.y, position.x, seed + 92 ) - 0.5f) * tile * 0.42f;
		var sampleX = position.x + squiggle.x + jitterX;
		var sampleY = position.y + squiggle.y + jitterY;

		var biome = BiomeAtWorld( new Vector3( sampleX, sampleY, 0f ), starterBiome );
		var edgeWater = WorldEdgeWaterStrength( position.x, position.y );
		var dither = Hash01( position.x + 17f, position.y - 31f, seed + 93 );

		var shoreWater = ShouldPaintShoreWater( edgeWater, dither );

		return new GroundSample
		{
			Biome = biome,
			Water = shoreWater,
			Snow = false,
		};
	}

	/// <summary>Keep wilderness tiles grid-aligned — wobble breaks solid biome panels at edges.</summary>
	public static Vector2 GroundTilePlacementOffset( float x, float y ) => Vector2.Zero;

	private static Vector2 SquiggleSampleOffset( float x, float y )
	{
		var tile = GameConstants.GroundRenderTileSize;
		var plotSize = GameConstants.PlotSize;

		var wideX = SmoothNoise( x, y, plotSize * 0.52f, WorldLayoutSeed + 8801 );
		var wideY = SmoothNoise( x + plotSize * 0.6f, y, plotSize * 0.52f, WorldLayoutSeed + 8802 );
		var fineX = SmoothNoise( x, y, tile * 3.8f, WorldLayoutSeed + 8803 );
		var fineY = SmoothNoise( x - tile, y + tile * 1.4f, tile * 3.8f, WorldLayoutSeed + 8804 );
		var rippleX = SmoothNoise( x + 900f, y - 400f, tile * 1.6f, WorldLayoutSeed + 8805 );
		var rippleY = SmoothNoise( x - 600f, y + 1100f, tile * 1.6f, WorldLayoutSeed + 8806 );

		return new Vector2(
			(wideX - 0.5f) * plotSize * 0.32f + (fineX - 0.5f) * tile * 2.05f + (rippleX - 0.5f) * tile * 0.85f,
			(wideY - 0.5f) * plotSize * 0.32f + (fineY - 0.5f) * tile * 2.05f + (rippleY - 0.5f) * tile * 0.85f );
	}

	private struct GroundSample
	{
		public Biome Biome;
		public bool Water;
		public bool Snow;
	}

	public static float TreeDensity( Biome regionalBiome ) => regionalBiome switch
	{
		Biome.Forest => 1f,
		Biome.Rainforest => 1.15f,
		Biome.Grassland => 0.2f,
		Biome.Desert => 0.05f,
		Biome.Arctic => 0.25f,
		Biome.Swamp => 0.45f,
		Biome.Alpine => 0.55f,
		Biome.Coastal => 0.18f,
		_ => 0.3f,
	};

	public static float TreeDensity( int px, int py, Biome starterBiome ) =>
		TreeDensity( BiomeForPlot( px, py, starterBiome ) );

	public static float TreeDensityAtWorld( Vector3 position, Biome starterBiome )
	{
		var regional = TreeDensity( BiomeAtWorld( position, starterBiome ) );
		var plotSize = GameConstants.PlotSize;
		var fine = SmoothNoise( position.x, position.y, plotSize * 0.22f, WorldLayoutSeed + 44201 );
		return regional * (0.82f + fine * 0.28f);
	}

	public static Texture GroundTile( Biome biome ) => biome switch
	{
		Biome.Rainforest => PixelArt.TileRainforest,
		Biome.Forest => PixelArt.TileForest,
		Biome.Grassland => PixelArt.TileGrass,
		Biome.Desert => PixelArt.TileSand,
		Biome.Arctic => PixelArt.TileSnow,
		Biome.Swamp => PixelArt.TileMud,
		Biome.Alpine => PixelArt.TileRock,
		Biome.Coastal => PixelArt.TileBeach,
		_ => PixelArt.TileWilderness,
	};

	private static bool AllowsInlandWaterAccent( Biome biome ) => biome is Biome.Swamp or Biome.Coastal;

	private static float InlandWaterAccentThreshold( Biome biome ) => biome switch
	{
		Biome.Swamp => 0.78f,
		Biome.Coastal => 0.84f,
		_ => 1f,
	};

	private static bool AllowsSnowOverlay( Biome biome ) => biome is Biome.Arctic or Biome.Alpine;

	private static float SnowOverlayThreshold( Biome biome ) => biome switch
	{
		Biome.Arctic => 0.22f,
		Biome.Alpine => 0.3f,
		Biome.Forest => 0.38f,
		_ => 0.34f,
	};

	public static string RegionLabel( Biome biome ) => biome switch
	{
		Biome.Forest => "Forest wilds",
		Biome.Rainforest => "Rainforest wilds",
		Biome.Grassland => "Grassland wilds",
		Biome.Desert => "Desert wilds",
		Biome.Arctic => "Arctic wilds",
		Biome.Swamp => "Swamp wilds",
		Biome.Alpine => "Alpine wilds",
		Biome.Coastal => "Coastal wilds",
		_ => "Wilderness",
	};

	/// <summary>Top two biome influences at a plot-normalized coordinate — drives soft ground blending.</summary>
	public static BiomeWeights ComputeBiomeWeights( float nx, float ny )
	{
		var plotSize = GameConstants.PlotSize;
		var px = nx * plotSize;
		var py = ny * plotSize;
		var half = GameConstants.WorldHalfExtent;
		var blendWidth = BiomeEdgeBlendWidth();

		var (warpX, warpY) = DomainWarp( nx, ny );
		var x = px + (warpX - nx) * plotSize * 0.24f;
		var y = py + (warpY - ny) * plotSize * 0.24f;
		(x, y) = BiomeBoundaryWarp( x, y );

		var bestDist = float.MaxValue;
		var secondDist = float.MaxValue;
		var primary = Biome.Grassland;
		var secondary = Biome.Forest;

		foreach ( var seed in MacroSeeds() )
		{
			var dxw = x - seed.Position.x;
			var dyw = y - seed.Position.y;
			var edgeWarp = (SmoothNoise( x + seed.Position.x * 0.01f, y - seed.Position.y * 0.01f, half * 0.28f, WorldLayoutSeed + 5020 + (int)(seed.Biome) ) - 0.5f) * half * 0.075f;
			var dist = MathF.Sqrt( dxw * dxw + dyw * dyw ) + edgeWarp;

			if ( dist < bestDist )
			{
				secondDist = bestDist;
				secondary = primary;
				bestDist = dist;
				primary = seed.Biome;
			}
			else if ( dist < secondDist )
			{
				secondDist = dist;
				secondary = seed.Biome;
			}
		}

		var edgeGap = MathF.Max( 0f, secondDist - bestDist );
		var edgeMix = SmoothStep( Math.Clamp( 1f - edgeGap / blendWidth, 0f, 1f ) );
		var primaryWeight = MathF.Max( 0.001f, 1f - edgeMix );
		var secondaryWeight = edgeMix * 0.95f;

		if ( secondary == primary )
			secondaryWeight = 0f;

		return new BiomeWeights( primary, primaryWeight, secondary, secondaryWeight, edgeGap );
	}

	private static readonly Biome[] BiomeLayoutOrder =
	[
		Biome.Rainforest,
		Biome.Forest,
		Biome.Grassland,
		Biome.Desert,
		Biome.Swamp,
		Biome.Arctic,
		Biome.Alpine,
		Biome.Coastal,
	];

	/// <summary>Jittered macro Voronoi — same field as regional biomes.</summary>
	public static BiomeWeights ComputeVisualGroundWeights( float x, float y )
	{
		var plotSize = GameConstants.PlotSize;
		return ComputeBiomeWeights( x / plotSize, y / plotSize );
	}

	private static ReadOnlySpan<MacroSeed> MacroSeeds()
	{
		_macroSeeds ??= BuildMacroSeeds();
		return _macroSeeds;
	}

	/// <summary>Eight macro regions on an offset ring — avoids grid stripes, keeps ~equal area.</summary>
	private static MacroSeed[] BuildMacroSeeds()
	{
		var half = GameConstants.WorldHalfExtent;
		var seeds = new MacroSeed[BiomeLayoutOrder.Length];
		var count = BiomeLayoutOrder.Length;

		for ( var i = 0; i < count; i++ )
		{
			// Half-sector offset keeps boundaries off the world axes (no horizontal/vertical stripes).
			var angle = (i / (float)count) * MathF.PI * 2f + MathF.PI / count;
			var radiusFrac = 0.52f + (i % 2) * 0.11f + Hash01( i, 0, WorldLayoutSeed + 6100 ) * 0.05f;
			var radius = half * radiusFrac;
			var angleJitter = (Hash01( i, 1, WorldLayoutSeed + 6101 ) - 0.5f) * (MathF.PI / count * 0.42f);

			seeds[i] = new MacroSeed
			{
				Position = new Vector2(
					MathF.Cos( angle + angleJitter ) * radius,
					MathF.Sin( angle + angleJitter ) * radius ),
				Biome = BiomeLayoutOrder[i],
			};
		}

		return seeds;
	}

	private static float DirectionalScore(
		float angle,
		float dist,
		float targetAngle,
		float angleWidth,
		float distInner,
		float distOuter,
		float px,
		float py )
	{
		var angular = AngularScore( angle, targetAngle, angleWidth );
		var radial = RadialBand( dist, distInner, (distInner + distOuter) * 0.5f, distOuter );
		var n = SmoothNoise( px + targetAngle * 40f, py - targetAngle * 25f, GameConstants.PlotSize * 1.25f, WorldLayoutSeed + 1400 + (int)(targetAngle * 100f) );
		return angular * radial * (0.68f + n * 0.62f);
	}

	private static float AngularScore( float angle, float target, float width )
	{
		var diff = AngleDelta( angle, target );
		var t = Math.Clamp( 1f - diff / width, 0f, 1f );
		return SmoothStep( t );
	}

	private static float RadialBand( float dist, float innerSoft, float peak, float outerSoft )
	{
		if ( dist <= peak )
		{
			if ( dist <= innerSoft )
				return SmoothStep( dist / MathF.Max( innerSoft, 0.05f ) );
			return 1f;
		}

		var over = dist - peak;
		var span = MathF.Max( outerSoft - peak, 0.08f );
		return SmoothStep( Math.Clamp( 1f - over / span, 0f, 1f ) );
	}

	private static float AngleDelta( float a, float b )
	{
		var diff = a - b;
		while ( diff > MathF.PI ) diff -= MathF.PI * 2f;
		while ( diff < -MathF.PI ) diff += MathF.PI * 2f;
		return MathF.Abs( diff );
	}

	public readonly struct BiomeWeights
	{
		public Biome Primary { get; }
		public float PrimaryWeight { get; }
		public Biome Secondary { get; }
		public float SecondaryWeight { get; }
		/// <summary>Distance between nearest and second-nearest macro seed — small at biome borders.</summary>
		public float EdgeGap { get; }

		public BiomeWeights( Biome primary, float primaryWeight, Biome secondary, float secondaryWeight, float edgeGap )
		{
			Primary = primary;
			PrimaryWeight = primaryWeight;
			Secondary = secondary;
			SecondaryWeight = secondaryWeight;
			EdgeGap = edgeGap;
		}

		public float BlendRatio =>
			SecondaryWeight / MathF.Max( PrimaryWeight + SecondaryWeight, 0.001f );
	}

	private static (float x, float y) DomainWarp( float nx, float ny )
	{
		var plotSize = GameConstants.PlotSize;
		var px = nx * plotSize;
		var py = ny * plotSize;

		var w1x = (SmoothNoise( px, py, plotSize * 3.6f, WorldLayoutSeed + 701 ) - 0.5f) * 1.45f;
		var w1y = (SmoothNoise( py, px + 900f, plotSize * 3.6f, WorldLayoutSeed + 702 ) - 0.5f) * 1.45f;
		var w2x = (SmoothNoise( px + 2100f, py - 1500f, plotSize * 1.15f, WorldLayoutSeed + 703 ) - 0.5f) * 0.82f;
		var w2y = (SmoothNoise( py + 1800f, px - 2200f, plotSize * 1.15f, WorldLayoutSeed + 704 ) - 0.5f) * 0.82f;
		var w3x = EdgeNoise( nx + 1.7f, ny - 2.3f, 5 ) * 0.38f;
		var w3y = EdgeNoise( ny + 2.1f, nx - 1.9f, 6 ) * 0.38f;

		return (
			nx + w1x + w2x + w3x + EdgeNoise( nx, ny, 0 ) * 0.55f,
			ny + w1y + w2y + w3y + EdgeNoise( ny, nx, 1 ) * 0.55f );
	}

	private static float WetnessAt( float x, float y )
	{
		var seed = WorldLayoutSeed + 881;
		var plotSize = GameConstants.PlotSize;
		var nx = x / plotSize;
		var ny = y / plotSize;
		var cell = plotSize * 1.05f;
		var cx = MathF.Floor( x / cell );
		var cy = MathF.Floor( y / cell );
		var wetness = 0f;

		for ( var dy = -1; dy <= 1; dy++ )
		{
			for ( var dx = -1; dx <= 1; dx++ )
			{
				var ix = cx + dx;
				var iy = cy + dy;
				var cellRoll = Hash01( ix, iy, seed );
				if ( cellRoll < 0.82f ) continue;

				var centerX = (ix + 0.5f + (Hash01( ix, iy, seed + 1 ) - 0.5f) * 0.6f) * cell;
				var centerY = (iy + 0.5f + (Hash01( iy, ix, seed + 2 ) - 0.5f) * 0.6f) * cell;
				var radius = cell * (0.36f + Hash01( ix + 3f, iy + 7f, seed + 3 ) * 0.16f );

				var dxw = x - centerX;
				var dyw = y - centerY;
				var dist = MathF.Sqrt( dxw * dxw + dyw * dyw );
				var edgeWarp = (SmoothNoise( x, y, 1600f, seed + 40 ) - 0.5f) * radius * 0.28f;
				var influence = SoftBlobFalloff( dist + edgeWarp, radius );
				wetness = MathF.Max( wetness, influence * (0.58f + cellRoll * 0.42f) );
			}
		}

		wetness += CoastalWetnessBias( nx, ny, x, y );
		wetness += (SmoothNoise( x, y, 1200f, seed + 50 ) - 0.5f) * 0.1f;
		return wetness;
	}

	private static float CoastalWetnessBias( float nx, float ny, float x, float y )
	{
		var plotSize = GameConstants.PlotSize;
		var coastal = WorldEdgeWaterStrength( x, y );
		if ( coastal <= 0.02f )
			return 0f;

		var blob = SmoothNoise( x, y, plotSize * 1.35f, WorldLayoutSeed + 8811 );
		return coastal * (0.16f + blob * 0.12f);
	}

	private static float ColdnessAt( float x, float y )
	{
		var seed = WorldLayoutSeed + 993;
		var plotSize = GameConstants.PlotSize;
		var nx = x / plotSize;
		var ny = y / plotSize;
		var cell = plotSize * 1.15f;
		var cx = MathF.Floor( x / cell );
		var cy = MathF.Floor( y / cell );
		var cold = 0f;

		for ( var dy = -1; dy <= 1; dy++ )
		{
			for ( var dx = -1; dx <= 1; dx++ )
			{
				var ix = cx + dx;
				var iy = cy + dy;
				var cellRoll = Hash01( ix, iy, seed );
				if ( cellRoll < 0.54f ) continue;

				var centerX = (ix + 0.5f + (Hash01( ix, iy, seed + 1 ) - 0.5f) * 0.55f) * cell;
				var centerY = (iy + 0.5f + (Hash01( iy, ix, seed + 2 ) - 0.5f) * 0.55f) * cell;
				var radius = cell * (0.34f + Hash01( ix + 5f, iy + 2f, seed + 3 ) * 0.18f );

				var dxc = x - centerX;
				var dyc = y - centerY;
				var dist = MathF.Sqrt( dxc * dxc + dyc * dyc );
				var edgeWarp = (SmoothNoise( x + 5000f, y - 3000f, 1800f, seed + 40 ) - 0.5f) * radius * 0.16f;
				var influence = SoftBlobFalloff( dist + edgeWarp, radius );
				cold = MathF.Max( cold, influence * (0.55f + cellRoll * 0.45f) );
			}
		}

		cold += ArcticColdBias( nx, ny, x, y );
		cold += (SmoothNoise( x, y, 1400f, seed + 50 ) - 0.5f) * 0.08f;
		return cold;
	}

	private static float ArcticColdBias( float nx, float ny, float x, float y )
	{
		var plotSize = GameConstants.PlotSize;
		var (wx, wy) = DomainWarp( nx, ny );
		var angle = MathF.Atan2( wy, wx );
		var dist = MathF.Sqrt( wx * wx + wy * wy );
		var corner = AngularScore( angle, -MathF.PI * 0.78f, 1.05f ) * RadialBand( dist, 0.35f, 0.95f, 2.35f );
		var blob = SmoothNoise( x + 5000f, y - 3000f, plotSize * 1.5f, WorldLayoutSeed + 9931 );
		return corner * (0.18f + blob * 0.14f);
	}

	private static float EdgeNoise( float nx, float ny, int channel = 0 ) =>
		(SmoothNoise( nx * GameConstants.PlotSize, ny * GameConstants.PlotSize, GameConstants.PlotSize * 0.55f, WorldLayoutSeed + channel ) - 0.5f) * 2f;

	/// <summary>0 = dry inland, 1 = open ocean at the warped world rim.</summary>
	private static float WorldEdgeWaterStrength( float x, float y )
	{
		var half = GameConstants.WorldHalfExtent;
		var insetX = MathF.Min( half - x, half + x );
		var insetY = MathF.Min( half - y, half + y );

		// Evaluate each map edge independently so north/south get the same band as east/west.
		return MathF.Max(
			WaterStrengthFromAxisInset( WarpAxisInset( insetX, x, y, axisY: false ) ),
			WaterStrengthFromAxisInset( WarpAxisInset( insetY, x, y, axisY: true ) ) );
	}

	private static float WaterStrengthFromAxisInset( float inset )
	{
		var tile = GameConstants.GroundRenderTileSize;
		var deepWater = tile * CoastlineDeepWaterTiles;
		var dryLand = tile * CoastlineTileWidth;

		if ( inset >= dryLand )
			return 0f;
		if ( inset <= deepWater )
			return 1f;

		return 1f - SmoothStep( (inset - deepWater) / MathF.Max( dryLand - deepWater, 1f ) );
	}

	/// <summary>Warp the distance to one world edge — noise runs along the shore, not across it.</summary>
	private static float WarpAxisInset( float inset, float x, float y, bool axisY )
	{
		var tile = GameConstants.GroundRenderTileSize;
		var plotSize = GameConstants.PlotSize;
		var edgeCoord = axisY ? y : x;
		var alongCoord = axisY ? x : y;
		var channel = axisY ? 13 : 0;

		// Low-freq wobble along the shore ±~2 tiles; finer ripples add another ~1 tile.
		var warp =
			(SmoothNoise( alongCoord, edgeCoord, plotSize * 2.1f, WorldLayoutSeed + 9901 + channel ) - 0.5f) * tile * CoastlineEdgeWobbleTiles * 2f +
			(SmoothNoise( alongCoord + 1800f, edgeCoord - 900f, tile * 5.5f, WorldLayoutSeed + 9902 + channel ) - 0.5f) * tile * 0.95f;

		return inset + warp;
	}

	private static float SoftBlobFalloff( float dist, float radius )
	{
		if ( dist >= radius ) return 0f;
		var t = 1f - dist / radius;
		var smooth = SmoothStep( t );
		return smooth * smooth;
	}

	private static float SmoothNoise( float worldX, float worldY, float scale, int seed )
	{
		if ( scale <= 0.001f ) return 0.5f;

		var x = worldX / scale;
		var y = worldY / scale;
		var ix = MathF.Floor( x );
		var iy = MathF.Floor( y );
		var fx = SmoothStep( x - ix );
		var fy = SmoothStep( y - iy );

		var a = Hash01( ix, iy, seed );
		var b = Hash01( ix + 1f, iy, seed );
		var c = Hash01( ix, iy + 1f, seed );
		var d = Hash01( ix + 1f, iy + 1f, seed );

		var ab = a + (b - a) * fx;
		var cd = c + (d - c) * fx;
		return ab + (cd - ab) * fy;
	}

	private static float SmoothStep( float t ) => t * t * (3f - 2f * t);

	private static float Hash01( float x, float y, int seed )
	{
		var n = (int)(x * 374761393f + y * 668265263f + seed * 982451653f);
		n = (n << 13) ^ n;
		n = n * (n * n * 15731 + 789221) + 1376312589;
		return (n & 0x7fffffff) / (float)0x7fffffff;
	}
}
