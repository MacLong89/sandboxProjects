namespace PawnShop;

/// <summary>
/// Development console commands. All gated behind `pawn_dev 1` so they can't be used
/// accidentally in normal play.
/// </summary>
public static class DevCommands
{
	[ConVar( "pawn_dev" )]
	public static bool DevMode { get; set; } = false;

	[ConVar( "pawn_daylength" )]
	public static float DayLength
	{
		get => GameConstants.DayLengthSeconds;
		set => GameConstants.DayLengthSeconds = Math.Clamp( value, 60f, 3600f );
	}

	private static GameManager Game => GameManager.Instance;

	private static bool Check()
	{
		if ( Game is null ) { Log.Warning( "[pawn] Game is not running." ); return false; }
		if ( !DevMode ) { Log.Warning( "[pawn] Dev commands are disabled. Set pawn_dev 1 first." ); return false; }
		return true;
	}

	[ConCmd( "pawn_cash" )]
	public static void AddCash( int amount = 500 )
	{
		if ( !Check() ) return;
		if ( amount >= 0 ) Game.Economy.Earn( amount );
		else Game.Economy.Spend( -amount );
		UiState.Bump();
		Log.Info( $"[pawn] Cash {(amount >= 0 ? "+" : "")}{amount} → {Game.Economy.Cash}" );
	}

	[ConCmd( "pawn_rep" )]
	public static void SetReputation( float value = 50f )
	{
		if ( !Check() ) return;
		Game.Reputation.Add( value - Game.Save.Reputation, track: false );
		Log.Info( $"[pawn] Reputation set to {Game.Save.Reputation}" );
	}

	[ConCmd( "pawn_customer" )]
	public static void SpawnCustomer( string archetype = "Regular", string intent = "Sell" )
	{
		if ( !Check() ) return;
		if ( !Enum.TryParse<Archetype>( archetype, true, out var arch ) )
		{
			Log.Warning( $"[pawn] Unknown archetype '{archetype}'. Options: {string.Join( ", ", Enum.GetNames<Archetype>() )}" );
			return;
		}
		if ( !Enum.TryParse<CustomerIntent>( intent, true, out var i ) )
		{
			Log.Warning( $"[pawn] Unknown intent '{intent}'. Options: Sell, Pawn, Buy, Redeem" );
			return;
		}
		Game.Customers.DebugSpawn( arch, i );
		Log.Info( $"[pawn] Spawned {archetype} ({intent})." );
	}

	[ConCmd( "pawn_fake" )]
	public static void SpawnFakeSeller()
	{
		if ( !Check() ) return;
		Game.Customers.DebugSpawn( Archetype.Scammer, CustomerIntent.Sell, forceFake: true );
		Log.Info( "[pawn] Spawned a scammer with a guaranteed fake." );
	}

	[ConCmd( "pawn_goals" )]
	public static void ShowGoals()
	{
		if ( !Check() ) return;
		foreach ( var goal in Game.Goals.Today )
			Log.Info( $"[pawn] Goal '{goal.Text}' — {Game.Goals.Progress( goal )}/{goal.Target} {(Game.Goals.IsComplete( goal ) ? "DONE" : "")}" );
		if ( !Game.Goals.Today.Any() )
			Log.Info( "[pawn] No goals rolled yet (start a morning first)." );
	}

	[ConCmd( "pawn_crate" )]
	public static void BuyCrate()
	{
		if ( !Check() ) return;
		if ( !Game.Crate.OfferedToday ) { Log.Info( "[pawn] No crate offer this morning." ); return; }
		if ( Game.Crate.BoughtToday ) { Log.Info( "[pawn] Crate already bought today." ); return; }
		var items = Game.Crate.Buy( Game );
		Log.Info( items is null ? "[pawn] Crate purchase failed." : $"[pawn] Crate opened: {string.Join( ", ", items.Select( i => $"{i.Name} (#{i.Id})" ) )}" );
	}

	[ConCmd( "pawn_ledger" )]
	public static void ShowLedger()
	{
		if ( !Check() ) return;
		Log.Info( $"[pawn] Collector's ledger: {Game.Collection.FlippedTotal}/{Game.Collection.CatalogTotal} stamped." );
		foreach ( var cat in Enum.GetValues<ItemCategory>() )
		{
			var total = Game.Collection.TotalIn( cat );
			if ( total > 0 )
				Log.Info( $"[pawn]   {cat}: {Game.Collection.FlippedIn( cat )}/{total}{(Game.Collection.CategoryRewarded( cat ) ? " (bounty paid)" : "")}" );
		}
	}

	[ConCmd( "pawn_rare" )]
	public static void SpawnRareSeller()
	{
		if ( !Check() ) return;
		Game.Customers.DebugSpawn( Archetype.CluelessSeller, CustomerIntent.Sell, forceRare: true );
		Log.Info( "[pawn] Spawned a clueless seller with a very rare item." );
	}

	[ConCmd( "pawn_item" )]
	public static void GiveItem( string defId = "" )
	{
		if ( !Check() ) return;
		var def = string.IsNullOrEmpty( defId ) ? ItemCatalog.Random() : ItemCatalog.Get( defId );
		if ( def is null )
		{
			Log.Warning( $"[pawn] Unknown item '{defId}'." );
			return;
		}
		var item = ItemFactory.RollFromDef( Game.Save.NextItemId++, def, ArchetypeCatalog.Get( Archetype.Regular ), Game.Save.Day, 1f );
		Game.Inventory.Acquire( item, 0, Game.Save.Day );
		UiState.Bump();
		Log.Info( $"[pawn] Added {item.Name} to backroom." );
	}

	[ConCmd( "pawn_truth" )]
	public static void ShowTruth()
	{
		if ( !Check() ) return;
		foreach ( var item in Game.Save.Inventory.Where( i => i.Location is not (ItemLocation.Sold or ItemLocation.Scrapped) ) )
		{
			Log.Info( $"[pawn] #{item.Id} {item.Name}: {item.TrueAuthenticity}, {item.Condition}, {item.Rarity}, {item.LegalStatus}, " +
				$"defects=[{string.Join( ",", item.Defects )}], true={ItemValue.TrueValue( item, Game )}" );
		}

		var neg = Game.Negotiation;
		if ( neg.Active && neg.Item is not null )
		{
			var i = neg.Item;
			Log.Info( $"[pawn] COUNTER ITEM #{i.Id} {i.Name}: {i.TrueAuthenticity}, {i.Condition}, {i.Rarity}, {i.LegalStatus}, " +
				$"defects=[{string.Join( ",", i.Defects )}], true={ItemValue.TrueValue( i, Game )}" );
		}
	}

	[ConCmd( "pawn_skipday" )]
	public static void SkipDay()
	{
		if ( !Check() ) return;
		if ( Game.State == GameState.MorningPrep ) Game.OpenShop();
		if ( Game.State == GameState.ShopOpen ) Game.BeginClosing( auto: false );
		Log.Info( "[pawn] Fast-forwarding to day end." );
	}

	[ConCmd( "pawn_default" )]
	public static void ForcePawnDefault()
	{
		if ( !Check() ) return;
		foreach ( var c in Game.Save.PawnContracts )
			c.DueDay = Game.Save.Day - 1;
		Log.Info( $"[pawn] Forced {Game.Save.PawnContracts.Count} contracts to expire at day end." );
	}

	[ConCmd( "pawn_event" )]
	public static void ForceEvent( string eventId = "" )
	{
		if ( !Check() ) return;
		var def = string.IsNullOrEmpty( eventId ) ? EventCatalog.Random() : EventCatalog.Get( eventId );
		if ( def is null )
		{
			Log.Warning( $"[pawn] Unknown event '{eventId}'. Options: {string.Join( ", ", EventCatalog.All.Select( e => e.Id ) )}" );
			return;
		}
		Game.Events.Force( def );
		UiState.Bump();
		Log.Info( $"[pawn] Event set: {def.Name}" );
	}

	[ConCmd( "pawn_unlock" )]
	public static void UnlockAll()
	{
		if ( !Check() ) return;
		foreach ( var t in ToolCatalog.All )
			if ( !Game.Save.OwnsTool( t.Id ) )
				Game.Save.Tools.Add( t.Id.ToString() );
		foreach ( var u in UpgradeCatalog.All )
			if ( !Game.Save.OwnsUpgrade( u.Id ) )
				Game.Save.Upgrades.Add( u.Id.ToString() );
		Game.Shop.RebuildUpgradeGeometry();
		UiState.Bump();
		Log.Info( "[pawn] All tools and upgrades unlocked." );
	}

	[ConCmd( "pawn_clear_inventory" )]
	public static void ClearInventory()
	{
		if ( !Check() ) return;
		Game.Save.Inventory.Clear();
		Game.Save.PawnContracts.Clear();
		Game.Shop.RefreshDisplays();
		UiState.Bump();
		Log.Info( "[pawn] Inventory cleared." );
	}

	[ConCmd( "pawn_reset" )]
	public static void ResetSave()
	{
		if ( !Check() ) return;
		Game.RestartAfterBankruptcy();
		Log.Info( "[pawn] Save wiped — fresh game started." );
	}

	// ---- Headless loop drivers (used for automated smoke tests) ----

	[ConCmd( "pawn_open" )]
	public static void OpenShopCmd()
	{
		if ( !Check() ) return;
		Game.OpenShop();
		Log.Info( $"[pawn] State: {Game.State}" );
	}

	[ConCmd( "pawn_serve" )]
	public static void ServeCounter()
	{
		if ( !Check() ) return;
		var ok = Game.Customers.TryServeCounterCustomer();
		Log.Info( ok
			? $"[pawn] Serving {Game.Negotiation.Customer?.Name} — {Game.Negotiation.Kind}, ask {Game.Negotiation.CurrentAsk}"
			: "[pawn] Nobody at the counter (or already negotiating)." );
	}

	[ConCmd( "pawn_offer" )]
	public static void Offer( int amount )
	{
		if ( !Check() ) return;
		var neg = Game.Negotiation;
		if ( !neg.Active ) { Log.Warning( "[pawn] No active negotiation." ); return; }
		switch ( neg.Kind )
		{
			case NegotiationKind.Sell: neg.PlayerOffer( amount ); break;
			case NegotiationKind.Pawn: neg.OfferLoan( amount, GameConstants.PawnFeeDefault ); break;
			case NegotiationKind.BuyerOffer: neg.CounterBuyer( amount ); break;
			default: Log.Warning( $"[pawn] Offer not applicable to {neg.Kind}." ); return;
		}
		Log.Info( $"[pawn] {(neg.Active ? $"\"{neg.CustomerLine}\" (mood {neg.Customer?.Mood:0.00})" : "Negotiation ended.")}" );
	}

	[ConCmd( "pawn_accept" )]
	public static void Accept()
	{
		if ( !Check() ) return;
		var neg = Game.Negotiation;
		if ( !neg.Active ) { Log.Warning( "[pawn] No active negotiation." ); return; }
		switch ( neg.Kind )
		{
			case NegotiationKind.Sell: neg.AcceptAsk(); break;
			case NegotiationKind.BuyerOffer: neg.AcceptBuyerOffer(); break;
			case NegotiationKind.Redeem: neg.AcceptRedemption(); break;
			case NegotiationKind.Pawn: neg.OfferLoan( neg.CurrentAsk, GameConstants.PawnFeeDefault ); break;
		}
		Log.Info( $"[pawn] Negotiation active: {neg.Active}. Cash: {Game.Economy.Cash}" );
	}

	[ConCmd( "pawn_walk" )]
	public static void RejectDeal()
	{
		if ( !Check() ) return;
		if ( !Game.Negotiation.Active ) { Log.Warning( "[pawn] No active negotiation." ); return; }
		Game.Negotiation.Reject();
		Log.Info( "[pawn] Walked away from the deal." );
	}

	[ConCmd( "pawn_price" )]
	public static void PriceItem( int itemId, int price )
	{
		if ( !Check() ) return;
		var item = Game.Inventory.Get( itemId );
		if ( item is null ) { Log.Warning( $"[pawn] No item #{itemId}." ); return; }
		Game.SetSalePrice( item, price );
		Log.Info( $"[pawn] {item.Name} priced at {item.SalePrice}." );
	}

	[ConCmd( "pawn_display" )]
	public static void DisplayItemCmd( int itemId )
	{
		if ( !Check() ) return;
		var item = Game.Inventory.Get( itemId );
		if ( item is null ) { Log.Warning( $"[pawn] No item #{itemId}." ); return; }
		Game.DisplayItem( item );
		Log.Info( $"[pawn] {item.Name} location: {item.Location}, slot {item.DisplaySlot}." );
	}

	[ConCmd( "pawn_clean" )]
	public static void CleanCmd( int itemId )
	{
		if ( !Check() ) return;
		var item = Game.Inventory.Get( itemId );
		var ok = item is not null && Game.Workshop.Clean( item );
		Log.Info( ok ? $"[pawn] Cleaned {item.Name}. Dirtiness now {item.Dirtiness:0.00}." : "[pawn] Couldn't clean that." );
		UiState.Bump();
	}

	[ConCmd( "pawn_repair" )]
	public static void RepairCmd( int itemId )
	{
		if ( !Check() ) return;
		var item = Game.Inventory.Get( itemId );
		if ( item is null ) { Log.Warning( $"[pawn] No item #{itemId}." ); return; }
		var (success, message) = Game.Workshop.Repair( item, Game );
		Log.Info( $"[pawn] Repair {(success ? "OK" : "failed")}: {message} Condition={item.Condition}" );
		UiState.Bump();
	}

	[ConCmd( "pawn_research" )]
	public static void ResearchCmd( int itemId )
	{
		if ( !Check() ) return;
		var item = Game.Inventory.Get( itemId );
		if ( item is null ) { Log.Warning( $"[pawn] No item #{itemId}." ); return; }
		var (success, message) = Game.Workshop.Research( item, Game );
		Log.Info( $"[pawn] Research {(success ? "OK" : "failed")}: {message}" );
		UiState.Bump();
	}

	[ConCmd( "pawn_nextday" )]
	public static void NextDayCmd()
	{
		if ( !Check() ) return;
		Game.NextDay();
		Log.Info( $"[pawn] Now day {Game.Save.Day}, state {Game.State}." );
	}

	[ConCmd( "pawn_state" )]
	public static void PrintState()
	{
		if ( !Check() ) return;
		var g = Game;
		Log.Info( $"[pawn] State={g.State} Day={g.Save.Day} Clock={g.ClockLabel} Cash={g.Economy.Cash} Debt={g.Save.Debt} Rep={g.Save.Reputation:0}" );
		Log.Info( $"[pawn] Queue={g.Customers.QueueCount} Active={g.Customers.ActiveCount} AtCounter={g.Customers.CustomerAtCounter?.Profile?.Name ?? "-"} Negotiating={g.Negotiation.Active}" );
		Log.Info( $"[pawn] Backroom={g.Inventory.Backroom.Count()} Display={g.Inventory.OnDisplay.Count()} Pawned={g.Inventory.Pawned.Count()} Contracts={g.Save.PawnContracts.Count}" );
		var player = ShopPlayer.Instance;
		if ( player.IsValid() )
			Log.Info( $"[pawn] Player pos={player.WorldPosition:0} eye={player.EyePosition:0} fwd={player.EyeForward:0.00}" );
	}
}
