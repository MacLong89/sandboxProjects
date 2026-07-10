using System.Collections.Generic;

namespace Sandbox;

/// <summary>One shared floor height per hub so city interiors do not stair-step on macro slope.</summary>
public static class ThornsWorldGenSettlementPlateau
{
	public const float HubPlateauPeakBlend = 1f;
	public const float MainCityHubPadRadiusFraction = 0.92f;
	public const float TownHubPadRadiusFraction = 0.88f;
	public const float MainCityHubApronWorld = 2200f;
	public const float TownHubApronWorld = 1600f;
	public const float RimSampleRadiusFraction = 0.9f;

	/// <summary>Plateau height matched to natural ground at the hub rim (avoids mesa cliffs).</summary>
	public static float ResolveHubPlateauZ(
		ThornsWorldGenerationContext ctx,
		Vector2 center,
		float hubRadius )
	{
		return SampleRimMedianHeight( ctx, center, hubRadius * RimSampleRadiusFraction );
	}

	/// <summary>Re-sample the hub ring on the current heightmap (call after roads, before hub pad).</summary>
	public static float SampleHubRimHeight(
		ThornsWorldGenerationContext ctx,
		Vector2 center,
		float hubRadius ) =>
		SampleRimMedianHeight( ctx, center, hubRadius * RimSampleRadiusFraction );

	public static float RefineHubPlateauFromPlacements(
		IReadOnlyList<ThornsWorldGenProcBuildingFootprint> footprints,
		Vector2 hubCenter,
		float hubRadius,
		float fallbackZ )
	{
		var samples = new List<float>( 16 );
		for ( var i = 0; i < footprints.Count; i++ )
		{
			var fp = footprints[i];
			if ( float.IsNaN( fp.FloorSurfaceZ ) )
				continue;

			var dx = fp.CenterX - hubCenter.x;
			var dy = fp.CenterY - hubCenter.y;
			if ( dx * dx + dy * dy > hubRadius * hubRadius )
				continue;

			samples.Add( fp.FloorSurfaceZ );
		}

		if ( samples.Count == 0 )
			return fallbackZ;

		samples.Sort();
		return samples[samples.Count / 2];
	}

	public static void AddHubPlateauPad(
		ThornsTerrainNetSpec spec,
		Vector2 center,
		float hubRadius,
		float plateauZ,
		bool mainCity )
	{
		spec.ProcBuildingTerrainPads ??= new List<ThornsTerrainProcBuildingPad>();
		var padR = hubRadius * ( mainCity ? MainCityHubPadRadiusFraction : TownHubPadRadiusFraction );
		spec.ProcBuildingTerrainPads.Add( new ThornsTerrainProcBuildingPad
		{
			Kind = ThornsSettlementTerrainPadKind.HubPlateau,
			CenterX = center.x,
			CenterY = center.y,
			HalfW = padR,
			HalfD = padR,
			YawRadians = 0f,
			TargetZ = plateauZ,
			Apron = mainCity ? MainCityHubApronWorld : TownHubApronWorld,
			PeakBlend = HubPlateauPeakBlend
		} );
	}

	public static void SyncHubBuildingPads(
		ThornsTerrainNetSpec spec,
		Vector2 hubCenter,
		float hubRadius,
		float plateauZ )
	{
		var pads = spec.ProcBuildingTerrainPads;
		if ( pads is null )
			return;

		var r2 = hubRadius * hubRadius;
		for ( var p = 0; p < pads.Count; p++ )
		{
			var pad = pads[p];
			if ( pad.Kind is ThornsSettlementTerrainPadKind.MacroSettlement
			     or ThornsSettlementTerrainPadKind.HubPlateau )
				continue;

			var dx = pad.CenterX - hubCenter.x;
			var dy = pad.CenterY - hubCenter.y;
			if ( dx * dx + dy * dy > r2 )
				continue;

			pad.TargetZ = plateauZ;
		}
	}

	static float SampleRimMedianHeight(
		ThornsWorldGenerationContext ctx,
		Vector2 center,
		float ringRadius )
	{
		var samples = new List<float>( 28 );
		const int ringSamples = 24;
		for ( var i = 0; i < ringSamples; i++ )
		{
			var ang = i * ( MathF.PI * 2f / ringSamples );
			samples.Add( SampleH(
				ctx,
				center.x + MathF.Cos( ang ) * ringRadius,
				center.y + MathF.Sin( ang ) * ringRadius ) );
		}

		samples.Sort();
		var idx = Math.Clamp( samples.Count / 2, 0, samples.Count - 1 );
		return samples[idx];
	}

	static float SampleH( ThornsWorldGenerationContext ctx, float lx, float ly )
	{
		var h = ThornsTerrainGeometry.SampleHeightLocalZUp(
			ctx.HeightsSpan,
			ctx.HeightRx,
			ctx.HeightRz,
			ctx.WorldWidth,
			ctx.WorldDepth,
			ctx.Spec.CenterOnWorldOrigin,
			lx,
			ly );
		return float.IsNaN( h ) || float.IsInfinity( h ) ? 0f : h;
	}
}
