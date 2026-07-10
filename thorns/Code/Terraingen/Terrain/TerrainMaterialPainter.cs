namespace Terraingen.TerrainGen;

using System.Runtime.CompilerServices;

/// <summary>
/// Grass (slopes) → thin dirt at treeline → stone → snow on gentle peaks (cliffs stay rock).
/// Uses 7-layer layout: grass1–4 + dirt + rock + snow.
/// </summary>
public static class TerrainMaterialPainter
{
	static readonly ConditionalWeakTable<TerrainStorage, bool[]> DominantGrassMasks = new();

	public static void InitializeDefaultControlMap( TerrainStorage storage )
	{
		if ( storage.ControlMap is null || storage.ControlMap.Length == 0 )
			return;

		var defaultMat = new CompactTerrainMaterial( 0, 0, 0, false );
		for ( int i = 0; i < storage.ControlMap.Length; i++ )
			storage.ControlMap[i] = defaultMat.Packed;
	}

	public static void PaintControlMap(
		TerrainStorage storage,
		HeightmapField field,
		ThornsTerrainConfig config,
		int worldSeed = 0 )
	{
		var layout = TerrainMaterialLayout.FromStorage( storage );
		var maxIndex = (byte)Math.Max( 0, storage.Materials.Count - 1 );
		var hasSnow = layout.HasSnow;
		var hasDirt = layout.HasDirt;
		var hasRock = layout.HasRock;
		var blendStrength = Math.Clamp( config.MaterialElevationBlendStrength, 0.08f, 1f );

		static byte ScaleBlend( int raw, float strength ) =>
			(byte)Math.Clamp( (int)MathF.Round( raw * strength ), 0, 255 );

		if ( !hasSnow )
			Log.Warning( "[Thorns Terrain] Snow material missing (need 7 layers). Peaks will show rock only." );

		TerrainAnalysis.ComputeSlopeAndCurvature( field, out var slope, out _ );
		var maxSlope = BuildMaxNeighborSlope( slope, field.Width, field.Height );

		var sea = config.SeaLevelNormalized + 0.012f;
		var maxHeight = field.Heights.Max();
		var elevSpan = Math.Max( 0.001f, maxHeight - sea );

		var grassSlopeMax = config.GrassMaxSlope;
		var snowSlopeMax = config.SnowMaxSlope > 0f
			? config.SnowMaxSlope
			: grassSlopeMax * 1.35f;
		var cliffRockSlope = TerrainAnalysis.Percentile( slope, 0.975f );

		var rockLine = sea + elevSpan * config.RockLowerRangeFraction;
		var grassLine = sea + elevSpan * config.GrassUpperRangeFraction;
		var dirtStart = grassLine - elevSpan * config.DirtBandRangeFraction;

		var snowLine = hasSnow ? sea + elevSpan * (1f - config.SnowUpperRangeFraction) : 1.1f;
		var snowBand = Math.Max( 0.001f, maxHeight - snowLine );
		var snowApproach = elevSpan * Math.Clamp( config.SnowUpperRangeFraction * 0.42f, 0.08f, 0.24f );
		var meadowStart = Math.Min( rockLine, snowLine - snowApproach * 0.72f );

		if ( grassLine < rockLine + elevSpan * 0.02f )
			grassLine = rockLine + elevSpan * 0.02f;

		byte GrassAt( int cellIndex ) => layout.PickGrassVariant( cellIndex, worldSeed );
		byte dirt = (byte)Math.Min( layout.DirtIndex, maxIndex );
		byte rock = (byte)Math.Min( layout.RockIndex, maxIndex );
		byte snow = (byte)Math.Min( layout.SnowIndex, maxIndex );

		var snowCount = 0;
		var grassCount = 0;
		var dominantGrassMask = new bool[field.Heights.Length];

		for ( int i = 0; i < field.Heights.Length; i++ )
		{
			var h = field.Heights[i];
			var localSlope = slope[i];
			var s = maxSlope[i];
			byte baseMat = GrassAt( i );
			byte overlayMat = baseMat;
			byte blend = 0;

			var aboveSea = h > sea;
			var paintSlope = localSlope * 0.80f + s * 0.20f;
			var isHardCliff = aboveSea && hasRock && paintSlope >= cliffRockSlope;
			var steepGrass = paintSlope > grassSlopeMax;
			var steepSnow = paintSlope > snowSlopeMax;
			var isSteepRockFace = aboveSea && hasRock && steepSnow && paintSlope > snowSlopeMax * 1.22f;
			var onPeak = hasSnow && h >= snowLine;
			var nearSnow = hasSnow && h >= snowLine - snowApproach && h < snowLine && !isHardCliff;
			var onGrassSlope = aboveSea && !steepGrass && !isHardCliff && h < grassLine;
			var onAlpineMeadow = aboveSea && !steepGrass && !isHardCliff && h >= meadowStart && h < snowLine + snowApproach * 0.15f;
			var onSteepGrassBand = aboveSea && !isHardCliff && steepGrass && !steepSnow && h < snowLine + snowApproach * 0.25f;
			var onSteepSnowBand = hasSnow && aboveSea && !isHardCliff && steepSnow && !isSteepRockFace
				&& h >= snowLine - snowApproach * 1.75f && h < snowLine + snowApproach * 0.4f;

			if ( !aboveSea )
			{
				baseMat = GrassAt( i );
				overlayMat = baseMat;
			}
			else if ( onPeak )
			{
				if ( isHardCliff || isSteepRockFace )
				{
					baseMat = rock;
					overlayMat = rock;
				}
				else if ( steepSnow && hasRock )
				{
					var snowT = Math.Clamp( (h - snowLine) / snowBand, 0f, 1f );
					baseMat = snow;
					overlayMat = rock;
					var steepT = Math.Clamp(
						(paintSlope - snowSlopeMax) / Math.Max( 0.001f, cliffRockSlope - snowSlopeMax ),
						0f,
						1f );
					blend = ScaleBlend(
						(int)(steepT * 48f),
						blendStrength );
					if ( snowT > 0.35f )
					{
						overlayMat = snow;
						blend = ScaleBlend( (int)(steepT * 28f), blendStrength );
					}

					snowCount++;
				}
				else
				{
					var snowT = Math.Clamp( (h - snowLine) / snowBand, 0f, 1f );
					baseMat = snow;
					overlayMat = snow;
					blend = 0;

					if ( snowT < 0.15f && hasRock && steepGrass )
					{
						overlayMat = rock;
						blend = ScaleBlend( (int)((0.15f - snowT) / 0.15f * 32f), blendStrength );
					}

					snowCount++;
				}
			}
			else if ( nearSnow || onSteepSnowBand )
			{
				var bandTop = snowLine + (onSteepSnowBand ? snowApproach * 0.35f : 0f);
				var bandBottom = snowLine - snowApproach;
				var t = Math.Clamp( (h - bandBottom) / Math.Max( 0.001f, bandTop - bandBottom ), 0f, 1f );
				baseMat = steepGrass && hasRock ? rock : GrassAt( i );
				overlayMat = snow;
				var rawBlend = (int)(t * 72f);
				if ( steepGrass && hasRock )
					rawBlend = rawBlend / 2 + 18;
				if ( steepSnow )
					rawBlend += 14;
				blend = ScaleBlend( rawBlend, blendStrength );
				grassCount++;
				if ( t > 0.12f )
					snowCount++;
			}
			else if ( isHardCliff || isSteepRockFace )
			{
				baseMat = rock;
				overlayMat = rock;
			}
			else if ( onSteepGrassBand )
			{
				baseMat = GrassAt( i );
				overlayMat = hasRock ? rock : GrassAt( i );
				var steepT = Math.Clamp(
					(paintSlope - grassSlopeMax) / Math.Max( 0.001f, snowSlopeMax - grassSlopeMax ),
					0f,
					1f );
				blend = hasRock
					? ScaleBlend( (int)(steepT * 58f), blendStrength )
					: (byte)0;
				grassCount++;
			}
			else if ( onGrassSlope || onAlpineMeadow )
			{
				baseMat = GrassAt( i );
				overlayMat = baseMat;
				grassCount++;

				if ( hasDirt && h >= dirtStart )
				{
					overlayMat = dirt;
					var band = Math.Max( 0.001f, grassLine - dirtStart );
					var dirtT = onAlpineMeadow
						? Math.Clamp( (h - meadowStart) / Math.Max( 0.001f, snowLine - meadowStart ), 0.25f, 1f )
						: (h - dirtStart) / band;
					blend = ScaleBlend( (int)(dirtT * 52f), blendStrength );
				}
			}
			else if ( hasRock && h >= rockLine )
			{
				baseMat = rock;
				overlayMat = rock;
			}
			else
			{
				baseMat = GrassAt( i );
				overlayMat = baseMat;
				grassCount++;
			}

			baseMat = (byte)Math.Min( baseMat, maxIndex );
			overlayMat = (byte)Math.Min( overlayMat, maxIndex );

			dominantGrassMask[i] = HasVisibleGrassMaterial( baseMat, overlayMat, blend, layout.GrassVariantCount );
			storage.ControlMap[i] = new CompactTerrainMaterial( baseMat, overlayMat, blend, false ).Packed;
		}

		DominantGrassMasks.Remove( storage );
		DominantGrassMasks.Add( storage, dominantGrassMask );

		if ( hasSnow )
		{
			var snowPct = 100f * snowCount / field.Heights.Length;
			var grassPct = 100f * grassCount / field.Heights.Length;
			Log.Info(
				$"[Thorns Terrain] Materials: grass line {grassLine:F3}, dirt from {dirtStart:F3}, rock from {rockLine:F3}, snow from {snowLine:F3} "
				+ $"(top {config.SnowUpperRangeFraction * 100f:F0}% above sea) max grass slope {grassSlopeMax:F2} max snow slope {snowSlopeMax:F2} "
				+ $"| grass {grassPct:F0}% snow {snowPct:F1}%" );
		}
	}

	public static bool TryGetDominantGrassMask( TerrainStorage storage, out bool[] mask )
	{
		mask = null;
		return storage is not null && DominantGrassMasks.TryGetValue( storage, out mask );
	}

	/// <summary>True when grass contributes enough to the painted splat (matches terrain shader visibility).</summary>
	internal static bool HasVisibleGrassMaterial( byte baseMat, byte overlayMat, byte blend, int grassVariantCount )
	{
		var weight = 0;
		if ( baseMat < grassVariantCount )
			weight += 255 - blend;
		if ( overlayMat < grassVariantCount )
			weight += blend;
		return weight >= 32;
	}

	/// <summary>
	/// Max slope in a 3×3 neighborhood so cliff rims / tear edges pick rock instead of stretched grass.
	/// </summary>
	static float[] BuildMaxNeighborSlope( float[] slope, int width, int height )
	{
		var maxSlope = new float[slope.Length];
		for ( var y = 0; y < height; y++ )
		{
			for ( var x = 0; x < width; x++ )
			{
				var idx = y * width + x;
				var peak = slope[idx];
				for ( var dy = -1; dy <= 1; dy++ )
				{
					var ny = Math.Clamp( y + dy, 0, height - 1 );
					for ( var dx = -1; dx <= 1; dx++ )
					{
						var nx = Math.Clamp( x + dx, 0, width - 1 );
						peak = Math.Max( peak, slope[ny * width + nx] );
					}
				}

				maxSlope[idx] = peak;
			}
		}

		return maxSlope;
	}
}
