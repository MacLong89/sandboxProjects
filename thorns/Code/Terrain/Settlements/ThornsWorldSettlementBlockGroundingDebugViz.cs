namespace Sandbox;

/// <summary>Block terrain ownership, apron overlap, and density suppression debug overlays.</summary>
public static class ThornsWorldSettlementBlockGroundingDebugViz
{
	public static void Draw(
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

		var blocks = spec.SettlementBlockTerrain;
		var pads = spec.ProcBuildingTerrainPads;
		if ( blocks is null || blocks.Count == 0 )
			return;

		var dbg = scene.GetSystem<DebugOverlaySystem>();
		if ( dbg is null )
			return;

		var wt = chunkRoot.Transform.World;
		var rx = Math.Max( 2, spec.HeightmapResolutionX );
		var rz = Math.Max( 2, spec.HeightmapResolutionZ );

		foreach ( var block in blocks )
		{
			var h = SampleH( heights, rx, rz, worldWidth, worldDepth, spec.CenterOnWorldOrigin, block.CenterX, block.CenterY );
			var center = wt.PointToWorld( new Vector3( block.CenterX, block.CenterY, h + 20f ) );
			var col = block.BuildingCount >= 3
				? new Color( 0.35f, 0.85f, 1f, 0.75f )
				: block.BuildingCount >= 2
					? new Color( 0.55f, 0.95f, 0.45f, 0.75f )
					: new Color( 0.9f, 0.85f, 0.35f, 0.65f );
			DrawObb( dbg, center, block.HalfW, block.HalfD, block.YawRadians, col, durationSeconds );
			dbg.Line(
				center,
				center + Vector3.Up * (24f + block.SurfaceStrength * 48f),
				col,
				durationSeconds,
				default,
				false );
		}

		if ( pads is null )
			return;

		const int overlapSamples = 10;
		foreach ( var block in blocks )
		{
			if ( block.BuildingCount < 2 )
				continue;

			for ( var s = 0; s < overlapSamples; s++ )
			{
				var ang = s * ( MathF.PI * 2f / overlapSamples );
				var lx = block.CenterX + MathF.Cos( ang ) * block.HalfW * 0.35f;
				var ly = block.CenterY + MathF.Sin( ang ) * block.HalfD * 0.35f;
				var padHits = 0;
				for ( var p = 0; p < pads.Count; p++ )
				{
					var pad = pads[p];
					if ( pad.BlockIndex != block.BlockIndex && pad.BlockIndex >= 0 )
						continue;

					if ( ThornsBuildingFoundationTerrain.TryEvaluate(
						     in pad,
						     lx,
						     ly,
						     SampleH( heights, rx, rz, worldWidth, worldDepth, spec.CenterOnWorldOrigin, lx, ly ),
						     out _,
						     out var w,
						     out _,
						     out _ )
					     && w > 0.08f )
						padHits++;
				}

				if ( padHits < 2 )
					continue;

				var h = SampleH( heights, rx, rz, worldWidth, worldDepth, spec.CenterOnWorldOrigin, lx, ly );
				var worldPt = wt.PointToWorld( new Vector3( lx, ly, h + 14f ) );
				dbg.Line( worldPt + Vector3.Left * 12f, worldPt + Vector3.Right * 12f, new Color( 1f, 0.35f, 0.85f, 0.85f ), durationSeconds, default, false );
			}
		}
	}

	static void DrawObb(
		DebugOverlaySystem dbg,
		Vector3 center,
		float halfW,
		float halfD,
		float yaw,
		Color color,
		float duration )
	{
		var cy = MathF.Cos( yaw );
		var sy = MathF.Sin( yaw );
		var corners = new (float bx, float by)[]
		{
			(-halfW, -halfD), (halfW, -halfD), (halfW, halfD), (-halfW, halfD)
		};

		for ( var i = 0; i < 4; i++ )
		{
			var (bx0, by0) = corners[i];
			var (bx1, by1) = corners[(i + 1) % 4];
			var ox0 = bx0 * cy - by0 * sy;
			var oy0 = bx0 * sy + by0 * cy;
			var ox1 = bx1 * cy - by1 * sy;
			var oy1 = bx1 * sy + by1 * cy;
			var a = center + new Vector3( ox0, oy0, 0 );
			var b = center + new Vector3( ox1, oy1, 0 );
			dbg.Line( a, b, color, duration, default, false );
		}
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
		return float.IsNaN( h ) ? 0f : h;
	}
}
