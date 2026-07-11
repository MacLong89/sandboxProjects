namespace FinalOutpost;

/// <summary>
/// Owns the parcels of land surrounding the base. The player claims plots outward from the home
/// base; each non-home plot carries a resource (wood / stone / water) that foragers can harvest.
/// Also drives the visual props (trees, rocks, shoreline reeds) and plot boundary markers.
/// </summary>
public sealed class PlotManager : Component
{
	public static PlotManager Instance { get; private set; }

	private readonly HashSet<(int x, int y)> _owned = new();
	private readonly Dictionary<(int x, int y), float> _clearProgress = new();
	private GameObject _root;

	protected override void OnAwake() => Instance = this;

	protected override void OnDestroy()
	{
		if ( Instance == this ) Instance = null;
		_root?.Destroy();
	}

	public bool IsOwned( int x, int y ) => _owned.Contains( (x, y) );

	public bool IsCleared( int x, int y )
	{
		var save = GameCore.Instance?.Save;
		return save is not null && save.ClearedPlots.Contains( PlotGrid.Key( x, y ) );
	}

	/// <summary>The home base and any owned+cleared plot can host player structures.</summary>
	public bool IsBuildable( int x, int y )
	{
		if ( PlotGrid.IsHome( x, y ) ) return true;
		return IsOwned( x, y ) && IsCleared( x, y );
	}

	/// <summary>0..1 land-clearing progress for an owned plot (1 once fully cleared).</summary>
	public float ClearFraction( int x, int y )
	{
		if ( IsCleared( x, y ) ) return 1f;
		return _clearProgress.TryGetValue( (x, y), out var p ) ? Math.Clamp( p, 0f, 1f ) : 0f;
	}

	/// <summary>A plot can be claimed if it's in-grid, unowned, and orthogonally touches an owned plot.</summary>
	public bool IsBuyable( int x, int y )
	{
		if ( !PlotGrid.InGrid( x, y ) || IsOwned( x, y ) ) return false;
		return IsOwned( x - 1, y ) || IsOwned( x + 1, y ) || IsOwned( x, y - 1 ) || IsOwned( x, y + 1 );
	}

	/// <summary>Half-extent (from world origin) of the claimed territory's bounding square.</summary>
	public float ClaimedHalfExtent
	{
		get
		{
			var half = GameConstants.PlotSize * 0.5f;
			foreach ( var (x, y) in _owned )
			{
				var reach = (Math.Max( Math.Abs( x ), Math.Abs( y ) ) + 0.5f) * GameConstants.PlotSize;
				if ( reach > half ) half = reach;
			}
			return half;
		}
	}

	public void RebuildFromSave()
	{
		_owned.Clear();
		_clearProgress.Clear();
		var save = GameCore.Instance?.Save;
		if ( save is not null )
		{
			foreach ( var key in save.OwnedPlots )
				if ( PlotGrid.ParseKey( key, out var x, out var y ) && PlotGrid.InGrid( x, y ) )
					_owned.Add( (x, y) );
		}

		_owned.Add( (0, 0) );
		RebuildVisuals();
	}

	protected override void OnUpdate()
	{
		var core = GameCore.Instance;
		if ( core is null ) return;
		if ( core.Phase != GamePhase.Day && core.Phase != GamePhase.Night ) return;

		var wm = WorkerManager.Instance;
		if ( wm is null ) return;

		var dt = Time.Delta;
		List<(int x, int y)> justCleared = null;

		foreach ( var (x, y) in _owned )
		{
			if ( PlotGrid.IsHome( x, y ) || IsCleared( x, y ) ) continue;
			if ( PlotGrid.HarvestResourceAt( x, y ) == ResourceKind.None ) continue;

			var foragers = wm.CountForagersOn( x, y );
			if ( foragers <= 0 ) continue;

			_clearProgress.TryGetValue( (x, y), out var p );
			var clearMult = 1f;
			if ( core.IsCure )
				clearMult = TeamBonuses.PlotClearSpeedMult( core );

			p += foragers / GameConstants.PlotClearSeconds * dt * clearMult;

			if ( p >= 1f )
			{
				(justCleared ??= new()).Add( (x, y) );
				_clearProgress.Remove( (x, y) );
			}
			else
			{
				_clearProgress[(x, y)] = p;
			}
		}

		if ( justCleared is null ) return;

		foreach ( var (x, y) in justCleared )
		{
			var key = PlotGrid.Key( x, y );
			if ( !core.Save.ClearedPlots.Contains( key ) )
				core.Save.ClearedPlots.Add( key );

			PlotRewards.OnPlotCleared( core, x, y );
		}

		RebuildVisuals();
		Sfx.Play( Sfx.Purchase );
		core.SaveManagerTouch();
	}

	public bool TryBuyPlot( int x, int y )
	{
		var core = GameCore.Instance;
		if ( core is null || core.Phase != GamePhase.Day ) return false;
		if ( !IsBuyable( x, y ) ) return false;

		if ( !core.Wallet.TrySpend( PlotGrid.BuyCostEffective( x, y ) ) ) return false;

		_owned.Add( (x, y) );
		if ( !core.Save.OwnedPlots.Contains( PlotGrid.Key( x, y ) ) )
			core.Save.OwnedPlots.Add( PlotGrid.Key( x, y ) );

		RebuildVisuals();
		Sfx.Play( Sfx.Purchase );
		core.SaveManagerTouch();
		return true;
	}

	// --- Visuals ------------------------------------------------------------

	private void RebuildVisuals()
	{
		_root?.Destroy();
		_root = new GameObject( true, "Plots" );

		for ( var x = -PlotGrid.Radius; x <= PlotGrid.Radius; x++ )
		for ( var y = -PlotGrid.Radius; y <= PlotGrid.Radius; y++ )
		{
			if ( PlotGrid.IsHome( x, y ) )
				continue;

			var kind = PlotGrid.HarvestResourceAt( x, y );
			var cleared = IsOwned( x, y ) && IsCleared( x, y );
			var feature = PlotGrid.FeatureKindAt( x, y );

			// Resource props render on every plot (owned or not) so the surrounding land
			// looks alive from the very first game and players can see what each plot offers.
			// Cleared plots are stripped bare — the trees/rocks are gone and the land is buildable.
			if ( !cleared )
			{
				BuildResourceProps( x, y, kind );
				if ( feature != PlotKind.Standard )
					BuildFeatureMarker( x, y, feature );
			}

			// Only owned plots get the coloured boundary rail (marks your territory).
			if ( IsOwned( x, y ) )
				BuildBoundary( x, y, cleared ? new Color( 0.7f, 0.7f, 0.55f ) : ResourceInfo.Tint( kind ).WithAlpha( 1f ), height: 12f );
		}
	}

	private void BuildBoundary( int x, int y, Color tint, float height )
	{
		var center = PlotGrid.CenterWorld( x, y );
		var half = GameConstants.PlotSize * 0.5f - 8f;
		var t = 10f;

		// Four low edge rails to read the parcel outline without hiding the grass.
		AddRail( center + new Vector3( 0f, half, 0f ), new Vector3( half * 2f, t, height ), tint );
		AddRail( center + new Vector3( 0f, -half, 0f ), new Vector3( half * 2f, t, height ), tint );
		AddRail( center + new Vector3( half, 0f, 0f ), new Vector3( t, half * 2f, height ), tint );
		AddRail( center + new Vector3( -half, 0f, 0f ), new Vector3( t, half * 2f, height ), tint );
	}

	private void AddRail( Vector3 baseCenter, Vector3 size, Color tint )
	{
		var pos = baseCenter;
		pos.z = OutpostTerrain.SampleHeight( pos.x, pos.y ) + size.z * 0.5f;

		var go = new GameObject( _root, true, "PlotRail" );
		go.WorldPosition = pos;
		go.LocalScale = MeshPrimitives.BoxScale( size );
		var mr = go.Components.Create<ModelRenderer>();
		mr.Model = MeshPrimitives.Box;
		mr.MaterialOverride = MeshPrimitives.Mat;
		mr.Tint = tint;
	}

	private void BuildResourceProps( int x, int y, ResourceKind kind )
	{
		var center = PlotGrid.CenterWorld( x, y );
		var rng = new Random( unchecked( x * 92821 + y * 68917 + 5 ) );
		var spread = GameConstants.PlotSize * 0.36f;

		switch ( kind )
		{
			case ResourceKind.Wood:
				for ( var i = 0; i < 9; i++ )
					BuildTree( ScatterXY( center, rng, spread ), rng );
				ScatterGrass( center, rng, spread, 10, new Color( 0.32f, 0.55f, 0.26f ) );
				break;
			case ResourceKind.Stone:
				for ( var i = 0; i < 5; i++ )
					BuildRockCluster( ScatterXY( center, rng, spread ), rng );
				ScatterGrass( center, rng, spread, 6, new Color( 0.4f, 0.46f, 0.34f ) );
				break;
			case ResourceKind.Water:
				// Shoreline dressing only — the global sea plane provides the water visual.
				ScatterGrass( center, rng, spread, 8, new Color( 0.38f, 0.56f, 0.3f ) );
				for ( var i = 0; i < 4; i++ )
					BuildReed( ScatterXY( center, rng, GameConstants.PlotSize * 0.28f ), rng );
				break;
			case ResourceKind.Food:
				for ( var i = 0; i < 6; i++ )
					ScatterGrass( center, rng, spread, 1, new Color( 0.55f, 0.82f, 0.32f ) );
				break;
			case ResourceKind.Supplies:
				for ( var i = 0; i < 4; i++ )
					BuildRockCluster( ScatterXY( center, rng, spread * 0.6f ), rng );
				break;
			case ResourceKind.Knowledge:
				AddProp( "Ruin", MeshPrimitives.Box, StylizedMaterials.Stone,
					center + Vector3.Up * 28f, new Vector3( 36f, 36f, 56f ), new Color( 0.55f, 0.75f, 0.95f ) );
				break;
		}
	}

	private void BuildFeatureMarker( int x, int y, PlotKind feature )
	{
		var def = PlotFeatureCatalog.Get( feature );
		var center = PlotGrid.CenterWorld( x, y );
		AddProp( "Feature", MeshPrimitives.Cylinder, MeshPrimitives.Mat,
			center + Vector3.Up * 48f, new Vector3( 18f, 18f, 96f ), def.MarkerTint.WithAlpha( 0.85f ) );
	}

	private static Vector3 ScatterXY( Vector3 center, Random rng, float spread )
	{
		var px = center.x + ((float)rng.NextDouble() * 2f - 1f) * spread;
		var py = center.y + ((float)rng.NextDouble() * 2f - 1f) * spread;
		return new Vector3( px, py, OutpostTerrain.SampleHeight( px, py ) );
	}

	private ModelRenderer AddProp( string name, Model model, Material mat, Vector3 pos, Vector3 size, Color tint, float yaw = 0f )
	{
		var go = new GameObject( _root, true, name );
		go.WorldPosition = pos;
		if ( yaw != 0f ) go.WorldRotation = Rotation.FromYaw( yaw );
		go.LocalScale = MeshPrimitives.ScaleFor( model, size );
		var mr = go.Components.Create<ModelRenderer>();
		mr.Model = model;
		mr.MaterialOverride = mat;
		mr.Tint = tint;
		return mr;
	}

	private void BuildTree( Vector3 at, Random rng )
	{
		var scale = 0.8f + (float)rng.NextDouble() * 0.6f;
		var trunkH = 50f * scale;

		AddProp( "Trunk", MeshPrimitives.Cylinder, StylizedMaterials.Wood,
			at + Vector3.Up * (trunkH * 0.5f), new Vector3( 13f, 13f, trunkH ),
			new Color( 0.42f, 0.29f, 0.16f ) );

		// Stacked foliage tiers give a stylized conifer silhouette.
		var green = new Color( 0.22f + (float)rng.NextDouble() * 0.06f, 0.5f, 0.24f );
		AddProp( "Foliage", MeshPrimitives.Pyramid, MeshPrimitives.Mat,
			at + Vector3.Up * (trunkH + 22f * scale), new Vector3( 78f * scale, 78f * scale, 62f * scale ), green );
		AddProp( "Foliage", MeshPrimitives.Pyramid, MeshPrimitives.Mat,
			at + Vector3.Up * (trunkH + 52f * scale), new Vector3( 58f * scale, 58f * scale, 54f * scale ), green * 1.08f );
		AddProp( "Foliage", MeshPrimitives.Pyramid, MeshPrimitives.Mat,
			at + Vector3.Up * (trunkH + 80f * scale), new Vector3( 38f * scale, 38f * scale, 46f * scale ), green * 1.16f );
	}

	private void BuildRockCluster( Vector3 at, Random rng )
	{
		var count = 2 + rng.Next( 3 );
		for ( var i = 0; i < count; i++ )
		{
			var ox = ((float)rng.NextDouble() * 2f - 1f) * 34f;
			var oy = ((float)rng.NextDouble() * 2f - 1f) * 34f;
			var p = new Vector3( at.x + ox, at.y + oy, OutpostTerrain.SampleHeight( at.x + ox, at.y + oy ) );
			var s = 24f + (float)rng.NextDouble() * 40f;
			var shade = 0.5f + (float)rng.NextDouble() * 0.14f;
			AddProp( "Rock", MeshPrimitives.Cylinder, StylizedMaterials.Stone,
				p + Vector3.Up * (s * 0.35f), new Vector3( s, s * 0.9f, s * 0.7f ),
				new Color( shade, shade, shade + 0.04f ), (float)rng.NextDouble() * 360f );
		}
	}

	private void BuildReed( Vector3 at, Random rng )
	{
		var h = 40f + (float)rng.NextDouble() * 30f;
		AddProp( "Reed", MeshPrimitives.Cylinder, MeshPrimitives.Mat,
			at + Vector3.Up * (h * 0.5f), new Vector3( 6f, 6f, h ), new Color( 0.5f, 0.62f, 0.3f ) );
	}

	private void ScatterGrass( Vector3 center, Random rng, float spread, int count, Color tint )
	{
		for ( var i = 0; i < count; i++ )
		{
			var p = ScatterXY( center, rng, spread );
			var h = 12f + (float)rng.NextDouble() * 12f;
			AddProp( "Grass", MeshPrimitives.Pyramid, MeshPrimitives.Mat,
				p + Vector3.Up * (h * 0.5f), new Vector3( 22f, 22f, h ), tint, (float)rng.NextDouble() * 360f );
		}
	}
}
