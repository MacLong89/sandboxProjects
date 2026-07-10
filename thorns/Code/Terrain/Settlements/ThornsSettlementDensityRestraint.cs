namespace Sandbox;

/// <summary>Density-aware caps for settlement interior terrain — favors traversal over perfect grounding.</summary>
public static class ThornsSettlementDensityRestraint
{
	public readonly struct Restraint
	{
		public float ApronStrengthMul { get; init; }
		public float PeakBlendCap { get; init; }
		public float MaxCooperativeBlend { get; init; }
		public float BlockSurfaceStrengthMul { get; init; }
		public bool PreferBlockSurfaceOnly { get; init; }
	}

	public static Restraint Compute( int blockBuildingCount ) =>
		blockBuildingCount switch
		{
			>= 4 => new Restraint
			{
				ApronStrengthMul = 0.08f,
				PeakBlendCap = 0.4f,
				MaxCooperativeBlend = 0.3f,
				BlockSurfaceStrengthMul = 0.82f,
				PreferBlockSurfaceOnly = true
			},
			3 => new Restraint
			{
				ApronStrengthMul = 0.12f,
				PeakBlendCap = 0.46f,
				MaxCooperativeBlend = 0.36f,
				BlockSurfaceStrengthMul = 0.88f,
				PreferBlockSurfaceOnly = true
			},
			2 => new Restraint
			{
				ApronStrengthMul = 0.18f,
				PeakBlendCap = 0.54f,
				MaxCooperativeBlend = 0.44f,
				BlockSurfaceStrengthMul = 0.94f,
				PreferBlockSurfaceOnly = true
			},
			1 => new Restraint
			{
				ApronStrengthMul = 0.62f,
				PeakBlendCap = 0.78f,
				MaxCooperativeBlend = 0.56f,
				BlockSurfaceStrengthMul = 1f,
				PreferBlockSurfaceOnly = false
			},
			_ => new Restraint
			{
				ApronStrengthMul = 1f,
				PeakBlendCap = 1f,
				MaxCooperativeBlend = ThornsBuildingFoundationTerrain.MaxCooperativeBlend,
				BlockSurfaceStrengthMul = 1f,
				PreferBlockSurfaceOnly = false
			}
		};

	public static float ComputeBlockSurfaceStrength( int buildingCount, float baseStrength )
	{
		var r = Compute( buildingCount );
		return baseStrength * r.BlockSurfaceStrengthMul;
	}
}
