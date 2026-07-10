using System;
using System.Collections.Generic;

namespace Sandbox;

/// <summary>Phase 6 — overlap, slope, terrain suitability, and rotation tests before a footprint is committed.</summary>
public sealed class ThornsWorldGenFootprintReservation
{
	readonly ThornsWorldGenerationContext _ctx;
	readonly List<ThornsWorldGenProcBuildingFootprint> _placed;
	readonly ThornsWorldGenerationHostBridge _host;

	public const float DefaultSlopeMax = 42f;
	public const float CitySlopeMax = 95f;

	public ThornsWorldGenFootprintReservation(
		ThornsWorldGenerationContext context,
		ThornsWorldGenerationHostBridge host,
		List<ThornsWorldGenProcBuildingFootprint> placedFootprints )
	{
		_ctx = context;
		_host = host;
		_placed = placedFootprints;
	}

	public float SettlementFootprintEdgeGap( float spacingMul, ThornsWorldSettlementKind kind )
	{
		var mul = Math.Clamp( spacingMul, 0.35f, 1.75f );
		var baseGap = ThornsBuildingModule.Cell * ( 0.55f + 0.7f * mul );
		return kind == ThornsWorldSettlementKind.MainCity ? baseGap * 0.44f : baseGap * 0.85f;
	}

	public bool TrySampleHeight( float lx, float ly, out float h )
	{
		h = ThornsTerrainGeometry.SampleHeightLocalZUp(
			_ctx.HeightsSpan,
			_ctx.HeightRx,
			_ctx.HeightRz,
			_ctx.WorldWidth,
			_ctx.WorldDepth,
			_ctx.Spec.CenterOnWorldOrigin,
			lx,
			ly );
		return ThornsTerrainSystem.IsSpawnableLandHeight( _ctx.Spec, h );
	}

	public bool TryValidateFootprint(
		float lx,
		float ly,
		float halfW,
		float halfD,
		float yawRad,
		float maxSlope,
		ThornsWorldSettlementKind settlementKind,
		out float baseZ,
		out string rejectReason ) =>
		TryValidateFootprint(
			lx,
			ly,
			halfW,
			halfD,
			yawRad,
			maxSlope,
			settlementKind,
			drawDebugOnReject: true,
			out baseZ,
			out rejectReason,
			out _ );

	public bool TryValidateFootprint(
		float lx,
		float ly,
		float halfW,
		float halfD,
		float yawRad,
		float maxSlope,
		ThornsWorldSettlementKind settlementKind,
		bool drawDebugOnReject,
		out float baseZ,
		out string rejectReason,
		out ThornsWorldSettlementPlacementFailureReason failureReason )
	{
		rejectReason = null;
		failureReason = ThornsWorldSettlementPlacementFailureReason.Unknown;
		baseZ = 0f;

		// Main city lots get macro terrain feather pads at spawn; macro heightmap checks here reject most candidates.
		if ( settlementKind == ThornsWorldSettlementKind.MainCity )
		{
			if ( !TryResolveFootprintBaseZ( lx, ly, halfW, halfD, out baseZ ) )
			{
				rejectReason = "InvalidCenterHeight";
				failureReason = ThornsWorldSettlementPlacementFailureReason.Unknown;
				return false;
			}

			return true;
		}

		if ( !ThornsWorldSettlementTerrainValidation.TryEvaluateFootprint(
			     _ctx.HeightsSpan,
			     _ctx.Spec,
			     _ctx.WorldWidth,
			     _ctx.WorldDepth,
			     lx,
			     ly,
			     halfW,
			     halfD,
			     yawRad,
			     settlementKind,
			     out var terrainMetrics,
			     out rejectReason ) )
		{
			failureReason = ClassifyTerrainRejectReason( rejectReason );
			ThornsWorldSettlementTerrainDiagnostics.RecordTerrainValidationRejected();
			ThornsWorldGenerationQaMetrics.RecordTerrainReject( failureReason );
			if ( drawDebugOnReject && _host.DrawSettlementLayoutDebug )
			{
				ThornsWorldSettlementTerrainDebugViz.DrawRejectedFootprint(
					_host.Scene,
					_host.ChunkRoot,
					lx,
					ly,
					halfW,
					halfD,
					rejectReason );
			}

			return false;
		}

		ThornsWorldSettlementTerrainDiagnostics.RecordTerrainValidationPassed();

		if ( !TryResolveFootprintBaseZ( lx, ly, halfW, halfD, out baseZ ) )
		{
			rejectReason = "InvalidCenterHeight";
			failureReason = ThornsWorldSettlementPlacementFailureReason.Unknown;
			return false;
		}

		if ( !TrySampleHeight( lx - halfW, ly - halfD, out var h00 ) ) return false;
		if ( !TrySampleHeight( lx + halfW, ly - halfD, out var h10 ) ) return false;
		if ( !TrySampleHeight( lx - halfW, ly + halfD, out var h01 ) ) return false;
		if ( !TrySampleHeight( lx + halfW, ly + halfD, out var h11 ) ) return false;
		var minH = MathF.Min( MathF.Min( h00, h10 ), MathF.Min( h01, h11 ) );
		var maxH = MathF.Max( MathF.Max( h00, h10 ), MathF.Max( h01, h11 ) );
		if ( maxH - minH > maxSlope )
		{
			rejectReason = $"LegacySlope={maxH - minH:F0}>{maxSlope:F0}";
			failureReason = ThornsWorldSettlementPlacementFailureReason.TerrainSlope;
			return false;
		}

		failureReason = ThornsWorldSettlementPlacementFailureReason.Unknown;
		return true;
	}

	bool TryResolveFootprintBaseZ( float lx, float ly, float halfW, float halfD, out float baseZ )
	{
		baseZ = 0f;
		var floorT = ThornsBuildingModule.FloorThickness;
		if ( !TrySampleHeight( lx, ly, out _ ) )
			return false;

		if ( !TrySampleHeight( lx - halfW, ly - halfD, out var h00 ) ) return false;
		if ( !TrySampleHeight( lx + halfW, ly - halfD, out var h10 ) ) return false;
		if ( !TrySampleHeight( lx - halfW, ly + halfD, out var h01 ) ) return false;
		if ( !TrySampleHeight( lx + halfW, ly + halfD, out var h11 ) ) return false;
		var minH = MathF.Min( MathF.Min( h00, h10 ), MathF.Min( h01, h11 ) );
		baseZ = minH + floorT * 0.5f;
		return true;
	}

	/// <summary>Fast overlap + terrain check using registry max footprint before layout compile.</summary>
	public bool TryPreviewFootprint(
		float lx,
		float ly,
		float estHalfW,
		float estHalfD,
		float yawRad,
		float edgeGap,
		float maxSlope,
		ThornsWorldSettlementKind settlementKind,
		Rotation chunkWorldRot,
		out ThornsWorldSettlementPlacementFailureReason failureReason,
		IReadOnlyList<ThornsWorldRoadCorridor> roadCorridors = null )
	{
		failureReason = ThornsWorldSettlementPlacementFailureReason.Unknown;
		roadCorridors ??= _ctx.Spec.RoadCorridors;

		if ( ThornsWorldSettlementRoadCorridors.FootprintIntersectsCorridor(
			     lx, ly, estHalfW, estHalfD, roadCorridors ) )
		{
			failureReason = ThornsWorldSettlementPlacementFailureReason.Overlap;
			return false;
		}

		if ( FootprintOverlapsPlaced( lx, ly, estHalfW, estHalfD, yawRad, edgeGap, chunkWorldRot, settlementKind ) )
		{
			failureReason = ThornsWorldSettlementPlacementFailureReason.Overlap;
			return false;
		}

		if ( !TryValidateFootprint(
			     lx,
			     ly,
			     estHalfW,
			     estHalfD,
			     yawRad,
			     maxSlope,
			     settlementKind,
			     drawDebugOnReject: false,
			     out _,
			     out var terrainReject,
			     out failureReason ) )
		{
			if ( failureReason == ThornsWorldSettlementPlacementFailureReason.Unknown )
				failureReason = ClassifyTerrainRejectReason( terrainReject );
			return false;
		}

		return true;
	}

	public static ThornsWorldSettlementPlacementFailureReason ClassifyTerrainRejectReason( string rejectReason )
	{
		if ( string.IsNullOrEmpty( rejectReason ) )
			return ThornsWorldSettlementPlacementFailureReason.Unknown;

		if ( rejectReason.StartsWith( "CornerDelta", StringComparison.Ordinal ) )
			return ThornsWorldSettlementPlacementFailureReason.TerrainCornerDelta;
		if ( rejectReason.StartsWith( "MaxSlope", StringComparison.Ordinal )
		     || rejectReason.StartsWith( "LegacySlope", StringComparison.Ordinal ) )
			return ThornsWorldSettlementPlacementFailureReason.TerrainSlope;
		if ( rejectReason.StartsWith( "Variance", StringComparison.Ordinal ) )
			return ThornsWorldSettlementPlacementFailureReason.TerrainVariance;
		if ( rejectReason.StartsWith( "CliffSeverity", StringComparison.Ordinal ) )
			return ThornsWorldSettlementPlacementFailureReason.CliffSeverity;

		return ThornsWorldSettlementPlacementFailureReason.Unknown;
	}

	public bool FootprintOverlapsPlaced(
		float lx,
		float ly,
		float halfW,
		float halfD,
		float yawRad,
		float edgeGap,
		Rotation chunkWorldRot,
		ThornsWorldSettlementKind settlementKind = ThornsWorldSettlementKind.Town )
	{
		var wallPad = ThornsBuildingModule.Cell
		              * ( settlementKind == ThornsWorldSettlementKind.MainCity ? 0.1f : 0.35f );
		var gap = edgeGap + wallPad;

		for ( var i = 0; i < _placed.Count; i++ )
		{
			var fp = _placed[i];
			if ( ThornsProcBuildingFootprintOverlap.ObbsOverlap(
				     lx,
				     ly,
				     halfW,
				     halfD,
				     yawRad,
				     fp.CenterX,
				     fp.CenterY,
				     fp.HalfW,
				     fp.HalfD,
				     fp.YawRad,
				     gap ) )
				return true;
		}

		return false;
	}

	public bool TryPickYaw(
		float lx,
		float ly,
		float halfW,
		float halfD,
		float edgeGap,
		Rotation chunkWorldRot,
		Random rnd,
		out float yawDeg,
		out float yawRad ) =>
		TryPickYaw( lx, ly, halfW, halfD, edgeGap, chunkWorldRot, rnd, attemptIndex: 0, out yawDeg, out yawRad );

	public bool TryPickYaw(
		float lx,
		float ly,
		float halfW,
		float halfD,
		float edgeGap,
		Rotation chunkWorldRot,
		Random rnd,
		int attemptIndex,
		out float yawDeg,
		out float yawRad )
	{
		yawDeg = 0f;
		yawRad = 0f;
		var startYawSlot = ( rnd.Next( 0, 4 ) + attemptIndex ) % 4;
		for ( var rot = 0; rot < 4; rot++ )
		{
			var tryYawDeg = 90f * ( ( startYawSlot + rot ) % 4 );
			var tryYawRad = ThornsTerrainSystem.CombinedPlanarYawRadians( chunkWorldRot, tryYawDeg );
			if ( FootprintOverlapsPlaced( lx, ly, halfW, halfD, tryYawRad, edgeGap, chunkWorldRot ) )
				continue;

			yawDeg = tryYawDeg;
			yawRad = tryYawRad;
			return true;
		}

		return false;
	}

	/// <summary>Finds the first yaw (4 cardinal) that passes overlap + terrain preview.</summary>
	public bool TryPickYawWithPreview(
		float lx,
		float ly,
		float halfW,
		float halfD,
		float edgeGap,
		float maxSlope,
		ThornsWorldSettlementKind settlementKind,
		Rotation chunkWorldRot,
		Random rnd,
		int attemptIndex,
		out float yawDeg,
		out float yawRad,
		out ThornsWorldSettlementPlacementFailureReason failureReason,
		IReadOnlyList<ThornsWorldRoadCorridor> roadCorridors = null,
		float? preferredYawRad = null )
	{
		yawDeg = 0f;
		yawRad = 0f;
		failureReason = ThornsWorldSettlementPlacementFailureReason.NoValidYaw;
		roadCorridors ??= _ctx.Spec.RoadCorridors;
		var startYawSlot = ( rnd.Next( 0, 4 ) + attemptIndex ) % 4;

		if ( preferredYawRad.HasValue )
		{
			var tryYawRad = preferredYawRad.Value;
			if ( !TryResolveLocalYawDegrees( chunkWorldRot, tryYawRad, out var tryYawDeg ) )
				tryYawDeg = tryYawRad * ( 180f / MathF.PI );

			if ( TryYawCandidate(
				     lx, ly, halfW, halfD, edgeGap, maxSlope, settlementKind, chunkWorldRot, roadCorridors,
				     tryYawDeg, tryYawRad, out failureReason ) )
			{
				yawDeg = tryYawDeg;
				yawRad = tryYawRad;
				return true;
			}
		}

		for ( var rot = 0; rot < 4; rot++ )
		{
			var tryYawDeg = 90f * ( ( startYawSlot + rot ) % 4 );
			var tryYawRad = ThornsTerrainSystem.CombinedPlanarYawRadians( chunkWorldRot, tryYawDeg );

			if ( TryYawCandidate(
				     lx, ly, halfW, halfD, edgeGap, maxSlope, settlementKind, chunkWorldRot, roadCorridors,
				     tryYawDeg, tryYawRad, out failureReason ) )
			{
				yawDeg = tryYawDeg;
				yawRad = tryYawRad;
				return true;
			}
		}

		return false;
	}

	bool TryYawCandidate(
		float lx,
		float ly,
		float halfW,
		float halfD,
		float edgeGap,
		float maxSlope,
		ThornsWorldSettlementKind settlementKind,
		Rotation chunkWorldRot,
		IReadOnlyList<ThornsWorldRoadCorridor> roadCorridors,
		float tryYawDeg,
		float tryYawRad,
		out ThornsWorldSettlementPlacementFailureReason failureReason )
	{
		failureReason = ThornsWorldSettlementPlacementFailureReason.Unknown;
		_ = tryYawDeg;

		if ( ThornsWorldSettlementRoadCorridors.FootprintIntersectsCorridor(
			     lx, ly, halfW, halfD, roadCorridors ) )
		{
			failureReason = ThornsWorldSettlementPlacementFailureReason.Overlap;
			return false;
		}

		if ( FootprintOverlapsPlaced( lx, ly, halfW, halfD, tryYawRad, edgeGap, chunkWorldRot, settlementKind ) )
		{
			failureReason = ThornsWorldSettlementPlacementFailureReason.Overlap;
			return false;
		}

		if ( !TryPreviewFootprint(
			     lx,
			     ly,
			     halfW,
			     halfD,
			     tryYawRad,
			     edgeGap,
			     maxSlope,
			     settlementKind,
			     chunkWorldRot,
			     out failureReason,
			     roadCorridors ) )
			return false;

		return true;
	}

	public static bool TryResolveLocalYawDegrees( Rotation chunkWorldRot, float targetYawRad, out float yawDeg )
	{
		for ( var d = 0; d < 360; d += 90 )
		{
			if ( MathF.Abs( ThornsTerrainSystem.CombinedPlanarYawRadians( chunkWorldRot, d ) - targetYawRad ) < 0.12f )
			{
				yawDeg = d;
				return true;
			}
		}

		yawDeg = targetYawRad * ( 180f / MathF.PI );
		return false;
	}

	public void Commit( float lx, float ly, float halfW, float halfD, float yawRad, float floorSurfaceZ = float.NaN ) =>
		_placed.Add( new ThornsWorldGenProcBuildingFootprint( lx, ly, halfW, halfD, yawRad, floorSurfaceZ ) );
}
