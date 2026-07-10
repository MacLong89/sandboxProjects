namespace Sandbox;

/// <summary>Thresholds for automatic post-generation terrain validation and repair.</summary>
public sealed class ThornsTerrainRepairConfig
{
	public bool Enabled { get; set; } = true;

	public float InvalidHeightMin { get; set; } = -512f;

	public float InvalidHeightMax { get; set; } = 14000f;

	/// <summary>Max allowed |Δh| between cardinal neighbors (inches). Zero = derive from cell size.</summary>
	public float MaxNeighborHeightDelta { get; set; }

	/// <summary>Max 3D edge length for a heightfield quad diagonal (inches). Zero = derive.</summary>
	public float MaxTriangleEdgeLength { get; set; }

	/// <summary>Max surface slope in degrees (from height gradient).</summary>
	public float MaxSlopeAngleDegrees { get; set; } = 82f;

	public float IsolatedSpikeMultiplier { get; set; } = 2.2f;

	public float MinIsolatedSpikeInches { get; set; } = 96f;

	public float MaxGradeMultiplier { get; set; } = 6.25f;

	public float MinMaxStepInches { get; set; } = 300f;

	public int StepClampPasses { get; set; } = 10;

	public int ReconstructSearchRadius { get; set; } = 6;

	public int LocalSmoothRadius { get; set; } = 2;

	public float LocalSmoothStrength { get; set; } = 0.42f;

	public int BorderWeldWidth { get; set; } = 2;

	public bool DebugVisualization { get; set; }

	public static ThornsTerrainRepairConfig Resolve(
		in ThornsTerrainNetSpec spec,
		Terraingen.TerrainGen.ThornsTerrainConfig terraingen = null )
	{
		var cfg = FromSettings( terraingen?.Repair ) ?? new ThornsTerrainRepairConfig();
		if ( !cfg.Enabled )
			return cfg;

		ThornsTerrainGeometry.GetExtents( spec, out var worldW, out var worldD );
		var rx = Math.Max( 2, spec.HeightmapResolutionX );
		var rz = Math.Max( 2, spec.HeightmapResolutionZ );
		var cell = MathF.Max( worldW / (rx - 1f), worldD / (rz - 1f ) );

		cfg.InvalidHeightMax = MathF.Max(
			cfg.InvalidHeightMax,
			MathF.Max( spec.HeightMultiplier, terraingen?.MaxTerrainHeightInches ?? spec.HeightMultiplier ) * 1.05f );

		if ( cfg.MaxNeighborHeightDelta <= 0f )
			cfg.MaxNeighborHeightDelta = MathF.Max( cell * cfg.MaxGradeMultiplier, cfg.MinMaxStepInches );

		if ( cfg.MaxTriangleEdgeLength <= 0f )
		{
			var planar = MathF.Sqrt( cell * cell * 2f );
			cfg.MaxTriangleEdgeLength = MathF.Sqrt( planar * planar + cfg.MaxNeighborHeightDelta * cfg.MaxNeighborHeightDelta );
		}

		return cfg;
	}

	static ThornsTerrainRepairConfig FromSettings( Terraingen.TerrainGen.ThornsTerrainRepairSettings s )
	{
		if ( s is null )
			return null;

		return new ThornsTerrainRepairConfig
		{
			Enabled = s.Enabled,
			InvalidHeightMin = s.InvalidHeightMin,
			InvalidHeightMax = s.InvalidHeightMax,
			MaxNeighborHeightDelta = s.MaxNeighborHeightDelta,
			MaxTriangleEdgeLength = s.MaxTriangleEdgeLength,
			MaxSlopeAngleDegrees = s.MaxSlopeAngleDegrees,
			ReconstructSearchRadius = s.ReconstructSearchRadius,
			LocalSmoothRadius = s.LocalSmoothRadius,
			LocalSmoothStrength = s.LocalSmoothStrength,
			BorderWeldWidth = s.BorderWeldWidth,
			DebugVisualization = s.DebugVisualization
		};
	}
}
