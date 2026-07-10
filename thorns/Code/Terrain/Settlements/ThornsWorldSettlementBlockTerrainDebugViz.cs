namespace Sandbox;

/// <summary>Debug overlays for terraced block targets and neighbor deltas.</summary>
public static class ThornsWorldSettlementBlockTerrainDebugViz
{
	public static void Draw(
		Scene scene,
		GameObject chunkRoot,
		in ThornsTerrainNetSpec spec,
		ReadOnlySpan<float> heights,
		float worldWidth,
		float worldDepth,
		float durationSeconds = 90f )
	{
		if ( scene is null || chunkRoot is null || !chunkRoot.IsValid() )
			return;

		var dbg = scene.GetSystem<DebugOverlaySystem>();
		if ( dbg is null )
			return;

		var wt = chunkRoot.Transform.World;
		var rx = Math.Max( 2, spec.HeightmapResolutionX );
		var rz = Math.Max( 2, spec.HeightmapResolutionZ );
		var entries = ThornsWorldSettlementBlockTerrain.LastDebug;
		var hasPrev = false;
		ThornsWorldSettlementBlockTerrainDebug prev = default;

		for ( var i = 0; i < entries.Count; i++ )
		{
			var e = entries[i];
			var h = SampleH( heights, rx, rz, worldWidth, worldDepth, spec.CenterOnWorldOrigin, e.CenterLocal.x, e.CenterLocal.y );
			var p = wt.PointToWorld( new Vector3( e.CenterLocal.x, e.CenterLocal.y, h ) );
			var col = Color.Lerp(
				new Color( 0.25f, 0.55f, 1f, 0.85f ),
				new Color( 0.35f, 1f, 0.55f, 0.9f ),
				Math.Clamp( (e.TargetZ - h + 40f) / 80f, 0f, 1f ) );
			dbg.Line( p, p + Vector3.Up * 48f, col, durationSeconds, default, false );
			DrawBlockRing( dbg, p, e.HalfW, e.HalfD, col, durationSeconds );

			if ( hasPrev )
			{
				var dh = MathF.Abs( e.TargetZ - prev.TargetZ );
				var lineCol = dh > ThornsWorldSettlementBlockTerrain.MaxCityBlockDelta
					? new Color( 1f, 0.25f, 0.2f, 0.75f )
					: new Color( 0.9f, 0.85f, 0.25f, 0.55f );
				var p0 = wt.PointToWorld( new Vector3( prev.CenterLocal.x, prev.CenterLocal.y, prev.TargetZ + 20f ) );
				var p1 = wt.PointToWorld( new Vector3( e.CenterLocal.x, e.CenterLocal.y, e.TargetZ + 20f ) );
				dbg.Line( p0, p1, lineCol, durationSeconds, default, false );
			}

			prev = e;
			hasPrev = true;
		}

		DrawMacroVsLocalLegend( dbg, wt, durationSeconds );
	}

	static void DrawBlockRing(
		DebugOverlaySystem dbg,
		Vector3 center,
		float halfW,
		float halfD,
		Color color,
		float duration )
	{
		var corners = new[]
		{
			center + new Vector3( -halfW, -halfD, 0 ),
			center + new Vector3( halfW, -halfD, 0 ),
			center + new Vector3( halfW, halfD, 0 ),
			center + new Vector3( -halfW, halfD, 0 )
		};

		for ( var i = 0; i < 4; i++ )
			dbg.Line( corners[i], corners[(i + 1) % 4], color, duration, default, false );
	}

	static void DrawMacroVsLocalLegend( DebugOverlaySystem dbg, Transform wt, float duration )
	{
		var p = wt.PointToWorld( new Vector3( 0, 0, 200f ) );
		dbg.Line( p, p + Vector3.Up * 40f, new Color( 0.3f, 0.7f, 1f, 0.8f ), duration, default, false );
		dbg.Line( p + Vector3.Right * 20f, p + Vector3.Right * 20f + Vector3.Up * 40f,
			new Color( 0.35f, 1f, 0.5f, 0.8f ), duration, default, false );
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
