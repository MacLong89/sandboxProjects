namespace Sandbox;

/// <summary>Footprint terrain suitability checks before settlement building placement.</summary>
public static class ThornsWorldSettlementTerrainValidation
{
	public readonly struct FootprintTerrainMetrics
	{
		public float CenterHeight { get; init; }
		public float MinCornerHeight { get; init; }
		public float MaxCornerHeight { get; init; }
		public float CornerDelta { get; init; }
		public float MeanHeight { get; init; }
		public float HeightVariance { get; init; }
		public float MaxLocalSlope { get; init; }
		public float CliffSeverity { get; init; }
	}

	public static bool TryEvaluateFootprint(
		ReadOnlySpan<float> heights,
		in ThornsTerrainNetSpec spec,
		float worldWidth,
		float worldDepth,
		float lx,
		float ly,
		float halfW,
		float halfD,
		float yawRad,
		ThornsWorldSettlementKind kind,
		out FootprintTerrainMetrics metrics,
		out string rejectReason )
	{
		metrics = default;
		rejectReason = null;

		var samples = ThornsTerrainGeometry.SampleFootprintHeightMetrics(
			heights,
			in spec,
			worldWidth,
			worldDepth,
			lx,
			ly,
			halfW,
			halfD,
			yawRad );

		metrics = new FootprintTerrainMetrics
		{
			CenterHeight = samples.CenterHeight,
			MinCornerHeight = samples.MinHeight,
			MaxCornerHeight = samples.MaxHeight,
			CornerDelta = samples.CornerDelta,
			MeanHeight = samples.MeanHeight,
			HeightVariance = samples.Variance,
			MaxLocalSlope = samples.MaxStepSlope,
			CliffSeverity = samples.CliffSeverity
		};

		var maxCornerDelta = kind switch
		{
			ThornsWorldSettlementKind.MainCity => 72f,
			ThornsWorldSettlementKind.Town => 28f,
			ThornsWorldSettlementKind.Isolated => 52f,
			_ => 36f
		};
		var maxSlope = kind switch
		{
			ThornsWorldSettlementKind.MainCity => 48f,
			ThornsWorldSettlementKind.Town => 24f,
			ThornsWorldSettlementKind.Isolated => 42f,
			_ => 30f
		};
		var maxVariance = kind switch
		{
			ThornsWorldSettlementKind.MainCity => 520f,
			ThornsWorldSettlementKind.Town => 140f,
			ThornsWorldSettlementKind.Isolated => 320f,
			_ => 200f
		};
		var maxCliff = kind switch
		{
			ThornsWorldSettlementKind.MainCity => 88f,
			ThornsWorldSettlementKind.Town => 36f,
			ThornsWorldSettlementKind.Isolated => 64f,
			_ => 44f
		};

		if ( metrics.CornerDelta > maxCornerDelta )
		{
			rejectReason = $"CornerDelta={metrics.CornerDelta:F0}>{maxCornerDelta:F0}";
			return false;
		}

		if ( metrics.MaxLocalSlope > maxSlope )
		{
			rejectReason = $"MaxSlope={metrics.MaxLocalSlope:F0}>{maxSlope:F0}";
			return false;
		}

		if ( metrics.HeightVariance > maxVariance )
		{
			rejectReason = $"Variance={metrics.HeightVariance:F0}>{maxVariance:F0}";
			return false;
		}

		if ( metrics.CliffSeverity > maxCliff )
		{
			rejectReason = $"CliffSeverity={metrics.CliffSeverity:F0}>{maxCliff:F0}";
			return false;
		}

		if ( !ThornsTerrainSystem.IsSpawnableLandHeight( spec, metrics.CenterHeight ) )
		{
			rejectReason = "UnderwaterOrInvalidCenter";
			return false;
		}

		return true;
	}
}
