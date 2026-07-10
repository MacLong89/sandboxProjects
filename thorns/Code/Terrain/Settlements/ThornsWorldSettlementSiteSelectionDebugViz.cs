namespace Sandbox;

/// <summary>Debug overlays for settlement site analysis (horizon, retainability, rejected candidates).</summary>
public static class ThornsWorldSettlementSiteSelectionDebugViz
{
	public static void Draw(
		Scene scene,
		GameObject chunkRoot,
		ReadOnlySpan<float> heights,
		in ThornsTerrainNetSpec spec,
		float worldWidth,
		float worldDepth,
		Vector2 chosenCityCenter,
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

		foreach ( var cand in ThornsWorldSettlementSiteAnalysis.LastCityCandidates )
		{
			var h = SampleH( heights, rx, rz, worldWidth, worldDepth, spec.CenterOnWorldOrigin, cand.LocalX, cand.LocalY );
			var p = wt.PointToWorld( new Vector3( cand.LocalX, cand.LocalY, h + 24f ) );

			if ( cand.Selected )
			{
				var sel = new Color( 0.2f, 1f, 0.45f, 0.9f );
				dbg.Line( p, p + Vector3.Up * 80f, sel, durationSeconds, default, false );
				DrawRing( dbg, p, 64f, sel, durationSeconds );
				continue;
			}

			if ( !cand.Accepted )
			{
				var col = RejectColor( cand.RejectReason );
				dbg.Line( p + Vector3.Left * 36f, p + Vector3.Right * 36f, col, durationSeconds, default, false );
				dbg.Line( p + Vector3.Forward * 36f, p + Vector3.Backward * 36f, col, durationSeconds, default, false );
				continue;
			}

			var retainCol = Color.Lerp(
				new Color( 1f, 0.35f, 0.2f, 0.55f ),
				new Color( 0.25f, 0.9f, 0.5f, 0.65f ),
				Math.Clamp( cand.Retainability, 0f, 1f ) );
			dbg.Line( p, p + Vector3.Up * (20f + cand.CompositeScore * 40f), retainCol, durationSeconds, default, false );
		}

		var eval = ThornsWorldSettlementSiteAnalysis.LastCityEvaluation;
		if ( eval is { Acceptable: true, SectorMeans: not null } )
			DrawHorizonSectors( dbg, wt, heights, rx, rz, worldWidth, worldDepth, spec.CenterOnWorldOrigin, chosenCityCenter, eval.Value, durationSeconds );

		DrawContinuityGrid(
			dbg,
			wt,
			heights,
			rx,
			rz,
			worldWidth,
			worldDepth,
			spec.CenterOnWorldOrigin,
			chosenCityCenter,
			durationSeconds );
	}

	static void DrawHorizonSectors(
		DebugOverlaySystem dbg,
		Transform wt,
		ReadOnlySpan<float> heights,
		int rx,
		int rz,
		float ww,
		float wd,
		bool centerOnOrigin,
		Vector2 center,
		ThornsWorldSettlementSiteEvaluation eval,
		float duration )
	{
		var centerH = SampleH( heights, rx, rz, ww, wd, centerOnOrigin, center.x, center.y );
		var p0 = wt.PointToWorld( new Vector3( center.x, center.y, centerH + 32f ) );

		for ( var s = 0; s < ThornsWorldSettlementSiteAnalysis.HorizonSectorCount; s++ )
		{
			var ang = s * ( MathF.PI * 2f / ThornsWorldSettlementSiteAnalysis.HorizonSectorCount );
			var dist = 2800f;
			var lx = center.x + MathF.Cos( ang ) * dist;
			var ly = center.y + MathF.Sin( ang ) * dist;
			var h = SampleH( heights, rx, rz, ww, wd, centerOnOrigin, lx, ly );
			var p1 = wt.PointToWorld( new Vector3( lx, ly, h + 24f ) );

			var sectorMean = eval.SectorMeans is not null && s < eval.SectorMeans.Length
				? eval.SectorMeans[s]
				: h;
			var t = Math.Clamp( 1f - MathF.Abs( sectorMean - centerH ) / 90f, 0f, 1f );
			var col = Color.Lerp(
				new Color( 1f, 0.2f, 0.15f, 0.85f ),
				new Color( 0.25f, 0.85f, 1f, 0.75f ),
				t );

			dbg.Line( p0, p1, col, duration, default, false );
		}
	}

	static void DrawContinuityGrid(
		DebugOverlaySystem dbg,
		Transform wt,
		ReadOnlySpan<float> heights,
		int rx,
		int rz,
		float ww,
		float wd,
		bool centerOnOrigin,
		Vector2 center,
		float duration )
	{
		const float span = 2400f;
		const int steps = 4;
		var step = span * 2f / steps;

		for ( var iz = -steps; iz <= steps; iz++ )
		for ( var ix = -steps; ix <= steps; ix++ )
		{
			var lx = center.x + ix * step;
			var ly = center.y + iz * step;
			if ( (lx - center.x) * (lx - center.x) + (ly - center.y) * (ly - center.y) > span * span )
				continue;

			var h = SampleH( heights, rx, rz, ww, wd, centerOnOrigin, lx, ly );
			var hc = SampleH( heights, rx, rz, ww, wd, centerOnOrigin, center.x, center.y );
			var t = Math.Clamp( 1f - MathF.Abs( h - hc ) / 70f, 0f, 1f );
			var col = Color.Lerp(
				new Color( 0.9f, 0.25f, 0.2f, 0.35f ),
				new Color( 0.2f, 0.75f, 0.95f, 0.4f ),
				t );
			var p = wt.PointToWorld( new Vector3( lx, ly, h + 8f ) );
			dbg.Line( p, p + Vector3.Up * 22f, col, duration, default, false );
		}
	}

	static Color RejectColor( ThornsWorldSettlementSiteRejectReason reason ) => reason switch
	{
		ThornsWorldSettlementSiteRejectReason.TerrainShelf => new Color( 1f, 0.5f, 0.1f, 0.9f ),
		ThornsWorldSettlementSiteRejectReason.ShelfOrSidehill => new Color( 1f, 0.55f, 0.15f, 0.9f ),
		ThornsWorldSettlementSiteRejectReason.OneSidedCliff => new Color( 1f, 0.2f, 0.25f, 0.9f ),
		ThornsWorldSettlementSiteRejectReason.HorizonAsymmetry => new Color( 1f, 0.35f, 0.55f, 0.9f ),
		ThornsWorldSettlementSiteRejectReason.LowRetainability => new Color( 0.85f, 0.2f, 1f, 0.9f ),
		ThornsWorldSettlementSiteRejectReason.RidgeOrMountainEdge => new Color( 0.55f, 0.25f, 1f, 0.9f ),
		ThornsWorldSettlementSiteRejectReason.RegionalDiscontinuity => new Color( 1f, 0.15f, 0.5f, 0.9f ),
		_ => new Color( 0.75f, 0.75f, 0.75f, 0.75f )
	};

	static void DrawRing( DebugOverlaySystem dbg, Vector3 center, float radius, Color color, float duration )
	{
		const int segments = 20;
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
