namespace Terraingen.Buildings;

using Terraingen.TerrainGen;

/// <summary>Shared terrain footprint validation for procedural building lots.</summary>
public static class ThornsProcBuildingTerrainUtil
{
	/// <summary>Default lot slope limit — keeps ground-floor entries walkable without giant stilts.</summary>
	public const float DefaultMaxFootprintReliefInches = 32f;

	/// <summary>Small fallback slope allowance when filling building quotas — still player-accessible.</summary>
	public const float DefaultFallbackMaxFootprintReliefInches = 44f;

	/// <summary>Last-resort slope allowance when filling POI building quotas.</summary>
	public const float DefaultEmergencyMaxFootprintReliefInches = 56f;

	/// <summary>Per-POI quota backfill — allows clamped deep foundations on steep coastal lots.</summary>
	public const float DefaultQuotaFillMaxFootprintReliefInches = 72f;

	public const float DefaultFoundationLiftInches = 6f;

	/// <summary>Hard cap on visible foundation pillar height (inches).</summary>
	public const float MaxFoundationDepthInches = 64f;

	/// <summary>Ignore terrain spikes this far above the footprint median (bad heightfield samples).</summary>
	public const float MaxProbeOutlierAboveMedianInches = 32f;

	/// <summary>Every foundation sample must be at least this far above sea level (not in the ocean).</summary>
	public const float MinFoundationAboveSeaInches = 40f;

	/// <summary>Low coastal band for towns and NPC guild site centers (matches player coastal spawn cap).</summary>
	public const float LowlandMaxAboveSeaMeters = 5f;
	public const float LowlandMaxAboveSeaInches = LowlandMaxAboveSeaMeters * ThornsTerrainSurface.InchesPerMeter;

	public const float TownCenterMaxReliefInches = 72f;
	public const float NpcGuildCenterMaxReliefInches = 72f;

	public static float MinAllowedGroundWorldZ( Terrain terrain, ThornsTerrainConfig config )
		=> ThornsTerrainSurface.GetSeaLevelWorldZ( terrain, config ) + MinFoundationAboveSeaInches;

	public static bool IsAboveSeaLevel( Terrain terrain, ThornsTerrainConfig config, float groundWorldZ )
		=> groundWorldZ >= MinAllowedGroundWorldZ( terrain, config );

	public static bool IsWithinLowlandElevation( Terrain terrain, ThornsTerrainConfig config, float groundWorldZ )
	{
		if ( !IsAboveSeaLevel( terrain, config, groundWorldZ ) )
			return false;

		var seaZ = ThornsTerrainSurface.GetSeaLevelWorldZ( terrain, config );
		return groundWorldZ <= seaZ + LowlandMaxAboveSeaInches;
	}

	public static bool TryResolveLowlandLotBase(
		Terrain terrain,
		ThornsTerrainConfig config,
		Vector3 worldXY,
		Rotation rotation,
		float maxAllowedRelief,
		out float baseZ,
		out float relief,
		out bool failedSnap )
	{
		baseZ = 0f;
		relief = 0f;
		failedSnap = false;

		if ( !ThornsTerrainSurface.TrySnapToTerrain( terrain, worldXY, out var ground )
		     || !IsWithinLowlandElevation( terrain, config, ground.z ) )
			return false;

		return TryResolveLotBase(
			terrain,
			config,
			ground,
			rotation,
			maxAllowedRelief,
			out baseZ,
			out _,
			out relief,
			out failedSnap );
	}

	public static bool TryResolveLotBase(
		Terrain terrain,
		ThornsTerrainConfig config,
		Vector3 worldXY,
		Rotation rotation,
		float maxAllowedRelief,
		out float baseZ,
		out float foundationDepth,
		out float relief,
		out bool failedSnap,
		float foundationLiftInches = DefaultFoundationLiftInches )
	{
		var half = ThornsBuildingModule.Cell * 1.5f + 24f;
		return TryResolveLotBase(
			terrain,
			config,
			worldXY,
			rotation,
			half,
			half,
			maxAllowedRelief,
			out baseZ,
			out foundationDepth,
			out relief,
			out failedSnap,
			foundationLiftInches );
	}

	public static bool TryResolveLotBase(
		Terrain terrain,
		ThornsTerrainConfig config,
		Vector3 worldXY,
		Rotation rotation,
		float halfExtentX,
		float halfExtentY,
		float maxAllowedRelief,
		out float baseZ,
		out float foundationDepth,
		out float relief,
		out bool failedSnap,
		float foundationLiftInches = DefaultFoundationLiftInches,
		bool clampFoundationDepth = false )
	{
		baseZ = 0f;
		foundationDepth = 0f;
		relief = 0f;
		failedSnap = false;

		if ( terrain is null || !terrain.IsValid() || config is null )
			return false;

		Span<float> samples = stackalloc float[49];
		var sampleCount = 0;
		var minAllowedZ = MinAllowedGroundWorldZ( terrain, config );

		for ( var ix = -3; ix <= 3; ix++ )
		{
			for ( var iy = -3; iy <= 3; iy++ )
			{
				var local = new Vector3( ix / 3f * halfExtentX, iy / 3f * halfExtentY, 0f );
				var probe = worldXY + rotation * local;
				if ( !ThornsTerrainSurface.TrySnapToTerrain( terrain, probe, out var hit ) )
				{
					failedSnap = true;
					return false;
				}

				if ( hit.z < minAllowedZ )
					return false;

				samples[sampleCount++] = hit.z;
			}
		}

		if ( !TrySummarizeAccessibleFootprint(
			     samples[..sampleCount],
			     maxAllowedRelief,
			     foundationLiftInches,
			     out baseZ,
			     out foundationDepth,
			     out relief,
			     clampFoundationDepth ) )
			return false;

		return baseZ >= minAllowedZ;
	}

	internal static bool TrySummarizeAccessibleFootprint(
		ReadOnlySpan<float> samples,
		float maxAllowedRelief,
		float foundationLiftInches,
		out float baseZ,
		out float foundationDepth,
		out float relief,
		bool clampFoundationDepth = false )
	{
		baseZ = 0f;
		foundationDepth = 0f;
		relief = 0f;

		if ( samples.Length == 0 )
			return false;

		var scratch = samples.Length <= 64 ? stackalloc float[samples.Length] : new float[samples.Length];
		samples.CopyTo( scratch );
		scratch.Sort();

		var median = scratch[scratch.Length / 2];
		var minZ = float.MaxValue;
		var maxZ = float.MinValue;
		var used = 0;

		for ( var i = 0; i < scratch.Length; i++ )
		{
			var z = scratch[i];
			if ( z < median - MaxProbeOutlierAboveMedianInches || z > median + MaxProbeOutlierAboveMedianInches )
				continue;

			minZ = Math.Min( minZ, z );
			maxZ = Math.Max( maxZ, z );
			used++;
		}

		if ( used < Math.Max( 12, scratch.Length * 2 / 3 ) )
			return false;

		relief = maxZ - minZ;
		var reliefCap = Math.Min(
			maxAllowedRelief,
			MaxFoundationDepthInches - foundationLiftInches - 8f );
		if ( relief > reliefCap )
			return false;

		baseZ = maxZ + foundationLiftInches;
		foundationDepth = Math.Max(
			ThornsProcBuildingSpawnDefaults.MinFoundationDepthInches,
			baseZ - minZ + 12f );

		if ( foundationDepth > MaxFoundationDepthInches )
		{
			if ( !clampFoundationDepth )
				return false;

			foundationDepth = MaxFoundationDepthInches;
		}

		return true;
	}
}
