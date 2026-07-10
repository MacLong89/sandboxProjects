namespace Sandbox;

/// <summary>Density suppression, corridor width, and local grounding strength overlays.</summary>
public static class ThornsWorldSettlementInteriorReadabilityDebugViz
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

		var dbg = scene.GetSystem<DebugOverlaySystem>();
		if ( dbg is null )
			return;

		var wt = chunkRoot.Transform.World;
		var rx = Math.Max( 2, spec.HeightmapResolutionX );
		var rz = Math.Max( 2, spec.HeightmapResolutionZ );
		DrawCorridorWidths( dbg, wt, spec.RoadCorridors, heights, rx, rz, worldWidth, worldDepth, spec.CenterOnWorldOrigin, durationSeconds );
		DrawDensityHeatmap(
			dbg,
			wt,
			spec,
			heights,
			rx,
			rz,
			worldWidth,
			worldDepth,
			durationSeconds );
	}

	static void DrawCorridorWidths(
		DebugOverlaySystem dbg,
		Transform wt,
		IReadOnlyList<ThornsWorldRoadCorridor> corridors,
		ReadOnlySpan<float> heights,
		int rx,
		int rz,
		float ww,
		float wd,
		bool centerOnOrigin,
		float duration )
	{
		if ( corridors is null )
			return;

		foreach ( var c in corridors )
		{
			var ab = c.B - c.A;
			var len = ab.Length;
			if ( len < 8f )
				continue;

			var steps = Math.Clamp( (int)(len / 80f), 2, 12 );
			var dir = ab / len;
			var perp = new Vector2( -dir.y, dir.x );
			for ( var i = 0; i <= steps; i++ )
			{
				var t = i / (float)steps;
				var p = c.A + ab * t;
				var h = SampleH( heights, rx, rz, ww, wd, centerOnOrigin, p.x, p.y );
				var center = wt.PointToWorld( new Vector3( p.x, p.y, h + 10f ) );
				var w0 = wt.PointToWorld( new Vector3( p.x + perp.x * c.HalfWidth, p.y + perp.y * c.HalfWidth, h + 10f ) );
				var w1 = wt.PointToWorld( new Vector3( p.x - perp.x * c.HalfWidth, p.y - perp.y * c.HalfWidth, h + 10f ) );
				var col = c.Kind switch
				{
					ThornsWorldRoadCorridorKind.Radial => new Color( 0.45f, 0.75f, 1f, 0.55f ),
					ThornsWorldRoadCorridorKind.Ring => new Color( 0.4f, 0.9f, 0.65f, 0.5f ),
					ThornsWorldRoadCorridorKind.MainStreet => new Color( 0.95f, 0.7f, 0.35f, 0.55f ),
					_ => new Color( 0.7f, 0.55f, 0.4f, 0.45f )
				};
				dbg.Line( w0, w1, col, duration, default, false );
				dbg.Line( center, center + Vector3.Up * 6f, col.WithAlpha( 0.35f ), duration, default, false );
			}
		}
	}

	static void DrawDensityHeatmap(
		DebugOverlaySystem dbg,
		Transform wt,
		in ThornsTerrainNetSpec spec,
		ReadOnlySpan<float> heights,
		int rx,
		int rz,
		float ww,
		float wd,
		float duration )
	{
		var blocks = spec.SettlementBlockTerrain;
		var pads = spec.ProcBuildingTerrainPads;
		if ( blocks is null || blocks.Count == 0 )
			return;

		foreach ( var block in blocks )
		{
			if ( block.BuildingCount < 2 )
				continue;

			var restraint = ThornsSettlementDensityRestraint.Compute( block.BuildingCount );
			var samples = 5;
			for ( var sy = 0; sy < samples; sy++ )
			{
				for ( var sx = 0; sx < samples; sx++ )
				{
					var fx = ( sx + 0.5f ) / samples * 2f - 1f;
					var fy = ( sy + 0.5f ) / samples * 2f - 1f;
					var lx = block.CenterX + fx * block.HalfW * 0.75f;
					var ly = block.CenterY + fy * block.HalfD * 0.75f;
					var hNatural = SampleH( heights, rx, rz, ww, wd, spec.CenterOnWorldOrigin, lx, ly );

					var grounding = 0f;
					if ( pads is not null )
					{
						for ( var p = 0; p < pads.Count; p++ )
						{
							var pad = pads[p];
							if ( ThornsBuildingFoundationTerrain.TryEvaluate(
								     in pad,
								     lx,
								     ly,
								     hNatural,
								     out _,
								     out var w,
								     out _,
								     out _ ) )
								grounding = MathF.Max( grounding, w );
						}
					}

					ThornsWorldSettlementBlockTerrain.TrySampleBlockSurfaceBlend(
						blocks,
						lx,
						ly,
						out _,
						out var blockW );

					var suppression = Math.Clamp( 1f - restraint.ApronStrengthMul, 0f, 1f );
					var heat = Math.Clamp( grounding * 0.55f + blockW * 0.35f - suppression * 0.25f, 0f, 1f );
					var col = Color.Lerp(
						new Color( 0.2f, 0.85f, 0.45f, 0.35f ),
						new Color( 1f, 0.25f, 0.2f, 0.75f ),
						heat );
					var worldPt = wt.PointToWorld( new Vector3( lx, ly, hNatural + 8f + suppression * 12f ) );
					dbg.Line(
						worldPt,
						worldPt + Vector3.Up * (6f + restraint.MaxCooperativeBlend * 20f),
						col,
						duration,
						default,
						false );
				}
			}

			var labelH = SampleH( heights, rx, rz, ww, wd, spec.CenterOnWorldOrigin, block.CenterX, block.CenterY );
			var labelPt = wt.PointToWorld( new Vector3( block.CenterX, block.CenterY, labelH + 22f ) );
			var labelCol = new Color( 0.35f, 0.9f, 1f, 0.85f );
			var labelHgt = 10f + restraint.ApronStrengthMul * 40f;
			dbg.Line( labelPt, labelPt + Vector3.Up * labelHgt, labelCol, duration, default, false );
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
