namespace Sandbox;

/// <summary>Road centerline, width, and influence falloff debug overlays.</summary>
public static class ThornsWorldRoadTerrainDebugViz
{
	public static void Draw(
		Scene scene,
		GameObject chunkRoot,
		in ThornsTerrainNetSpec spec,
		float durationSeconds = 90f )
	{
		if ( scene is null || chunkRoot is null || !chunkRoot.IsValid() )
			return;

		var corridors = spec.RoadCorridors;
		if ( corridors is null || corridors.Count == 0 )
			return;

		var dbg = scene.GetSystem<DebugOverlaySystem>();
		if ( dbg is null )
			return;

		var tuning = spec.RoadTuning ?? ThornsTerrainRoadTuningNet.EngineDefaults();
		var wt = chunkRoot.Transform.World;

		foreach ( var c in corridors )
		{
			var col = c.Kind switch
			{
				ThornsWorldRoadCorridorKind.Radial => new Color( 0.75f, 0.55f, 0.35f, 0.9f ),
				ThornsWorldRoadCorridorKind.Ring => new Color( 0.65f, 0.5f, 0.32f, 0.75f ),
				ThornsWorldRoadCorridorKind.MainStreet => new Color( 0.55f, 0.42f, 0.28f, 0.85f ),
				_ => new Color( 0.45f, 0.38f, 0.3f, 0.55f )
			};

			var (falloff, widthMul) = c.Kind switch
			{
				ThornsWorldRoadCorridorKind.Radial or ThornsWorldRoadCorridorKind.Ring
					=> (tuning.CityEdgeFalloff, tuning.CityInfluenceWidthMul),
				ThornsWorldRoadCorridorKind.MainStreet
					=> (tuning.TownEdgeFalloff, tuning.TownInfluenceWidthMul),
				_ => (tuning.TrailEdgeFalloff, tuning.TrailInfluenceWidthMul)
			};
			var halfW = c.HalfWidth * widthMul;
			var outer = halfW + falloff;

			var a = wt.PointToWorld( new Vector3( c.A.x, c.A.y, 74f ) );
			var b = wt.PointToWorld( new Vector3( c.B.x, c.B.y, 74f ) );
			dbg.Line( a, b, col, durationSeconds, default, false );

			var mid = ( a + b ) * 0.5f;
			dbg.Line( mid, mid + Vector3.Up * ( halfW * 0.12f ), col, durationSeconds, default, false );

			DrawWidthTick( dbg, wt, c.A, halfW, outer, col.WithAlpha( col.a * 0.45f ), durationSeconds );
			DrawWidthTick( dbg, wt, c.B, halfW, outer, col.WithAlpha( col.a * 0.45f ), durationSeconds );
		}
	}

	static void DrawWidthTick(
		DebugOverlaySystem dbg,
		Transform wt,
		Vector2 p,
		float halfW,
		float outer,
		Color color,
		float duration )
	{
		var c = wt.PointToWorld( new Vector3( p.x, p.y, 78f ) );
		dbg.Line( c + new Vector3( halfW, 0, 0 ), c + new Vector3( outer, 0, 0 ), color, duration, default, false );
		dbg.Line( c + new Vector3( -halfW, 0, 0 ), c + new Vector3( -outer, 0, 0 ), color, duration, default, false );
	}
}
