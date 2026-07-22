namespace PawnShop;

/// <summary>
/// The morning "mystery crate": a wholesaler occasionally offers a sealed lot of
/// unseen items for a flat price. Pure risk/reward — contents roll on purchase.
/// </summary>
public sealed class CrateSystem
{
	private readonly SaveData _save;

	public CrateSystem( SaveData save )
	{
		_save = save;
	}

	public bool OfferedToday => _save.CrateDay == _save.Day && _save.CrateCost > 0;
	public bool BoughtToday => _save.CrateDay == _save.Day && _save.CrateBought;
	public int Cost => _save.CrateCost;
	public int ItemCount => _save.CrateItemCount;

	public string PitchLine => _save.CrateCost switch
	{
		>= 500 => "\"Estate clean-out. Sealed boxes, no peeking. Could be treasure.\"",
		>= 250 => "\"Storage unit default. I haven't opened it, that's the deal.\"",
		_ => "\"Van's full, need it gone today. Sold as-is.\"",
	};

	/// <summary>Maybe generate a crate offer for this morning. ~40% of days from day 2.</summary>
	public void RollForDay( int day )
	{
		if ( _save.CrateDay == day ) return;

		_save.CrateDay = day;
		_save.CrateBought = false;
		_save.CrateCost = 0;

		if ( day < 2 || Game.Random.Float() > 0.4f ) return;

		_save.CrateItemCount = Game.Random.Int( 2, 4 );
		var tier = Math.Min( 3, 1 + day / 4 );
		_save.CrateCost = _save.CrateItemCount * 55 * tier + Game.Random.Int( -30, 60 );
		_save.CrateCost = Math.Max( 80, _save.CrateCost / 5 * 5 );
	}

	/// <summary>Buy and open the crate. Returns the items added, or null if it failed.</summary>
	public List<ItemInstance> Buy( GameManager game )
	{
		if ( !OfferedToday || BoughtToday ) return null;
		if ( !game.Economy.CanAfford( Cost ) )
		{
			game.Toast( "Not enough cash for the crate.", "error" );
			Sfx.Play( Sfx.UiError, 0.5f );
			return null;
		}
		if ( game.Inventory.BackroomCount + ItemCount > game.Inventory.StorageCapacity )
		{
			game.Toast( "Not enough backroom space for the crate.", "inventory" );
			Sfx.Play( Sfx.UiError, 0.5f );
			return null;
		}

		game.Economy.Spend( Cost );
		game.Economy.Ledger.Purchases++;
		game.Economy.Ledger.PurchaseSpend += Cost;
		_save.CrateBought = true;

		var items = new List<ItemInstance>();
		var archetype = ArchetypeCatalog.Get( Archetype.CluelessSeller );
		var costShare = Cost / ItemCount;
		for ( var i = 0; i < ItemCount; i++ )
		{
			var item = ItemFactory.Roll( _save.NextItemId++, archetype, _save.Day, scamMult: 1.4f );
			// Crate stock skews grubby — that's the gamble.
			item.Dirtiness = Math.Max( item.Dirtiness, Game.Random.Float( 0.2f, 0.8f ) );
			game.Inventory.Acquire( item, costShare, _save.Day );
			items.Add( item );
		}

		Sfx.Play( Sfx.ItemPlaced, 0.8f );
		Sfx.Play( Sfx.CashRegister, 0.6f );
		game.Toast( $"Crate dumped on the backroom table: {string.Join( ", ", items.Select( i => i.Name ) )}.", "package_2" );
		game.Shop?.RefreshDisplays();
		UiState.Bump();
		return items;
	}
}
