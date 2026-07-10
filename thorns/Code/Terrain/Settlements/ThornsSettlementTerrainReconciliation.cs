using System.Collections.Generic;

namespace Sandbox;

/// <summary>
/// Localized post-blend reconciliation: slope-limited erosion, step clamps, road exits.
/// Only runs inside settlement transition / outer-feather bands.
/// </summary>
public static class ThornsSettlementTerrainReconciliation
{
	public const float MaxNeighborStepCity = 14f;
	public const float MaxNeighborStepTown = 18f;
	public const float SteepSlopeThreshold = 0.42f;
	public const float ErosionBlend = 0.42f;
	public const int ReconcilePasses = 2;

	public static IReadOnlyList<ThornsSettlementTerrainReconcileDebugCell> LastDebugCells { get; private set; } =
		Array.Empty<ThornsSettlementTerrainReconcileDebugCell>();

	public static void ReconcileSettlementEdges( in ThornsTerrainNetSpec spec, Span<float> heights, bool collectDebug = false )
	{
		var influences = spec.SettlementTerrainInfluences;
		if ( influences is null || influences.Count == 0 || heights.IsEmpty )
			return;

		var rx = Math.Max( 2, spec.HeightmapResolutionX );
		var rz = Math.Max( 2, spec.HeightmapResolutionZ );
		if ( heights.Length < rx * rz )
			return;

		var worldW = Math.Max( 64f, spec.WorldWidth );
		var worldD = Math.Max( 64f, spec.WorldDepth );
		var cellX = worldW / (rx - 1f );
		var cellY = worldD / (rz - 1f );
		var maxStep = MathF.Max( MaxNeighborStepCity, MaxNeighborStepTown );
		var debugCells = collectDebug ? new List<ThornsSettlementTerrainReconcileDebugCell>( 512 ) : null;

		for ( var pass = 0; pass < ReconcilePasses; pass++ )
		{
			for ( var gy = 1; gy < rz - 1; gy++ )
			{
				var row = gy * rx;
				for ( var gx = 1; gx < rx - 1; gx++ )
				{
					var i = row + gx;
					var wx = GridToLocalX( gx, cellX, worldW, spec.CenterOnWorldOrigin );
					var wy = GridToLocalY( gy, cellY, worldD, spec.CenterOnWorldOrigin );
					if ( !TrySampleBandStrength( wx, wy, influences, out var band, out var isCity ) )
						continue;

					if ( band < 0.08f )
						continue;

					var h = heights[i];
					var avg = (heights[i - 1] + heights[i + 1] + heights[i - rx] + heights[i + rx]) * 0.25f;
					var stepX = MathF.Abs( heights[i + 1] - heights[i - 1] );
					var stepY = MathF.Abs( heights[i + rx] - heights[i - rx] );
					var slope = MathF.Max( stepX / (cellX * 2f), stepY / (cellY * 2f) );
					var maxAllowed = isCity ? MaxNeighborStepCity : MaxNeighborStepTown;

					var didErode = false;
					if ( slope > SteepSlopeThreshold )
					{
						h = h + (avg - h) * ErosionBlend * band;
						didErode = true;
					}

					h = ClampToNeighbors( i, rx, heights, h, maxAllowed * (0.55f + band * 0.45f) );

					if ( MathF.Abs( h - heights[i] ) > 0.01f )
					{
						heights[i] = h;
						if ( debugCells is not null && debugCells.Count < 900 && (didErode || slope > SteepSlopeThreshold * 0.85f) )
						{
							debugCells.Add( new ThornsSettlementTerrainReconcileDebugCell
							{
								LocalX = wx,
								LocalY = wy,
								Slope = slope,
								BandStrength = band,
								Eroded = didErode
							} );
						}
					}
				}
			}
		}

		LastDebugCells = debugCells is not null
			? debugCells
			: Array.Empty<ThornsSettlementTerrainReconcileDebugCell>();
	}

	public static void SoftenRoadExitBanks( in ThornsTerrainNetSpec spec, Span<float> heights )
	{
		var corridors = spec.RoadCorridors;
		if ( corridors is null || corridors.Count == 0 || heights.IsEmpty )
			return;

		var rx = Math.Max( 2, spec.HeightmapResolutionX );
		var rz = Math.Max( 2, spec.HeightmapResolutionZ );
		if ( heights.Length < rx * rz )
			return;

		var worldW = Math.Max( 64f, spec.WorldWidth );
		var worldD = Math.Max( 64f, spec.WorldDepth );
		var cellX = worldW / (rx - 1f );
		var cellY = worldD / (rz - 1f );
		var halfW = spec.CenterOnWorldOrigin ? worldW * 0.5f : 0f;
		var halfD = spec.CenterOnWorldOrigin ? worldD * 0.5f : 0f;
		var tuning = spec.RoadTuning ?? ThornsTerrainRoadTuningNet.EngineDefaults();

		for ( var gy = 1; gy < rz - 1; gy++ )
		{
			var wy = gy * cellY - halfD;
			var row = gy * rx;
			for ( var gx = 1; gx < rx - 1; gx++ )
			{
				var wx = gx * cellX - halfW;
				var i = row + gx;
				if ( !ThornsWorldRoadTerrain.TrySampleInfluenceAt(
					     heights,
					     spec,
					     new Vector2( wx, wy ),
					     corridors,
					     tuning,
					     out var inf ) )
					continue;

				if ( inf.Weight < 0.12f )
					continue;

				var h = heights[i];
				var avg = (heights[i - 1] + heights[i + 1] + heights[i - rx] + heights[i + rx]) * 0.25f;
				var stepX = MathF.Abs( heights[i + 1] - heights[i - 1] );
				var stepY = MathF.Abs( heights[i + rx] - heights[i - rx] );
				var slope = MathF.Max( stepX / (cellX * 2f), stepY / (cellY * 2f) );
				if ( slope < SteepSlopeThreshold * 0.75f )
					continue;

				heights[i] = h + (avg - h) * 0.35f * inf.Weight;
			}
		}
	}

	static float ClampToNeighbors( int i, int rx, Span<float> heights, float h, float maxStep )
	{
		var iL = i - 1;
		var iR = i + 1;
		var iD = i - rx;
		var iU = i + rx;
		h = ClampToNeighbor( h, heights[iL], maxStep );
		h = ClampToNeighbor( h, heights[iR], maxStep );
		h = ClampToNeighbor( h, heights[iD], maxStep );
		h = ClampToNeighbor( h, heights[iU], maxStep );
		return h;
	}

	static float ClampToNeighbor( float h, float neighbor, float maxStep )
	{
		var d = h - neighbor;
		if ( d > maxStep )
			return neighbor + maxStep;
		if ( d < -maxStep )
			return neighbor - maxStep;
		return h;
	}

	static bool TrySampleBandStrength(
		float wx,
		float wy,
		List<ThornsSettlementTerrainInfluenceNet> influences,
		out float bandStrength,
		out bool isCity )
	{
		bandStrength = 0f;
		isCity = false;
		for ( var n = 0; n < influences.Count; n++ )
		{
			var inf = influences[n];
			var dx = wx - inf.CenterX;
			var dy = wy - inf.CenterY;
			var planar = MathF.Sqrt( dx * dx + dy * dy );
			if ( planar >= inf.OuterFeatherRadius || planar < inf.CoreRadius * 0.92f )
				continue;

			ThornsSettlementTerrainInfluence.ComputeRingWeights(
				planar,
				inf.CoreRadius,
				inf.TransitionRadius,
				inf.OuterFeatherRadius,
				out _,
				out var transW,
				out var outerW );

			var band = MathF.Max( transW, outerW );
			if ( band > bandStrength )
			{
				bandStrength = band;
				isCity = inf.Kind == ThornsWorldSettlementKind.MainCity;
			}
		}

		return bandStrength > 0.02f;
	}

	static float GridToLocalX( int gx, float cellX, float worldW, bool centerOnOrigin ) =>
		gx * cellX - (centerOnOrigin ? worldW * 0.5f : 0f );

	static float GridToLocalY( int gy, float cellY, float worldD, bool centerOnOrigin ) =>
		gy * cellY - (centerOnOrigin ? worldD * 0.5f : 0f );
}

public readonly struct ThornsSettlementTerrainReconcileDebugCell
{
	public float LocalX { get; init; }
	public float LocalY { get; init; }
	public float Slope { get; init; }
	public float BandStrength { get; init; }
	public bool Eroded { get; init; }
}
