namespace OffshoreFishing.Core;

public sealed class ShopService
{
	private readonly GameContent _content;
	private readonly List<IDomainEvent> _events = new();

	public ShopService( GameContent content )
	{
		_content = content;
	}

	public IReadOnlyList<IDomainEvent> DrainEvents()
	{
		var copy = _events.ToList();
		_events.Clear();
		return copy;
	}

	public bool TryBuyItem( GameState state, string itemId )
	{
		if ( !_content.TryGetItem( itemId, out var item ) ) return false;
		if ( state.Gold < item.Price ) return false;
		if ( !string.IsNullOrEmpty( item.UnlockAfterItemId ) && !state.OwnedItemIds.Contains( item.UnlockAfterItemId ) )
			return false;

		state.Gold -= item.Price;
		_events.Add( new GoldChangedEvent { Gold = state.Gold, Delta = -item.Price } );

		if ( item.Category == ItemCategory.Bait || item.Consumable )
		{
			state.AddItem( item.Id, item.Category == ItemCategory.Bait ? 10 : 1 );
		}
		else
		{
			state.AddItem( item.Id, 1 );
			EquipBest( state, item );
		}

		_events.Add( new ItemPurchasedEvent { ItemId = item.Id, Price = item.Price } );
		return true;
	}

	public bool TryBuyBoat( GameState state, string boatId )
	{
		var boat = _content.Boats.FirstOrDefault( b => b.Id == boatId );
		if ( boat == null ) return false;
		if ( state.OwnedBoatId == boat.Id || state.OwnedItemIds.Contains( boat.Id ) ) return false;
		if ( state.Gold < boat.Price ) return false;
		if ( !string.IsNullOrEmpty( boat.UnlockAfterBoatId ) && state.OwnedBoatId != boat.UnlockAfterBoatId
			&& !state.OwnedItemIds.Contains( boat.UnlockAfterBoatId ) )
			return false;

		state.Gold -= boat.Price;
		state.OwnedBoatId = boat.Id;
		state.AddItem( boat.Id, 1 );
		_events.Add( new GoldChangedEvent { Gold = state.Gold, Delta = -boat.Price } );
		_events.Add( new ItemPurchasedEvent { ItemId = boat.Id, Price = boat.Price } );
		_events.Add( new NotificationEvent { Text = $"Purchased {boat.Name}!" } );
		return true;
	}

	public bool TryHireBoat( GameState state, string hiredId )
	{
		var def = _content.HiredBoats.FirstOrDefault( h => h.Id == hiredId );
		if ( def == null ) return false;
		if ( state.OwnedHiredBoatIds.Contains( hiredId ) ) return false;
		if ( state.Gold < def.Price ) return false;
		if ( !string.IsNullOrEmpty( def.RequiredBoatId )
			&& state.OwnedBoatId != def.RequiredBoatId
			&& !state.OwnedItemIds.Contains( def.RequiredBoatId ) )
			return false;

		state.Gold -= def.Price;
		state.OwnedHiredBoatIds.Add( hiredId );
		state.HiredBoatTripTimers[hiredId] = 0;
		_events.Add( new GoldChangedEvent { Gold = state.Gold, Delta = -def.Price } );
		_events.Add( new ItemPurchasedEvent { ItemId = hiredId, Price = def.Price } );
		return true;
	}

	public int SellAll( GameState state )
	{
		if ( state.Hold.Count == 0 ) return 0;
		var total = state.Hold.Sum( f => f.Worth );
		var count = state.Hold.Count;
		state.Hold.Clear();
		state.Gold += total;
		state.TotalGoldEarned += total;
		state.TutorialFirstSaleDone = true;
		_events.Add( new GoldChangedEvent { Gold = state.Gold, Delta = total } );
		_events.Add( new FishSoldEvent { Count = count, GoldGained = total } );
		return total;
	}

	public void Equip( GameState state, string itemId )
	{
		if ( !_content.TryGetItem( itemId, out var item ) ) return;
		if ( state.CountItem( itemId ) <= 0 && !state.OwnedItemIds.Contains( itemId ) ) return;

		switch ( item.Category )
		{
			case ItemCategory.Rod: state.EquippedRodId = itemId; break;
			case ItemCategory.Spool: state.EquippedSpoolId = itemId; break;
			case ItemCategory.Hook: state.EquippedHookId = itemId; break;
			case ItemCategory.Bait: state.EquippedBaitId = itemId; break;
		}
	}

	private void EquipBest( GameState state, ItemDef item )
	{
		switch ( item.Category )
		{
			case ItemCategory.Rod: state.EquippedRodId = item.Id; break;
			case ItemCategory.Spool: state.EquippedSpoolId = item.Id; break;
			case ItemCategory.Hook: state.EquippedHookId = item.Id; break;
			case ItemCategory.Bait: state.EquippedBaitId = item.Id; break;
		}
	}
}
