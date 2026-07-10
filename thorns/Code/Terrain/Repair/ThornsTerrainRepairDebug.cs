namespace Sandbox;

/// <summary>Debug overlay for repaired / invalid terrain cells.</summary>
public static class ThornsTerrainRepairDebug
{
	const int MaxMarkers = 384;

	public static void DrawOverlay(
		Scene scene,
		GameObject chunkRoot,
		in ThornsTerrainNetSpec spec,
		float durationSeconds = 8f )
	{
		if ( scene is null || chunkRoot is null || !chunkRoot.IsValid() )
			return;

		var dbg = scene.GetSystem<DebugOverlaySystem>();
		if ( dbg is null )
			return;

		var mask = ThornsTerrainRepairPipeline.LastFaultMask;
		if ( mask is null || mask.Length == 0 )
			return;

		var rx = Math.Max( 2, spec.HeightmapResolutionX );
		var rz = Math.Max( 2, spec.HeightmapResolutionZ );
		if ( mask.Length < rx * rz )
			return;

		ThornsTerrainGeometry.GetExtents( spec, out var worldW, out var worldD );
		var cellX = worldW / (rx - 1f);
		var cellY = worldD / (rz - 1f);
		var halfW = spec.CenterOnWorldOrigin ? worldW * 0.5f : 0f;
		var halfD = spec.CenterOnWorldOrigin ? worldD * 0.5f : 0f;
		var wt = chunkRoot.Transform.World;

		var stats = ThornsTerrainRepairPipeline.LastStats;
		var drawn = 0;
		var stride = Math.Max( 1, (rx * rz) / MaxMarkers );

		for ( var i = 0; i < rx * rz && drawn < MaxMarkers; i += stride )
		{
			var fault = (ThornsTerrainCellFault)mask[i];
			if ( fault == ThornsTerrainCellFault.None )
				continue;

			var gx = i % rx;
			var gy = i / rx;
			var wx = gx * cellX - halfW;
			var wy = gy * cellY - halfD;
			var wz = (ThornsTerrainRepairPipeline.LastHeightsSnapshot?[i] ?? 0f) + 24f;

			var col = fault switch
			{
				_ when (fault & ThornsTerrainCellFault.NonFinite) != 0 => new Color( 1f, 0.1f, 0.1f, 0.95f ),
				_ when (fault & ThornsTerrainCellFault.ExcessiveStep) != 0 => new Color( 1f, 0.55f, 0.1f, 0.85f ),
				_ when (fault & ThornsTerrainCellFault.StretchedQuad) != 0 => new Color( 1f, 0.95f, 0.15f, 0.85f ),
				_ when (fault & ThornsTerrainCellFault.IsolatedSpike) != 0 => new Color( 0.95f, 0.2f, 1f, 0.85f ),
				_ => new Color( 0.35f, 0.85f, 1f, 0.65f )
			};

			var p = wt.PointToWorld( new Vector3( wx, wy, wz ) );
			dbg.Sphere( new Sphere( p, 18f ), col, durationSeconds );
			drawn++;
		}

		DrawBorderSeams( dbg, wt, spec, worldW, worldD, halfW, halfD, durationSeconds );

		Log.Info(
			$"[Thorns Terrain Repair] debug markers={drawn} invalid={stats.InvalidCellsDetected} "
			+ $"repaired={stats.CellsAdjusted} meshSkippedTris={stats.MeshTrianglesSkipped}" );
	}

	static void DrawBorderSeams(
		DebugOverlaySystem dbg,
		Transform wt,
		in ThornsTerrainNetSpec spec,
		float worldW,
		float worldD,
		float halfW,
		float halfD,
		float duration )
	{
		var z = spec.WaterLevelWorldZ + 48f;
		var seamCol = new Color( 0.2f, 1f, 0.45f, 0.75f );
		var wx0 = spec.CenterOnWorldOrigin ? -halfW : 0f;
		var wx1 = spec.CenterOnWorldOrigin ? halfW : worldW;
		var wy0 = spec.CenterOnWorldOrigin ? -halfD : 0f;
		var wy1 = spec.CenterOnWorldOrigin ? halfD : worldD;

		dbg.Line( wt.PointToWorld( new Vector3( wx0, wy0, z ) ), wt.PointToWorld( new Vector3( wx0, wy1, z ) ), seamCol, duration, default, false );
		dbg.Line( wt.PointToWorld( new Vector3( wx1, wy0, z ) ), wt.PointToWorld( new Vector3( wx1, wy1, z ) ), seamCol, duration, default, false );
		dbg.Line( wt.PointToWorld( new Vector3( wx0, wy0, z ) ), wt.PointToWorld( new Vector3( wx1, wy0, z ) ), seamCol, duration, default, false );
		dbg.Line( wt.PointToWorld( new Vector3( wx0, wy1, z ) ), wt.PointToWorld( new Vector3( wx1, wy1, z ) ), seamCol, duration, default, false );
	}

	public static void TryDrawIfEnabled( Scene scene, GameObject chunkRoot, in ThornsTerrainNetSpec spec )
	{
		var cfg = ThornsTerrainRepairPipeline.LastConfig;
		if ( cfg is null || !cfg.DebugVisualization )
			return;

		DrawOverlay( scene, chunkRoot, in spec );
	}
}
