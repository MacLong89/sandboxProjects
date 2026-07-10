namespace Terraingen.Foliage;

using Terraingen.TerrainGen;

/// <summary>
/// Samples height, slope, moisture, ecosystem masks, and species weights from the sculpted heightfield.
/// </summary>
public sealed class ThornsFoliageBiomeSampler
{
	static readonly Vector3[] ChunkEcologyOffsets =
	[
		Vector3.Zero,
		new( 0.32f, 0f, 0f ),
		new( -0.32f, 0f, 0f ),
		new( 0f, 0.32f, 0f ),
		new( 0f, -0.32f, 0f ),
		new( 0.22f, 0.22f, 0f ),
	];

	readonly HeightmapField _field;
	readonly float[] _slope;
	readonly float[] _curvature;
	readonly ThornsFoliageEcosystemField _ecosystem;
	readonly Vector3 _terrainOrigin;
	readonly float _terrainSize;
	readonly float _maxHeight;
	readonly float _seaLevel;
	readonly float _maxTreeSlope;
	readonly float _maxGrassSlope;
	readonly ThornsFoliageConfig _config;
	readonly bool[] _dominantGrassMask;

	/// <summary>Heightmap gradient limit for tree placement (from <see cref="ThornsFoliageConfig.MaxTreeSlopeDegrees"/>).</summary>
	public float MaxTreeSlope => _maxTreeSlope;

	public ThornsFoliageBiomeSampler(
		HeightmapField field,
		Terrain terrain,
		ThornsTerrainConfig terrainConfig,
		ThornsFoliageConfig foliageConfig )
	{
		_field = field;
		_config = foliageConfig;
		_terrainOrigin = terrain.GameObject.WorldPosition;
		_terrainSize = terrain.TerrainSize;
		_maxHeight = terrain.TerrainHeight;
		_seaLevel = terrainConfig.SeaLevelNormalized;
		_maxTreeSlope = foliageConfig.GetMaxTreeHeightmapSlope( field, terrain );
		_maxGrassSlope = foliageConfig.MaxSlopeForGrass;
		TerrainMaterialPainter.TryGetDominantGrassMask( terrain.Storage, out var dominantGrassMask );
		_dominantGrassMask = dominantGrassMask;

		TerrainAnalysis.ComputeSlopeAndCurvature( field, out _slope, out _curvature );
		_ecosystem = ThornsFoliageEcosystemField.Build( field, terrain, terrainConfig, foliageConfig, foliageConfig.FoliageSeed );

		if ( foliageConfig.VerboseDebug )
			Log.Info( "[Thorns Foliage] Ecosystem field built (forest mass blur, openings, treeline)." );
	}

	public FoliageBiomeSample Sample( float worldX, float worldY )
	{
		if ( !TryWorldToUv( worldX, worldY, out var u, out var v ) )
			return default;

		var h = _field.SampleBilinear( u, v );
		var slope = SampleSlopeBilinear( u, v );
		var curvature = SampleCurvatureBilinear( u, v );
		var ridgeExposure = Smooth( slope, _maxTreeSlope * 0.35f, _maxTreeSlope * 1.4f )
			* Smooth( MathF.Abs( curvature ), 0.02f, 0.12f );
		var exposure = (1f - MathF.Abs( curvature )).Clamp( 0f, 1f );

		var moisture = ComputeMoisture( h, slope );
		var waterProximity = 1f - Smooth( h, _seaLevel, _seaLevel + 0.14f );
		moisture = (moisture * 0.65f + waterProximity * 0.35f).Clamp( 0f, 1f );

		var alpine = Smooth( h, 0.52f, 0.78f );
		var lowland = 1f - Smooth( h, 0.58f, 0.82f );
		var cliff = Smooth( slope, _maxTreeSlope * 0.55f, _maxTreeSlope * 1.8f );
		var valley = (1f - slope * 4f).Clamp( 0f, 1f ) * moisture * lowland;
		var gentle = (1f - Smooth( slope, 0.01f, _maxGrassSlope * 1.05f )).Clamp( 0f, 1f ) * (0.65f + exposure * 0.35f);

		var forestMass = _ecosystem.SampleForestMass( u, v );
		var opening = _ecosystem.SampleOpening( u, v );
		var treeline = 1f - Smooth( h, _config.TreelineStartNormalized, _config.TreelineEndNormalized );
		var wetland = valley * moisture * (1f - alpine * 0.7f) * (1f - ridgeExposure * 0.6f);
		var riverCorridor = valley * moisture * (1f - alpine * 0.55f) * Smooth( forestMass, 0.15f, 0.55f );

		var flow = SampleFlowDirection( u, v );

		var massBoost = MathX.Lerp( 0.45f, 1f, forestMass.Clamp( 0f, 1f ) );
		var midForest = Smooth( h, _seaLevel + 0.12f, 0.58f ) * (1f - alpine * 0.35f);
		var pine = midForest * gentle * (1f - cliff) * massBoost * treeline * (1f - opening * 0.45f) * 1.2f;
		var aspen = (wetland + riverCorridor * 0.45f) * gentle * (1f - cliff) * massBoost * (1f - opening * 0.4f) * 0.9f;
		var oak = lowland * gentle * valley * (1f - moisture * 0.25f) * (1f - alpine * 0.85f) * massBoost * (1f - opening * 0.35f) * 0.6f;
		var grass = (wetland + riverCorridor * _config.RiverCorridorBoost) * gentle * (1f - cliff * 0.85f) * (1f - opening * 0.4f) * 0.85f;
		var alpineMeadow = alpine * gentle * (1f - cliff * 0.45f) * (1f - opening * 0.45f) * 1.05f;
		grass = (grass + alpineMeadow).Clamp( 0f, 1f );

		var treeDensity = massBoost * treeline * (1f - opening * 0.55f) * gentle * (1f - ridgeExposure * 0.5f );
		var grassDensity = grass.Clamp( 0f, 1f ) * (1f - opening * 0.3f) * MathF.Max( treeline, alpine * 0.72f );

		var aboveSea = IsHeightAboveSea( h );
		var canTrees = aboveSea
			&& cliff < 0.48f
			&& slope < _maxTreeSlope
			&& opening < 0.94f;
		var canGrass = aboveSea && cliff < 0.82f && slope < _maxGrassSlope * 1.15f && opening < 0.96f;

		if ( !aboveSea )
		{
			treeDensity = 0f;
			grassDensity = 0f;
			pine = aspen = oak = grass = 0f;
			forestMass = 0f;
			wetland = 0f;
			riverCorridor = 0f;
		}

		return new FoliageBiomeSample
		{
			Height = h,
			Slope = slope,
			Moisture = moisture,
			Valley = valley,
			Alpine = alpine,
			Cliff = cliff,
			ForestMass = forestMass,
			Opening = opening,
			Treeline = treeline,
			RiverCorridor = riverCorridor,
			Wetland = wetland,
			RidgeExposure = ridgeExposure,
			TreeDensityScale = treeDensity,
			GrassDensityScale = grassDensity,
			FlowDirection = flow,
			PineWeight = pine,
			AspenWeight = aspen,
			OakWeight = oak,
			GrassWeight = grass,
			CanPlaceTrees = canTrees,
			CanPlaceGrass = canGrass,
		};
	}

	public bool IsAboveSeaLevel( float worldX, float worldY )
	{
		if ( !TryWorldToUv( worldX, worldY, out var u, out var v ) )
			return false;

		return IsHeightAboveSea( _field.SampleBilinear( u, v ) );
	}

	public bool TrySampleGrassPlacement( float worldX, float worldY, out float normalizedHeight, out float alpine )
	{
		if ( !TryWorldToUv( worldX, worldY, out var u, out var v ) )
		{
			normalizedHeight = 0f;
			alpine = 0f;
			return false;
		}

		normalizedHeight = _field.SampleBilinear( u, v );
		alpine = Smooth( normalizedHeight, 0.52f, 0.78f );
		return IsHeightAboveSea( normalizedHeight );
	}

	public bool IsDominantTerrainMaterialGrass( float worldX, float worldY )
	{
		if ( _dominantGrassMask is null || !TryWorldToUv( worldX, worldY, out var u, out var v ) )
			return false;

		var fx = u * (_field.Width - 1);
		var fy = v * (_field.Height - 1);
		var x0 = (int)MathF.Floor( fx );
		var y0 = (int)MathF.Floor( fy );
		var x1 = Math.Min( x0 + 1, _field.Width - 1 );
		var y1 = Math.Min( y0 + 1, _field.Height - 1 );

		return CellHasVisibleGrass( x0, y0 )
		       || CellHasVisibleGrass( x1, y0 )
		       || CellHasVisibleGrass( x0, y1 )
		       || CellHasVisibleGrass( x1, y1 );
	}

	bool CellHasVisibleGrass( int x, int y )
	{
		var index = _field.Index( x, y );
		return index >= 0 && index < _dominantGrassMask.Length && _dominantGrassMask[index];
	}

	bool IsHeightAboveSea( float normalizedHeight )
	{
		return normalizedHeight > _seaLevel + _config.MinHeightAboveSea;
	}

	public ThornsFoliageChunkEcology SampleChunkEcology( Vector3 chunkCenter, float chunkSize )
	{
		var forestMass = 0f;
		var opening = 0f;
		var treeline = 0f;
		var river = 0f;
		var wetland = 0f;
		var treeSuit = 0f;
		var grassSuit = 0f;
		var treeScale = 0f;
		var grassScale = 0f;
		var flow = Vector2.Zero;

		foreach ( var offset in ChunkEcologyOffsets )
		{
			var s = Sample( chunkCenter.x + offset.x * chunkSize, chunkCenter.y + offset.y * chunkSize );
			forestMass += s.ForestMass;
			opening += s.Opening;
			treeline += s.Treeline;
			river += s.RiverCorridor;
			wetland += s.Wetland;
			var aboveSea = IsHeightAboveSea( s.Height );
			var slopeOk = aboveSea && s.Slope < _maxTreeSlope;
			treeSuit += slopeOk
				? s.TreeDensityScale + (s.PineWeight + s.AspenWeight + s.OakWeight) * 0.14f
				: 0f;
			grassSuit += aboveSea && s.CanPlaceGrass ? s.GrassDensityScale : 0f;
			treeScale += s.TreeDensityScale;
			grassScale += s.GrassDensityScale;
			flow += s.FlowDirection;
		}

		var inv = 1f / ChunkEcologyOffsets.Length;
		flow = flow * inv;
		if ( flow.Length > 0.001f )
			flow = flow.Normal;

		var fm = forestMass * inv;
		var op = opening * inv;
		var hero = fm * treeline * inv * (1f - op) * 0.35f;

		return new ThornsFoliageChunkEcology
		{
			ForestMass = fm,
			Opening = op,
			Treeline = treeline * inv,
			RiverCorridor = river * inv,
			Wetland = wetland * inv,
			TreeSuitability = treeSuit.Clamp( 0.05f, 1.25f ),
			GrassSuitability = grassSuit.Clamp( 0.05f, 1.25f ),
			TreeDensityScale = treeScale.Clamp( 0.05f, 1.2f ),
			GrassDensityScale = grassScale.Clamp( 0.05f, 1.2f ),
			FlowDirection = flow,
			HeroTreeChance = hero.Clamp( 0f, 1f ),
		};
	}

	Vector2 SampleFlowDirection( float u, float v )
	{
		const float du = 0.0025f;
		var hL = _field.SampleBilinear( (u - du).Clamp( 0f, 1f ), v );
		var hR = _field.SampleBilinear( (u + du).Clamp( 0f, 1f ), v );
		var hD = _field.SampleBilinear( u, (v - du).Clamp( 0f, 1f ) );
		var hU = _field.SampleBilinear( u, (v + du).Clamp( 0f, 1f ) );

		var gx = hL - hR;
		var gy = hD - hU;
		var flow = new Vector2( gx, gy );
		if ( flow.Length < 0.0001f )
			return Vector2.Zero;

		return flow.Normal;
	}

	bool TryWorldToUv( float worldX, float worldY, out float u, out float v )
	{
		u = (worldX - _terrainOrigin.x) / _terrainSize;
		v = (worldY - _terrainOrigin.y) / _terrainSize;
		return u >= 0f && v >= 0f && u <= 1f && v <= 1f;
	}

	float SampleCurvatureBilinear( float u, float v )
	{
		var fx = u * (_field.Width - 1);
		var fy = v * (_field.Height - 1);
		var x0 = (int)Math.Floor( fx );
		var y0 = (int)Math.Floor( fy );
		var x1 = Math.Min( x0 + 1, _field.Width - 1 );
		var y1 = Math.Min( y0 + 1, _field.Height - 1 );
		var tx = fx - x0;
		var ty = fy - y0;

		var c00 = _curvature[_field.Index( x0, y0 )];
		var c10 = _curvature[_field.Index( x1, y0 )];
		var c01 = _curvature[_field.Index( x0, y1 )];
		var c11 = _curvature[_field.Index( x1, y1 )];

		var cx0 = c00 + (c10 - c00) * tx;
		var cx1 = c01 + (c11 - c01) * tx;
		return cx0 + (cx1 - cx0) * ty;
	}

	float SampleSlopeBilinear( float u, float v )
	{
		var fx = u * (_field.Width - 1);
		var fy = v * (_field.Height - 1);
		var x0 = (int)Math.Floor( fx );
		var y0 = (int)Math.Floor( fy );
		var x1 = Math.Min( x0 + 1, _field.Width - 1 );
		var y1 = Math.Min( y0 + 1, _field.Height - 1 );
		var tx = fx - x0;
		var ty = fy - y0;

		var s00 = _slope[_field.Index( x0, y0 )];
		var s10 = _slope[_field.Index( x1, y0 )];
		var s01 = _slope[_field.Index( x0, y1 )];
		var s11 = _slope[_field.Index( x1, y1 )];

		var sx0 = s00 + (s10 - s00) * tx;
		var sx1 = s01 + (s11 - s01) * tx;
		return sx0 + (sx1 - sx0) * ty;
	}

	static float ComputeMoisture( float height, float slope )
	{
		var lowBias = 1f - Smooth( height, 0.1f, 0.42f );
		var flatBias = 1f - Smooth( slope, 0.03f, 0.12f );
		return (lowBias * 0.65f + flatBias * 0.35f).Clamp( 0f, 1f );
	}

	static float Smooth( float value, float edge0, float edge1 )
	{
		if ( edge1 <= edge0 )
			return value >= edge0 ? 1f : 0f;
		var t = ((value - edge0) / (edge1 - edge0)).Clamp( 0f, 1f );
		return t * t * (3f - 2f * t);
	}

	public float SampleWorldHeight( float worldX, float worldY )
	{
		if ( !TryWorldToUv( worldX, worldY, out var u, out var v ) )
			return 0f;
		return _field.SampleBilinear( u, v ) * _maxHeight;
	}
}
