using System.Collections.Generic;

namespace Sandbox;

/// <summary>
/// INTERNAL implementation — host wildlife brain registry and peer spatial index.
/// Public access: <see cref="ThornsPopulationDirector"/> only. Do not reference from gameplay code.
/// </summary>
public static class ThornsWildlifePopulation
{
	static readonly List<ThornsWildlifeBrain> Brains = new();
	static readonly ThornsHostWildlifeSpatialIndex PeerSpatial = new();
	static readonly List<ThornsWildlifeBrain> PeerQueryScratch = new();
	static int _peerSpatialBuiltSerial = -1;

	public static int HostGlobalCount => Brains.Count;

	public static void HostRegister( ThornsWildlifeBrain brain ) => Register( brain );

	public static void Register( ThornsWildlifeBrain brain )
	{
		if ( brain is null || !brain.IsValid() )
			return;

		if ( !Brains.Contains( brain ) )
			Brains.Add( brain );
	}

	public static void HostUnregister( ThornsWildlifeBrain brain ) => Unregister( brain );

	public static void Unregister( ThornsWildlifeBrain brain )
	{
		if ( brain is null )
			return;

		Brains.Remove( brain );
	}

	public static IReadOnlyList<ThornsWildlifeBrain> HostBrainsReadOnly => Brains;

	static void HostRebuildPeerSpatialIndexForCurrentFixedStep()
	{
		if ( !Networking.IsHost )
			return;

		var serial = ThornsWildlifeLosBudget.HostFixedStepSerial;
		if ( _peerSpatialBuiltSerial == serial )
			return;

		_peerSpatialBuiltSerial = serial;
		PeerSpatial.CellSize = ThornsPerformanceBudgets.HostWildlifeSpatialCellSizeWorld;
		PeerSpatial.Rebuild( Brains );
		ThornsAiPerceptionMetrics.LastWildlifeSpatialGridCells = PeerSpatial.LastRebuildBucketCount;
		ThornsAiPerceptionMetrics.LastWildlifeSpatialGridBrains = PeerSpatial.LastRebuildBrainCount;
	}

	static void HostEnsurePeerSpatialIndex() => HostRebuildPeerSpatialIndexForCurrentFixedStep();

	/// <summary>Planar peers within <paramref name="radiusWorld"/> (XY), excluding <paramref name="excludeSelf"/>.</summary>
	public static void HostQueryPeersNearPlanar(
		Vector3 flat,
		float radiusWorld,
		List<ThornsWildlifeBrain> results,
		ThornsWildlifeBrain excludeSelf = null )
	{
		results.Clear();
		if ( !Networking.IsHost )
			return;

		HostEnsurePeerSpatialIndex();
		var q = radiusWorld * ThornsPerformanceBudgets.HostWildlifeSpatialQueryRadiusInflateMul;
		PeerSpatial.QueryNearPlanar( flat, q, results, excludeSelf );
		ThornsAiPerceptionMetrics.RecordWildlifePeerSpatialQuery( results.Count );
	}

	/// <summary>Shared scratch for a single peer-separation evaluation (not re-entrant).</summary>
	internal static List<ThornsWildlifeBrain> HostBorrowPeerQueryScratch() => PeerQueryScratch;

	/// <summary>Host director calls once per physics step before wildlife motors run when possible.</summary>
	public static void HostRebuildPeerSpatialIndexForFixedStep() => HostRebuildPeerSpatialIndexForCurrentFixedStep();

	/// <summary>Population pressure near a world point — used by spawner (not per-frame over all animals).</summary>
	public static int HostCountNear( Vector3 world, float radius )
	{
		if ( !Networking.IsHost )
			return 0;

		var scratch = HostBorrowPeerQueryScratch();
		HostQueryPeersNearPlanar( world.WithZ( 0 ), radius, scratch );
		var n = 0;
		for ( var i = 0; i < scratch.Count; i++ )
		{
			if ( scratch[i].IsValid() )
				n++;
		}

		return n;
	}

	[Obsolete( "Use ThornsPopulationDirector.HostCountWildlifeNearAnyPlayer" )]
	public static int HostCountNearAnyPlayer( ThornsWildlifeDirector director, float radius ) =>
		ThornsPopulationDirector.HostCountWildlifeNearAnyPlayer( radius );
}
