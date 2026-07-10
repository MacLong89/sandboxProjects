namespace Sandbox;

/// <summary>Terrain affinity for settlement placement — delegates macro gates to site analysis.</summary>
public static class ThornsWorldSettlementTerrainScore
{
	public static float ScoreCityCenter(
		ReadOnlySpan<float> heights,
		int rx,
		int rz,
		float ww,
		float wd,
		bool centerOnOrigin,
		float lx,
		float ly )
	{
		var eval = ThornsWorldSettlementSiteAnalysis.EvaluateCity(
			heights, rx, rz, ww, wd, centerOnOrigin, lx, ly );

		if ( !eval.Acceptable )
			return -1f;

		return eval.CompositeScore;
	}

	public static ThornsWorldSettlementSiteEvaluation ScoreCityCenterDetailed(
		ReadOnlySpan<float> heights,
		int rx,
		int rz,
		float ww,
		float wd,
		bool centerOnOrigin,
		float lx,
		float ly ) =>
		ThornsWorldSettlementSiteAnalysis.EvaluateCity( heights, rx, rz, ww, wd, centerOnOrigin, lx, ly );

	public static float ScoreTownCenter(
		ReadOnlySpan<float> heights,
		int rx,
		int rz,
		float ww,
		float wd,
		bool centerOnOrigin,
		float lx,
		float ly,
		ThornsPerlinNoise2D foliageNoise,
		in ThornsTerrainNetSpec spec )
	{
		var eval = ThornsWorldSettlementSiteAnalysis.EvaluateTown(
			heights, rx, rz, ww, wd, centerOnOrigin, lx, ly );

		if ( !eval.Acceptable )
			return -1f;

		var forest = ThornsWorldNoise.SampleFoliageProps01( foliageNoise, lx, ly, in spec );
		if ( forest > 0.78f )
			return -1f;

		return eval.CompositeScore * 1.05f - forest * 0.45f;
	}

	public static float ScoreIsolated(
		ThornsProcBuildingType type,
		ReadOnlySpan<float> heights,
		int rx,
		int rz,
		float ww,
		float wd,
		bool centerOnOrigin,
		float lx,
		float ly,
		ThornsPerlinNoise2D foliageNoise,
		in ThornsTerrainNetSpec spec )
	{
		var forest = ThornsWorldNoise.SampleFoliageProps01( foliageNoise, lx, ly, in spec );
		var slope = SlopePenalty( heights, rx, rz, ww, wd, centerOnOrigin, lx, ly, 380f );

		return type switch
		{
			ThornsProcBuildingType.Cabin => forest * 1.1f + 0.25f - slope * 0.2f,
			ThornsProcBuildingType.Barn => forest * 0.35f + 0.4f - slope * 0.25f,
			ThornsProcBuildingType.Ruin => 0.55f + forest * 0.2f - slope * 0.15f,
			ThornsProcBuildingType.MilitaryComplex => 0.35f + slope * 0.85f - forest * 0.35f,
			ThornsProcBuildingType.RadioOutpost => 0.4f + slope * 0.5f - forest * 0.2f,
			_ => 0.3f - slope
		};
	}

	static float SlopePenalty(
		ReadOnlySpan<float> heights,
		int rx,
		int rz,
		float ww,
		float wd,
		bool centerOnOrigin,
		float lx,
		float ly,
		float radius )
	{
		if ( !Sample( heights, rx, rz, ww, wd, centerOnOrigin, lx, ly, out _ ) )
			return 1f;

		if ( !Sample( heights, rx, rz, ww, wd, centerOnOrigin, lx + radius, ly, out var h1 ) ) return 1f;
		if ( !Sample( heights, rx, rz, ww, wd, centerOnOrigin, lx - radius, ly, out var h2 ) ) return 1f;

		var delta = MathF.Abs( h1 - h2 );
		return Math.Clamp( delta / 80f, 0f, 1f );
	}

	static bool Sample(
		ReadOnlySpan<float> heights,
		int rx,
		int rz,
		float ww,
		float wd,
		bool centerOnOrigin,
		float lx,
		float ly,
		out float hz )
	{
		hz = ThornsTerrainGeometry.SampleHeightLocalZUp( heights, rx, rz, ww, wd, centerOnOrigin, lx, ly );
		return !float.IsNaN( hz ) && !float.IsInfinity( hz );
	}
}
