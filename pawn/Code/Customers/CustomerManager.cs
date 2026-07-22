namespace PawnShop;

/// <summary>
/// Spawns and orchestrates customers: traffic pacing, archetype mix, queue slots,
/// redemption visits, and handing the counter customer to the negotiation system.
/// </summary>
public sealed class CustomerManager : Component
{
	private readonly List<CustomerActor> _actors = new();
	private readonly CustomerActor[] _queue = new CustomerActor[1 + 3]; // 0 = counter + 3 line spots
	private TimeUntil _nextSpawn;
	private readonly HashSet<string> _namedPresentToday = new();
	private readonly HashSet<int> _contractsVisitedToday = new();

	private GameManager Game => GameManager.Instance;

	public int ActiveCount => _actors.Count( a => a.IsValid() && !a.IsLeaving );
	public int QueueCount => _queue.Count( a => a.IsValid() );

	/// <summary>The customer standing at the counter waiting for service, if any.</summary>
	public CustomerActor CustomerAtCounter => _queue[0].IsValid() && _queue[0].IsAtCounter ? _queue[0] : null;

	public void OnShopOpened()
	{
		_namedPresentToday.Clear();
		_contractsVisitedToday.Clear();
		_nextSpawn = 3f; // first customer arrives quickly
	}

	public void OnShopClosing()
	{
		foreach ( var actor in _actors.ToList() )
		{
			if ( !actor.IsValid() ) continue;
			if ( actor.State is CustomerState.Negotiating or CustomerState.CheckingOut ) continue;
			actor.Leave( annoyed: false );
		}
	}

	public void ClearAll()
	{
		foreach ( var actor in _actors.ToList() )
			if ( actor.IsValid() )
				actor.GameObject.Destroy();
		_actors.Clear();
		Array.Clear( _queue, 0, _queue.Length );
		Game?.Shop?.SetCounterItem( null );
		UiState.Bump();
	}

	protected override void OnUpdate()
	{
		var game = Game;
		if ( game is null || game.State != GameState.ShopOpen )
			return;

		if ( _nextSpawn )
		{
			var interval = GameConstants.BaseSpawnInterval;
			interval /= game.Events.TrafficMult;
			interval /= game.Reputation.TrafficMult;
			if ( game.Save.OwnsUpgrade( UpgradeId.AdSign ) ) interval /= 1.35f;
			interval *= Sandbox.Game.Random.Float( 0.7f, 1.3f );
			_nextSpawn = Math.Max( 8f, interval );

			TrySpawnCustomer();
		}
	}

	// ==================================================================== Spawning

	private void TrySpawnCustomer()
	{
		var game = Game;

		// Room check: full queue AND full browsers = skip.
		var browsers = _actors.Count( a => a.IsValid() && a.State is CustomerState.Browsing or CustomerState.GoingToCounter or CustomerState.CheckingOut );
		var queueFull = QueueCount >= _queue.Length;
		if ( queueFull && browsers >= GameConstants.MaxBrowsers )
			return;

		// 1) Redemption visits take priority: contracts due today/tomorrow send their owner.
		var dueContract = game.Save.PawnContracts.FirstOrDefault( c =>
			c.DueDay - game.Save.Day <= 1 && !_contractsVisitedToday.Contains( c.ItemId ) );
		if ( dueContract is not null && !queueFull && Sandbox.Game.Random.Float() < 0.6f )
		{
			_contractsVisitedToday.Add( dueContract.ItemId );
			// Defaulting customers just never show up.
			if ( Sandbox.Game.Random.Float() <= dueContract.RepayLikelihood + 0.15f )
			{
				SpawnRedeemer( dueContract );
				return;
			}
		}

		// 2) Named or procedural customer.
		var profile = RollProfile();
		if ( profile is null ) return;

		// 3) Intent from archetype weights (buyers need stock on display).
		var hasStock = game.Inventory.OnDisplay.Any( i => !i.NotForSale );
		var arch = profile.Archetype;
		var sellW = arch.SellWeight;
		var pawnW = arch.PawnWeight;
		var buyW = hasStock ? arch.BuyWeight : 0f;
		if ( queueFull ) { sellW = 0f; pawnW = 0f; }
		var total = sellW + pawnW + buyW;
		if ( total <= 0f ) return;

		var roll = Sandbox.Game.Random.Float( 0f, total );
		profile.Intent = roll < sellW ? CustomerIntent.Sell
			: roll < sellW + pawnW ? CustomerIntent.Pawn
			: CustomerIntent.Buy;

		if ( profile.Intent is CustomerIntent.Sell or CustomerIntent.Pawn )
		{
			profile.Item = ItemFactory.Roll(
				game.Save.NextItemId++,
				arch, game.Save.Day, game.Events.ScamMult,
				profile.Named?.Favorites );
		}
		else
		{
			// Buyer budget scales with typical stock price.
			var avgPrice = game.Inventory.OnDisplay.Where( i => !i.NotForSale ).Select( i => i.SalePrice ).DefaultIfEmpty( 200 ).Average();
			profile.Budget = (int)(avgPrice * arch.BudgetMult * Sandbox.Game.Random.Float( 0.8f, 1.5f ));
			profile.Budget = Math.Max( 40, profile.Budget );
		}

		Spawn( profile );
	}

	private CustomerProfile RollProfile()
	{
		var game = Game;

		// ~35% chance a recurring named customer walks in (if not already here today).
		if ( Sandbox.Game.Random.Float() < 0.35f )
		{
			var candidates = NamedCustomers.All.Where( n => !_namedPresentToday.Contains( n.Id ) ).ToList();
			if ( candidates.Count > 0 )
			{
				var def = candidates[Sandbox.Game.Random.Int( 0, candidates.Count - 1 )];
				_namedPresentToday.Add( def.Id );
				var rel = game.Relationships.Get( def.Id, def.Name );
				return CustomerProfile.FromNamed( def, rel );
			}
		}

		// Procedural: weight archetypes by reputation and events.
		var rep = game.Save.Reputation;
		var weights = new List<(ArchetypeDef Def, float W)>();
		foreach ( var arch in ArchetypeCatalog.All )
		{
			var w = 1f;
			switch ( arch.Id )
			{
				case Archetype.Scammer:
					w = 0.7f * game.Events.ScamMult * (rep < 40f ? 1.5f : 1f);
					if ( game.Save.Day <= 2 ) w *= 0.4f;
					break;
				case Archetype.SuspiciousSeller:
					w = 0.5f * (rep < 40f ? 1.6f : 1f);
					if ( game.Save.Day <= 2 ) w *= 0.3f;
					break;
				case Archetype.WealthyBuyer:
					w = rep >= 60f ? 1.2f : 0.25f;
					break;
				case Archetype.Collector:
					w = rep >= 50f ? 1.1f : 0.6f;
					break;
				case Archetype.Regular:
				case Archetype.PawnRegular:
					w = 1.1f;
					break;
				default:
					w = 1.2f;
					break;
			}
			weights.Add( (arch, w) );
		}

		var total = weights.Sum( x => x.W );
		var roll = Sandbox.Game.Random.Float( 0f, total );
		foreach ( var (def, w) in weights )
		{
			roll -= w;
			if ( roll <= 0f )
				return CustomerProfile.Procedural( def );
		}
		return CustomerProfile.Procedural( ArchetypeCatalog.Get( Archetype.Regular ) );
	}

	private void SpawnRedeemer( PawnContract contract )
	{
		var named = NamedCustomers.Get( contract.CustomerId );
		var profile = named is not null
			? CustomerProfile.FromNamed( named, Game.Relationships.Get( named.Id, named.Name ) )
			: CustomerProfile.Procedural( ArchetypeCatalog.Get( Archetype.PawnRegular ) );

		if ( named is null ) profile.Name = contract.CustomerName;
		profile.Intent = CustomerIntent.Redeem;
		profile.Contract = contract;
		Spawn( profile );
	}

	private void Spawn( CustomerProfile profile )
	{
		var go = new GameObject( GameObject, true, $"Customer {profile.Name}" );
		var actor = go.Components.Create<CustomerActor>();
		_actors.Add( actor );
		actor.Setup( profile );
		UiState.Bump();
	}

	/// <summary>Dev helper: force-spawn a specific archetype + intent right now.</summary>
	public void DebugSpawn( Archetype archetype, CustomerIntent intent, bool forceFake = false, bool forceRare = false )
	{
		var game = Game;
		var profile = CustomerProfile.Procedural( ArchetypeCatalog.Get( archetype ) );
		profile.Intent = intent;

		if ( intent is CustomerIntent.Sell or CustomerIntent.Pawn )
		{
			profile.Item = ItemFactory.Roll( game.Save.NextItemId++, profile.Archetype, game.Save.Day, forceFake ? 99f : game.Events.ScamMult );
			if ( forceRare )
			{
				profile.Item.Rarity = Rarity.VeryRare;
				profile.Item.TrueAuthenticity = Authenticity.Genuine;
			}
		}
		else if ( intent == CustomerIntent.Buy )
		{
			profile.Budget = 1500;
		}

		Spawn( profile );
	}

	// ==================================================================== Queue bookkeeping

	/// <summary>Claim the first free queue slot. Returns -1 if the line is full.</summary>
	public int ClaimQueueSpot( CustomerActor actor )
	{
		for ( var i = 0; i < _queue.Length; i++ )
		{
			if ( !_queue[i].IsValid() )
			{
				_queue[i] = actor;
				UiState.Bump();
				return i;
			}
		}
		return -1;
	}

	public void ReleaseQueueSpot( CustomerActor actor )
	{
		var idx = Array.IndexOf( _queue, actor );
		if ( idx < 0 ) return;

		_queue[idx] = null;

		// Shuffle everyone behind up one place.
		for ( var i = idx; i < _queue.Length - 1; i++ )
		{
			_queue[i] = _queue[i + 1];
			_queue[i + 1] = null;
			if ( _queue[i].IsValid() )
			{
				_queue[i].QueueIndex = i;
				_queue[i].OnQueueAdvanced();
			}
		}
		UiState.Bump();
	}

	public void OnActorDespawned( CustomerActor actor )
	{
		_actors.Remove( actor );
		ReleaseQueueSpot( actor );
	}

	/// <summary>Player pressed Use at the counter: serve whoever is waiting there.</summary>
	public bool TryServeCounterCustomer()
	{
		var game = Game;
		if ( game.Negotiation.Active ) return false;

		var customer = CustomerAtCounter;
		if ( customer is null ) return false;

		customer.BeginNegotiation();

		// BeginNegotiation may bail out (e.g. a buyer whose target item vanished) —
		// only report success if a negotiation actually started.
		return game.Negotiation.Active;
	}
}
