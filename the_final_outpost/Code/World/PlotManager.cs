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
			if ( GameCore.Instance?.IsCure == true )
				RivalCivManager.EnsureSeeded( save );

			foreach ( var key in save.OwnedPlots )
				if ( PlotGrid.ParseKey( key, out var x, out var y ) && PlotGrid.InGrid( x, y ) )
					_owned.Add( (x, y) );

			// AUDIT FIX M7: restore mid-clear progress (was runtime-only → lost on quit).
			save.PlotClearProgress ??= new Dictionary<string, float>();
			foreach ( var (key, frac) in save.PlotClearProgress )
			{
				if ( !PlotGrid.ParseKey( key, out var x, out var y ) ) continue;
				if ( !IsOwned( x, y ) || IsCleared( x, y ) ) continue;
				_clearProgress[(x, y)] = Math.Clamp( frac, 0f, 0.999f );
			}
		}

		_owned.Add( (0, 0) );
		RebuildVisuals();
	}

	/// <summary>
	/// AUDIT FIX M7: push live clear fractions into the save before disk write.
	/// Called from GameCore.SaveManagerTouch.
	/// </summary>
	public void SaveClearProgress( SaveData save )
	{
		if ( save is null ) return;
		save.PlotClearProgress ??= new Dictionary<string, float>();
		save.PlotClearProgress.Clear();
		foreach ( var ((x, y), frac) in _clearProgress )
		{
			if ( frac <= 0f || IsCleared( x, y ) ) continue;
			save.PlotClearProgress[PlotGrid.Key( x, y )] = Math.Clamp( frac, 0f, 0.999f );
		}
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
			if ( core.IsCure
				&& PlotFeatureGrid.KindAt( x, y ) == PlotKind.NeutralCiv
				&& !PlotCivActions.IsRaided( core.Save, x, y ) )
				continue;

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
			KnowledgeGain.OnPlotCleared( core );
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

		// Rival territory requires an assault fight, not an instant claim.
		if ( RivalCivManager.IsRivalOwned( core.Save, x, y ) )
			return core.TryStartRivalAssault( x, y );

		var cost = PlotGrid.BuyCostEffective( x, y );
		if ( !core.Wallet.TrySpend( cost ) ) return false;

		var key = PlotGrid.Key( x, y );
		_owned.Add( (x, y) );
		if ( !core.Save.OwnedPlots.Contains( key ) )
			core.Save.OwnedPlots.Add( key );

		KnowledgeGain.OnPlotClaimed( core );
		RebuildVisuals();
		Sfx.Play( Sfx.Purchase );
		core.SaveManagerTouch();
		return true;
	}

	/// <summary>Called when a rival plot assault is won — claim the tile without charging again.</summary>
	public void CompleteRivalAssaultClaim( int x, int y )
	{
		var core = GameCore.Instance;
		if ( core is null ) return;

		var key = PlotGrid.Key( x, y );
		core.Save.RivalOwnedPlots?.Remove( key );
		_owned.Add( (x, y) );
		if ( !core.Save.OwnedPlots.Contains( key ) )
			core.Save.OwnedPlots.Add( key );

		KnowledgeGain.OnPlotClaimed( core );
		RebuildVisuals();
		core.SaveManagerTouch();
	}

	// --- Visuals ------------------------------------------------------------

	public void RebuildVisuals()
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
			var save = GameCore.Instance?.Save;
			var rivalOwned = RivalCivManager.IsRivalOwned( save, x, y );
			var rivalSeed = RivalCivManager.IsSeedPlot( x, y );
			var assaultingThis = HostileForceSystem.Instance is { IsAssault: true } h
				&& h.AssaultPlotX == x && h.AssaultPlotY == y;
			var boss = PlotWorldRolls.BossAt( x, y );
			var boost = PlotWorldRolls.BoostAt( x, y );
			var bossCleared = save?.ClearedBossPlots?.Contains( PlotGrid.Key( x, y ) ) == true;
			var boostClaimed = PlotBoosts.IsClaimed( save, x, y );

			if ( !cleared )
			{
				BuildResourceProps( x, y, kind );
				if ( feature != PlotKind.Standard )
					BuildFeatureMarker( x, y, feature );
				if ( boss != BossKind.None && !bossCleared )
					BuildBossLandmark( x, y, boss );
				else if ( boost != PlotBoostKind.None && !boostClaimed )
					BuildBoostLandmark( x, y, boost );
			}

			if ( rivalSeed && !IsOwned( x, y ) )
			{
				// Keep walls/CP/civic buildings during assault; live units replace daytime guards/towers.
				BuildRivalCommandPost( x, y, includeGuards: !assaultingThis, includeDefenseBuildings: !assaultingThis );
			}
			else if ( rivalOwned && !IsOwned( x, y ) && !rivalSeed && !assaultingThis )
			{
				// Expanded rival land: lighter presence — a few guards without a command post.
				BuildRivalOutpostGuards( x, y );
			}

			if ( rivalOwned && !IsOwned( x, y ) )
				BuildBoundary( x, y, new Color( 0.85f, 0.28f, 0.32f ), height: 16f );

			if ( IsOwned( x, y ) )
				BuildBoundary( x, y, cleared ? new Color( 0.7f, 0.7f, 0.55f ) : ResourceInfo.Tint( kind ).WithAlpha( 1f ), height: 12f );
		}
	}

	private void BuildBoundary( int x, int y, Color tint, float height )
	{
		var center = PlotGrid.CenterWorld( x, y );
		var half = GameConstants.PlotSize * 0.5f - GameConstants.U( 8f );
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

	private void BuildBoostLandmark( int x, int y, PlotBoostKind boost )
	{
		var def = PlotWorldRolls.GetBoost( boost );
		var center = PlotGrid.CenterWorld( x, y );
		var rng = new Random( unchecked( x * 48271 + y * 27733 ) );

		switch ( boost )
		{
			case PlotBoostKind.FertileSoil:
				for ( var i = 0; i < 5; i++ )
					ScatterGrass( center, rng, GameConstants.PlotSize * 0.22f, 1, new Color( 0.72f, 0.92f, 0.28f ) );
				AddProp( "Boost", MeshPrimitives.Pyramid, MeshPrimitives.Mat,
					center + Vector3.Up * 36f, new Vector3( 48f, 48f, 40f ), def.Tint );
				break;
			case PlotBoostKind.ScrapForge:
				AddProp( "Boost", MeshPrimitives.Box, StylizedMaterials.Stone,
					center + Vector3.Up * 24f, new Vector3( 52f, 40f, 48f ), def.Tint );
				AddProp( "BoostGlow", MeshPrimitives.Cylinder, MeshPrimitives.Mat,
					center + Vector3.Up * 56f, new Vector3( 20f, 20f, 8f ), new Color( 1f, 0.55f, 0.15f ) );
				break;
			case PlotBoostKind.Archive:
				AddProp( "Boost", MeshPrimitives.Box, StylizedMaterials.Stone,
					center + Vector3.Up * 40f, new Vector3( 34f, 34f, 80f ), def.Tint );
				AddProp( "BoostTop", MeshPrimitives.Pyramid, MeshPrimitives.Mat,
					center + Vector3.Up * 88f, new Vector3( 40f, 40f, 28f ), def.Tint * 1.2f );
				break;
			case PlotBoostKind.IronRich:
				for ( var i = 0; i < 6; i++ )
					BuildRockCluster( ScatterXY( center, rng, GameConstants.PlotSize * 0.28f ), rng );
				break;
			case PlotBoostKind.HealingSpring:
				AddProp( "Boost", MeshPrimitives.Cylinder, MeshPrimitives.Mat,
					center + Vector3.Up * 4f, new Vector3( 64f, 64f, 8f ), def.Tint.WithAlpha( 0.75f ) );
				AddProp( "BoostSpring", MeshPrimitives.Cylinder, MeshPrimitives.Mat,
					center + Vector3.Up * 22f, new Vector3( 28f, 28f, 44f ), def.Tint );
				break;
		}
	}

	private void BuildBossLandmark( int x, int y, BossKind boss )
	{
		var def = PlotWorldRolls.GetBoss( boss );
		var center = PlotGrid.CenterWorld( x, y );

		switch ( boss )
		{
			case BossKind.Giant:
				AddProp( "Boss", MeshPrimitives.Box, MeshPrimitives.Mat,
					center + Vector3.Up * 70f, new Vector3( 44f, 36f, 140f ), def.Tint );
				AddProp( "BossHead", MeshPrimitives.Box, MeshPrimitives.Mat,
					center + Vector3.Up * 150f, new Vector3( 32f, 28f, 32f ), def.Tint * 0.9f );
				break;
			case BossKind.MutantBeast:
				AddProp( "Boss", MeshPrimitives.Cylinder, MeshPrimitives.Mat,
					center + Vector3.Up * 36f, new Vector3( 90f, 70f, 72f ), def.Tint );
				AddProp( "BossSpine", MeshPrimitives.Pyramid, MeshPrimitives.Mat,
					center + Vector3.Up * 78f, new Vector3( 36f, 36f, 48f ), def.Tint * 1.1f );
				break;
			case BossKind.MilitaryConvoy:
				for ( var i = -1; i <= 1; i++ )
					AddProp( "Boss", MeshPrimitives.Box, StylizedMaterials.Stone,
						center + new Vector3( i * 55f, 0f, 22f ), new Vector3( 48f, 28f, 44f ), def.Tint );
				break;
			case BossKind.InfectedHive:
				for ( var i = 0; i < 5; i++ )
				{
					var ang = i * MathF.PI * 2f / 5f;
					var offset = new Vector3( MathF.Cos( ang ) * 40f, MathF.Sin( ang ) * 40f, 0f );
					AddProp( "Boss", MeshPrimitives.Cylinder, MeshPrimitives.Mat,
						center + offset + Vector3.Up * 34f, new Vector3( 22f, 22f, 68f ), def.Tint );
				}
				AddProp( "BossCore", MeshPrimitives.Cylinder, MeshPrimitives.Mat,
					center + Vector3.Up * 48f, new Vector3( 36f, 36f, 96f ), def.Tint * 0.8f );
				break;
		}
	}

	private void BuildRivalOutpostGuards( int x, int y )
	{
		var center = PlotGrid.CenterWorld( x, y );
		var layout = RivalGarrison.Build( x, y );
		var tint = new Color( 0.78f, 0.3f, 0.32f );
		var show = Math.Min( 3, layout.Recruits.Count );
		for ( var i = 0; i < show; i++ )
		{
			var slot = layout.Recruits[i];
			var rp = center + slot.LocalOffset * 0.7f;
			rp.z = OutpostTerrain.SampleHeight( rp.x, rp.y );
			var go = new GameObject( _root, true, "RivalPatrolVisual" );
			go.WorldPosition = rp;
			var character = go.Components.Create<CharacterModel>();
			var def = RecruitWeapons.Get( slot.Weapon );
			character.Setup( tint, def.WorldModel, def.Hold, def.WeaponScale * 0.9f );
			character.Tick( Vector3.Zero, Rotation.FromYaw( i * 90f ) );
		}

		if ( layout.Buildings.Count > 0 )
		{
			var slot = layout.Buildings[0];
			var tp = center + slot.LocalOffset * 0.8f;
			tp.z = OutpostTerrain.SampleHeight( tp.x, tp.y );
			var go = new GameObject( _root, true, "RivalOutpostBuilding" );
			go.WorldPosition = tp;
			BuildingVisual.Build( go, slot.Id, tp, includeRubble: false );
		}
	}

	private void BuildRivalCommandPost( int x, int y, bool includeGuards, bool includeDefenseBuildings )
	{
		var center = PlotGrid.CenterWorld( x, y );
		var layout = RivalGarrison.Build( x, y );
		RivalBaseVisual.BuildSeed( _root, center, layout, includeGuards, includeDefenseBuildings );
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
