using System.Buffers;
using Terraingen.TerrainGen;

namespace Sandbox;

/// <summary>
/// Post-generation heightfield validation + reconstruction pass (before Sandbox.Terrain / legacy mesh build).
/// </summary>
public static class ThornsTerrainRepairPipeline
{
	public static ThornsTerrainRepairStats LastStats { get; private set; } = new();

	/// <summary>Last fault mask from the most recent run (for debug overlays). Length = width × height.</summary>
	public static byte[] LastFaultMask { get; private set; }

	/// <summary>Height snapshot when debug visualization is enabled (same length as fault mask).</summary>
	public static float[] LastHeightsSnapshot { get; private set; }

	public static ThornsTerrainRepairConfig LastConfig { get; private set; }

	public static ThornsTerrainRepairStats Run(
		in ThornsTerrainNetSpec spec,
		Span<float> heights,
		ThornsTerrainRepairConfig config = null,
		IReadOnlyList<ThornsTerrainBorderSync.ChunkBorderSample> neighborEdges = null )
	{
		var stats = new ThornsTerrainRepairStats();
		LastStats = stats;

		var rx = Math.Max( 2, spec.HeightmapResolutionX );
		var rz = Math.Max( 2, spec.HeightmapResolutionZ );
		var count = rx * rz;
		if ( heights.Length < count )
			return stats;

		config ??= ThornsTerrainRepairConfig.Resolve( in spec, ThornsTerraingenTerrainRuntime.ActiveTerrainConfig );
		LastConfig = config;
		if ( !config.Enabled )
			return stats;

		ThornsTerrainGeometry.GetExtents( spec, out var worldW, out var worldD );
		var cell = MathF.Max( worldW / (rx - 1f), worldD / (rz - 1f ) );

		stats.MaxNeighborStepBefore = ThornsTerrainHeightValidator.MeasureMaxNeighborStep( rx, rz, heights );

		var faults = ArrayPool<ThornsTerrainCellFault>.Shared.Rent( count );
		var scratch = ArrayPool<float>.Shared.Rent( count );
		try
		{
			ThornsTerrainHeightValidator.Validate( rx, rz, heights, config, cell, faults, stats );

			stats.ReconstructedCells = ThornsTerrainHeightReconstructor.ReconstructInvalidCells(
				rx, rz, heights, faults, config );

			stats.SpikesFixed = HeightmapSheerSmooth.RemoveIsolatedSpikesWorld( rx, rz, heights, cell );
			stats.StepsClamped = HeightmapSheerSmooth.ClampExcessiveNeighborSteps(
				rx, rz, heights, config.MaxNeighborHeightDelta, config.StepClampPasses );

			// Re-validate after reconstruction / clamping.
			faults.AsSpan( 0, count ).Clear();
			ThornsTerrainHeightValidator.Validate( rx, rz, heights, config, cell, faults, new ThornsTerrainRepairStats() );

			stats.LocalSmoothCells = ThornsTerrainRepairSmoother.SmoothRepairedRegions(
				rx, rz, heights, faults, config, scratch );

			stats.BorderCellsWelded = ThornsTerrainBorderSync.SynchronizeMapEdges(
				rx, rz, heights, config.BorderWeldWidth, neighborEdges );

			stats.MaxNeighborStepAfter = ThornsTerrainHeightValidator.MeasureMaxNeighborStep( rx, rz, heights );

			if ( config.DebugVisualization )
				CaptureDebugSnapshot( heights, faults, count );
		}
		finally
		{
			ArrayPool<ThornsTerrainCellFault>.Shared.Return( faults );
			ArrayPool<float>.Shared.Return( scratch );
		}

		LastStats = stats;
		LogStats( stats );
		return stats;
	}

	static void CaptureDebugSnapshot( ReadOnlySpan<float> heights, ThornsTerrainCellFault[] faults, int count )
	{
		var mask = LastFaultMask;
		if ( mask is null || mask.Length != count )
			mask = new byte[count];

		for ( var i = 0; i < count; i++ )
			mask[i] = (byte)faults[i];

		LastFaultMask = mask;

		var snap = LastHeightsSnapshot;
		if ( snap is null || snap.Length != count )
			snap = new float[count];

		heights.CopyTo( snap );
		LastHeightsSnapshot = snap;
	}

	static void LogStats( ThornsTerrainRepairStats stats )
	{
		if ( stats.CellsAdjusted == 0 && stats.MaxNeighborStepBefore < 400f )
			return;

		Log.Info(
			$"[Thorns Terrain Repair] invalid={stats.InvalidCellsDetected} reconstructed={stats.ReconstructedCells} "
			+ $"spikes={stats.SpikesFixed} steps={stats.StepsClamped} smooth={stats.LocalSmoothCells} "
			+ $"border={stats.BorderCellsWelded} maxStep {stats.MaxNeighborStepBefore:F0}\"→{stats.MaxNeighborStepAfter:F0}\"" );
	}
}
