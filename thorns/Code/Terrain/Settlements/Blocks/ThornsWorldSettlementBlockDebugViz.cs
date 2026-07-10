namespace Sandbox;

/// <summary>Block, lot, corridor, and frontage debug overlays.</summary>
public static class ThornsWorldSettlementBlockDebugViz
{
	public static void Draw(
		Scene scene,
		GameObject chunkRoot,
		ThornsWorldSettlementBlockPlan blockPlan,
		float durationSeconds = 90f )
	{
		if ( scene is null || chunkRoot is null || !chunkRoot.IsValid() || blockPlan is null || !blockPlan.IsPopulated )
			return;

		var dbg = scene.GetSystem<DebugOverlaySystem>();
		if ( dbg is null )
			return;

		var wt = chunkRoot.Transform.World;

		DrawCorridors( dbg, wt, blockPlan.InterSettlementCorridors, new Color( 0.45f, 0.35f, 0.28f, 0.5f ), durationSeconds );

		if ( blockPlan.Areas is null )
			return;

		foreach ( var area in blockPlan.Areas )
		{
			DrawCorridors( dbg, wt, area.Corridors, new Color( 0.55f, 0.48f, 0.38f, 0.75f ), durationSeconds );

			if ( area.Districts is null )
				continue;

			foreach ( var district in area.Districts )
			{
				var dCol = DistrictColor( district.Kind );
				DrawDistrictRing( dbg, wt, area.CenterLocal, district.InnerRadius, district.OuterRadius, dCol, durationSeconds );

				foreach ( var block in district.Blocks )
					DrawBlock( dbg, wt, block, dCol, durationSeconds );
			}

			foreach ( var lot in area.Lots )
				DrawLot( dbg, wt, lot, durationSeconds );
		}
	}

	static void DrawCorridors(
		DebugOverlaySystem dbg,
		Transform wt,
		IReadOnlyList<ThornsWorldRoadCorridor> corridors,
		Color color,
		float duration )
	{
		if ( corridors is null )
			return;

		foreach ( var c in corridors )
		{
			var a = wt.PointToWorld( new Vector3( c.A.x, c.A.y, 76f ) );
			var b = wt.PointToWorld( new Vector3( c.B.x, c.B.y, 76f ) );
			dbg.Line( a, b, color, duration, default, false );
			var mid = ( a + b ) * 0.5f;
			dbg.Line( mid, mid + Vector3.Up * ( c.HalfWidth * 0.15f ), color.WithAlpha( color.a * 0.6f ), duration, default, false );
		}
	}

	static void DrawDistrictRing(
		DebugOverlaySystem dbg,
		Transform wt,
		Vector2 center,
		float innerR,
		float outerR,
		Color color,
		float duration )
	{
		if ( outerR <= innerR + 8f )
			return;

		DrawRing( dbg, wt, center, outerR, color.WithAlpha( color.a * 0.55f ), duration, 82f );
	}

	static void DrawBlock(
		DebugOverlaySystem dbg,
		Transform wt,
		ThornsWorldSettlementBlock block,
		Color color,
		float duration )
	{
		var c = block.CenterLocal;
		var cy = MathF.Cos( block.YawRadians );
		var sy = MathF.Sin( block.YawRadians );
		var corners = new (float bx, float by)[]
		{
			(-block.HalfW, -block.HalfD), (block.HalfW, -block.HalfD),
			(block.HalfW, block.HalfD), (-block.HalfW, block.HalfD)
		};

		for ( var i = 0; i < 4; i++ )
		{
			var (bx0, by0) = corners[i];
			var (bx1, by1) = corners[(i + 1) % 4];
			var wx0 = c.x + bx0 * cy - by0 * sy;
			var wy0 = c.y + bx0 * sy + by0 * cy;
			var wx1 = c.x + bx1 * cy - by1 * sy;
			var wy1 = c.y + bx1 * sy + by1 * cy;
			var a = wt.PointToWorld( new Vector3( wx0, wy0, 84f ) );
			var b = wt.PointToWorld( new Vector3( wx1, wy1, 84f ) );
			dbg.Line( a, b, color.WithAlpha( 0.45f ), duration, default, false );
		}
	}

	static void DrawLot( DebugOverlaySystem dbg, Transform wt, ThornsWorldSettlementLot lot, float duration )
	{
		var assigned = lot.State == ThornsWorldSettlementLotState.Assigned;
		var col = assigned
			? new Color( 0.3f, 0.85f, 1f, 0.85f )
			: new Color( 0.35f, 0.35f, 0.4f, 0.35f );
		var z = assigned ? 98f : 86f;
		var cy = MathF.Cos( lot.YawRadians );
		var sy = MathF.Sin( lot.YawRadians );
		var corners = new (float bx, float by)[]
		{
			(-lot.HalfW, -lot.HalfD), (lot.HalfW, -lot.HalfD),
			(lot.HalfW, lot.HalfD), (-lot.HalfW, lot.HalfD)
		};

		for ( var i = 0; i < 4; i++ )
		{
			var (bx0, by0) = corners[i];
			var (bx1, by1) = corners[(i + 1) % 4];
			var wx0 = lot.CenterLocal.x + bx0 * cy - by0 * sy;
			var wy0 = lot.CenterLocal.y + bx0 * sy + by0 * cy;
			var wx1 = lot.CenterLocal.x + bx1 * cy - by1 * sy;
			var wy1 = lot.CenterLocal.y + bx1 * sy + by1 * cy;
			var a = wt.PointToWorld( new Vector3( wx0, wy0, z ) );
			var b = wt.PointToWorld( new Vector3( wx1, wy1, z ) );
			dbg.Line( a, b, col, duration, default, false );
		}

		var fc = lot.CenterLocal;
		var fd = lot.FrontageDirection;
		if ( fd.LengthSquared > 0.0001f )
		{
			var p0 = wt.PointToWorld( new Vector3( fc.x, fc.y, z + 6f ) );
			var p1 = wt.PointToWorld( new Vector3( fc.x + fd.x * 48f, fc.y + fd.y * 48f, z + 6f ) );
			dbg.Line( p0, p1, new Color( 1f, 0.9f, 0.25f, 0.9f ), duration, default, false );
		}
	}

	static void DrawRing(
		DebugOverlaySystem dbg,
		Transform wt,
		Vector2 center,
		float radius,
		Color color,
		float duration,
		float height )
	{
		const int segments = 28;
		var prev = wt.PointToWorld( new Vector3( center.x + radius, center.y, height ) );
		for ( var i = 1; i <= segments; i++ )
		{
			var ang = i * ( MathF.PI * 2f / segments );
			var p = wt.PointToWorld( new Vector3( center.x + MathF.Cos( ang ) * radius, center.y + MathF.Sin( ang ) * radius, height ) );
			dbg.Line( prev, p, color, duration, default, false );
			prev = p;
		}
	}

	static Color DistrictColor( ThornsWorldSettlementDistrictKind kind ) =>
		kind switch
		{
			ThornsWorldSettlementDistrictKind.Core => new Color( 1f, 0.35f, 0.35f, 0.8f ),
			ThornsWorldSettlementDistrictKind.MidCommercialResidential => new Color( 1f, 0.78f, 0.2f, 0.75f ),
			ThornsWorldSettlementDistrictKind.OuterIndustrial => new Color( 0.4f, 0.7f, 1f, 0.7f ),
			ThornsWorldSettlementDistrictKind.TownCenter => new Color( 0.5f, 0.95f, 0.55f, 0.75f ),
			_ => new Color( 0.75f, 0.55f, 0.95f, 0.65f )
		};
}
