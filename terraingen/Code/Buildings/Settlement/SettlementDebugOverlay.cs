namespace Terraingen.Buildings.Settlement;

using Terraingen.Combat;

/// <summary>Debug overlay for street-first settlement layout (roads, lots, bounds).</summary>
public static class SettlementDebugOverlay
{
	[ConVar( "thorns_settlement_debug" )]
	public static bool Enabled { get; set; }

	public static IReadOnlyList<SettlementLayout> LastLayouts => _lastLayouts;

	static readonly List<SettlementLayout> _lastLayouts = new();

	public static void Publish( IEnumerable<SettlementLayout> layouts )
	{
		_lastLayouts.Clear();
		if ( layouts is null )
			return;

		foreach ( var layout in layouts )
		{
			if ( layout is null )
				continue;

			_lastLayouts.Add( layout );
		}
	}

	public static void Draw( DebugOverlaySystem overlay, float duration = 0.12f )
	{
		if ( !Enabled || overlay is null || _lastLayouts.Count == 0 )
			return;

		foreach ( var layout in _lastLayouts )
			DrawLayout( overlay, layout, duration );
	}

	static void DrawLayout( DebugOverlaySystem overlay, SettlementLayout layout, float duration )
	{
		if ( layout is null )
			return;

		DrawBounds( overlay, layout, duration );

		for ( var i = 0; i < layout.Roads.Count; i++ )
		{
			var road = layout.Roads[i];
			overlay.Line( road.Start, road.End, duration: duration );
			var mid = (road.Start + road.End) * 0.5f + Vector3.Up * 8f;
			overlay.Text( mid, $"{road.Type} #{i}", duration );
		}

		for ( var i = 0; i < layout.Lots.Count; i++ )
		{
			var lot = layout.Lots[i];
			var forward = lot.Rotation.Forward.WithZ( 0f ).Normal;
			var right = lot.Rotation.Right.WithZ( 0f ).Normal;
			var half = ThornsBuildingModule.ProcTownScatterExclusionHalfExtent;
			var c = lot.Position + Vector3.Up * 6f;
			overlay.Line( c, c + forward * half, duration: duration );
			overlay.Line( c - right * half, c + right * half, duration: duration );
		}
	}

	static void DrawBounds( DebugOverlaySystem overlay, SettlementLayout layout, float duration )
	{
		var center = layout.Center + Vector3.Up * 4f;
		var r = MathF.Max( 120f, layout.BoundsRadius );
		ThornsCollisionDebugDraw.DrawHorizontalRing( overlay, center, r, duration );
		overlay.Text( center + Vector3.Up * 48f, $"{ThornsPoiIdentityCatalog.Get( layout.Identity ).DisplayName} — {layout.TargetBuildingCount} bld", duration );
	}
}
