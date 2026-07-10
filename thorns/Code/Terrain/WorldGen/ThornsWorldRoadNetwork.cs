using System.Collections.Generic;

namespace Sandbox;

/// <summary>Spline/segment road data produced by world-gen (mesh/decals applied in a later pass).</summary>
public sealed class ThornsWorldRoadNetwork
{
	public IReadOnlyList<ThornsWorldTrailSegment> Segments { get; init; } = [];

	public static ThornsWorldRoadNetwork FromSettlementPlan( ThornsWorldSettlementPlan plan ) =>
		new() { Segments = plan?.Trails ?? [] };
}
