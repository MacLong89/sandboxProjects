namespace Terraingen.TerrainGen;

public static class TerrainSculptPipeline
{
	static readonly ITerrainSculptPass[] Passes =
	{
		new MicroNoiseReductionPass(),
		new PlainsSmoothingPass(),
		new ValleyWideningPass(),
		new MountainExaggerationPass(),
		new RidgeSharpeningPass(),
		new CliffExposurePass(),
	};

	public static HeightmapField Sculpt( HeightmapField input, ThornsTerrainConfig config )
	{
		var field = input.Clone();
		TerrainAnalysis.ComputeSlopeAndCurvature( field, out var slope, out var curvature );

		foreach ( var pass in Passes )
			pass.Apply( field, config, slope, curvature );

		ApplyVerticalExaggeration( field, config );
		LowlandTerraceSmoothingPass.Apply( field, config );
		ApplySeaLevel( field, config );
		ApplyPeakHeadroom( field, config );

		TerrainAnalysis.ComputeSlopeAndCurvature( field, out _, out _ );
		return field;
	}

	static void ApplySeaLevel( HeightmapField field, ThornsTerrainConfig config )
	{
		var sea = config.SeaLevelNormalized;

		for ( int i = 0; i < field.Heights.Length; i++ )
		{
			if ( field.Heights[i] < sea )
				field.Heights[i] = Math.Max( 0f, sea - config.LakeBedDepth );
		}
	}

	/// <summary>
	/// Lifts peaks and deepens valleys relative to sea level — not a flat pivot at 0.5.
	/// </summary>
	static void ApplyVerticalExaggeration( HeightmapField field, ThornsTerrainConfig config )
	{
		if ( Math.Abs( config.VerticalExaggeration - 1f ) < 0.001f )
			return;

		var anchor = config.SeaLevelNormalized;
		var aboveSpan = Math.Max( 0.001f, 1f - anchor );
		var belowSpan = Math.Max( 0.001f, anchor );

		var lowlandTop = anchor + config.LowlandBandHeight;

		for ( int i = 0; i < field.Heights.Length; i++ )
		{
			var h = field.Heights[i];
			var delta = h - anchor;
			float exaggeration;

			if ( delta >= 0f )
			{
				if ( h <= lowlandTop )
				{
					var bandT = (delta / Math.Max( 0.001f, lowlandTop - anchor )).Clamp( 0f, 1f );
					var bandExag = MathX.Lerp( 1f, config.LowlandVerticalCap, bandT * bandT );
					field.Heights[i] = anchor + delta * bandExag;
					continue;
				}

				var t = (h - lowlandTop) / Math.Max( 0.001f, 1f - lowlandTop );
				var highExag = config.VerticalExaggeration
					* MathX.Lerp( 1f, config.PeakExaggerationMultiplier, t * t );
				var foothillExag = config.LowlandVerticalCap;
				var foothillBlend = Math.Clamp( (h - lowlandTop) / Math.Max( 0.001f, config.LowlandBlendHeight ), 0f, 1f );
				exaggeration = MathX.Lerp( foothillExag, highExag, foothillBlend * foothillBlend );
			}
			else
			{
				var t = Math.Clamp( -delta / belowSpan, 0f, 1f );
				exaggeration = MathX.Lerp( 1f, config.ValleyDepthMultiplier, t );
			}

			field.Heights[i] = anchor + delta * exaggeration;
		}
	}

	/// <summary>
	/// Stylization passes can push heights past 1.0; scaling preserves peaks instead of flattening them at the ceiling.
	/// </summary>
	static void ApplyPeakHeadroom( HeightmapField field, ThornsTerrainConfig config )
	{
		var max = 0f;
		for ( int i = 0; i < field.Heights.Length; i++ )
			max = Math.Max( max, field.Heights[i] );

		var targetMax = config.PeakHeightNormalized;
		if ( max <= targetMax + 0.0001f || max < 0.0001f )
			return;

		var scale = targetMax / max;
		for ( int i = 0; i < field.Heights.Length; i++ )
			field.Heights[i] *= scale;
	}
}
