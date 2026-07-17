namespace FinalOutpost;

/// <summary>
/// Builds and owns the outpost: ground, command post, and perimeter walls.
/// Tracks command-post HP and wall segment health.
/// </summary>
public sealed class OutpostManager : Component
{
	public static OutpostManager Instance { get; private set; }

	public Vector3 CorePosition { get; private set; }
	public float CoreHealth { get; private set; }
	public float CoreMaxHealth { get; private set; }

	public IReadOnlyList<WallSegment> Walls => _walls;

	private readonly List<WallSegment> _walls = new();
	private readonly List<GameObject> _wallCorners = new();
	private readonly List<(ModelRenderer Renderer, Color Base)> _coreParts = new();
	private GameObject _coreGo;
	private GameObject _terrainGo;
	private double _scrapAccum;

	protected override void OnAwake() => Instance = this;

	protected override void OnDestroy()
	{
		if ( Instance == this ) Instance = null;
	}

	protected override void OnUpdate()
	{
		var core = GameCore.Instance;
		if ( core is null || CoreHealth <= 0f || !core.IsCure ) return;
		if ( core.Phase is not GamePhase.Day and not GamePhase.Night ) return;

		_scrapAccum += CureConstants.CommandPostScrapPerSec * Time.Delta;
		if ( _scrapAccum < 0.5 ) return;

		var whole = Math.Floor( _scrapAccum );
		_scrapAccum -= whole;
		core.Wallet.Earn( whole, applyIncomeScale: false );
	}

	/// <summary>
	/// Writes live core/wall HP into the save blob.
	/// Call from GameCore.SaveManagerTouch so autosave/continue keeps damage.
	/// AUDIT FIX H1 — pair with <see cref="LoadPersistedHealth"/>.
	/// </summary>
	public void SavePersistedHealth( SaveData save )
	{
		if ( save is null ) return;

		save.SavedCoreHealth = CoreHealth;
		save.WallHealthByKey ??= new Dictionary<string, float>();
		save.WallHealthByKey.Clear();
		foreach ( var w in _walls )
		{
			if ( string.IsNullOrEmpty( w.Key ) ) continue;
			save.WallHealthByKey[w.Key] = w.Health;
		}
	}

	/// <summary>
	/// Applies saved core/wall HP after <see cref="Build"/> (which always spawns full HP).
	/// Legacy saves with SavedCoreHealth &lt; 0 keep the full heal (one-time migration behavior).
	/// AUDIT FIX H1.
	/// </summary>
	public void LoadPersistedHealth( SaveData save )
	{
		if ( save is null ) return;

		// Legacy / wipe / new run: negative sentinel means "leave at full".
		if ( save.SavedCoreHealth >= 0f )
			SetCoreHealth( save.SavedCoreHealth );

		if ( save.WallHealthByKey is null || save.WallHealthByKey.Count == 0 )
			return;

		foreach ( var w in _walls )
		{
			if ( string.IsNullOrEmpty( w.Key ) ) continue;
			if ( !save.WallHealthByKey.TryGetValue( w.Key, out var hp ) ) continue;
			w.SetHealth( hp );
		}
	}

	public void Build( UpgradeSystem upgrades )
	{
		Clear();

		BuildGround();
		BuildCore();
		BuildWalls( upgrades );
		ApplyUpgrades( upgrades, healToFull: true );
	}

	public void ApplyUpgrades( UpgradeSystem upgrades, bool healToFull )
	{
		CoreMaxHealth = upgrades.CoreMaxHp;
		CoreHealth = healToFull ? CoreMaxHealth : MathF.Min( CoreHealth, CoreMaxHealth );

		foreach ( var w in _walls )
			w.SetMaxHealth( upgrades.WallMaxHp, healToFull );

		RefreshCoreVisual();
	}

	public void DamageCore( float amount )
	{
		if ( CoreHealth <= 0f || amount <= 0f ) return;

		CoreHealth = MathF.Max( 0f, CoreHealth - amount );
		if ( CoreHealth <= 0f )
			DestructionFx.Burst( CorePosition, 1.6f );

		RefreshCoreVisual();
	}

	public WallSegment NearestWall( Vector3 pos, float maxDist )
	{
		WallSegment best = null;
		var bestD = maxDist * maxDist;

		foreach ( var w in _walls )
		{
			if ( w.IsBroken ) continue;
			var d = (w.Center - pos).LengthSquared;
			if ( d < bestD ) { bestD = d; best = w; }
		}

		return best;
	}

	public double MissingRepairCost()
	{
		var missing = MathF.Max( 0f, CoreMaxHealth - CoreHealth );
		foreach ( var w in _walls )
			missing += MathF.Max( 0f, w.MaxHealth - w.Health );

		return missing * GameConstants.RepairCostPerHp;
	}

	/// <summary>
	/// Dead API — instant free full heal. Prefer <see cref="RepairManager"/> (paid, timed).
	/// AUDIT note: left in place but do NOT wire to UI; conflicting with paid repairs.
	/// </summary>
	public void RepairAll()
	{
		CoreHealth = CoreMaxHealth;
		foreach ( var w in _walls )
			w.RepairToFull();
		RefreshCoreVisual();
	}

	public void RepairCore()
	{
		CoreHealth = CoreMaxHealth;
		RefreshCoreVisual();
	}

	public void RepairCoreBy( float amount )
	{
		if ( amount <= 0f ) return;
		CoreHealth = MathF.Min( CoreMaxHealth, CoreHealth + amount );
		RefreshCoreVisual();
	}

	public void SetCoreHealth( float hp )
	{
		CoreHealth = MathF.Min( CoreMaxHealth, MathF.Max( 0f, hp ) );
		RefreshCoreVisual();
	}

	public float WallIntegrityFraction
	{
		get
		{
			if ( _walls.Count == 0 ) return 1f;
			var sum = 0f;
			foreach ( var w in _walls ) sum += w.HealthFraction;
			return sum / _walls.Count;
		}
	}

	private void Clear()
	{
		TileOccupancy.ClearWalls();
		foreach ( var w in _walls ) w.Go?.Destroy();
		_walls.Clear();
		foreach ( var corner in _wallCorners ) corner?.Destroy();
		_wallCorners.Clear();
		_coreGo?.Destroy();
	}

	private void BuildGround()
	{
		if ( !_terrainGo.IsValid() )
		{
			_terrainGo = new GameObject( true, "Terrain" );
			_terrainGo.Components.Create<OutpostTerrain>().Build();
		}
	}

	private void BuildCore()
	{
		CorePosition = Vector3.Zero;
		_coreParts.Clear();
		_coreGo = new GameObject( true, "CommandPost" );
		_coreGo.WorldPosition = CorePosition;

		var stone = StylizedMaterials.Stone;
		var wood = StylizedMaterials.Wood;
		var roof = StylizedMaterials.Roof;

		CorePart( MeshPrimitives.Cylinder, stone, new Vector3( 0, 0, 16 ), new Vector3( 150, 150, 32 ), Color.White, new Color( 0.62f, 0.62f, 0.64f ) );
		CorePart( MeshPrimitives.Box, stone, new Vector3( 0, 0, 68 ), new Vector3( 110, 110, 72 ), new Color( 0.9f, 0.93f, 1f ), new Color( 0.5f, 0.55f, 0.7f ) );
		CorePart( MeshPrimitives.Box, wood, new Vector3( 0, 0, 106 ), new Vector3( 118, 118, 10 ), Color.White, new Color( 0.5f, 0.36f, 0.22f ) );
		CorePart( MeshPrimitives.Pyramid, roof, new Vector3( 0, 0, 134 ), new Vector3( 150, 150, 52 ), Color.White, new Color( 0.78f, 0.28f, 0.16f ) );
	}

	private void CorePart( Model model, Material mat, Vector3 localPos, Vector3 size, Color textured, Color fallback )
	{
		var useTexture = mat is not null && mat.IsValid() && mat != MeshPrimitives.Mat;
		var tint = useTexture ? textured : fallback;

		var go = new GameObject( _coreGo, true, "CorePart" );
		go.LocalPosition = localPos;
		go.LocalScale = MeshPrimitives.ScaleFor( model, size );
		var mr = go.Components.Create<ModelRenderer>();
		mr.Model = model;
		mr.MaterialOverride = mat;
		mr.Tint = tint;
		_coreParts.Add( (mr, tint) );
	}

	private void BuildWalls( UpgradeSystem upgrades )
	{
		var half = GameConstants.ArenaHalf;
		var segLen = (half * 2f) / GameConstants.SegmentsPerSide;
		var h = GameConstants.WallHeight;
		var t = GameConstants.WallThickness;

		for ( var i = 0; i < GameConstants.SegmentsPerSide; i++ )
		{
			var cx = -half + segLen * 0.5f + i * segLen;
			SpawnWall( new Vector3( cx, half, h * 0.5f ), new Vector3( segLen, t, h ), upgrades );
		}

		for ( var i = 0; i < GameConstants.SegmentsPerSide; i++ )
		{
			var cx = -half + segLen * 0.5f + i * segLen;
			SpawnWall( new Vector3( cx, -half, h * 0.5f ), new Vector3( segLen, t, h ), upgrades );
		}

		for ( var i = 0; i < GameConstants.SegmentsPerSide; i++ )
		{
			var cy = -half + segLen * 0.5f + i * segLen;
			SpawnWall( new Vector3( half, cy, h * 0.5f ), new Vector3( t, segLen, h ), upgrades );
		}

		for ( var i = 0; i < GameConstants.SegmentsPerSide; i++ )
		{
			var cy = -half + segLen * 0.5f + i * segLen;
			SpawnWall( new Vector3( -half, cy, h * 0.5f ), new Vector3( t, segLen, h ), upgrades );
		}

		// Visual-only corner piers — seal the ring joins (no extra HP / collision segments).
		SpawnCornerVisual( half, half, 1f, 1f );
		SpawnCornerVisual( -half, half, -1f, 1f );
		SpawnCornerVisual( half, -half, 1f, -1f );
		SpawnCornerVisual( -half, -half, -1f, -1f );
	}

	private void SpawnCornerVisual( float x, float y, float outwardX, float outwardY )
	{
		var h = GameConstants.WallHeight;
		var t = GameConstants.WallThickness;
		var center = new Vector3( x, y, h * 0.5f );
		center.z += OutpostTerrain.SampleHeight( center.x, center.y );

		var go = new GameObject( true, "WallCorner" );
		go.WorldPosition = center;
		WallScaffoldVisual.BuildCorner( go, t, h, outwardX, outwardY, null );
		_wallCorners.Add( go );
	}

	private void SpawnWall( Vector3 center, Vector3 size, UpgradeSystem upgrades )
	{
		var key = $"{(int)MathF.Round( center.x )},{(int)MathF.Round( center.y )}";

		// Skip segments the player has torn down (persisted across reloads).
		var save = GameCore.Instance?.Save;
		if ( save is not null && save.RemovedWalls.Contains( key ) )
			return;

		center.z += OutpostTerrain.SampleHeight( center.x, center.y );

		var go = new GameObject( true, "Wall" );
		go.WorldPosition = center;

		var seg = new WallSegment
		{
			Go = go,
			Center = center,
			FootprintSize = new Vector3( size.x, size.y, 0f ),
			Key = key,
			MaxHealth = upgrades.WallMaxHp,
			Health = upgrades.WallMaxHp
		};

		WallScaffoldVisual.Build(
			go,
			size,
			( mr, tint ) => seg.VisualParts.Add( (mr, tint) ),
			center );
		seg.RefreshVisual();
		_walls.Add( seg );
	}

	/// <summary>Permanently tears down a perimeter wall segment (persists so it stays gone on reload).</summary>
	public bool RemoveWall( WallSegment wall )
	{
		if ( wall is null ) return false;

		var save = GameCore.Instance?.Save;
		if ( save is not null && !string.IsNullOrEmpty( wall.Key ) && !save.RemovedWalls.Contains( wall.Key ) )
			save.RemovedWalls.Add( wall.Key );

		DestructionFx.Burst( wall.Center.WithZ( 0f ), 1.15f );
		TileOccupancy.UnmarkWall( wall );
		wall.Go?.Destroy();
		_walls.Remove( wall );
		Sfx.Play( Sfx.Purchase );
		GameCore.Instance?.SaveManagerTouch();
		return true;
	}

	private void RefreshCoreVisual()
	{
		var t = CoreMaxHealth <= 0f ? 0f : CoreHealth / CoreMaxHealth;
		foreach ( var (mr, baseTint) in _coreParts )
		{
			if ( !mr.IsValid() ) continue;
			mr.Tint = Color.Lerp( new Color( 0.6f, 0.2f, 0.18f ), baseTint, t );
		}
	}
}
