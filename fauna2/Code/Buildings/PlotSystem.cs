namespace Fauna2;

/// <summary>
/// Land ownership. The world is a grid of plots; the zoo starts with the
/// center plot and buys adjacent ones, which raises build area, the animal cap
/// and the guest cap. Expansion is the primary late-game money sink.
/// </summary>
public sealed class PlotSystem : Component
{
	public static PlotSystem Instance { get; private set; }

	/// <summary>Owned plot coordinates as "x,y" strings (NetList-friendly).</summary>
	[Sync( SyncFlags.FromHost )] public NetList<string> OwnedPlots { get; set; } = new();

	public int PlotCount => OwnedPlots.Count;
	public int AnimalCap => PlotCount * GameConstants.AnimalsPerPlot + (FranchiseSystem.Instance?.LegacyTokens ?? 0);
	public int GuestCap =>
		PlotCount * GameConstants.GuestsPerPlot + (ZooState.Instance?.GuestCapBonus ?? 0) + (FranchiseSystem.Instance?.LegacyTokens ?? 0) * 15;

	protected override void OnAwake() => Instance = this;
	protected override void OnDestroy() { if ( Instance == this ) Instance = null; }

	public void SetNewGameDefaults()
	{
		if ( !Networking.IsHost ) return;
		OwnedPlots.Clear();
		OwnedPlots.Add( Key( 0, 0 ) );
		Log.Info( $"[Fauna2 Plots] New game defaults — owned={OwnedPlots.Count} [{string.Join( ", ", OwnedPlots )}]" );
	}

	/// <summary>Host safety net when NetList drops the starter plot between session start and build.</summary>
	public void EnsureStarterPlot()
	{
		if ( !Networking.IsHost || OwnedPlots.Count > 0 ) return;

		OwnedPlots.Add( Key( 0, 0 ) );
		Log.Warning( "[Fauna2 Plots] OwnedPlots was empty — restored starter plot 0,0." );
	}

	// ── Queries (run on any machine) ────────────────────────

	public static string Key( int x, int y ) => $"{x},{y}";

	public static bool TryParseKey( string key, out int x, out int y )
	{
		x = y = 0;
		var parts = key.Split( ',' );
		return parts.Length == 2 && int.TryParse( parts[0], out x ) && int.TryParse( parts[1], out y );
	}

	public bool IsOwned( int x, int y ) => OwnedPlots.Contains( Key( x, y ) );

	/// <summary>Which plot a world position falls in.</summary>
	public static (int x, int y) PlotAt( Vector3 position )
	{
		var s = GameConstants.PlotSize;
		return ((int)MathF.Round( position.x / s ), (int)MathF.Round( position.y / s ));
	}

	public static Vector3 PlotCenter( int x, int y ) =>
		new( x * GameConstants.PlotSize, y * GameConstants.PlotSize, 0 );

	/// <summary>World-space plot bounds — avoids grid-rounding false negatives near plot edges.</summary>
	public bool IsWorldPointOnOwnedPlot( Vector3 position )
	{
		if ( OwnedPlots.Count == 0 && GameManager.Instance?.GameStarted == true )
			return IsPointInPlot( position, 0, 0 );

		foreach ( var key in OwnedPlots )
		{
			if ( !TryParseKey( key, out var px, out var py ) ) continue;
			if ( IsPointInPlot( position, px, py ) )
				return true;
		}

		return false;
	}

	/// <summary>All four corners on owned land — strict interior placement.</summary>
	public bool ContainsRect( Vector3 center, Vector2 size ) =>
		ContainsRect( center, size, requireAllCorners: true );

	/// <summary>
	/// Footprint ownership check. Entrances on plot edges may hang over wilderness —
	/// only the center must sit on owned grass.
	/// </summary>
	public bool ContainsRect( Vector3 center, Vector2 size, bool requireAllCorners )
	{
		if ( !requireAllCorners )
			return IsWorldPointOnOwnedPlot( center );

		var hx = size.x * 0.5f;
		var hy = size.y * 0.5f;
		return IsWorldPointOnOwnedPlot( center + new Vector3( hx, hy, 0 ) )
			&& IsWorldPointOnOwnedPlot( center + new Vector3( -hx, hy, 0 ) )
			&& IsWorldPointOnOwnedPlot( center + new Vector3( hx, -hy, 0 ) )
			&& IsWorldPointOnOwnedPlot( center + new Vector3( -hx, -hy, 0 ) );
	}

	/// <summary>True when a 1×1 build tile center sits on owned grass.</summary>
	public bool IsBuildTileCenterOnOwnedLand( Vector3 tileCenter ) =>
		IsWorldPointOnOwnedPlot( tileCenter );

	public bool IsValidEntrancePlacement( Vector3 center, Vector2 footprint )
	{
		if ( !IsWorldPointOnOwnedPlot( center ) )
			return false;

		var snapped = BuildSnap.SnapPlacement( center, footprint );
		foreach ( var tile in FootprintTileCenters( snapped, footprint ) )
		{
			if ( IsOnExteriorTerritoryEdge( tile ) )
				return true;
		}

		return false;
	}

	/// <summary>Nearest snapped 4×6 entrance anchor on the owned-land border.</summary>
	public Vector3? FindNearestEntrancePlacement( Vector3 cursor, Vector2 footprint )
	{
		Vector3? best = null;
		var bestDist = float.MaxValue;

		void Consider( Vector3 candidate )
		{
			if ( !IsValidEntrancePlacement( candidate, footprint ) )
				return;

			var dist = cursor.WithZ( 0 ).Distance( BuildSnap.SnapPlacement( candidate, footprint ).WithZ( 0 ) );
			if ( dist >= bestDist )
				return;

			bestDist = dist;
			best = BuildSnap.SnapPlacement( candidate, footprint );
		}

		Consider( cursor );

		foreach ( var edgeCell in EnumerateEntranceEdgeCells() )
			Consider( edgeCell );

		return best;
	}

	/// <summary>Build-cell centers occupied by a snapped footprint.</summary>
	public static IEnumerable<Vector3> FootprintTileCenters( Vector3 center, Vector2 footprint )
	{
		var snapped = BuildSnap.SnapPlacement( center, footprint );
		var hx = footprint.x * 0.5f;
		var hy = footprint.y * 0.5f;
		var tile = GameConstants.TileSize;

		foreach ( var tileCenter in GroundGrid.TileCentersInRect(
			         snapped.x - hx,
			         snapped.y - hy,
			         snapped.x + hx,
			         snapped.y + hy,
			         tile ) )
			yield return new Vector3( tileCenter.x, tileCenter.y, 0 );
	}

	/// <summary>All 1×1 build cells on owned land that face wilderness — used as entrance snap hints.</summary>
	public IEnumerable<Vector3> EnumerateEntranceEdgeCells()
	{
		var tile = GameConstants.TileSize;
		var half = GameConstants.PlotSize * 0.5f;

		foreach ( var (px, py) in OwnedPlotCoords() )
		{
			var plotCenter = PlotCenter( px, py );
			foreach ( var tileCenter in GroundGrid.TileCentersCoveringRect(
				         plotCenter.x - half,
				         plotCenter.y - half,
				         plotCenter.x + half,
				         plotCenter.y + half,
				         tile ) )
			{
				var candidate = new Vector3( tileCenter.x, tileCenter.y, 0 );
				if ( IsBuildTileCenterOnOwnedLand( candidate ) && IsOnExteriorTerritoryEdge( candidate ) )
					yield return candidate;
			}
		}
	}

	public static bool IsPointInPlot( Vector3 position, int px, int py )
	{
		var half = GameConstants.PlotSize * 0.5f;
		var center = PlotCenter( px, py );
		return MathF.Abs( position.x - center.x ) <= half && MathF.Abs( position.y - center.y ) <= half;
	}

	/// <summary>Owned plot coords, including starter fallback when NetList has not synced yet.</summary>
	public IEnumerable<(int x, int y)> OwnedPlotCoords()
	{
		if ( OwnedPlots.Count == 0 && GameManager.Instance?.GameStarted == true )
		{
			yield return (0, 0);
			yield break;
		}

		foreach ( var key in OwnedPlots )
		{
			if ( TryParseKey( key, out var px, out var py ) )
				yield return (px, py);
		}
	}

	/// <summary>
	/// True when a build tile center sits on the outer band of owned land facing wilderness.
	/// </summary>
	public bool IsOnExteriorTerritoryEdge( Vector3 position )
	{
		if ( !IsWorldPointOnOwnedPlot( position ) )
			return false;

		var band = GameConstants.OwnedOutskirtsBand;
		var half = GameConstants.PlotSize * 0.5f;

		foreach ( var (px, py) in OwnedPlotCoords() )
		{
			var center = PlotCenter( px, py );
			var local = position - center;
			if ( MathF.Abs( local.x ) > half || MathF.Abs( local.y ) > half )
				continue;

			var distToEdge = half - MathF.Max( MathF.Abs( local.x ), MathF.Abs( local.y ) );
			if ( distToEdge > band )
				continue;

			if ( !IsOwned( px + 1, py ) && local.x >= half - band )
				return true;

			if ( !IsOwned( px - 1, py ) && local.x <= -half + band )
				return true;

			if ( !IsOwned( px, py + 1 ) && local.y >= half - band )
				return true;

			if ( !IsOwned( px, py - 1 ) && local.y <= -half + band )
				return true;
		}

		return false;
	}

	/// <summary>Clamp a world point to the nearest position still inside owned plots.</summary>
	public Vector3 ClampToOwnedTerritory( Vector3 position, float inset = 0f )
	{
		if ( IsWorldPointOnOwnedPlot( position ) )
			return position;

		var half = GameConstants.PlotSize * 0.5f - inset;
		var best = position;
		var bestDist = float.MaxValue;

		foreach ( var (px, py) in OwnedPlotCoords() )
		{
			var center = PlotCenter( px, py );
			var clamped = new Vector3(
				position.x.Clamp( center.x - half, center.x + half ),
				position.y.Clamp( center.y - half, center.y + half ),
				position.z );

			var dist = position.WithZ( 0 ).Distance( clamped.WithZ( 0 ) );
			if ( dist >= bestDist )
				continue;

			bestDist = dist;
			best = clamped;
		}

		return best;
	}

	public int NextPlotCost() =>
		(int)(GameConstants.PlotBaseCost * MathF.Pow( GameConstants.PlotCostGrowth, PlotCount - 1 ));

	public bool IsBuyable( int x, int y )
	{
		if ( Math.Abs( x ) > GameConstants.PlotGridRadius || Math.Abs( y ) > GameConstants.PlotGridRadius )
			return false;
		if ( IsOwned( x, y ) ) return false;

		return IsOwned( x + 1, y ) || IsOwned( x - 1, y ) || IsOwned( x, y + 1 ) || IsOwned( x, y - 1 );
	}

	public IEnumerable<(int x, int y)> BuyablePlots()
	{
		var r = GameConstants.PlotGridRadius;
		for ( var x = -r; x <= r; x++ )
			for ( var y = -r; y <= r; y++ )
				if ( IsBuyable( x, y ) )
					yield return (x, y);
	}

	// ── Requests ────────────────────────────────────────────

	[Rpc.Host]
	public void RequestBuyPlot( int x, int y )
	{
		if ( !RpcAuthorization.IsOwnerCaller() ) return;

		var state = ZooState.Instance;

		if ( !IsBuyable( x, y ) )
		{
			state.Notify( "That plot can't be purchased.", "block" );
			return;
		}

		var cost = NextPlotCost();
		if ( !state.TrySpend( cost ) )
		{
			state.Notify( $"Expansion costs ${cost:n0} — keep earning!", "payments" );
			return;
		}

		OwnedPlots.Add( Key( x, y ) );
		state.AddXp( GameConstants.XpBuyPlot );
		state.AddPrestige( GameConstants.PrestigePlotPurchased );
		GameEvents.RaisePlotPurchased();
		state.Notify( $"Zoo expanded! ({PlotCount} plots, {AnimalCap} animal capacity)", "map" );
	}

	public bool TryGrantFreeExpansion()
	{
		if ( !Networking.IsHost ) return false;

		var buyable = BuyablePlots()
			.OrderBy( p => Math.Abs( p.x ) + Math.Abs( p.y ) )
			.ThenBy( p => p.x )
			.ThenBy( p => p.y )
			.ToList();
		if ( buyable.Count == 0 )
			return false;

		var next = buyable[0];
		if ( !IsBuyable( next.x, next.y ) )
			return false;

		OwnedPlots.Add( Key( next.x, next.y ) );
		ZooState.Instance?.AddXp( GameConstants.XpBuyPlot );
		ZooState.Instance?.AddPrestige( GameConstants.PrestigePlotPurchased );
		GameEvents.RaisePlotPurchased();
		return true;
	}

	/// <summary>Host only — used by save loading.</summary>
	public void SetOwnedPlots( IEnumerable<string> plots )
	{
		if ( !Networking.IsHost ) return;
		OwnedPlots.Clear();
		foreach ( var p in plots ) OwnedPlots.Add( p );
	}
}
