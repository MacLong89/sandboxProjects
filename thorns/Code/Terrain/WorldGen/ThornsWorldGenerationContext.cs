using System;
using System.Buffers;
using System.Collections.Generic;

namespace Sandbox;

/// <summary>
/// Mutable state shared across world-gen phases. Supports deterministic seeds, debug hooks,
/// and future chunk/streaming extensions via <see cref="WorldSeed"/> + heightmap ownership.
/// </summary>
public sealed class ThornsWorldGenerationContext : IDisposable
{
	public ThornsTerrainNetSpec Spec { get; }
	public int WorldSeed => Spec.Seed;

	public float[] Heights { get; private set; }
	public int HeightRx { get; private set; }
	public int HeightRz { get; private set; }
	public int HeightCells { get; private set; }

	public float WorldWidth { get; private set; }
	public float WorldDepth { get; private set; }
	public float MinX { get; private set; }
	public float MaxX { get; private set; }
	public float MinY { get; private set; }
	public float MaxY { get; private set; }

	public ThornsWorldSettlementPlan Plan { get; set; }
	public ThornsWorldSettlementConfig SettlementConfig { get; set; }
	public ThornsWorldRoadNetwork RoadNetwork { get; set; }
	public ThornsWorldSettlementBlockPlan BlockPlan { get; set; }

	public Random PlacementRng { get; set; }
	public ThornsProcBuildingDistrictPlanner DistrictPlanner { get; set; }
	public ThornsPerlinNoise2D FoliagePropsNoise { get; set; }

	public ThornsWorldGenFootprintReservation FootprintReservation { get; set; }
	public ThornsWorldGenBuildingLayoutFactory LayoutFactory { get; set; }

	public int SpawnedBuildingCount { get; set; }
	public int SpawnedPieceCount { get; set; }
	public int CityPlacedCount { get; set; }
	public int TownPlacedCount { get; set; }
	public int IsolatedPlacedCount { get; set; }
	public int[] TownPlacedPerTown { get; set; } = new int[3];

	bool _disposed;

	ThornsWorldGenerationContext( ThornsTerrainNetSpec spec ) => Spec = spec;

	/// <summary>Metadata-only context for post-chunk phases (no heightmap rent).</summary>
	public static ThornsWorldGenerationContext CreatePostChunk( ThornsTerrainNetSpec spec ) => new( spec );

	public static ThornsWorldGenerationContext Create(
		ThornsTerrainNetSpec spec,
		float edgeInsetFraction )
	{
		var ctx = new ThornsWorldGenerationContext( spec );
		spec.ProcBuildingTerrainPads = new List<ThornsTerrainProcBuildingPad>();
		spec.SettlementTerrainInfluences = new List<ThornsSettlementTerrainInfluenceNet>();
		spec.SettlementBlockTerrain = new List<ThornsSettlementBlockTerrainNet>();

		ctx.HeightRx = Math.Max( 2, spec.HeightmapResolutionX );
		ctx.HeightRz = Math.Max( 2, spec.HeightmapResolutionZ );
		ctx.HeightCells = ctx.HeightRx * ctx.HeightRz;
		ctx.Heights = ArrayPool<float>.Shared.Rent( ctx.HeightCells );

		ctx.WorldWidth = Math.Max( 64f, spec.WorldWidth );
		ctx.WorldDepth = Math.Max( 64f, spec.WorldDepth );
		var hw = ctx.WorldWidth * 0.5f;
		var hd = ctx.WorldDepth * 0.5f;
		var inset = Math.Clamp( edgeInsetFraction, 0f, 0.45f );
		ctx.MinX = -hw + ctx.WorldWidth * inset;
		ctx.MaxX = hw - ctx.WorldWidth * inset;
		ctx.MinY = -hd + ctx.WorldDepth * inset;
		ctx.MaxY = hd - ctx.WorldDepth * inset;

		return ctx;
	}

	public ReadOnlySpan<float> HeightsSpan =>
		Heights is null ? ReadOnlySpan<float>.Empty : Heights.AsSpan( 0, HeightCells );

	/// <summary>Transfers pooled heights to the host terrain system (caller must not return them to the pool).</summary>
	public float[] TransferHeightsOwnership()
	{
		var h = Heights;
		Heights = null;
		return h;
	}

	public void Dispose()
	{
		if ( _disposed )
			return;

		_disposed = true;
		if ( Heights is null )
			return;

		ArrayPool<float>.Shared.Return( Heights );
		Heights = null;
	}
}
