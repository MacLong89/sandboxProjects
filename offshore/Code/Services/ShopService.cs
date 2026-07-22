namespace Offshore;

public sealed class ShopService
{
	public ShopTab Tab { get; set; } = ShopTab.Bait;
	public string SelectedId { get; set; } = "worm";

	readonly InventoryService _inv;
	readonly Action _autosave;
	readonly Action<string> _notice;
	readonly Func<string, bool> _onObjective;

	public ShopService( InventoryService inv, Action autosave, Action<string> notice, Func<string, bool> onObjective )
	{
		_inv = inv;
		_autosave = autosave;
		_notice = notice;
		_onObjective = onObjective;
	}

	public string BuySelected()
	{
		var save = _inv.Save;
		switch ( Tab )
		{
			case ShopTab.Bait:
			{
				var bait = Catalog.BaitById( SelectedId );
				if ( bait is null ) return "Unknown bait.";
				if ( save.Coins < bait.Price ) return "Not enough coins.";
				save.Coins -= bait.Price;
				save.BaitCounts[bait.Id] = _inv.GetBait( bait.Id ) + bait.BundleCount;
				if ( string.IsNullOrEmpty( save.EquippedBait ) )
					save.EquippedBait = bait.Id;
				_onObjective( "equip_bait" );
				_autosave();
				_notice( $"Bought {bait.BundleCount}x {bait.Name}." );
				return null;
			}
			case ShopTab.Rods: return BuyUnique( SelectedId, save.OwnedRods, Catalog.RodById( SelectedId )?.Price ?? -1, id => save.EquippedRod = id, "Rod equipped." );
			case ShopTab.Reels: return BuyUnique( SelectedId, save.OwnedReels, Catalog.ReelById( SelectedId )?.Price ?? -1, id => save.EquippedReel = id, "Reel equipped." );
			case ShopTab.Hooks: return BuyUnique( SelectedId, save.OwnedHooks, Catalog.HookById( SelectedId )?.Price ?? -1, id => save.EquippedHook = id, "Hook equipped." );
			case ShopTab.Lines: return BuyUnique( SelectedId, save.OwnedLines, Catalog.LineById( SelectedId )?.Price ?? -1, id => save.EquippedLine = id, "Line equipped." );
			case ShopTab.Boats:
			{
				var wasOwned = save.OwnedBoats.Contains( SelectedId );
				var boatMsg = BuyUnique( SelectedId, save.OwnedBoats, Catalog.BoatById( SelectedId )?.Price ?? -1, id => save.EquippedBoat = id, "Boat ready at dock." );
				if ( boatMsg is null && !wasOwned )
					_onObjective( "buy_boat" );
				return boatMsg;
			}
		}
		return "Nothing selected.";
	}

	public string EquipSelected()
	{
		var save = _inv.Save;
		switch ( Tab )
		{
			case ShopTab.Bait:
				if ( _inv.GetBait( SelectedId ) <= 0 && SelectedId != save.EquippedBait )
					return "You don't own that bait.";
				save.EquippedBait = SelectedId;
				_onObjective( "equip_bait" );
				_autosave();
				return null;
			case ShopTab.Rods:
				if ( !save.OwnedRods.Contains( SelectedId ) ) return "Not owned.";
				save.EquippedRod = SelectedId; _autosave(); return null;
			case ShopTab.Reels:
				if ( !save.OwnedReels.Contains( SelectedId ) ) return "Not owned.";
				save.EquippedReel = SelectedId; _autosave(); return null;
			case ShopTab.Hooks:
				if ( !save.OwnedHooks.Contains( SelectedId ) ) return "Not owned.";
				save.EquippedHook = SelectedId; _autosave(); return null;
			case ShopTab.Lines:
				if ( !save.OwnedLines.Contains( SelectedId ) ) return "Not owned.";
				save.EquippedLine = SelectedId; _autosave(); return null;
			case ShopTab.Boats:
				if ( !save.OwnedBoats.Contains( SelectedId ) ) return "Not owned.";
				save.EquippedBoat = SelectedId; _autosave(); return null;
		}
		return "Can't equip.";
	}

	string BuyUnique( string id, List<string> owned, int price, Action<string> equip, string ok )
	{
		if ( price < 0 ) return "Unknown item.";
		if ( owned.Contains( id ) )
		{
			equip( id );
			_autosave();
			_notice( ok );
			return null;
		}
		if ( price == 0 )
		{
			owned.Add( id );
			equip( id );
			_autosave();
			return null;
		}
		if ( _inv.Save.Coins < price ) return "Not enough coins.";
		_inv.Save.Coins -= price;
		owned.Add( id );
		equip( id );
		_onObjective( "buy_upgrade" );
		_autosave();
		_notice( $"Purchased and equipped." );
		return null;
	}
}
