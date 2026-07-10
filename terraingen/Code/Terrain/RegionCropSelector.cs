namespace Terraingen.TerrainGen;

public readonly struct HeightmapCropResult
{
	public int OriginX { get; init; }
	public int OriginY { get; init; }
	public int CropWidth { get; init; }
	public int CropHeight { get; init; }
	public TerrainRegionMetrics Metrics { get; init; }
}

public static class RegionCropSelector
{
	public static HeightmapCropResult SelectBestCrop( HeightmapField source, ThornsTerrainConfig config, int seed )
	{
		var rng = new Random( seed );

		var fraction = MathX.Lerp( config.CropMinFraction, config.CropMaxFraction, (float)rng.NextDouble() );
		var cropW = Math.Max( 64, (int)(source.Width * fraction) );
		var cropH = Math.Max( 64, (int)(source.Height * fraction) );

		cropW = Math.Min( cropW, source.Width );
		cropH = Math.Min( cropH, source.Height );

		HeightmapCropResult? best = null;

		for ( int i = 0; i < config.RegionCandidateCount; i++ )
		{
			var ox = rng.Next( 0, Math.Max( 1, source.Width - cropW ) );
			var oy = rng.Next( 0, Math.Max( 1, source.Height - cropH ) );

			var metrics = TerrainAnalysis.EvaluateRegion( source, ox, oy, cropW, cropH );

			var candidate = new HeightmapCropResult
			{
				OriginX = ox,
				OriginY = oy,
				CropWidth = cropW,
				CropHeight = cropH,
				Metrics = metrics
			};

			if ( best is null || metrics.Score > best.Value.Metrics.Score )
				best = candidate;
		}

		return best ?? new HeightmapCropResult
		{
			OriginX = 0,
			OriginY = 0,
			CropWidth = cropW,
			CropHeight = cropH
		};
	}
}
