namespace Sandbox;

/// <summary>Aggregated counters from the terrain repair pipeline.</summary>
public sealed class ThornsTerrainRepairStats
{
	public int InvalidCellsDetected { get; set; }
	public int NonFiniteCells { get; set; }
	public int OutOfRangeCells { get; set; }
	public int ExcessiveStepCells { get; set; }
	public int SteepSlopeCells { get; set; }
	public int StretchedQuadCells { get; set; }
	public int IsolatedSpikeCells { get; set; }

	public int ReconstructedCells { get; set; }
	public int SpikesFixed { get; set; }
	public int StepsClamped { get; set; }
	public int LocalSmoothCells { get; set; }
	public int BorderCellsWelded { get; set; }

	public int MeshQuadsSanitized { get; set; }
	public int MeshTrianglesSkipped { get; set; }

	public float MaxNeighborStepBefore { get; set; }
	public float MaxNeighborStepAfter { get; set; }

	public int CellsAdjusted =>
		ReconstructedCells + SpikesFixed + StepsClamped + LocalSmoothCells + BorderCellsWelded;

	public void Merge( ThornsTerrainRepairStats other )
	{
		InvalidCellsDetected += other.InvalidCellsDetected;
		NonFiniteCells += other.NonFiniteCells;
		OutOfRangeCells += other.OutOfRangeCells;
		ExcessiveStepCells += other.ExcessiveStepCells;
		SteepSlopeCells += other.SteepSlopeCells;
		StretchedQuadCells += other.StretchedQuadCells;
		IsolatedSpikeCells += other.IsolatedSpikeCells;
		ReconstructedCells += other.ReconstructedCells;
		SpikesFixed += other.SpikesFixed;
		StepsClamped += other.StepsClamped;
		LocalSmoothCells += other.LocalSmoothCells;
		BorderCellsWelded += other.BorderCellsWelded;
		MeshQuadsSanitized += other.MeshQuadsSanitized;
		MeshTrianglesSkipped += other.MeshTrianglesSkipped;
		MaxNeighborStepBefore = MathF.Max( MaxNeighborStepBefore, other.MaxNeighborStepBefore );
		MaxNeighborStepAfter = MathF.Max( MaxNeighborStepAfter, other.MaxNeighborStepAfter );
	}
}
