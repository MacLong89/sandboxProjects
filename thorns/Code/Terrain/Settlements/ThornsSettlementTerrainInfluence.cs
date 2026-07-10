using System.Collections.Generic;

namespace Sandbox;

/// <summary>
/// Multi-ring settlement terrain: inner core, transition, outer feather, wilderness.
/// Shave-down bias, rim-matched targets, directional feather, post-blend reconciliation.
/// </summary>
public static class ThornsSettlementTerrainInfluence
{
	public const float CityCoreRadiusMul = 0.42f;
	public const float CityTransitionRadiusMul = 1.35f;
	public const float CityOuterFeatherRadiusMul = 3.65f;
	public const float TownCoreRadiusMul = 0.38f;
	public const float TownTransitionRadiusMul = 1.22f;
	public const float TownOuterFeatherRadiusMul = 3.05f;

	public const float CoreNoiseAmpMul = 0.14f;
	public const float TransitionNoiseAmpMul = 0.52f;
	public const float OuterFeatherNoiseAmpMul = 0.88f;

	public const float CoreHeightBlend = 0.32f;
	public const float TransitionHeightBlend = 0.20f;
	public const float OuterFeatherHeightBlend = 0.10f;

	public const float DirectionalFeatherMaxBoost = 0.38f;

	public static IReadOnlyList<ThornsWorldSettlementInfluenceZone> LastZones { get; private set; } =
		Array.Empty<ThornsWorldSettlementInfluenceZone>();

	public static IReadOnlyList<ThornsSettlementTerrainDirectionalDebugCell> LastDirectionalDebugCells { get; private set; } =
		Array.Empty<ThornsSettlementTerrainDirectionalDebugCell>();

	public static void RegisterFromPlan(
		ThornsWorldSettlementPlan plan,
		ThornsTerrainNetSpec spec,
		ReadOnlySpan<float> heights,
		float worldWidth,
		float worldDepth )
	{
		if ( plan is null || spec is null )
			return;

		spec.SettlementTerrainInfluences ??= new List<ThornsSettlementTerrainInfluenceNet>();
		spec.SettlementTerrainInfluences.Clear();

		var rx = Math.Max( 2, spec.HeightmapResolutionX );
		var rz = Math.Max( 2, spec.HeightmapResolutionZ );
		var zones = new List<ThornsWorldSettlementInfluenceZone>( 4 );

		RegisterHub(
			plan.MainCity.CenterLocal,
			plan.MainCity.Radius,
			ThornsWorldSettlementKind.MainCity,
			spec,
			heights,
			rx,
			rz,
			worldWidth,
			worldDepth,
			spec.CenterOnWorldOrigin,
			zones );

		for ( var i = 0; i < plan.Towns.Count; i++ )
		{
			RegisterHub(
				plan.Towns[i].CenterLocal,
				plan.Towns[i].Radius,
				ThornsWorldSettlementKind.Town,
				spec,
				heights,
				rx,
				rz,
				worldWidth,
				worldDepth,
				spec.CenterOnWorldOrigin,
				zones );
		}

		LastZones = zones;
	}

	/// <summary>Blend influence + localized edge reconciliation (call after roads when applicable).</summary>
	public static void ApplyToHeightmap(
		in ThornsTerrainNetSpec spec,
		Span<float> heights,
		bool reconcile = true,
		bool collectDirectionalDebug = false )
	{
		ApplyInfluenceBlend( in spec, heights, collectDirectionalDebug );
		if ( reconcile )
			ThornsSettlementTerrainReconciliation.ReconcileSettlementEdges( spec, heights, collectDirectionalDebug );
	}

	static void ApplyInfluenceBlend(
		in ThornsTerrainNetSpec spec,
		Span<float> heights,
		bool collectDirectionalDebug )
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
		var halfW = spec.CenterOnWorldOrigin ? worldW * 0.5f : 0f;
		var halfD = spec.CenterOnWorldOrigin ? worldD * 0.5f : 0f;
		var directionalDebug = collectDirectionalDebug ? new List<ThornsSettlementTerrainDirectionalDebugCell>( 400 ) : null;

		for ( var gy = 0; gy < rz; gy++ )
		{
			var wy = gy * cellY - halfD;
			var row = gy * rx;
			for ( var gx = 0; gx < rx; gx++ )
			{
				var wx = gx * cellX - halfW;
				var i = row + gx;
				var hNatural = heights[i];
				var bestW = 0f;
				var bestTz = hNatural;
				var bestCore = 0f;
				ThornsSettlementTerrainInfluenceNet bestInf = null;

				for ( var n = 0; n < influences.Count; n++ )
				{
					var inf = influences[n];
					var dx = wx - inf.CenterX;
					var dy = wy - inf.CenterY;
					var planar = MathF.Sqrt( dx * dx + dy * dy );
					if ( planar >= inf.OuterFeatherRadius )
						continue;

					ComputeRingWeights(
						planar,
						inf.CoreRadius,
						inf.TransitionRadius,
						inf.OuterFeatherRadius,
						out var coreW,
						out var transW,
						out var outerW );

					var w = MathF.Max( coreW * CoreHeightBlend,
						MathF.Max( transW * TransitionHeightBlend, outerW * OuterFeatherHeightBlend ) );

					w += ComputeDirectionalBlendBoost(
						wx,
						wy,
						inf,
						hNatural,
						planar,
						heights,
						rx,
						rz,
						cellX,
						cellY,
						worldW,
						worldD,
						spec.CenterOnWorldOrigin,
						directionalDebug );

					w = Math.Clamp( w, 0f, 1f );
					if ( w <= bestW + 1e-5f )
						continue;

					bestW = w;
					var anchorZ = ResolveInfluenceAnchorZ( in spec, wx, wy, inf );
					bestTz = EffectiveTargetZ( planar, inf, hNatural, anchorZ );
					bestCore = coreW;
					bestInf = inf;
				}

				if ( bestW <= 0.001f || bestInf is null )
					continue;

				var blended = hNatural + (bestTz - hNatural) * bestW;
				if ( bestCore >= 0.32f )
					heights[i] = blended;
				else if ( hNatural > bestTz )
					heights[i] = blended;
				else
					heights[i] = hNatural + (bestTz - hNatural) * bestW * 0.4f;
			}
		}

		LastDirectionalDebugCells = directionalDebug is not null
			? directionalDebug
			: Array.Empty<ThornsSettlementTerrainDirectionalDebugCell>();
	}

	static float ResolveInfluenceAnchorZ(
		in ThornsTerrainNetSpec spec,
		float wx,
		float wy,
		ThornsSettlementTerrainInfluenceNet inf )
	{
		var blockZ = ThornsWorldSettlementBlockTerrain.SampleBlockTargetAt( in spec, wx, wy );
		if ( float.IsNaN( blockZ ) )
			return inf.TargetZ;

		return blockZ * (1f - ThornsWorldSettlementBlockTerrain.MacroHubBlendWeight)
		       + inf.TargetZ * ThornsWorldSettlementBlockTerrain.MacroHubBlendWeight;
	}

	public static void SyncHubTargetsFromBlocks( ThornsTerrainNetSpec spec )
	{
		var influences = spec?.SettlementTerrainInfluences;
		var blocks = spec?.SettlementBlockTerrain;
		if ( influences is null || blocks is null || blocks.Count == 0 )
			return;

		for ( var i = 0; i < influences.Count; i++ )
		{
			var inf = influences[i];
			var sum = 0f;
			var count = 0;
			for ( var b = 0; b < blocks.Count; b++ )
			{
				var block = blocks[b];
				var dx = block.CenterX - inf.CenterX;
				var dy = block.CenterY - inf.CenterY;
				if ( dx * dx + dy * dy > inf.HubRadius * inf.HubRadius * 1.05f )
					continue;

				sum += block.TargetZ;
				count++;
			}

			if ( count > 0 )
			{
				var avg = sum / count;
				inf.TargetZ = avg * (1f - ThornsWorldSettlementBlockTerrain.MacroHubBlendWeight)
				              + inf.TargetZ * ThornsWorldSettlementBlockTerrain.MacroHubBlendWeight;
			}
		}
	}

	/// <summary>Target height eases to natural terrain at outer rings — prevents retaining walls.</summary>
	static float EffectiveTargetZ( float planar, ThornsSettlementTerrainInfluenceNet inf, float hNatural, float hubTargetZ )
	{
		if ( planar <= inf.CoreRadius )
			return hubTargetZ;

		if ( planar <= inf.TransitionRadius )
		{
			var t = (planar - inf.CoreRadius) / MathF.Max( inf.TransitionRadius - inf.CoreRadius, 1f );
			t = Smooth( t );
			return hubTargetZ + (hNatural - hubTargetZ) * t * 0.92f;
		}

		if ( planar <= inf.OuterFeatherRadius )
		{
			var t = (planar - inf.TransitionRadius) / MathF.Max( inf.OuterFeatherRadius - inf.TransitionRadius, 1f );
			t = Smooth( t );
			return hNatural + (hubTargetZ - hNatural) * (1f - t) * 0.18f;
		}

		return hNatural;
	}

	static float ComputeDirectionalBlendBoost(
		float wx,
		float wy,
		ThornsSettlementTerrainInfluenceNet inf,
		float hNatural,
		float planar,
		Span<float> heights,
		int rx,
		int rz,
		float cellX,
		float cellY,
		float worldW,
		float worldD,
		bool centerOnOrigin,
		List<ThornsSettlementTerrainDirectionalDebugCell> debugOut )
	{
		if ( planar < inf.CoreRadius || planar >= inf.OuterFeatherRadius )
			return 0f;

		var gx = (int)MathF.Round( (wx + (centerOnOrigin ? worldW * 0.5f : 0f)) / MathF.Max( cellX, 1f ) );
		var gy = (int)MathF.Round( (wy + (centerOnOrigin ? worldD * 0.5f : 0f)) / MathF.Max( cellY, 1f ) );
		gx = Math.Clamp( gx, 1, rx - 2 );
		gy = Math.Clamp( gy, 1, rz - 2 );

		var i = gy * rx + gx;
		var dhdx = (heights[i + 1] - heights[i - 1]) / (cellX * 2f );
		var dhdy = (heights[i + rx] - heights[i - rx]) / (cellY * 2f );
		var gradLen = MathF.Sqrt( dhdx * dhdx + dhdy * dhdy );
		if ( gradLen < 0.02f )
			return 0f;

		var toX = wx - inf.CenterX;
		var toY = wy - inf.CenterY;
		var toLen = MathF.Sqrt( toX * toX + toY * toY );
		if ( toLen < 1f )
			return 0f;

		toX /= toLen;
		toY /= toLen;
		var downX = -dhdx / gradLen;
		var downY = -dhdy / gradLen;
		var downhillAlign = MathF.Max( 0f, toX * downX + toY * downY );

		var outwardLow = 0f;
		var sampleStep = MathF.Max( cellX, cellY ) * 2.5f;
		var ox = wx + toX * sampleStep;
		var oy = wy + toY * sampleStep;
		var hOut = ThornsTerrainGeometry.SampleHeightLocalZUp(
			heights,
			rx,
			rz,
			worldW,
			worldD,
			centerOnOrigin,
			ox,
			oy );
		if ( !float.IsNaN( hOut ) )
			outwardLow = MathF.Max( 0f, hNatural - hOut );

		var cliff = MathF.Max( 0f, gradLen - 0.38f );
		var ringT = planar < inf.TransitionRadius
			? (planar - inf.CoreRadius) / MathF.Max( inf.TransitionRadius - inf.CoreRadius, 1f )
			: (planar - inf.TransitionRadius) / MathF.Max( inf.OuterFeatherRadius - inf.TransitionRadius, 1f );

		var boost = DirectionalFeatherMaxBoost
		            * downhillAlign
		            * (0.35f + 0.65f * MathF.Min( outwardLow / 72f, 1f ))
		            * (0.4f + 0.6f * MathF.Min( cliff / 0.35f, 1f ))
		            * (0.55f + 0.45f * ringT);

		if ( debugOut is not null && boost > 0.06f && debugOut.Count < 500 )
		{
			debugOut.Add( new ThornsSettlementTerrainDirectionalDebugCell
			{
				LocalX = wx,
				LocalY = wy,
				Boost = boost,
				DownhillAlign = downhillAlign
			} );
		}

		return boost;
	}

	public static float SampleNoiseAmplitudeMul( float wx, float wy, in ThornsTerrainNetSpec spec )
	{
		var influences = spec.SettlementTerrainInfluences;
		if ( influences is null || influences.Count == 0 )
			return 1f;

		var minMul = 1f;
		for ( var n = 0; n < influences.Count; n++ )
		{
			var inf = influences[n];
			var dx = wx - inf.CenterX;
			var dy = wy - inf.CenterY;
			var planar = MathF.Sqrt( dx * dx + dy * dy );
			if ( planar >= inf.OuterFeatherRadius )
				continue;

			ComputeRingWeights(
				planar,
				inf.CoreRadius,
				inf.TransitionRadius,
				inf.OuterFeatherRadius,
				out var coreW,
				out var transW,
				out var outerW );

			float amp;
			if ( coreW > 0.02f )
				amp = CoreNoiseAmpMul + (TransitionNoiseAmpMul - CoreNoiseAmpMul) * (1f - coreW);
			else if ( transW > 0.02f )
				amp = TransitionNoiseAmpMul + (OuterFeatherNoiseAmpMul - TransitionNoiseAmpMul) * (1f - transW);
			else if ( outerW > 0.02f )
				amp = OuterFeatherNoiseAmpMul + (1f - OuterFeatherNoiseAmpMul) * (1f - outerW);
			else
				amp = 1f;

			minMul = MathF.Min( minMul, amp );
		}

		return minMul;
	}

	public static void UpdateTargetFromPlacements(
		ThornsTerrainNetSpec spec,
		Vector2 hubCenter,
		float hubRadius,
		IReadOnlyList<ThornsWorldGenProcBuildingFootprint> footprints,
		float fallbackZ )
	{
		if ( spec.SettlementTerrainInfluences is null )
			return;

		var refined = ThornsWorldGenSettlementPlateau.RefineHubPlateauFromPlacements(
			footprints,
			hubCenter,
			hubRadius,
			fallbackZ );

		for ( var i = 0; i < spec.SettlementTerrainInfluences.Count; i++ )
		{
			var inf = spec.SettlementTerrainInfluences[i];
			if ( MathF.Abs( inf.CenterX - hubCenter.x ) > 4f || MathF.Abs( inf.CenterY - hubCenter.y ) > 4f )
				continue;

			inf.TargetZ = refined;
			return;
		}
	}

	static void RegisterHub(
		Vector2 center,
		float hubRadius,
		ThornsWorldSettlementKind kind,
		ThornsTerrainNetSpec spec,
		ReadOnlySpan<float> heights,
		int rx,
		int rz,
		float ww,
		float wd,
		bool centerOnOrigin,
		List<ThornsWorldSettlementInfluenceZone> zones )
	{
		if ( hubRadius <= 48f )
			return;

		var isCity = kind == ThornsWorldSettlementKind.MainCity;
		var coreR = hubRadius * ( isCity ? CityCoreRadiusMul : TownCoreRadiusMul );
		var transR = hubRadius * ( isCity ? CityTransitionRadiusMul : TownTransitionRadiusMul );
		var outerR = hubRadius * ( isCity ? CityOuterFeatherRadiusMul : TownOuterFeatherRadiusMul );

		var rimZ = SampleRimHeight( heights, rx, rz, ww, wd, centerOnOrigin, center, transR * 0.94f );
		var centerZ = SampleH( heights, rx, rz, ww, wd, centerOnOrigin, center.x, center.y );
		var targetZ = rimZ * 0.72f + centerZ * 0.28f;

		spec.SettlementTerrainInfluences.Add( new ThornsSettlementTerrainInfluenceNet
		{
			CenterX = center.x,
			CenterY = center.y,
			HubRadius = hubRadius,
			CoreRadius = coreR,
			TransitionRadius = transR,
			OuterFeatherRadius = outerR,
			TargetZ = targetZ,
			Kind = kind
		} );

		zones.Add( new ThornsWorldSettlementInfluenceZone
		{
			Kind = kind,
			CenterLocal = center,
			InfluenceRadius = hubRadius,
			CoreRadius = coreR,
			TransitionRadius = transR,
			OuterFeatherRadius = outerR,
			TargetSurfaceZ = targetZ,
			BlendOuterRadius = outerR
		} );
	}

	public static void ComputeRingWeights(
		float planar,
		float coreR,
		float transitionR,
		float outerR,
		out float coreW,
		out float transitionW,
		out float outerW )
	{
		coreW = transitionW = outerW = 0f;
		if ( planar >= outerR || outerR <= 1f )
			return;

		if ( planar <= coreR && coreR > 1f )
		{
			var t = planar / coreR;
			coreW = 1f - Smooth( t ) * 0.1f;
			return;
		}

		if ( planar <= transitionR && transitionR > coreR )
		{
			var t = (planar - coreR) / MathF.Max( transitionR - coreR, 1f );
			transitionW = 1f - Smooth( t );
			return;
		}

		var ot = (planar - transitionR) / MathF.Max( outerR - transitionR, 1f );
		outerW = 1f - Smooth( ot );
	}

	static float Smooth( float t )
	{
		t = Math.Clamp( t, 0f, 1f );
		return t * t * (3f - 2f * t);
	}

	static float SampleRimHeight(
		ReadOnlySpan<float> heights,
		int rx,
		int rz,
		float ww,
		float wd,
		bool centerOnOrigin,
		Vector2 center,
		float ringRadius )
	{
		var sum = 0f;
		var count = 0;
		const int n = 24;
		for ( var i = 0; i < n; i++ )
		{
			var ang = i * ( MathF.PI * 2f / n );
			var h = SampleH(
				heights,
				rx,
				rz,
				ww,
				wd,
				centerOnOrigin,
				center.x + MathF.Cos( ang ) * ringRadius,
				center.y + MathF.Sin( ang ) * ringRadius );
			sum += h;
			count++;
		}

		return count > 0 ? sum / count : 0f;
	}

	static float SampleH(
		ReadOnlySpan<float> heights,
		int rx,
		int rz,
		float ww,
		float wd,
		bool centerOnOrigin,
		float lx,
		float ly )
	{
		var h = ThornsTerrainGeometry.SampleHeightLocalZUp( heights, rx, rz, ww, wd, centerOnOrigin, lx, ly );
		return float.IsNaN( h ) || float.IsInfinity( h ) ? 0f : h;
	}
}

public readonly struct ThornsSettlementTerrainDirectionalDebugCell
{
	public float LocalX { get; init; }
	public float LocalY { get; init; }
	public float Boost { get; init; }
	public float DownhillAlign { get; init; }
}
