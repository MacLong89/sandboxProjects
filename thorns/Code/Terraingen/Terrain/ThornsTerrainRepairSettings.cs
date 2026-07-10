namespace Terraingen.TerrainGen;

/// <summary>Editor-tunable thresholds for post-generation terrain repair (mapped to <see cref="Sandbox.ThornsTerrainRepairConfig"/>).</summary>
public sealed class ThornsTerrainRepairSettings
{
	[Property] public bool Enabled { get; set; } = true;

	[Property] public float InvalidHeightMin { get; set; } = -512f;

	[Property] public float InvalidHeightMax { get; set; } = 14000f;

	[Property, Title( "Max neighbor Δh (inches; 0 = auto)" )]
	public float MaxNeighborHeightDelta { get; set; }

	[Property, Title( "Max triangle edge (inches; 0 = auto)" )]
	public float MaxTriangleEdgeLength { get; set; }

	[Property, Range( 45f, 89f )]
	public float MaxSlopeAngleDegrees { get; set; } = 82f;

	[Property] public int ReconstructSearchRadius { get; set; } = 6;

	[Property] public int LocalSmoothRadius { get; set; } = 2;

	[Property, Range( 0.05f, 0.95f )]
	public float LocalSmoothStrength { get; set; } = 0.42f;

	[Property] public int BorderWeldWidth { get; set; } = 2;

	[Property] public bool DebugVisualization { get; set; }
}
