namespace Terraingen.Player;

using Sandbox.Network;
using Terraingen.Economy;
using Terraingen.GameData;
using Terraingen.Multiplayer;
using Terraingen.UI;
using Terraingen.UI.Core;
using Terraingen.World;

/// <summary>Radio shop session, buy/sell validation, and owner UI sync.</summary>
public sealed partial class ThornsPlayerGameplay
{
	Guid _openRadioStationId = Guid.Empty;

	public bool HasOpenRadioShop => _openRadioStationId != Guid.Empty;

	public void RequestOpenRadioShop( Guid stationId )
	{
		if ( !IsLocalPlayer() || stationId == Guid.Empty )
			return;

		if ( Networking.IsActive && !Networking.IsHost )
			RpcOpenRadioShop( stationId );
		else
			HostOpenRadioShop( stationId );
	}

	public void RequestCloseRadioShop()
	{
		if ( !IsLocalPlayer() )
			return;

		if ( Networking.IsActive && !Networking.IsHost )
			RpcCloseRadioShop();
		else
			HostCloseRadioShop();
	}

	public void RequestRadioBuy( Guid stationId, int offerSlotIndex, int quantity )
	{
		if ( !IsLocalPlayer() || stationId == Guid.Empty )
			return;

		if ( Networking.IsActive && !Networking.IsHost )
			RpcRadioBuy( stationId, offerSlotIndex, quantity );
		else
			HostRadioBuy( stationId, offerSlotIndex, quantity );
	}

	public void RequestRadioSell( Guid stationId, ThornsContainerKind kind, int slotIndex, int quantity )
	{
		if ( !IsLocalPlayer() || stationId == Guid.Empty )
			return;

		if ( Networking.IsActive && !Networking.IsHost )
			RpcRadioSell( stationId, (byte)kind, slotIndex, quantity );
		else
			HostRadioSell( stationId, kind, slotIndex, quantity );
	}

	[Rpc.Host]
	void RpcOpenRadioShop( Guid stationId )
	{
		if ( !ValidateCaller() )
			return;

		HostOpenRadioShop( stationId );
	}

	[Rpc.Host]
	void RpcCloseRadioShop()
	{
		if ( !ValidateCaller() )
			return;

		HostCloseRadioShop();
	}

	[Rpc.Host]
	void RpcRadioBuy( Guid stationId, int offerSlotIndex, int quantity )
	{
		if ( !ValidateCaller() )
			return;

		HostRadioBuy( stationId, offerSlotIndex, quantity );
	}

	[Rpc.Host]
	void RpcRadioSell( Guid stationId, byte kindByte, int slotIndex, int quantity )
	{
		if ( !ValidateCaller() )
			return;

		HostRadioSell( stationId, (ThornsContainerKind)kindByte, slotIndex, quantity );
	}

	public void HostOpenRadioShop( Guid stationId )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || stationId == Guid.Empty )
		{
			PushRadioShopToOwner( closed: true );
			return;
		}

		if ( !ThornsRadioStation.ActiveById.TryGetValue( stationId, out var station ) || !station.IsValid() )
		{
			PushRadioShopToOwner( closed: true );
			return;
		}

		if ( !station.HostIsInRange( GameObject.WorldPosition ) )
		{
			PushRadioShopToOwner( closed: true );
			return;
		}

		var coneOnly = ThornsWorldUseAim.HasInteriorRadioRootTag( station.GameObject );
		if ( !ThornsWorldUseAim.PawnLooksAtInteractableRoot(
			     GameObject, station.GameObject, station.InteractionRadius, coneOnly ) )
		{
			PushRadioShopToOwner( closed: true );
			return;
		}

		if ( HasOpenWorldContainer )
			HostCloseWorldContainer();

		_openRadioStationId = stationId;
		PushRadioShopToOwner( HostBuildRadioShopDto() );
		HostTryFireMilestoneEventOnce( "open_radio_shop" );
	}

	public void HostCloseRadioShop()
	{
		if ( _openRadioStationId == Guid.Empty )
			return;

		_openRadioStationId = Guid.Empty;
		PushRadioShopToOwner( closed: true );
	}

	public void HostRadioBuy( Guid stationId, int offerSlotIndex, int quantity )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || HostIsDead() || !ValidateRadioStation( stationId ) )
			return;

		var qty = Math.Clamp( quantity, 1, 999 );
		var epoch = ThornsRadioShopRotation.CurrentEpochIndexHost();
		var offers = ThornsRadioShopRotation.HostBuildOffersForEpoch( epoch );
		if ( offerSlotIndex < 0 || offerSlotIndex >= offers.Length )
			return;

		var offer = offers[offerSlotIndex];
		if ( ThornsRadioShopCatalog.IsCurrencyTradeBlockedFromRadioShop( offer.ItemId ) )
			return;

		if ( !ThornsItemRegistry.TryGet( offer.ItemId, out var def ) )
			return;

		var maxStack = Math.Min( qty, offer.MaxBuyPerTransaction );
		if ( maxStack <= 0 )
			return;

		var unitPrice = offer.BuyPricePerUnit;
		var totalCost = unitPrice * maxStack;
		if ( totalCost <= 0 || totalCost / unitPrice != maxStack )
			return;

		var currencyId = ThornsRadioShopCatalog.CurrencyItemId;
		if ( HostCountItem( currencyId ) < totalCost )
			return;

		var grantQty = def.Category is ThornsItemCategory.Weapon or ThornsItemCategory.Tool or ThornsItemCategory.Armor
			? 1
			: maxStack;

		if ( def.Category is ThornsItemCategory.Weapon or ThornsItemCategory.Tool or ThornsItemCategory.Armor )
		{
			if ( maxStack != 1 || !HostHasEmptyCarrySlot() )
				return;
		}
		else if ( HostComputeAddableCount( offer.ItemId, grantQty ) < grantQty )
			return;

		HostRemoveItemCount( currencyId, totalCost );

		var boughtQty = 0;
		var spent = totalCost;
		if ( def.Category is ThornsItemCategory.Weapon or ThornsItemCategory.Tool or ThornsItemCategory.Armor )
		{
			var stack = new ThornsItemStack { ItemId = offer.ItemId, Count = 1 };
			if ( HostTryGrantItemStack( stack ) )
				boughtQty = 1;
			else
				HostTryAddItemCount( currencyId, totalCost );
		}
		else
		{
			var leftover = HostTryAddItemCount( offer.ItemId, grantQty );
			if ( leftover > 0 )
			{
				var refund = unitPrice * leftover;
				HostTryAddItemCount( currencyId, refund );
			}

			boughtQty = grantQty - leftover;
			spent = totalCost - unitPrice * leftover;
		}

		if ( boughtQty > 0 )
		{
			HostInitializeMatchingWeaponStacks( offer.ItemId );
			ThornsMilestoneTracker.OnInventoryChanged( this );
			PushInventoryToOwner();
			PushRadioShopToOwner();
			HostPersistPlayerState();
			PushEconomyTransactionSfxToOwner();

			var name = def.DisplayName;
			ThornsNotificationBus.Push(
				$"Purchased ×{boughtQty} {name}\n−{spent} metal ingots",
				"economy",
				3.6f );
		}
	}

	public void HostRadioSell( Guid stationId, ThornsContainerKind kind, int slotIndex, int quantity )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || HostIsDead() || !ValidateRadioStation( stationId ) )
			return;

		if ( kind is not (ThornsContainerKind.Inventory or ThornsContainerKind.Hotbar) )
			return;

		var qty = Math.Clamp( quantity, 1, 999 );
		var idx = NormalizeIndex( kind, slotIndex );
		var stack = _inventory.GetSlot( kind, idx );
		if ( stack.IsEmpty || stack.Count <= 0 )
			return;

		var take = Math.Min( qty, stack.Count );
		var itemId = stack.ItemId;
		if ( ThornsRadioShopCatalog.IsCurrencyTradeBlockedFromRadioShop( itemId ) )
			return;

		if ( !ThornsItemRegistry.TryGet( itemId, out var def ) )
			return;

		var unitSell = ThornsRadioShopCatalog.HostComputeSellForStack( in stack, def );
		var payout = unitSell * take;
		if ( payout <= 0 )
			return;

		var currencyId = ThornsRadioShopCatalog.CurrencyItemId;
		if ( HostComputeAddableCount( currencyId, payout ) < payout )
			return;

		if ( !HostTryRemoveFromSlot( kind, idx, take ) )
			return;

		var metalLeft = HostTryAddItemCount( currencyId, payout );
		if ( metalLeft > 0 )
		{
			HostTryAddItemCount( itemId, take );
			return;
		}

		ThornsMilestoneTracker.OnInventoryChanged( this );
		PushInventoryToOwner();
		PushRadioShopToOwner();
		HostPersistPlayerState();
		PushEconomyTransactionSfxToOwner();

		var name = def.DisplayName;
		ThornsNotificationBus.Push(
			$"Sold ×{take} {name}\n+{payout} metal ingots",
			"economy",
			3.4f );
	}

	bool ValidateRadioStation( Guid stationId )
	{
		if ( _openRadioStationId != stationId )
			return false;

		if ( !ThornsRadioStation.ActiveById.TryGetValue( stationId, out var station ) || !station.IsValid() )
			return false;

		return station.HostIsInRange( GameObject.WorldPosition );
	}

	public void HostTickRadioShopProximity()
	{
		if ( _openRadioStationId == Guid.Empty )
			return;

		if ( !ThornsRadioStation.ActiveById.TryGetValue( _openRadioStationId, out var station )
		     || !station.IsValid()
		     || !station.HostIsInRange( GameObject.WorldPosition ) )
			HostCloseRadioShop();
	}

	void PushRadioShopToOwner()
	{
		if ( _openRadioStationId == Guid.Empty )
		{
			PushRadioShopToOwner( closed: true );
			return;
		}

		PushRadioShopToOwner( HostBuildRadioShopDto() );
	}

	ThornsRadioShopSnapshotDto HostBuildRadioShopDto()
	{
		var epoch = ThornsRadioShopRotation.CurrentEpochIndexHost();
		var offers = ThornsRadioShopRotation.HostBuildOffersForEpoch( epoch );
		var dto = new ThornsRadioShopSnapshotDto
		{
			IsOpen = true,
			StationId = _openRadioStationId.ToString( "D" ),
			Epoch = epoch
		};

		foreach ( var offer in offers )
		{
			dto.Offers.Add( new ThornsRadioShopOfferDto
			{
				ItemId = offer.ItemId,
				BuyPrice = offer.BuyPricePerUnit,
				MaxBuy = offer.MaxBuyPerTransaction
			} );
		}

		return dto;
	}

	void PushRadioShopToOwner( ThornsRadioShopSnapshotDto dto = null, bool closed = false )
	{
		var snap = closed || dto is null
			? new ThornsRadioShopSnapshotDto()
			: dto;

		if ( IsLocalPlayer() )
			ThornsUiClientState.ApplyPartialRadioShop( snap );
		else if ( Networking.IsActive )
			RpcSyncRadioShopJson( Json.Serialize( snap ) );
	}

	[Rpc.Owner]
	void RpcSyncRadioShopJson( string json )
	{
		if ( !ThornsNetAuthority.TryDeserializeJson( json, ThornsNetAuthority.DefaultOwnerJsonMaxBytes, out ThornsRadioShopSnapshotDto snap ) )
			return;

		ThornsUiClientState.ApplyPartialRadioShop( snap );
	}

	void PushEconomyTransactionSfxToOwner()
	{
		if ( IsLocalPlayer() )
			ThornsUiSfx.PlayEconomyTransaction();
		else if ( Networking.IsActive )
			RpcOwnerEconomyTransactionSfx();
	}

	[Rpc.Owner]
	void RpcOwnerEconomyTransactionSfx() => ThornsUiSfx.PlayEconomyTransaction();

	bool HostHasEmptyCarrySlot()
	{
		for ( var i = 0; i < ThornsInventoryContainer.InventorySlotCount; i++ )
		{
			if ( _inventory.GetSlot( ThornsContainerKind.Inventory, i ).IsEmpty )
				return true;
		}

		for ( var i = 0; i < ThornsInventoryContainer.HotbarSlotCount; i++ )
		{
			if ( _inventory.GetSlot( ThornsContainerKind.Hotbar, i ).IsEmpty )
				return true;
		}

		return false;
	}

	int HostComputeAddableCount( string itemId, int desired )
	{
		if ( !ThornsItemRegistry.TryGet( itemId, out var def ) || desired <= 0 )
			return 0;

		if ( def.Category is ThornsItemCategory.Weapon or ThornsItemCategory.Tool or ThornsItemCategory.Armor )
			return HostHasEmptyCarrySlot() ? Math.Min( 1, desired ) : 0;

		var remaining = desired;
		foreach ( var kind in new[] { ThornsContainerKind.Inventory, ThornsContainerKind.Hotbar } )
		{
			var max = kind == ThornsContainerKind.Inventory
				? ThornsInventoryContainer.InventorySlotCount
				: ThornsInventoryContainer.HotbarSlotCount;

			for ( var i = 0; i < max && remaining > 0; i++ )
			{
				var s = _inventory.GetSlot( kind, i );
				if ( !s.IsEmpty && s.ItemId != itemId )
					continue;

				if ( s.IsEmpty )
					remaining -= Math.Min( remaining, def.MaxStack );
				else
					remaining -= Math.Min( remaining, Math.Max( 0, def.MaxStack - s.Count ) );
			}
		}

		return Math.Max( 0, desired - remaining );
	}

	int HostTryAddItemCount( string itemId, int count )
	{
		if ( count <= 0 || !ThornsItemRegistry.TryGet( itemId, out var def ) )
			return count;

		MarkInventorySyncDirty();
		var remaining = count;
		foreach ( var kind in new[] { ThornsContainerKind.Inventory, ThornsContainerKind.Hotbar } )
		{
			var max = kind == ThornsContainerKind.Inventory
				? ThornsInventoryContainer.InventorySlotCount
				: ThornsInventoryContainer.HotbarSlotCount;

			for ( var i = 0; i < max && remaining > 0; i++ )
			{
				var s = _inventory.GetSlot( kind, i );
				if ( !s.IsEmpty && s.ItemId != itemId )
					continue;

				if ( s.IsEmpty )
				{
					var put = Math.Min( remaining, def.MaxStack );
					_inventory.SetSlot( kind, i, new ThornsItemStack { ItemId = itemId, Count = put } );
					remaining -= put;
				}
				else
				{
					var space = def.MaxStack - s.Count;
					if ( space <= 0 )
						continue;

					var put = Math.Min( space, remaining );
					_inventory.SetSlot( kind, i, new ThornsItemStack { ItemId = itemId, Count = s.Count + put } );
					remaining -= put;
				}
			}
		}

		return remaining;
	}

	bool HostTryRemoveFromSlot( ThornsContainerKind kind, int index, int count )
	{
		var stack = _inventory.GetSlot( kind, index );
		if ( stack.IsEmpty || stack.Count < count )
			return false;

		MarkInventorySyncDirty();
		var left = stack.Count - count;
		_inventory.SetSlot( kind, index, left > 0
			? new ThornsItemStack { ItemId = stack.ItemId, Count = left, HasDurability = stack.HasDurability, Durability = stack.Durability }
			: ThornsItemStack.EmptyStack );
		return true;
	}
}
