namespace Sandbox;

/// <summary>Per-bake counters for proc-building heightmap sculpt (logged after terrain mesh build).</summary>
public static class ThornsTerrainPadSculptDiagnostics
{
	public static int PadsRegistered { get; private set; }
	public static int PadsSculpted { get; private set; }
	public static int PadsSkippedNoSculpt { get; private set; }
	public static int CellsModifiedByPads { get; private set; }

	public static void Reset()
	{
		PadsRegistered = 0;
		PadsSculpted = 0;
		PadsSkippedNoSculpt = 0;
		CellsModifiedByPads = 0;
	}

	public static void BeginBake( IReadOnlyList<ThornsTerrainProcBuildingPad> pads )
	{
		Reset();
		if ( pads is null || pads.Count == 0 )
			return;

		PadsRegistered = pads.Count;
		for ( var i = 0; i < pads.Count; i++ )
		{
			if ( pads[i].SculptHeightmap )
				PadsSculpted++;
			else
				PadsSkippedNoSculpt++;
		}
	}

	public static void RecordPadSculpted() => PadsSculpted++;
	public static void RecordPadSkippedNoSculpt() => PadsSkippedNoSculpt++;
	public static void AddCellsModified( int count ) => CellsModifiedByPads += count;

	public static void LogIfRelevant()
	{
		var repair = ThornsTerrainHeightRepair.LastRepairStats;
		if ( PadsRegistered == 0 && repair.CellsAdjusted == 0 && repair.MaxNeighborStepBefore < 400f )
			return;

		Log.Info(
			$"[Thorns Terrain] Height sculpt: pads={PadsRegistered} sculpted={PadsSculpted} "
			+ $"collisionOnly={PadsSkippedNoSculpt} padCells={CellsModifiedByPads} "
			+ $"invalid={repair.InvalidCellsDetected} reconstructed={repair.ReconstructedCells} "
			+ $"spikesFixed={repair.SpikesFixed} stepsClamped={repair.StepsClamped} "
			+ $"smooth={repair.LocalSmoothCells} border={repair.BorderCellsWelded} "
			+ $"maxStepBefore={repair.MaxNeighborStepBefore:F0}\" maxStepAfter={repair.MaxNeighborStepAfter:F0}\"" );
	}
}
