using System.Collections.Generic;

namespace Sandbox;

/// <summary>Syncs per-building pad targets from placed footprints — height changes go through pads only.</summary>
public static class ThornsWorldGenSettlementTerrainAligner
{
	public static void SyncPlacedBuildingPads(
		ThornsTerrainNetSpec spec,
		IReadOnlyList<ThornsWorldGenProcBuildingFootprint> footprints )
	{
		if ( spec?.ProcBuildingTerrainPads is null || footprints is null )
			return;

		for ( var i = 0; i < footprints.Count; i++ )
		{
			var fp = footprints[i];
			if ( float.IsNaN( fp.FloorSurfaceZ ) || float.IsInfinity( fp.FloorSurfaceZ ) )
				continue;

			SyncExistingPadTargetZ( spec, fp );
		}
	}

	static void SyncExistingPadTargetZ( ThornsTerrainNetSpec spec, ThornsWorldGenProcBuildingFootprint fp )
	{
		var pads = spec.ProcBuildingTerrainPads;
		for ( var p = 0; p < pads.Count; p++ )
		{
			var pad = pads[p];
			if ( pad.Kind is ThornsSettlementTerrainPadKind.MacroSettlement
			     or ThornsSettlementTerrainPadKind.HubPlateau )
				continue;

			if ( MathF.Abs( pad.CenterX - fp.CenterX ) > 2f || MathF.Abs( pad.CenterY - fp.CenterY ) > 2f )
				continue;

			pad.TargetZ = fp.FloorSurfaceZ;
			return;
		}
	}
}
