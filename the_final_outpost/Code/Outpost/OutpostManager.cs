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
		CoreHealth = MathF.Max( 0f, CoreHealth - amount );
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
		foreach ( var w in _walls ) w.Go?.Destroy();
		_walls.Clear();
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
	}

	private void SpawnWall( Vector3 center, Vector3 size, UpgradeSystem upgrades )
	{
		var key = $"{(int)MathF.Round( center.x )},{(int)MathF.Round( center.y )}";

		// Skip segments the player has torn down (persisted across reloads).
		var save = GameCore.Instance?.Save;
		if ( save is not null && save.RemovedWalls.Contains( key ) )
			return;

		center.z += OutpostTerrain.SampleHeight( center.x, center.y );

		var stone = StylizedMaterials.Stone;
		var useTexture = stone is not null && stone.IsValid() && stone != MeshPrimitives.Mat;

		var go = new GameObject( true, "Wall" );
		go.WorldPosition = center;
		go.LocalScale = MeshPrimitives.BoxScale( size );
		var mr = go.Components.Create<ModelRenderer>();
		mr.Model = MeshPrimitives.Box;
		mr.MaterialOverride = stone;

		var seg = new WallSegment
		{
			Go = go,
			Renderer = mr,
			Center = center,
			Key = key,
			IntactColor = useTexture ? Color.White : new Color( 0.55f, 0.58f, 0.62f ),
			MaxHealth = upgrades.WallMaxHp,
			Health = upgrades.WallMaxHp
		};
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
