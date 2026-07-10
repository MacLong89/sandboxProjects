using Terraingen.TerrainGen;

namespace Sandbox;

/// <summary>
/// Removes heightmap spikes and mesh-breaking sheer steps (stretched cliff textures / vertical tears).
/// Delegates to <see cref="ThornsTerrainRepairPipeline"/> for full validation + reconstruction.
/// </summary>
public static class ThornsTerrainHeightRepair
{
	/// <summary>Used by pad rim soften — neighbor step × cell above this is a spike.</summary>
	public const float SheerSpikeGradeMultiplier = 1.45f;

	public readonly struct RepairStats
	{
		public int SpikesFixed { get; init; }
		public int StepsClamped { get; init; }
		public int InvalidCellsDetected { get; init; }
		public int ReconstructedCells { get; init; }
		public int LocalSmoothCells { get; init; }
		public int BorderCellsWelded { get; init; }
		public float MaxNeighborStepBefore { get; init; }
		public float MaxNeighborStepAfter { get; init; }

		public int CellsAdjusted =>
			ReconstructedCells + SpikesFixed + StepsClamped + LocalSmoothCells + BorderCellsWelded;
	}

	public static RepairStats LastRepairStats { get; private set; }

	public static void RepairMeshBreakingDiscontinuities( in ThornsTerrainNetSpec spec, Span<float> heights )
	{
		var cfg = ThornsTerrainRepairConfig.Resolve( in spec, ThornsTerraingenTerrainRuntime.ActiveTerrainConfig );
		var stats = ThornsTerrainRepairPipeline.Run( in spec, heights, cfg );
		LastRepairStats = ToLegacyStats( stats );
	}

	public static RepairStats RepairMeshBreakingDiscontinuitiesCore( in ThornsTerrainNetSpec spec, Span<float> heights )
	{
		RepairMeshBreakingDiscontinuities( in spec, heights );
		return LastRepairStats;
	}

	static RepairStats ToLegacyStats( ThornsTerrainRepairStats stats ) => new()
	{
		SpikesFixed = stats.SpikesFixed,
		StepsClamped = stats.StepsClamped,
		InvalidCellsDetected = stats.InvalidCellsDetected,
		ReconstructedCells = stats.ReconstructedCells,
		LocalSmoothCells = stats.LocalSmoothCells,
		BorderCellsWelded = stats.BorderCellsWelded,
		MaxNeighborStepBefore = stats.MaxNeighborStepBefore,
		MaxNeighborStepAfter = stats.MaxNeighborStepAfter
	};
}
