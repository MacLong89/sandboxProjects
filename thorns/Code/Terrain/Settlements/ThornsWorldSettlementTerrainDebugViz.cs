namespace Sandbox;

/// <summary>Multi-ring settlement terrain debug overlays.</summary>
public static class ThornsWorldSettlementTerrainDebugViz
{
	public static void DrawMacroInfluence(
		Scene scene,
		GameObject chunkRoot,
		float durationSeconds = 90f )
	{
		if ( scene is null || chunkRoot is null || !chunkRoot.IsValid() )
			return;

		var dbg = scene.GetSystem<DebugOverlaySystem>();
		if ( dbg is null )
			return;

		var wt = chunkRoot.Transform.World;
		foreach ( var zone in ThornsSettlementTerrainInfluence.LastZones )
		{
			var isCity = zone.Kind == ThornsWorldSettlementKind.MainCity;
			var coreCol = isCity
				? new Color( 0.2f, 0.75f, 1f, 0.55f )
				: new Color( 0.35f, 0.95f, 0.5f, 0.45f );
			var transCol = new Color( coreCol.r, coreCol.g, coreCol.b, coreCol.a * 0.55f );
			var outerCol = new Color( coreCol.r, coreCol.g, coreCol.b, coreCol.a * 0.28f );
			var z = zone.TargetSurfaceZ + 28f;

			DrawRing( dbg, wt, zone.CenterLocal, zone.CoreRadius, z, coreCol, durationSeconds );
			DrawRing( dbg, wt, zone.CenterLocal, zone.TransitionRadius, z + 8f, transCol, durationSeconds );
			DrawRing( dbg, wt, zone.CenterLocal, zone.OuterFeatherRadius, z + 16f, outerCol, durationSeconds );
		}
	}

	public static void DrawNoiseAttenuationHeatmap(
		Scene scene,
		GameObject chunkRoot,
		in ThornsTerrainNetSpec spec,
		float durationSeconds = 60f )
	{
		if ( scene is null || chunkRoot is null || !chunkRoot.IsValid() )
			return;

		var dbg = scene.GetSystem<DebugOverlaySystem>();
		if ( dbg is null )
			return;

		var wt = chunkRoot.Transform.World;
		var worldW = Math.Max( 64f, spec.WorldWidth );
		var worldD = Math.Max( 64f, spec.WorldDepth );
		var halfW = spec.CenterOnWorldOrigin ? worldW * 0.5f : 0f;
		var halfD = spec.CenterOnWorldOrigin ? worldD * 0.5f : 0f;

		foreach ( var zone in ThornsSettlementTerrainInfluence.LastZones )
		{
			const int steps = 14;
			var r = zone.OuterFeatherRadius;
			for ( var iz = -steps; iz <= steps; iz++ )
			for ( var ix = -steps; ix <= steps; ix++ )
			{
				var lx = zone.CenterLocal.x + ix * (r * 2f / steps);
				var ly = zone.CenterLocal.y + iz * (r * 2f / steps );
				var dx = lx - zone.CenterLocal.x;
				var dy = ly - zone.CenterLocal.y;
				if ( dx * dx + dy * dy > r * r )
					continue;

				var amp = ThornsSettlementTerrainInfluence.SampleNoiseAmplitudeMul( lx, ly, in spec );
				var col = Color.Lerp(
					new Color( 0.15f, 0.35f, 1f, 0.5f ),
					new Color( 0.85f, 0.85f, 0.2f, 0.15f ),
					amp );
				var h = zone.TargetSurfaceZ + 36f;
				var p0 = wt.PointToWorld( new Vector3( lx - 24f, ly, h ) );
				var p1 = wt.PointToWorld( new Vector3( lx + 24f, ly, h ) );
				dbg.Line( p0, p1, col, durationSeconds, default, false );
			}
		}

		_ = halfW;
		_ = halfD;
	}

	public static void DrawRejectedFootprint(
		Scene scene,
		GameObject chunkRoot,
		float lx,
		float ly,
		float halfW,
		float halfD,
		string reason,
		float durationSeconds = 12f )
	{
		if ( scene is null || chunkRoot is null || !chunkRoot.IsValid() )
			return;

		var dbg = scene.GetSystem<DebugOverlaySystem>();
		if ( dbg is null )
			return;

		var wt = chunkRoot.Transform.World;
		var c = new Color( 1f, 0.25f, 0.15f, 0.9f );
		var z = 96f;
		var corners = new[]
		{
			new Vector2( lx - halfW, ly - halfD ),
			new Vector2( lx + halfW, ly - halfD ),
			new Vector2( lx + halfW, ly + halfD ),
			new Vector2( lx - halfW, ly + halfD )
		};

		for ( var i = 0; i < 4; i++ )
		{
			var a = wt.PointToWorld( new Vector3( corners[i].x, corners[i].y, z ) );
			var b = wt.PointToWorld( new Vector3( corners[(i + 1) % 4].x, corners[(i + 1) % 4].y, z ) );
			dbg.Line( a, b, c, durationSeconds, default, false );
		}

		_ = reason;
	}

	public static void DrawReconciliationOverlays(
		Scene scene,
		GameObject chunkRoot,
		in ThornsTerrainNetSpec spec,
		ReadOnlySpan<float> heights,
		float worldWidth,
		float worldDepth,
		float durationSeconds = 75f )
	{
		if ( scene is null || chunkRoot is null || !chunkRoot.IsValid() )
			return;

		var dbg = scene.GetSystem<DebugOverlaySystem>();
		if ( dbg is null )
			return;

		var wt = chunkRoot.Transform.World;
		var rx = Math.Max( 2, spec.HeightmapResolutionX );
		var rz = Math.Max( 2, spec.HeightmapResolutionZ );

		foreach ( var cell in ThornsSettlementTerrainReconciliation.LastDebugCells )
		{
			var h = ThornsTerrainGeometry.SampleHeightLocalZUp(
				heights, rx, rz, worldWidth, worldDepth, spec.CenterOnWorldOrigin, cell.LocalX, cell.LocalY );
			var p = wt.PointToWorld( new Vector3( cell.LocalX, cell.LocalY, h + 12f ) );
			var col = cell.Eroded
				? new Color( 1f, 0.55f, 0.1f, 0.85f )
				: new Color( 0.95f, 0.9f, 0.2f, 0.65f );
			dbg.Line( p, p + Vector3.Up * (18f + cell.BandStrength * 24f), col, durationSeconds, default, false );
		}

		foreach ( var cell in ThornsSettlementTerrainInfluence.LastDirectionalDebugCells )
		{
			var h = ThornsTerrainGeometry.SampleHeightLocalZUp(
				heights, rx, rz, worldWidth, worldDepth, spec.CenterOnWorldOrigin, cell.LocalX, cell.LocalY );
			var p = wt.PointToWorld( new Vector3( cell.LocalX, cell.LocalY, h + 20f ) );
			var col = Color.Lerp(
				new Color( 0.2f, 0.65f, 1f, 0.45f ),
				new Color( 0.15f, 1f, 0.85f, 0.9f ),
				Math.Clamp( cell.Boost / ThornsSettlementTerrainInfluence.DirectionalFeatherMaxBoost, 0f, 1f ) );
			var len = 16f + cell.Boost * 48f;
			dbg.Line( p, p + Vector3.Up * len, col, durationSeconds, default, false );
		}

		DrawTransitionDiscontinuities(
			dbg,
			wt,
			heights,
			in spec,
			worldWidth,
			worldDepth,
			durationSeconds );
	}

	static void DrawTransitionDiscontinuities(
		DebugOverlaySystem dbg,
		Transform wt,
		ReadOnlySpan<float> heights,
		in ThornsTerrainNetSpec spec,
		float worldWidth,
		float worldDepth,
		float durationSeconds )
	{
		var influences = spec.SettlementTerrainInfluences;
		if ( influences is null || influences.Count == 0 || heights.IsEmpty )
			return;

		var rx = Math.Max( 2, spec.HeightmapResolutionX );
		var rz = Math.Max( 2, spec.HeightmapResolutionZ );
		var worldW = Math.Max( 64f, spec.WorldWidth );
		var worldD = Math.Max( 64f, spec.WorldDepth );
		var cellX = worldW / (rx - 1f );
		var cellY = worldD / (rz - 1f );
		var drawn = 0;

		for ( var gy = 2; gy < rz - 2 && drawn < 220; gy += 2 )
		{
			var row = gy * rx;
			for ( var gx = 2; gx < rx - 2 && drawn < 220; gx += 2 )
			{
				var i = row + gx;
				var wx = gx * cellX - (spec.CenterOnWorldOrigin ? worldW * 0.5f : 0f );
				var wy = gy * cellY - (spec.CenterOnWorldOrigin ? worldD * 0.5f : 0f );
				if ( !InTransitionBand( wx, wy, influences ) )
					continue;

				var h = heights[i];
				var stepX = MathF.Abs( heights[i + 1] - heights[i - 1] );
				var stepY = MathF.Abs( heights[i + rx] - heights[i - rx] );
				var slope = MathF.Max( stepX / (cellX * 2f), stepY / (cellY * 2f) );
				var maxStep = MathF.Max( stepX, stepY );
				if ( slope < ThornsSettlementTerrainReconciliation.SteepSlopeThreshold * 0.9f
				     && maxStep < 22f )
					continue;

				var p = wt.PointToWorld( new Vector3( wx, wy, h + 10f ) );
				var col = maxStep > 28f
					? new Color( 1f, 0.15f, 0.2f, 0.9f )
					: new Color( 1f, 0.45f, 0.35f, 0.7f );
				dbg.Line( p + Vector3.Left * 14f, p + Vector3.Right * 14f, col, durationSeconds, default, false );
				dbg.Line( p + Vector3.Forward * 14f, p + Vector3.Backward * 14f, col, durationSeconds, default, false );
				drawn++;
			}
		}
	}

	static bool InTransitionBand(
		float wx,
		float wy,
		List<ThornsSettlementTerrainInfluenceNet> influences )
	{
		for ( var n = 0; n < influences.Count; n++ )
		{
			var inf = influences[n];
			var dx = wx - inf.CenterX;
			var dy = wy - inf.CenterY;
			var planar = MathF.Sqrt( dx * dx + dy * dy );
			if ( planar >= inf.OuterFeatherRadius || planar < inf.CoreRadius * 0.9f )
				continue;

			ThornsSettlementTerrainInfluence.ComputeRingWeights(
				planar,
				inf.CoreRadius,
				inf.TransitionRadius,
				inf.OuterFeatherRadius,
				out _,
				out var transW,
				out var outerW );

			if ( transW > 0.08f || outerW > 0.12f )
				return true;
		}

		return false;
	}

	public static void DrawLocalBuildingFeather(
		Scene scene,
		GameObject chunkRoot,
		float lx,
		float ly,
		float halfW,
		float halfD,
		float yawRad,
		float durationSeconds = 20f )
	{
		if ( scene is null || chunkRoot is null || !chunkRoot.IsValid() )
			return;

		var dbg = scene.GetSystem<DebugOverlaySystem>();
		if ( dbg is null )
			return;

		var wt = chunkRoot.Transform.World;
		var col = new Color( 0.95f, 0.85f, 0.2f, 0.55f );
		var cy = MathF.Cos( yawRad );
		var sy = MathF.Sin( yawRad );
		var pts = new (float bx, float by)[]
		{
			(-halfW, -halfD), (halfW, -halfD), (halfW, halfD), (-halfW, halfD)
		};

		for ( var i = 0; i < 4; i++ )
		{
			var (bx, by) = pts[i];
			var wx = lx + bx * cy - by * sy;
			var wy = ly + bx * sy + by * cy;
			var p = wt.PointToWorld( new Vector3( wx, wy, 88f ) );
			dbg.Line( p, p + Vector3.Up * 32f, col, durationSeconds, default, false );
		}
	}

	public static void DrawSlopeHeatmap(
		Scene scene,
		GameObject chunkRoot,
		ReadOnlySpan<float> heights,
		in ThornsTerrainNetSpec spec,
		float worldWidth,
		float worldDepth,
		float durationSeconds = 45f )
	{
		if ( scene is null || chunkRoot is null || !chunkRoot.IsValid() || heights.IsEmpty )
			return;

		var dbg = scene.GetSystem<DebugOverlaySystem>();
		if ( dbg is null )
			return;

		var rx = Math.Max( 2, spec.HeightmapResolutionX );
		var rz = Math.Max( 2, spec.HeightmapResolutionZ );
		var wt = chunkRoot.Transform.World;
		const int steps = 12;

		foreach ( var zone in ThornsSettlementTerrainInfluence.LastZones )
		{
			var r = zone.OuterFeatherRadius;
			for ( var iz = -steps; iz <= steps; iz++ )
			for ( var ix = -steps; ix <= steps; ix++ )
			{
				var lx = zone.CenterLocal.x + ix * (r * 2f / steps);
				var ly = zone.CenterLocal.y + iz * (r * 2f / steps);
				if ( (lx - zone.CenterLocal.x) * (lx - zone.CenterLocal.x)
				     + (ly - zone.CenterLocal.y) * (ly - zone.CenterLocal.y) > r * r )
					continue;

				var stats = ThornsTerrainGeometry.SampleFootprintHeightMetrics(
					heights,
					in spec,
					worldWidth,
					worldDepth,
					lx,
					ly,
					48f,
					48f,
					0f );
				var t = Math.Clamp( stats.MaxStepSlope / 40f, 0f, 1f );
				var col = Color.Lerp( new Color( 0.2f, 0.85f, 0.35f, 0.35f ), new Color( 1f, 0.2f, 0.1f, 0.75f ), t );
				var h = ThornsTerrainGeometry.SampleHeightLocalZUp(
					heights, rx, rz, worldWidth, worldDepth, spec.CenterOnWorldOrigin, lx, ly );
				var p0 = wt.PointToWorld( new Vector3( lx - 32f, ly, h + 8f ) );
				var p1 = wt.PointToWorld( new Vector3( lx + 32f, ly, h + 8f ) );
				dbg.Line( p0, p1, col, durationSeconds, default, false );
			}
		}
	}

	static void DrawRing(
		DebugOverlaySystem dbg,
		Transform wt,
		Vector2 center,
		float radius,
		float height,
		Color color,
		float duration )
	{
		const int segments = 32;
		var prev = wt.PointToWorld( new Vector3( center.x + radius, center.y, height ) );
		for ( var i = 1; i <= segments; i++ )
		{
			var ang = i * ( MathF.PI * 2f / segments );
			var p = wt.PointToWorld( new Vector3(
				center.x + MathF.Cos( ang ) * radius,
				center.y + MathF.Sin( ang ) * radius,
				height ) );
			dbg.Line( prev, p, color, duration, default, false );
			prev = p;
		}
	}
}
