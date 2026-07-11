namespace Fauna2;

/// <summary>
/// A fenced enclosure animals live in. Placed through the build system as a
/// single piece: ground pad, perimeter fence and a guest-side gate are built
/// procedurally on every machine from synced data. The host periodically
/// rescoring habitats drives happiness, breeding and guest appeal.
/// </summary>
public sealed class HabitatComponent : Component
{
	[Sync( SyncFlags.FromHost )] public string HabitatId { get; set; } = "";
	[Sync( SyncFlags.FromHost )] public string DefinitionId { get; set; } = "";
	[Sync( SyncFlags.FromHost )] public Vector2 Size { get; set; } = new Vector2( 512, 512 );
	[Sync( SyncFlags.FromHost )] public Biome Biome { get; set; } = Biome.Grassland;
	[Sync( SyncFlags.FromHost )] public float Score { get; set; } = 100f;

	public PlaceableDefinition Definition => _definition ??= Defs.Placeable( DefinitionId );
	public HabitatScoreBreakdown LastBreakdown { get; private set; }

	/// <summary>Where hungry animals wander to feed (abstracted feeding).</summary>
	public Vector3 FoodPoint => GameObject.WorldPosition + new Vector3( -Size.x * 0.3f, -Size.y * 0.3f, 0f );

	private PlaceableDefinition _definition;
	private GameObject _visualRoot;
	private IReadOnlyList<FenceTile> _fenceTiles;
	private Vector2 _visualSize = new( -1f, -1f );
	private string _visualDefinitionId = "";
	private Biome _visualBiome = Biome.Grassland;
	private TimeUntil _nextRescore;

	protected override void OnStart()
	{
		BuildVisuals();
		HabitatRegistry.Register( this );

		if ( !IsProxy )
			_nextRescore = Game.Random.Float( 0.5f, GameConstants.HabitatRescoreInterval );
	}

	protected override void OnFixedUpdate()
	{
		// Clients only: rebuild once synced footprint arrives (defaults to 512×512).
		if ( IsProxy && NeedsVisualRebuild() )
			BuildVisuals();

		if ( IsProxy || !_nextRescore ) return;
		_nextRescore = GameConstants.HabitatRescoreInterval;

		var clock = DebugStats.StartTimer();
		LastBreakdown = HabitatScoring.Score( this );
		Score = LastBreakdown.Total;
		DebugStats.StopTimer( "Habitats", clock );
	}

	private bool NeedsVisualRebuild() =>
		_visualRoot is null || !_visualRoot.IsValid()
		|| Size.x != _visualSize.x || Size.y != _visualSize.y
		|| ( DefinitionId ?? "" ) != _visualDefinitionId
		|| Biome != _visualBiome;

	protected override void OnDestroy()
	{
		HabitatRegistry.Unregister( this );
		_visualRoot?.Destroy();
	}

	// ── Spatial queries ─────────────────────────────────────

	public bool ContainsPoint( Vector3 point )
	{
		var pos = GameObject.WorldPosition;
		return MathF.Abs( point.x - pos.x ) <= Size.x * 0.5f
			&& MathF.Abs( point.y - pos.y ) <= Size.y * 0.5f;
	}

	public Vector3 ClampInside( Vector3 point )
	{
		var pos = GameObject.WorldPosition;
		var margin = GameConstants.TileSize;
		var hx = Size.x * 0.5f - margin;
		var hy = Size.y * 0.5f - margin;
		return new Vector3(
			point.x.Clamp( pos.x - hx, pos.x + hx ),
			point.y.Clamp( pos.y - hy, pos.y + hy ),
			point.z );
	}

	public Vector3 RandomPointInside()
	{
		var pos = GameObject.WorldPosition;
		var margin = GameConstants.TileSize + 8f;
		var hx = Size.x * 0.5f - margin;
		var hy = Size.y * 0.5f - margin;
		return new Vector3(
			pos.x + Game.Random.Float( -hx, hx ),
			pos.y + Game.Random.Float( -hy, hy ),
			0f );
	}

	public int CapacityFor( AnimalDefinition def )
	{
		if ( def is null || def.SpaceNeed <= 0f ) return 0;
		return (int)(Size.x * Size.y / def.SpaceNeed);
	}

	public bool HasRoomFor( AnimalDefinition def, AnimalComponent exclude = null )
	{
		if ( def is null || !AnimalHabitatRules.CanHouse( this, def, out _ ) )
			return false;

		// Mixed-species habitats share space proportionally.
		var area = Size.x * Size.y;
		var used = AnimalRegistry.InHabitat( HabitatId ).Sum( a =>
			a == exclude ? 0f : a.Definition?.SpaceNeed ?? 0f );
		return used + def.SpaceNeed <= area * 1.1f;
	}

	public bool TryAccept( AnimalDefinition def, AnimalComponent exclude, out string error )
	{
		if ( !AnimalHabitatRules.CanHouse( this, def, out error ) )
			return false;

		var area = Size.x * Size.y;
		var used = AnimalRegistry.InHabitat( HabitatId ).Sum( a =>
			a == exclude ? 0f : a.Definition?.SpaceNeed ?? 0f );

		if ( used + def.SpaceNeed > area * 1.1f )
		{
			error = "That habitat is too crowded.";
			return false;
		}

		error = null;
		return true;
	}

	// ── Procedural visuals ──────────────────────────────────

	public static Color BiomeColor( Biome biome ) => biome switch
	{
		Biome.Forest => new Color( 0.22f, 0.58f, 0.24f ),
		Biome.Rainforest => new Color( 0.08f, 0.48f, 0.28f ),
		Biome.Grassland => new Color( 0.52f, 0.78f, 0.32f ),
		Biome.Desert => new Color( 0.78f, 0.66f, 0.38f ),
		Biome.Arctic => new Color( 0.82f, 0.90f, 0.98f ),
		Biome.Swamp => new Color( 0.30f, 0.50f, 0.34f ),
		Biome.Alpine => new Color( 0.46f, 0.58f, 0.62f ),
		Biome.Coastal => new Color( 0.26f, 0.58f, 0.72f ),
		_ => new Color( 0.48f, 0.68f, 0.34f ),
	};

	private void BuildVisuals()
	{
		if ( Size.x <= 0f || Size.y <= 0f )
			return;

		_visualSize = Size;
		_visualDefinitionId = DefinitionId ?? "";
		_visualBiome = Biome;

		_visualRoot?.Destroy();
		_visualRoot = null;

		_visualRoot = new GameObject( GameObject, true, "Visuals" );

		var footprint = HabitatSizing.EffectiveFootprint( Size );
		var center = Vector3.Zero;
		HabitatGroundOverlay.Attach(
			_visualRoot,
			footprint,
			Biome,
			context: $"habitat id={HabitatId} def={DefinitionId} world=({GameObject.WorldPosition.x:0.##},{GameObject.WorldPosition.y:0.##})" );
		_fenceTiles = HabitatFenceGenerator.CreateHabitatFence( center, footprint );
		HabitatFenceRenderer.Build( _visualRoot, _fenceTiles, center, collision: true );

		if ( Fauna2Debug.Enabled || _fenceTiles.Count == 0 )
		{
			Log.Info( $"[Fauna2 Habitat] visuals id={HabitatId} size={footprint.x:0}x{footprint.y:0} " +
			          $"world=({GameObject.WorldPosition.x:0.##},{GameObject.WorldPosition.y:0.##}) fence={_fenceTiles.Count} biome={Biome}" );
		}
	}
}
