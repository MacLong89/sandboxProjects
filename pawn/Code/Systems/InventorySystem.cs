namespace PawnShop;

/// <summary>
/// Owns the item lists: backroom, pawn storage, repair bench, and display slots.
/// Display slot indices map to physical shelf positions built by ShopBuilder.
/// </summary>
public sealed class InventorySystem
{
	private readonly SaveData _save;

	public InventorySystem( SaveData save )
	{
		_save = save;
	}

	public IEnumerable<ItemInstance> All => _save.Inventory;
	public IEnumerable<ItemInstance> Backroom => _save.Inventory.Where( i => i.Location == ItemLocation.Backroom );
	public IEnumerable<ItemInstance> OnDisplay => _save.Inventory.Where( i => i.Location == ItemLocation.OnDisplay );
	public IEnumerable<ItemInstance> Pawned => _save.Inventory.Where( i => i.Location == ItemLocation.PawnStorage );
	public IEnumerable<ItemInstance> SoldHistory => _save.Inventory.Where( i => i.Location == ItemLocation.Sold );

	public ItemInstance Get( int id ) => _save.Inventory.FirstOrDefault( i => i.Id == id );

	// --- Capacities ---
	public int StorageCapacity
	{
		get
		{
			var cap = 10;
			if ( _save.OwnsUpgrade( UpgradeId.Storage1 ) ) cap += 8;
			if ( _save.OwnsUpgrade( UpgradeId.Storage2 ) ) cap += 12;
			return cap;
		}
	}

	public int DisplayCapacity
	{
		get
		{
			var cap = 0;
			for ( var s = 0; s < ShopLayout.Slots.Length; s++ )
				if ( ShopLayout.SlotAvailable( s, _save ) )
					cap++;
			return cap;
		}
	}

	public int BackroomCount => Backroom.Count() + _save.Inventory.Count( i => i.Location == ItemLocation.RepairBench );
	public int DisplayCount => OnDisplay.Count();
	public bool StorageFull => BackroomCount >= StorageCapacity;
	public bool DisplayFull => DisplayCount >= DisplayCapacity;

	/// <summary>Whether a display slot index exists with current upgrades.</summary>
	public bool SlotUnlocked( int slot ) => ShopLayout.SlotAvailable( slot, _save );

	public bool SlotOccupied( int slot ) => OnDisplay.Any( i => i.DisplaySlot == slot );

	public ItemInstance ItemInSlot( int slot ) => OnDisplay.FirstOrDefault( i => i.DisplaySlot == slot );

	public int FirstFreeSlot()
	{
		for ( var s = 0; s < ShopLayout.Slots.Length; s++ )
			if ( SlotUnlocked( s ) && !SlotOccupied( s ) )
				return s;
		return -1;
	}

	/// <summary>Add a newly acquired item into the backroom (always succeeds — capacity gates deals up front).</summary>
	public void Acquire( ItemInstance item, int pricePaid, int day )
	{
		item.Location = ItemLocation.Backroom;
		item.PurchasePrice = pricePaid;
		item.DayAcquired = day;
		item.DisplaySlot = -1;
		if ( !_save.Inventory.Contains( item ) )
			_save.Inventory.Add( item );
	}

	public void AddPawned( ItemInstance item, int day )
	{
		item.Location = ItemLocation.PawnStorage;
		item.DayAcquired = day;
		item.DisplaySlot = -1;
		if ( !_save.Inventory.Contains( item ) )
			_save.Inventory.Add( item );
	}

	/// <summary>Move a backroom item onto a display slot. Returns false if invalid.</summary>
	public bool Display( ItemInstance item, int slot = -1 )
	{
		if ( item is null ) return false;
		if ( item.Location is not (ItemLocation.Backroom or ItemLocation.OnDisplay) ) return false;

		if ( slot < 0 ) slot = FirstFreeSlot();
		if ( !SlotUnlocked( slot ) || SlotOccupied( slot ) ) return false;

		if ( item.SalePrice <= 0 )
			item.SalePrice = ItemValue.SuggestedPrice( item, GameManager.Instance );

		item.Location = ItemLocation.OnDisplay;
		item.DisplaySlot = slot;
		GameManager.Instance?.Shop?.RefreshDisplays();
		return true;
	}

	public void Stow( ItemInstance item )
	{
		if ( item is null ) return;
		if ( item.Location == ItemLocation.OnDisplay )
		{
			item.Location = ItemLocation.Backroom;
			item.DisplaySlot = -1;
			GameManager.Instance?.Shop?.RefreshDisplays();
		}
	}

	public void MarkSold( ItemInstance item, int salePrice )
	{
		if ( item is null ) return;
		item.Location = ItemLocation.Sold;
		item.SalePrice = salePrice;
		item.DisplaySlot = -1;
		GameManager.Instance?.Shop?.RefreshDisplays();
	}

	public void Remove( ItemInstance item )
	{
		if ( item is null ) return;
		_save.Inventory.Remove( item );
		GameManager.Instance?.Shop?.RefreshDisplays();
	}

	public void Confiscate( ItemInstance item )
	{
		if ( item is null ) return;
		item.Location = ItemLocation.Confiscated;
		item.DisplaySlot = -1;
		GameManager.Instance?.Shop?.RefreshDisplays();
	}

	/// <summary>Prune sold/confiscated/scrapped records older than a few days to keep saves lean.</summary>
	public void PruneHistory( int currentDay )
	{
		_save.Inventory.RemoveAll( i =>
			i.Location is ItemLocation.Sold or ItemLocation.Confiscated or ItemLocation.Scrapped
			&& currentDay - i.DayAcquired > 5 );
	}
}
