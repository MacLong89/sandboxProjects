namespace Fauna2;

/// <summary>Spawns and maintains wild animals on unowned wilderness plots visible to the player.</summary>
public sealed class WildernessSpawner : Component
{
	public static WildernessSpawner Instance { get; private set; }

	private readonly List<GameObject> _spawned = new();
	private TimeUntil _nextTopUp;

	protected override void OnAwake()
	{
		Instance = this;
		GameEvents.PlotPurchased += OnPlotPurchased;
	}

	protected override void OnDestroy()
	{
		GameEvents.PlotPurchased -= OnPlotPurchased;
		Clear();
		if ( Instance == this ) Instance = null;
	}

	protected override void OnFixedUpdate()
	{
		if ( IsProxy || !_nextTopUp ) return;
		_nextTopUp = GameConstants.WildAnimalTopUpDuration;
		TopUpVisibleWilderness();
	}

	public void Restore( IEnumerable<WildAnimalSave> animals )
	{
		if ( !Networking.IsHost ) return;

		Clear();
		_nextTopUp = GameConstants.WildAnimalTopUpDuration;

		foreach ( var save in animals )
		{
			if ( string.IsNullOrEmpty( save.SpeciesId ) ) continue;
			if ( WildAnimalRegistry.ActiveCount >= GameConstants.WildAnimalsWorldMax )
				break;

			SpawnSavedWild( save );
		}

		ClientWorldSync.Instance?.ScheduleWildSync();
	}

	public List<WildAnimalSave> CaptureSave()
	{
		var list = new List<WildAnimalSave>();

		foreach ( var wild in WildAnimalRegistry.All )
		{
			if ( !wild.IsValid() ) continue;

			// AUDIT FIX B8: Fled wilds are host-local "invisible until respawn".
			// Including them in snapshots made clients spawn catchable ghosts of
			// animals the host had already driven off. Skip fled entries entirely.
			// Revert hint: if clients never see animals after a failed catch, ensure
			// Flee() still calls ScheduleWildSync so the exclusion propagates.
			if ( wild.Fled ) continue;

			list.Add( new WildAnimalSave
			{
				WildId = wild.WildId,
				SpeciesId = wild.SpeciesId,
				Position = new SaveVector3( wild.WorldPosition ),
				PlotX = wild.PlotX,
				PlotY = wild.PlotY,
			} );
		}

		return list;
	}

	/// <summary>Client mirror — rebuild wild animals from a host snapshot.</summary>
	public void ApplyClientSnapshot( string snapshot )
	{
		if ( Networking.IsHost ) return;

		Clear();

		foreach ( var save in WorldSnapshotFormat.ParseWild( snapshot ) )
			SpawnSavedWild( save );
	}

	public void GenerateWorld( Biome starterBiome )
	{
		if ( !Networking.IsHost ) return;
		Clear();
		_nextTopUp = GameConstants.WildAnimalTopUpDuration * 0.5f;
		TopUpVisibleWilderness( starterBiome, initialFill: true );
	}

	private void TopUpVisibleWilderness( Biome starterBiome = default, bool initialFill = false )
	{
		if ( WildAnimalRegistry.ActiveCount >= GameConstants.WildAnimalsWorldMax )
			return;

		var state = ZooState.Instance;
		var plots = PlotSystem.Instance;
		if ( !state.IsValid() || !plots.IsValid() )
			return;

		starterBiome = starterBiome == default ? state.StarterBiome : starterBiome;

		var visible = VisibleWildernessPlots().ToList();
		if ( visible.Count == 0 )
			return;

		// Border plots first so wildlife appears where the player is exploring.
		foreach ( var (px, py) in visible.Where( p => IsBorderWildernessPlot( p.x, p.y, plots ) ) )
		{
			if ( WildAnimalRegistry.ActiveCount >= GameConstants.WildAnimalsWorldMax )
				return;
			if ( plots.IsOwned( px, py ) ) continue;
			TryTopUpPlot( px, py, starterBiome, preferMore: true, initialFill );
		}

		foreach ( var (px, py) in visible )
		{
			if ( WildAnimalRegistry.ActiveCount >= GameConstants.WildAnimalsWorldMax )
				return;
			if ( plots.IsOwned( px, py ) ) continue;
			if ( IsBorderWildernessPlot( px, py, plots ) ) continue;

			var hotspot = WildernessBiomeMap.IsWildlifeHotspot( px, py );
			TryTopUpPlot( px, py, starterBiome, preferMore: hotspot, initialFill );
		}
	}

	private void TryTopUpPlot( int px, int py, Biome starterBiome, bool preferMore, bool initialFill )
	{
		if ( WildAnimalRegistry.ActiveCount >= GameConstants.WildAnimalsWorldMax )
			return;

		var cap = PlotCap( px, py );
		var onPlot = CountOnPlot( px, py );
		if ( onPlot >= cap )
			return;

		int min;
		int max;
		if ( initialFill )
		{
			min = GameConstants.WildAnimalsPerPlotSpawnMin;
			max = GameConstants.WildAnimalsPerPlotSpawnMax;
			if ( IsBorderWildernessPlot( px, py, PlotSystem.Instance ) )
				max += GameConstants.WildAnimalsBorderPlotBonus;
			if ( WildernessBiomeMap.IsWildlifeHotspot( px, py ) )
				max += GameConstants.WildAnimalsBiomeHotspotBonus;
		}
		else
		{
			min = preferMore ? Math.Max( GameConstants.WildAnimalTopUpMin, 1 ) : GameConstants.WildAnimalTopUpMin;
			max = preferMore ? GameConstants.WildAnimalTopUpMax + 1 : GameConstants.WildAnimalTopUpMax;
		}

		var worldRoom = GameConstants.WildAnimalsWorldMax - WildAnimalRegistry.ActiveCount;
		var plotRoom = cap - onPlot;
		var spawnCount = Math.Min( plotRoom, Math.Min( worldRoom, Game.Random.Int( min, max ) ) );
		if ( spawnCount <= 0 )
			return;

		SpawnOnPlot( px, py, starterBiome, spawnCount );
	}

	private static int PlotCap( int px, int py ) =>
		WildernessBiomeMap.IsWildlifeHotspot( px, py )
			? GameConstants.WildAnimalsPerPlotCapHotspot
			: GameConstants.WildAnimalsPerPlotCap;

	private void OnPlotPurchased()
	{
		if ( !Networking.IsHost ) return;
		RemoveWildlifeOnOwnedPlots();
	}

	private void RemoveWildlifeOnOwnedPlots()
	{
		var plots = PlotSystem.Instance;
		if ( !plots.IsValid() ) return;

		foreach ( var wild in WildAnimalRegistry.All )
		{
			if ( !wild.IsValid() ) continue;
			if ( !plots.IsOwned( wild.PlotX, wild.PlotY ) ) continue;

			_spawned.Remove( wild.GameObject );
			wild.GameObject.Destroy();
		}

		ClientWorldSync.Instance?.ScheduleWildSync();
	}

	private int CountOnPlot( int px, int py ) => WildAnimalRegistry.CountOnPlot( px, py );

	private void SpawnSavedWild( WildAnimalSave save )
	{
		var def = Defs.Animal( save.SpeciesId );
		if ( def is null ) return;

		SpawnLocalWild(
			def,
			save.Position.ToVector3(),
			save.PlotX,
			save.PlotY,
			save.WildId );
	}

	/// <summary>Host-local wildlife — avoids spending networked entity slots on ambient critters.</summary>
	private void SpawnLocalWild( AnimalDefinition def, Vector3 position, int plotX, int plotY, string wildId = null )
	{
		if ( WildAnimalRegistry.ActiveCount >= GameConstants.WildAnimalsWorldMax )
			return;

		var go = new GameObject( true, $"Wild - {def.DisplayName}" );
		go.Tags.Add( "wild_animal" );
		go.WorldPosition = position;

		var wild = go.AddComponent<WildAnimalComponent>();
		wild.WildId = string.IsNullOrEmpty( wildId ) ? Guid.NewGuid().ToString( "N" ) : wildId;
		wild.SpeciesId = Defs.IdOf( def );
		wild.PlotX = plotX;
		wild.PlotY = plotY;
		wild.KickMovement();

		_spawned.Add( go );
		ClientWorldSync.Instance?.ScheduleWildSync();
	}

	private void SpawnOnPlot( int px, int py, Biome starterBiome, int? count = null )
	{
		var spawnCount = count ?? Game.Random.Int(
			GameConstants.WildAnimalsPerPlotSpawnMin,
			GameConstants.WildAnimalsPerPlotSpawnMax );

		for ( var i = 0; i < spawnCount; i++ )
		{
			if ( WildAnimalRegistry.ActiveCount >= GameConstants.WildAnimalsWorldMax )
				return;

			var def = PickSpecies( px, py, starterBiome );
			if ( def is null ) continue;

			var position = WildAnimalComponent.RandomValidPointOnPlot( px, py, starterBiome, def );
			if ( position == Vector3.Zero )
				continue;

			SpawnLocalWild( def, position, px, py );
		}
	}

	public void SpawnRareSightingNow( AnimalDefinition def )
	{
		if ( !Networking.IsHost || def is null ) return;

		var plots = PlotSystem.Instance;
		if ( !plots.IsValid() ) return;

		var starter = ZooState.Instance?.StarterBiome ?? def.Biome;
		var candidates = VisibleWildernessPlots()
			.Where( p => !plots.IsOwned( p.x, p.y ) )
			.Where( p => BiomeEcology.CanWildSpawn( WildernessBiomeMap.BiomeForPlot( p.x, p.y, starter ), def.Biome ) )
			.OrderByDescending( p => IsBorderWildernessPlot( p.x, p.y, plots ) )
			.ThenBy( _ => Game.Random.Int( 0, 9999 ) )
			.ToList();

		if ( candidates.Count == 0 )
			return;

		var chosen = candidates[0];
		var position = WildAnimalComponent.RandomValidPointOnPlot( chosen.x, chosen.y, starter, def );
		if ( position == Vector3.Zero )
			return;

		SpawnLocalWild( def, position, chosen.x, chosen.y );
	}

	private static AnimalDefinition PickSpecies( int px, int py, Biome starterBiome )
	{
		var regional = WildernessBiomeMap.BiomeForPlot( px, py, starterBiome );
		var pool = Defs.Animals
			.Where( a => a.UnlockLevel <= 5 && BiomeEcology.CanWildSpawn( regional, a.Biome ) )
			.Where( a => PlotHasValidHabitat( px, py, starterBiome, a ) )
			.ToList();

		if ( pool.Count == 0 )
			return null;

		var totalWeight = pool.Sum( a => CatchDifficulty.SpawnWeight( a.Rarity ) );
		if ( totalWeight <= 0 )
			return pool[Game.Random.Int( 0, pool.Count - 1 )];

		var eventSystem = SanctuaryEventSystem.Instance;
		if ( eventSystem is not null && !string.IsNullOrEmpty( eventSystem.RareSightingSpeciesId ) )
		{
			var sighting = pool.FirstOrDefault( eventSystem.IsRareSighting );
			if ( sighting is not null && Game.Random.Float() < 0.35f * eventSystem.RareSpawnMultiplier )
				return sighting;
		}

		var weighted = pool.Select( a =>
		{
			var weight = CatchDifficulty.SpawnWeight( a.Rarity );
			if ( eventSystem?.IsRareSighting( a ) == true )
				weight = (int)(weight * eventSystem.RareSpawnMultiplier);
			return (Animal: a, Weight: Math.Max( 1, weight ));
		} ).ToList();

		totalWeight = weighted.Sum( x => x.Weight );
		var roll = Game.Random.Int( 0, totalWeight - 1 );
		var acc = 0;
		foreach ( var (def, weight) in weighted )
		{
			acc += weight;
			if ( roll < acc )
				return def;
		}

		return pool[^1];
	}

	private static bool PlotHasValidHabitat( int px, int py, Biome starterBiome, AnimalDefinition def )
	{
		for ( var i = 0; i < 8; i++ )
		{
			var point = WildAnimalComponent.RandomPointOnPlot( px, py );
			if ( BiomeEcology.CanWildAnimalAt( def, point, starterBiome ) )
				return true;
		}

		return false;
	}

	private static bool IsBorderWildernessPlot( int px, int py, PlotSystem plots )
	{
		if ( plots.IsOwned( px, py ) ) return false;

		for ( var dx = -1; dx <= 1; dx++ )
		{
			for ( var dy = -1; dy <= 1; dy++ )
			{
				if ( dx == 0 && dy == 0 ) continue;
				if ( plots.IsOwned( px + dx, py + dy ) )
					return true;
			}
		}

		return false;
	}

	/// <summary>Wilderness plots intersecting the active camera view (plus margin).</summary>
	private static IEnumerable<(int x, int y)> VisibleWildernessPlots()
	{
		var camera = ZooCameraController.Instance;
		var plotSize = GameConstants.PlotSize;
		float minX;
		float maxX;
		float minY;
		float maxY;

		if ( camera is not null )
		{
			var focus = camera.FocusPoint;
			var ortho = camera.GetOrthoHeight() * GameConstants.WildAnimalViewportMargin;
			var aspect = Screen.Height > 1 ? Screen.Width / (float)Screen.Height : 16f / 9f;
			var halfX = ortho * aspect;
			var halfY = ortho;
			minX = focus.x - halfX;
			maxX = focus.x + halfX;
			minY = focus.y - halfY;
			maxY = focus.y + halfY;
		}
		else
		{
			var starter = plotSize * 1.5f;
			minX = -starter;
			maxX = starter;
			minY = -starter;
			maxY = starter;
		}

		var minPx = (int)MathF.Floor( minX / plotSize );
		var maxPx = (int)MathF.Ceiling( maxX / plotSize );
		var minPy = (int)MathF.Floor( minY / plotSize );
		var maxPy = (int)MathF.Ceiling( maxY / plotSize );

		var radius = GameConstants.PlotGridRadius + 1;
		for ( var x = minPx; x <= maxPx; x++ )
		{
			for ( var y = minPy; y <= maxPy; y++ )
			{
				if ( x < -radius || x > radius || y < -radius || y > radius )
					continue;

				yield return (x, y);
			}
		}
	}

	public void Clear()
	{
		_spawned.Clear();

		// AUDIT FIX B3: Clients previously only cleared the registry and left the
		// GameObjects alive. ApplyClientSnapshot → Clear → respawn then LEAKED
		// every prior wild GO (stacked FixedUpdates + sprites). Host and client
		// now both destroy registered wilds before clearing the registry.
		// Revert hint: if client wilds vanish and never come back, check that
		// ApplyClientSnapshot still SpawnSavedWild after Clear, and that client
		// Destroy does not also wipe host-networked objects (wilds are host-local).
		foreach ( var wild in WildAnimalRegistry.All.ToList() )
		{
			if ( wild.IsValid() )
				wild.GameObject.Destroy();
		}

		WildAnimalRegistry.Clear();
	}
}
