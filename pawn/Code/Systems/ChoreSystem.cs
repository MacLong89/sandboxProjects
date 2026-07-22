namespace PawnShop;

public enum ChoreKind
{
	Sweep,
	Dust,
	Organize,
	TrashBag,
	Dumpster,
	WaterPlant,
	PolishCounter,
}

/// <summary>One active world chore the player can clear with E.</summary>
public sealed class ActiveChore
{
	public int Id;
	public ChoreKind Kind;
	public Vector3 Position;
	public bool Done;
	public GameObject Root;
}

/// <summary>
/// Non-customer shop work: sweeping, dusting, restacking, trash runs, watering,
/// polishing. Spawns physical spots on the map and feeds daily goal metrics.
/// </summary>
public sealed class ChoreSystem : Component
{
	public readonly List<ActiveChore> Active = new();
	public bool CarryingTrash { get; private set; }
	public int CompletedToday { get; private set; }

	private GameObject _root;
	private int _nextId = 1;
	private TimeUntil _nextMess;
	private GameManager Game => GameManager.Instance;

	public int PendingCount => Active.Count( c => !c.Done && c.Kind != ChoreKind.Dumpster );
	public int PendingSweeps => CountPending( ChoreKind.Sweep );
	public int PendingDust => CountPending( ChoreKind.Dust );
	public int PendingOrganize => CountPending( ChoreKind.Organize );
	public int PendingTrash => CountPending( ChoreKind.TrashBag ) + (CarryingTrash ? 1 : 0);
	public bool PlantNeedsWater => Active.Any( c => !c.Done && c.Kind == ChoreKind.WaterPlant );
	public bool CounterNeedsPolish => Active.Any( c => !c.Done && c.Kind == ChoreKind.PolishCounter );

	private int CountPending( ChoreKind kind ) => Active.Count( c => !c.Done && c.Kind == kind );

	protected override void OnStart()
	{
		_root = new GameObject( GameObject, true, "ChoreSpots" );
		EnsureDumpsterInteract();
	}

	/// <summary>Fresh morning duties — always something to do before the rush.</summary>
	public void RollMorning()
	{
		ClearAll();
		CarryingTrash = false;
		CompletedToday = 0;
		_nextMess = 75f;

		var day = Game?.Save.Day ?? 1;

		Spawn( ChoreKind.WaterPlant, ShopLayout.PlantSpot );
		Spawn( ChoreKind.PolishCounter, ShopLayout.CounterPolishSpot );

		var sweeps = Math.Clamp( 2 + day / 3, 2, 5 );
		foreach ( var pos in Pick( ShopLayout.DirtSpots, sweeps ) )
			Spawn( ChoreKind.Sweep, pos );

		var dusts = Math.Clamp( 1 + day / 4, 1, 3 );
		foreach ( var pos in Pick( ShopLayout.DustSpots, dusts ) )
			Spawn( ChoreKind.Dust, pos );

		var piles = Math.Clamp( 1 + day / 5, 1, 3 );
		foreach ( var pos in Pick( ShopLayout.MessPiles, piles ) )
			Spawn( ChoreKind.Organize, pos );

		Spawn( ChoreKind.TrashBag, Pick( ShopLayout.TrashSpots, 1 ).First() );

		EnsureDumpsterInteract();
		UiState.Bump();
	}

	public void Tick( float dt )
	{
		if ( Game is null || Game.State != GameState.ShopOpen ) return;
		if ( !_nextMess ) return;

		_nextMess = Sandbox.Game.Random.Float( 55f, 100f );

		// Foot traffic leaves mess; don't flood the floor.
		if ( PendingCount >= 10 ) return;

		var roll = Sandbox.Game.Random.Float();
		if ( roll < 0.45f && PendingSweeps < 4 )
			Spawn( ChoreKind.Sweep, PickFree( ShopLayout.DirtSpots ) );
		else if ( roll < 0.7f && PendingDust < 3 )
			Spawn( ChoreKind.Dust, PickFree( ShopLayout.DustSpots ) );
		else if ( roll < 0.88f && PendingOrganize < 2 )
			Spawn( ChoreKind.Organize, PickFree( ShopLayout.MessPiles ) );
		else if ( PendingTrash == 0 && !CarryingTrash )
			Spawn( ChoreKind.TrashBag, PickFree( ShopLayout.TrashSpots ) );

		if ( Sandbox.Game.Random.Float() < 0.15f && !PlantNeedsWater )
			Spawn( ChoreKind.WaterPlant, ShopLayout.PlantSpot );

		UiState.Bump();
	}

	public string PromptFor( ActiveChore chore )
	{
		if ( chore is null || chore.Done ) return "";
		return chore.Kind switch
		{
			ChoreKind.Sweep => "Sweep the floor",
			ChoreKind.Dust => "Dust this shelf",
			ChoreKind.Organize => "Restack these boxes",
			ChoreKind.TrashBag => CarryingTrash ? "Already carrying trash" : "Pick up trash bag",
			ChoreKind.Dumpster => CarryingTrash ? "Toss the trash" : "Dumpster (bring a bag)",
			ChoreKind.WaterPlant => "Water the plant",
			ChoreKind.PolishCounter => "Polish the counter",
			_ => "",
		};
	}

	public bool TryComplete( int choreId )
	{
		var chore = Active.FirstOrDefault( c => c.Id == choreId );
		if ( chore is null || chore.Done ) return false;

		switch ( chore.Kind )
		{
			case ChoreKind.TrashBag:
				if ( CarryingTrash ) return false;
				if ( Game?.CarriedItem is not null )
				{
					Game.Toast( "Put down what you're carrying first (Q).", "luggage" );
					return false;
				}
				CarryingTrash = true;
				MarkDone( chore );
				Sfx.Play( Sfx.ItemPlaced, 0.5f );
				Game?.Toast( "Trash bag in hand — press E at the BACK DOOR to dump it, or Q to put it down.", "delete" );
				UiState.Bump();
				return true;

			case ChoreKind.Dumpster:
				return DumpTrash();

			default:
				MarkDone( chore );
				FinishChore( chore.Kind );
				return true;
		}
	}

	/// <summary>Dispose carried trash at the dumpster / back door. Completes the chore.</summary>
	public bool DumpTrash()
	{
		if ( !CarryingTrash )
		{
			Game?.Toast( "Nothing to dump. Grab a trash bag inside first.", "delete" );
			Sfx.Play( Sfx.UiError, 0.4f );
			return false;
		}

		CarryingTrash = false;
		Sfx.Play( Sfx.Scrap, 0.55f );
		Game?.Toast( "Trash taken out. The alley thanks you.", "delete" );
		FinishChore( ChoreKind.TrashBag );
		UiState.Bump();
		return true;
	}

	/// <summary>Put the trash bag down without completing the chore (player can pick it up again).</summary>
	public bool DropTrash( Vector3? at = null )
	{
		if ( !CarryingTrash ) return false;

		CarryingTrash = false;
		var pos = at ?? ShopLayout.StockTable;
		if ( at is { } p )
			pos = p.WithZ( 0 );
		else
		{
			foreach ( var spot in ShopLayout.TrashSpots )
			{
				if ( !Active.Any( c => !c.Done && c.Kind == ChoreKind.TrashBag && (c.Position - spot).Length < 20f ) )
				{
					pos = spot;
					break;
				}
			}
		}

		Spawn( ChoreKind.TrashBag, pos );
		Sfx.Play( Sfx.ItemPlaced, 0.45f );
		Game?.Toast( "Dropped the trash bag. Pick it up again when you're ready.", "delete" );
		UiState.Bump();
		return true;
	}

	private void MarkDone( ActiveChore chore )
	{
		chore.Done = true;
		chore.Root?.Destroy();
		chore.Root = null;
	}

	private void FinishChore( ChoreKind kind )
	{
		CompletedToday++;
		var game = Game;
		if ( game is null ) return;

		var rep = game.Save.OwnsUpgrade( UpgradeId.CleanStore ) ? 0.8f : 0.4f;
		game.Reputation.Add( rep );

		switch ( kind )
		{
			case ChoreKind.Sweep:
				Sfx.Play( Sfx.Cleaning, 0.65f );
				game.Toast( "Floor's looking better.", "cleaning_services" );
				game.Goals.Notify( GoalMetric.FloorsSwept );
				break;
			case ChoreKind.Dust:
				Sfx.Play( Sfx.Cleaning, 0.55f );
				game.Toast( "Shelf dusted. Merchandise sparkles a bit more.", "auto_awesome" );
				game.Goals.Notify( GoalMetric.ShelvesDusted );
				break;
			case ChoreKind.Organize:
				Sfx.Play( Sfx.ItemPlaced, 0.6f );
				game.Toast( "Boxes restacked. Backroom breathes again.", "inventory_2" );
				game.Goals.Notify( GoalMetric.PilesOrganized );
				break;
			case ChoreKind.TrashBag:
				// Toast already shown at the dumpster.
				game.Goals.Notify( GoalMetric.TrashTakenOut );
				break;
			case ChoreKind.WaterPlant:
				Sfx.Play( Sfx.Splash, 0.7f );
				game.Toast( "Plant perks up. Cozy shop points.", "local_florist" );
				game.Goals.Notify( GoalMetric.PlantsWatered );
				break;
			case ChoreKind.PolishCounter:
				Sfx.Play( Sfx.Cleaning, 0.7f );
				game.Toast( "Counter gleams. Customers notice.", "countertops" );
				game.Goals.Notify( GoalMetric.CounterPolished );
				break;
		}

		game.Goals.Notify( GoalMetric.ChoresDone );
		UiState.Bump();
	}

	private void Spawn( ChoreKind kind, Vector3 pos )
	{
		if ( pos == default ) return;
		if ( kind != ChoreKind.Dumpster && Active.Any( c => !c.Done && c.Kind == kind && (c.Position - pos).Length < 20f ) )
			return;

		var chore = new ActiveChore
		{
			Id = _nextId++,
			Kind = kind,
			Position = pos,
		};
		Active.Add( chore );
		BuildVisual( chore );
	}

	private void BuildVisual( ActiveChore chore )
	{
		if ( !_root.IsValid() ) return;

		var go = new GameObject( _root, true, $"Chore_{chore.Kind}_{chore.Id}" );
		go.WorldPosition = chore.Position;
		chore.Root = go;

		switch ( chore.Kind )
		{
			case ChoreKind.Sweep:
				MeshKit.Spawn( go, "DirtA", new Vector3( -6, 2, 1.2f ), new Vector3( 22, 16, 1.5f ), new Color( 0.35f, 0.28f, 0.18f, 0.85f ) );
				MeshKit.Spawn( go, "DirtB", new Vector3( 8, -4, 1.4f ), new Vector3( 14, 12, 1.2f ), new Color( 0.4f, 0.32f, 0.2f, 0.75f ) );
				MeshKit.SpawnSphere( go, "Crumbs", new Vector3( 0, 6, 2.5f ), 5f, new Color( 0.45f, 0.38f, 0.25f ) );
				break;

			case ChoreKind.Dust:
				MeshKit.Spawn( go, "DustCloud", new Vector3( 0, 0, 8 ), new Vector3( 28, 18, 10 ), new Color( 0.75f, 0.72f, 0.62f, 0.35f ) );
				MeshKit.Spawn( go, "DustFilm", new Vector3( 0, 0, 2 ), new Vector3( 32, 22, 2 ), new Color( 0.7f, 0.66f, 0.55f, 0.45f ) );
				break;

			case ChoreKind.Organize:
				MeshKit.Spawn( go, "Box1", new Vector3( -8, 0, 12 ), new Vector3( 28, 24, 24 ), new Color( 0.62f, 0.48f, 0.3f ), new Angles( 0, 18, 8 ) );
				MeshKit.Spawn( go, "Box2", new Vector3( 10, 6, 10 ), new Vector3( 22, 20, 20 ), new Color( 0.55f, 0.4f, 0.25f ), new Angles( 0, -25, -6 ) );
				MeshKit.Spawn( go, "Box3", new Vector3( 0, -8, 26 ), new Vector3( 18, 16, 14 ), new Color( 0.7f, 0.55f, 0.35f ), new Angles( 12, 40, 0 ) );
				break;

			case ChoreKind.TrashBag:
				MeshKit.Spawn( go, "Bag", new Vector3( 0, 0, 14 ), new Vector3( 18, 18, 28 ), new Color( 0.28f, 0.32f, 0.26f ) );
				MeshKit.Spawn( go, "Tie", new Vector3( 0, 0, 30 ), new Vector3( 8, 8, 6 ), new Color( 0.2f, 0.22f, 0.18f ) );
				MeshKit.Spawn( go, "Spill", new Vector3( 10, -6, 1.5f ), new Vector3( 12, 10, 2 ), new Color( 0.4f, 0.35f, 0.2f ) );
				break;

			case ChoreKind.WaterPlant:
				MeshKit.Spawn( go, "DropletHint", new Vector3( 0, 0, 55 ), new Vector3( 6, 6, 10 ), new Color( 0.35f, 0.65f, 0.9f, 0.7f ) );
				break;

			case ChoreKind.PolishCounter:
				MeshKit.Spawn( go, "Smudge", new Vector3( 0, 0, 1 ), new Vector3( 50, 28, 1.5f ), new Color( 0.55f, 0.45f, 0.3f, 0.4f ) );
				MeshKit.Spawn( go, "RagHint", new Vector3( 18, 0, 3 ), new Vector3( 12, 8, 2 ), new Color( 0.7f, 0.75f, 0.8f ), new Angles( 0, 25, 0 ) );
				break;
		}

		var zone = go.Components.Create<Interactable>();
		zone.Kind = InteractKind.Chore;
		zone.ChoreId = chore.Id;
		zone.HalfExtents = chore.Kind switch
		{
			ChoreKind.PolishCounter => new Vector3( 50, 40, 40 ),
			ChoreKind.WaterPlant => new Vector3( 36, 36, 50 ),
			ChoreKind.Organize => new Vector3( 40, 40, 40 ),
			_ => new Vector3( 32, 32, 36 ),
		};
	}

	private void EnsureDumpsterInteract()
	{
		// Persistent dumpster zone (visual built by ShopBuilder).
		if ( Active.Any( c => c.Kind == ChoreKind.Dumpster ) ) return;

		var chore = new ActiveChore
		{
			Id = _nextId++,
			Kind = ChoreKind.Dumpster,
			Position = ShopLayout.Dumpster,
			Done = false,
		};
		Active.Add( chore );

		var go = new GameObject( _root.IsValid() ? _root : GameObject, true, "DumpsterZone" );
		go.WorldPosition = ShopLayout.Dumpster + new Vector3( 0, 0, 40 );
		chore.Root = go;
		var zone = go.Components.Create<Interactable>();
		zone.Kind = InteractKind.Chore;
		zone.ChoreId = chore.Id;
		zone.HalfExtents = new Vector3( 70, 60, 70 );
	}

	private void ClearAll()
	{
		foreach ( var c in Active )
			c.Root?.Destroy();
		Active.Clear();
		_root?.Destroy();
		_root = new GameObject( GameObject, true, "ChoreSpots" );
	}

	private static IEnumerable<Vector3> Pick( Vector3[] pool, int count )
	{
		if ( pool is null || pool.Length == 0 || count <= 0 )
			yield break;

		var indices = Enumerable.Range( 0, pool.Length ).OrderBy( _ => Sandbox.Game.Random.Float() ).Take( count );
		foreach ( var i in indices )
			yield return pool[i];
	}

	private Vector3 PickFree( Vector3[] pool )
	{
		if ( pool is null || pool.Length == 0 ) return default;
		foreach ( var pos in pool.OrderBy( _ => Sandbox.Game.Random.Float() ) )
		{
			if ( !Active.Any( c => !c.Done && (c.Position - pos).Length < 25f ) )
				return pos;
		}
		return pool[Sandbox.Game.Random.Int( 0, pool.Length - 1 )];
	}

	public ActiveChore Get( int id ) => Active.FirstOrDefault( c => c.Id == id );
}
