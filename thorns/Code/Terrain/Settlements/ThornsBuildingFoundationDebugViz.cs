namespace Sandbox;

/// <summary>Foundation apron / embed / perimeter support debug overlays.</summary>
public static class ThornsBuildingFoundationDebugViz
{
	public static void DrawPad(
		Scene scene,
		GameObject chunkRoot,
		ThornsTerrainProcBuildingPad pad,
		ReadOnlySpan<float> heights,
		in ThornsTerrainNetSpec spec,
		float worldWidth,
		float worldDepth,
		float durationSeconds = 45f )
	{
		if ( scene is null || chunkRoot is null || !chunkRoot.IsValid() || pad is null )
			return;

		if ( !ThornsBuildingFoundationTerrain.IsLocalFoundationPad( in pad ) )
			return;

		var dbg = scene.GetSystem<DebugOverlaySystem>();
		if ( dbg is null )
			return;

		var wt = chunkRoot.Transform.World;
		var rx = Math.Max( 2, spec.HeightmapResolutionX );
		var rz = Math.Max( 2, spec.HeightmapResolutionZ );
		var fw = pad.FoundationHalfW > 1f ? pad.FoundationHalfW : pad.HalfW * 0.82f;
		var fd = pad.FoundationHalfD > 1f ? pad.FoundationHalfD : pad.HalfD * 0.82f;
		var cy = MathF.Cos( pad.YawRadians );
		var sy = MathF.Sin( pad.YawRadians );

		DrawObb( dbg, wt, pad.CenterX, pad.CenterY, fw, fd, cy, sy, pad.TargetZ - pad.FoundationEmbed,
			new Color( 0.25f, 0.95f, 0.45f, 0.9f ), durationSeconds );
		DrawObb( dbg, wt, pad.CenterX, pad.CenterY, fw + pad.WallApron, fd + pad.WallApron, cy, sy, pad.TargetZ,
			new Color( 0.95f, 0.85f, 0.2f, 0.65f ), durationSeconds );
		DrawObb( dbg, wt, pad.CenterX, pad.CenterY, fw + pad.Apron, fd + pad.Apron, cy, sy, pad.TargetZ,
			new Color( 0.35f, 0.75f, 1f, 0.35f ), durationSeconds );

		if ( pad.DoorOutwardX * pad.DoorOutwardX + pad.DoorOutwardY * pad.DoorOutwardY > 0.01f )
		{
			var h = SampleH( heights, rx, rz, worldWidth, worldDepth, spec.CenterOnWorldOrigin, pad.CenterX, pad.CenterY );
			var p0 = wt.PointToWorld( new Vector3( pad.CenterX, pad.CenterY, h + 16f ) );
			var p1 = wt.PointToWorld( new Vector3(
				pad.CenterX + pad.DoorOutwardX * (fw + pad.WallApron),
				pad.CenterY + pad.DoorOutwardY * (fw + pad.WallApron),
				h + 16f ) );
			dbg.Line( p0, p1, new Color( 1f, 0.45f, 0.15f, 0.95f ), durationSeconds, default, false );
		}

		DrawPerimeterDelta( dbg, wt, pad, heights, rx, rz, spec, worldWidth, worldDepth, fw, fd, cy, sy, durationSeconds );
	}

	static void DrawPerimeterDelta(
		DebugOverlaySystem dbg,
		Transform wt,
		ThornsTerrainProcBuildingPad pad,
		ReadOnlySpan<float> heights,
		int rx,
		int rz,
		in ThornsTerrainNetSpec spec,
		float ww,
		float wd,
		float fw,
		float fd,
		float cy,
		float sy,
		float duration )
	{
		const int samples = 12;
		for ( var i = 0; i < samples; i++ )
		{
			var t = i / (float)samples;
			var ang = t * MathF.PI * 2f;
			var bx = MathF.Cos( ang ) * (fw + pad.WallApron * 0.55f);
			var by = MathF.Sin( ang ) * (fd + pad.WallApron * 0.55f );
			var wx = pad.CenterX + bx * cy - by * sy;
			var wy = pad.CenterY + bx * sy + by * cy;
			var h = SampleH( heights, rx, rz, ww, wd, spec.CenterOnWorldOrigin, wx, wy );
			if ( !ThornsBuildingFoundationTerrain.TryEvaluate( in pad, wx, wy, h, out var supportZ, out _, out _, out _ ) )
				continue;

			var delta = h - supportZ;
			var col = delta > 6f
				? new Color( 1f, 0.2f, 0.15f, 0.9f )
				: delta < -4f
					? new Color( 0.2f, 0.55f, 1f, 0.85f )
					: new Color( 0.35f, 0.9f, 0.4f, 0.7f );
			var p = wt.PointToWorld( new Vector3( wx, wy, h + 8f ) );
			dbg.Line( p, p + Vector3.Up * (12f + MathF.Abs( delta )), col, duration, default, false );
		}
	}

	static void DrawObb(
		DebugOverlaySystem dbg,
		Transform wt,
		float cx,
		float cy,
		float halfW,
		float halfD,
		float cyaw,
		float syaw,
		float z,
		Color color,
		float duration )
	{
		var corners = new (float bx, float by)[]
		{
			(-halfW, -halfD), (halfW, -halfD), (halfW, halfD), (-halfW, halfD)
		};

		for ( var i = 0; i < 4; i++ )
		{
			var (bx0, by0) = corners[i];
			var (bx1, by1) = corners[(i + 1) % 4];
			var wx0 = cx + bx0 * cyaw - by0 * syaw;
			var wy0 = cy + bx0 * syaw + by0 * cyaw;
			var wx1 = cx + bx1 * cyaw - by1 * syaw;
			var wy1 = cy + bx1 * syaw + by1 * cyaw;
			var a = wt.PointToWorld( new Vector3( wx0, wy0, z ) );
			var b = wt.PointToWorld( new Vector3( wx1, wy1, z ) );
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
