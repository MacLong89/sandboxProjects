using System.Collections.Generic;

namespace Sandbox;

/// <summary>Placement attempt overlays — accepted/rejected OBBs, ring zones, priority hints.</summary>
public static class ThornsWorldSettlementPlacementDebugViz
{
	public readonly struct FootprintDraw
	{
		public float CenterX { get; init; }
		public float CenterY { get; init; }
		public float HalfW { get; init; }
		public float HalfD { get; init; }
		public float YawRad { get; init; }
		public bool Accepted { get; init; }
		public ThornsWorldSettlementPlacementFailureReason? Reason { get; init; }
		public ThornsProcBuildingType BuildingType { get; init; }
	}

	static readonly List<FootprintDraw> _draws = new();
	const int MaxDraws = 256;

	public static void Clear() => _draws.Clear();

	public static void Record(
		float lx,
		float ly,
		float halfW,
		float halfD,
		float yawRad,
		bool accepted,
		ThornsProcBuildingType type,
		ThornsWorldSettlementPlacementFailureReason? reason = null )
	{
		if ( _draws.Count >= MaxDraws )
			return;

		_draws.Add( new FootprintDraw
		{
			CenterX = lx,
			CenterY = ly,
			HalfW = halfW,
			HalfD = halfD,
			YawRad = yawRad,
			Accepted = accepted,
			Reason = reason,
			BuildingType = type
		} );
	}

	public static void DrawAll(
		Scene scene,
		GameObject chunkRoot,
		ThornsWorldSettlementPlan plan,
		float durationSeconds = 90f )
	{
		if ( scene is null || chunkRoot is null || !chunkRoot.IsValid() )
			return;

		var dbg = scene.GetSystem<DebugOverlaySystem>();
		if ( dbg is null )
			return;

		var wt = chunkRoot.Transform.World;
		if ( plan is not null )
			DrawCityRingZones( dbg, wt, plan.MainCity, durationSeconds );

		for ( var i = 0; i < _draws.Count; i++ )
			DrawFootprint( dbg, wt, _draws[i], durationSeconds );
	}

	public static void DrawCityRingZones(
		DebugOverlaySystem dbg,
		Transform wt,
		ThornsWorldSettlementZone city,
		float duration )
	{
		var cell = ThornsBuildingModule.Cell;
		var center = wt.PointToWorld( new Vector3( city.CenterLocal.x, city.CenterLocal.y, 118f ) );
		DrawRing( dbg, center, cell * 4.5f, new Color( 1f, 0.3f, 0.35f, 0.75f ), duration, "core" );
		DrawRing( dbg, center, cell * 6.2f, new Color( 1f, 0.78f, 0.2f, 0.65f ), duration, null );
		DrawRing( dbg, center, cell * 5.4f, new Color( 0.35f, 0.75f, 1f, 0.55f ), duration, "outer" );
	}

	static void DrawRing(
		DebugOverlaySystem dbg,
		Vector3 center,
		float radius,
		Color color,
		float duration,
		string _ )
	{
		const int segments = 32;
		var step = MathF.PI * 2f / segments;
		var prev = center + new Vector3( radius, 0, 0 );
		for ( var i = 1; i <= segments; i++ )
		{
			var a = i * step;
			var next = center + new Vector3( MathF.Cos( a ) * radius, MathF.Sin( a ) * radius, 0 );
			dbg.Line( prev, next, color, duration, default, false );
			prev = next;
		}
	}

	static void DrawFootprint( DebugOverlaySystem dbg, Transform wt, FootprintDraw fp, float duration )
	{
		var col = fp.Accepted
			? new Color( 0.25f, 0.95f, 0.45f, 0.9f )
			: ReasonColor( fp.Reason ?? ThornsWorldSettlementPlacementFailureReason.Unknown );
		var z = fp.Accepted ? 104f : 92f;
		var cy = MathF.Cos( fp.YawRad );
		var sy = MathF.Sin( fp.YawRad );
		var corners = new (float bx, float by)[]
		{
			(-fp.HalfW, -fp.HalfD), (fp.HalfW, -fp.HalfD), (fp.HalfW, fp.HalfD), (-fp.HalfW, fp.HalfD)
		};

		for ( var i = 0; i < 4; i++ )
		{
			var (bx0, by0) = corners[i];
			var (bx1, by1) = corners[(i + 1) % 4];
			var wx0 = fp.CenterX + bx0 * cy - by0 * sy;
			var wy0 = fp.CenterY + bx0 * sy + by0 * cy;
			var wx1 = fp.CenterX + bx1 * cy - by1 * sy;
			var wy1 = fp.CenterY + bx1 * sy + by1 * cy;
			var a = wt.PointToWorld( new Vector3( wx0, wy0, z ) );
			var b = wt.PointToWorld( new Vector3( wx1, wy1, z ) );
			dbg.Line( a, b, col, duration, default, false );
		}

		if ( !fp.Accepted && fp.Reason.HasValue )
		{
			var p = wt.PointToWorld( new Vector3( fp.CenterX, fp.CenterY, z + 24f ) );
			dbg.Line( p, p + Vector3.Up * 36f, col, duration, default, false );
		}
	}

	static Color ReasonColor( ThornsWorldSettlementPlacementFailureReason reason ) =>
		reason switch
		{
			ThornsWorldSettlementPlacementFailureReason.Overlap => new Color( 1f, 0.55f, 0.1f, 0.85f ),
			ThornsWorldSettlementPlacementFailureReason.BlueprintInvalid => new Color( 0.85f, 0.2f, 0.95f, 0.85f ),
			ThornsWorldSettlementPlacementFailureReason.FallbackInvalid => new Color( 0.7f, 0.15f, 0.8f, 0.85f ),
			ThornsWorldSettlementPlacementFailureReason.NoValidYaw => new Color( 0.95f, 0.95f, 0.25f, 0.85f ),
			_ => new Color( 1f, 0.22f, 0.18f, 0.85f )
		};
}
