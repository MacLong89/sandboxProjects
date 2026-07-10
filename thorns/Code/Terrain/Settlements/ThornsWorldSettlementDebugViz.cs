namespace Sandbox;

/// <summary>Debug overlays for macro settlement layout (city, towns, trails, isolated POIs).</summary>
public static class ThornsWorldSettlementDebugViz
{
	public static void DrawPlan(
		Scene scene,
		GameObject chunkRoot,
		ThornsWorldSettlementPlan plan,
		float durationSeconds = 60f )
	{
		if ( scene is null || chunkRoot is null || !chunkRoot.IsValid() || plan is null )
			return;

		var dbg = scene.GetSystem<DebugOverlaySystem>();
		if ( dbg is null )
			return;

		var wt = chunkRoot.Transform.World;
		DrawZone( dbg, wt, plan.MainCity, new Color( 0.35f, 0.75f, 1f, 0.9f ), durationSeconds, 140f );
		DrawCityRings( dbg, wt, plan.MainCity, durationSeconds );
		DrawBuildingSlots( dbg, wt, plan.MainCity, new Color( 0.9f, 0.95f, 1f, 0.85f ), durationSeconds, 160f );

		for ( var i = 0; i < plan.Towns.Count; i++ )
		{
			var c = i switch
			{
				0 => new Color( 0.4f, 0.95f, 0.45f, 0.85f ),
				1 => new Color( 0.95f, 0.85f, 0.35f, 0.85f ),
				_ => new Color( 0.9f, 0.5f, 0.95f, 0.85f )
			};
			DrawZone( dbg, wt, plan.Towns[i], c, durationSeconds, 72f );
			DrawBuildingSlots( dbg, wt, plan.Towns[i], c, durationSeconds, 88f );
		}

		DrawTrails( dbg, wt, plan.Trails, durationSeconds );
		DrawIsolatedClearance( dbg, wt, plan, durationSeconds );
	}

	static void DrawBuildingSlots(
		DebugOverlaySystem dbg,
		Transform wt,
		ThornsWorldSettlementZone zone,
		Color color,
		float duration,
		float height )
	{
		if ( zone.BuildingSlots is null )
			return;

		for ( var i = 0; i < zone.BuildingSlots.Count; i++ )
		{
			var slot = zone.BuildingSlots[i];
			var ang = i * ( MathF.PI * 2f / Math.Max( 1, zone.BuildingSlots.Count ) );
			var rad = zone.Radius * 0.55f;
			var lx = zone.CenterLocal.x + MathF.Cos( ang ) * rad;
			var ly = zone.CenterLocal.y + MathF.Sin( ang ) * rad;
			var p = wt.PointToWorld( new Vector3( lx, ly, height ) );
			dbg.Line( p, p + Vector3.Up * 48f, color, duration, default, false );
		}
	}

	static void DrawTrails(
		DebugOverlaySystem dbg,
		Transform wt,
		IReadOnlyList<ThornsWorldTrailSegment> trails,
		float duration )
	{
		if ( trails is null )
			return;

		foreach ( var trail in trails )
		{
			var col = trail.Kind == ThornsWorldTrailKind.DirtRoad
				? new Color( 0.55f, 0.42f, 0.28f, 0.75f )
				: new Color( 0.45f, 0.38f, 0.3f, 0.55f );
			var a = wt.PointToWorld( new Vector3( trail.FromLocal.x, trail.FromLocal.y, 72f ) );
			var b = wt.PointToWorld( new Vector3( trail.ToLocal.x, trail.ToLocal.y, 72f ) );
			var steps = 12;
			var prev = a;
			for ( var s = 1; s <= steps; s++ )
			{
				var t = s / (float)steps;
				var wobble = MathF.Sin( t * MathF.PI * 2f ) * 18f;
				var mid = Vector3.Lerp( a, b, t );
				mid += new Vector3( wobble, -wobble * 0.5f, 0f );
				dbg.Line( prev, mid, col, duration, default, false );
				prev = mid;
			}
		}
	}

	static void DrawZone(
		DebugOverlaySystem dbg,
		Transform wt,
		ThornsWorldSettlementZone zone,
		Color color,
		float duration,
		float height )
	{
		var center = wt.PointToWorld( new Vector3( zone.CenterLocal.x, zone.CenterLocal.y, height ) );
		var segments = 48;
		var step = MathF.PI * 2f / segments;
		var prev = center + new Vector3( zone.Radius, 0, 0 );
		for ( var i = 1; i <= segments; i++ )
		{
			var a = i * step;
			var next = center + new Vector3( MathF.Cos( a ) * zone.Radius, MathF.Sin( a ) * zone.Radius, 0 );
			dbg.Line( prev, next, color, duration, default, false );
			prev = next;
		}

		dbg.Line( center, center + Vector3.Up * 220f, color, duration, default, false );
	}

	static void DrawCityRings( DebugOverlaySystem dbg, Transform wt, ThornsWorldSettlementZone city, float duration )
	{
		var cell = ThornsBuildingModule.Cell;
		var center = wt.PointToWorld( new Vector3( city.CenterLocal.x, city.CenterLocal.y, 120f ) );
		DrawRing( dbg, center, cell * 4.5f, new Color( 1f, 0.35f, 0.35f, 0.75f ), duration );
		DrawRing( dbg, center, cell * 6.2f, new Color( 1f, 0.75f, 0.2f, 0.65f ), duration );
		DrawRing( dbg, center, cell * 5.4f, new Color( 0.35f, 0.75f, 1f, 0.55f ), duration );
	}

	static void DrawRing( DebugOverlaySystem dbg, Vector3 center, float radius, Color color, float duration )
	{
		var segments = 36;
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

	static void DrawIsolatedClearance( DebugOverlaySystem dbg, Transform wt, ThornsWorldSettlementPlan plan, float duration )
	{
		var c = plan.MainCity.CenterLocal;
		var center = wt.PointToWorld( new Vector3( c.x, c.y, 80f ) );
		var r = plan.IsolatedMinDistanceFromSettlements;
		var segments = 32;
		var step = MathF.PI * 2f / segments;
		var col = new Color( 0.55f, 0.55f, 0.55f, 0.35f );
		var prev = center + new Vector3( r, 0, 0 );
		for ( var i = 1; i <= segments; i++ )
		{
			var a = i * step;
			var next = center + new Vector3( MathF.Cos( a ) * r, MathF.Sin( a ) * r, 0 );
			dbg.Line( prev, next, col, duration, default, false );
			prev = next;
		}
	}

	public static void DrawIsolatedSite(
		Scene scene,
		GameObject chunkRoot,
		Vector2 localPos,
		ThornsProcBuildingType type,
		float durationSeconds = 60f )
	{
		if ( scene is null || chunkRoot is null || !chunkRoot.IsValid() )
			return;

		var dbg = scene.GetSystem<DebugOverlaySystem>();
		if ( dbg is null )
			return;

		var wt = chunkRoot.Transform.World;
		var col = type switch
		{
			ThornsProcBuildingType.MilitaryComplex => new Color( 0.9f, 0.35f, 0.35f, 0.9f ),
			ThornsProcBuildingType.Cabin => new Color( 0.55f, 0.85f, 0.45f, 0.9f ),
			ThornsProcBuildingType.RadioOutpost => new Color( 0.75f, 0.75f, 0.95f, 0.9f ),
			_ => new Color( 0.85f, 0.75f, 0.5f, 0.85f )
		};
		var p = wt.PointToWorld( new Vector3( localPos.x, localPos.y, 95f ) );
		dbg.Line( p, p + Vector3.Up * 120f, col, durationSeconds, default, false );
	}

	public static void LogPlan( ThornsWorldSettlementPlan plan )
	{
		if ( plan is null )
			return;

		Log.Info(
			$"[Thorns Settlement] {plan.MainCity.Label} @ ({plan.MainCity.CenterLocal.x:F0},{plan.MainCity.CenterLocal.y:F0}) r={plan.MainCity.Radius:F0} buildings={plan.MainCity.BuildingSlots?.Count ?? 0}" );

		for ( var i = 0; i < plan.Towns.Count; i++ )
		{
			var t = plan.Towns[i];
			Log.Info(
				$"[Thorns Settlement] {t.Label} @ ({t.CenterLocal.x:F0},{t.CenterLocal.y:F0}) r={t.Radius:F0} buildings={t.BuildingSlots?.Count ?? 0}" );
		}

		for ( var i = 0; i < plan.IsolatedSites.Count; i++ )
		{
			var iso = plan.IsolatedSites[i];
			Log.Info( $"[Thorns Settlement] isolated{i + 1}: {iso.Type}" );
		}

		Log.Info(
			$"[Thorns Settlement] total={ThornsWorldSettlementPlan.TotalBuildingCount} trails={plan.Trails?.Count ?? 0} wildernessClearance={plan.IsolatedMinDistanceFromSettlements:F0}" );
	}
}
