namespace Sandbox;

/// <summary>
/// Radio shop RPCs + <see cref="TryUsePressedOpenRadioShop"/> (invoked from <see cref="ThornsDeathCrateInteractor"/> so Use wins vs loot/chest on the same frame).
/// </summary>
[Title( "Thorns — Radio shop interactor" )]
[Category( "Thorns" )]
[Icon( "shopping_cart" )]
[Order( 80 )]
public sealed class ThornsRadioShopInteractor : Component
{
	const float RadioLookPromptSearchHoriz = 420f;

	/// <summary>Called when Use is pressed — run from <see cref="ThornsDeathCrateInteractor"/> before chest/loot so E opens the shop.</summary>
	public bool TryUsePressedOpenRadioShop()
	{
		if ( !Game.IsPlaying )
			return false;

		if ( !ThornsPawn.IsLocalConnectionOwner( this ) )
			return false;

		if ( UiBlocksRadioInteraction() )
			return false;

		var station = ThornsRadioStation.FindBestUnderAimForPawn( GameObject.Scene, GameObject, RadioLookPromptSearchHoriz );
		if ( !station.IsValid() || station.StationId == Guid.Empty )
			return false;

		Log.Info( $"[Thorns] Radio shop open request station={station.StationId}" );
		RequestOpenRadioShop( station.StationId );
		return true;
	}

	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying )
			return;

		if ( !ThornsPawn.IsLocalConnectionOwner( this ) )
			return;

		var shell = Components.Get<ThornsGameShell>();
		if ( !shell.IsValid() || !shell.Enabled )
			return;

		// Proximity hint rail (<see cref="ThornsProximityInteractionHints"/>) owns the radio "Press E" prompt.
		shell.SetRadioShopLookPrompt( false );
	}

	bool UiBlocksRadioInteraction()
	{
		var hud = Components.Get<ThornsDebugHudHost>();
		var shell = Components.Get<ThornsGameShell>();
		var hp = Components.Get<ThornsHealth>();
		var build = Components.Get<ThornsBuildingController>();
		if ( shell is { IsValid: true, Enabled: true } && shell.BlocksGameplayShellOverlay )
			return true;
		if ( hud.IsValid() && (hud.ShowFullInventory || hud.ShowDebugOverlay || hud.ShowRadioShop) )
			return true;
		if ( hp.IsValid() && (hp.IsDeadState || !hp.IsAlive) )
			return true;
		if ( build.IsValid() && build.BuildModeActive )
			return true;
		return false;
	}

	[Rpc.Host]
	public void RequestOpenRadioShop( Guid stationId )
	{
		if ( !Networking.IsHost )
			return;

		if ( Rpc.Caller is null )
		{
			RpcRadioShopSession( false, Guid.Empty, 0, Array.Empty<string>(), Array.Empty<int>(), Array.Empty<int>() );
			return;
		}

		if ( Rpc.Caller.Id != GameObject.Network.OwnerId )
		{
			Log.Warning( "[Thorns] Radio shop open rejected: not_owner" );
			RpcRadioShopSession( false, Guid.Empty, 0, Array.Empty<string>(), Array.Empty<int>(), Array.Empty<int>() );
			return;
		}

		if ( !ThornsRadioStation.ActiveById.TryGetValue( stationId, out var station ) || !station.IsValid() )
		{
			Log.Warning( "[Thorns] Radio shop open rejected: bad_station" );
			RpcRadioShopSession( false, Guid.Empty, 0, Array.Empty<string>(), Array.Empty<int>(), Array.Empty<int>() );
			return;
		}

		if ( !station.HostIsInRange( GameObject.WorldPosition ) )
		{
			Log.Warning( "[Thorns] Radio shop open rejected: distance" );
			RpcRadioShopSession( false, Guid.Empty, 0, Array.Empty<string>(), Array.Empty<int>(), Array.Empty<int>() );
			return;
		}

		var coneOnly = ThornsWorldUseAim.HasInteriorRadioRootTag( station.GameObject );
		if ( !ThornsWorldUseAim.PawnLooksAtInteractableRoot( GameObject, station.GameObject, station.InteractionRadius, coneOnly ) )
		{
			Log.Warning( "[Thorns] Radio shop open rejected: not_looking_at_station" );
			RpcRadioShopSession( false, Guid.Empty, 0, Array.Empty<string>(), Array.Empty<int>(), Array.Empty<int>() );
			return;
		}

		var health = Components.Get<ThornsHealth>();
		if ( health.IsValid() && (health.IsDeadState || !health.IsAlive) )
		{
			RpcRadioShopSession( false, Guid.Empty, 0, Array.Empty<string>(), Array.Empty<int>(), Array.Empty<int>() );
			return;
		}

		var epoch = ThornsRadioShopRotation.CurrentEpochIndexHost();
		var offers = ThornsRadioShopRotation.HostBuildOffersForEpoch( epoch );
		var n = offers.Length;
		var ids = new string[n];
		var prices = new int[n];
		var maxB = new int[n];
		for ( var i = 0; i < n; i++ )
		{
			ids[i] = offers[i].ItemId;
			prices[i] = offers[i].BuyPricePerUnitMetal;
			maxB[i] = offers[i].MaxBuyPerTransaction;
		}

		RpcRadioShopSession( true, stationId, epoch, ids, prices, maxB );
	}

	[Rpc.Owner]
	void RpcRadioShopSession(
		bool ok,
		Guid stationId,
		long epoch,
		string[] itemIds,
		int[] buyPrices,
		int[] maxBuy )
	{
		var shell = Components.Get<ThornsGameShell>();
		if ( shell is { IsValid: true, Enabled: true } )
		{
			if ( !ok )
			{
				shell.CloseRadioShopUi();
				return;
			}

			shell.ApplyRadioShopCatalog( epoch, itemIds, buyPrices, maxBuy );
			shell.SetRadioShopOpen( true, stationId );
			return;
		}

		var hud = Components.Get<ThornsDebugHudHost>();
		if ( !hud.IsValid() )
			return;

		if ( !ok )
		{
			hud.SetRadioShopOpen( false );
			return;
		}

		hud.ApplyRadioShopCatalog( epoch, itemIds, buyPrices, maxBuy );
		hud.SetRadioShopOpen( true, stationId );
	}

	[Rpc.Host]
	public void RequestRadioBuy( Guid stationId, int offerSlotIndex, int quantity )
	{
		if ( !Networking.IsHost )
			return;

		if ( Rpc.Caller is null || Rpc.Caller.Id != GameObject.Network.OwnerId )
			return;

		if ( !ValidateStationDistance( stationId, out _ ) )
			return;

		var qty = Math.Clamp( quantity, 1, 999 );
		var epoch = ThornsRadioShopRotation.CurrentEpochIndexHost();
		var offers = ThornsRadioShopRotation.HostBuildOffersForEpoch( epoch );
		if ( offerSlotIndex < 0 || offerSlotIndex >= offers.Length )
			return;

		var offer = offers[offerSlotIndex];
		if ( ThornsRadioShopCatalog.IsMetalTradeBlockedFromRadioShop( offer.ItemId ) )
			return;

		if ( !ThornsItemRegistry.TryGet( offer.ItemId, out var def ) )
			return;

		var maxStack = Math.Min( qty, offer.MaxBuyPerTransaction );
		if ( maxStack <= 0 )
			return;

		var unitPrice = offer.BuyPricePerUnitMetal;
		var totalCost = unitPrice * maxStack;
		if ( totalCost <= 0 || totalCost / unitPrice != maxStack )
			return;

		var inv = Components.Get<ThornsInventory>();
		if ( !inv.IsValid() )
			return;

		var currencyId = ThornsRadioShopCatalog.CurrencyItemId;
		var metalHave = inv.ServerCountItemId( currencyId );
		if ( metalHave < totalCost )
		{
			Log.Warning( $"[Thorns] Radio buy rejected: insufficient metal need={totalCost} have={metalHave}" );
			return;
		}

		if ( def.ItemType == ThornsItemType.Weapon || def.ItemType == ThornsItemType.Tool || def.ItemType == ThornsItemType.Armor )
		{
			if ( maxStack != 1 )
				return;
			if ( !inv.HostCanAcceptCraftOutput( offer.ItemId, 1 ) )
				return;
		}
		else if ( !inv.HostCanFitStackableResourceQuantity( offer.ItemId, maxStack ) )
		{
			Log.Warning( "[Thorns] Radio buy rejected: inventory_full" );
			return;
		}

		var metalRemoved = inv.ServerRemoveItemId( currencyId, totalCost );
		if ( metalRemoved != totalCost )
		{
			Log.Warning( $"[Thorns] Radio buy invariant: metalRemoved={metalRemoved} expected={totalCost}" );
			if ( metalRemoved > 0 )
				inv.ServerAddItem( currencyId, metalRemoved, suppressMilestoneRecord: true );
			return;
		}

		var leftover = inv.ServerAddItem( offer.ItemId, maxStack );
		if ( leftover > 0 )
		{
			var refund = unitPrice * leftover;
			var refundLeft = inv.ServerAddItem( currencyId, refund, suppressMilestoneRecord: true );
			if ( refundLeft > 0 )
				Log.Warning( $"[Thorns] Radio buy partial grant leftover={leftover} refundMetal={refund} refundLeft={refundLeft}" );
		}
		var boughtQty = maxStack - leftover;
		var spent = totalCost - unitPrice * leftover;
		Log.Info( $"[Thorns] Radio buy ok item={offer.ItemId}×{boughtQty} cost={spent}" );
		ThornsGameShell.HostPushToastForPawnRoot(
			GameObject,
			$"Purchased ×{boughtQty} {def.DisplayName}\n−{spent} metal · supplies inbound.",
			3.6f,
			ThornsGameplayToastKind.Economy );
	}

	[Rpc.Host]
	public void RequestRadioSell( Guid stationId, int inventorySlotIndex, int quantity )
	{
		if ( !Networking.IsHost )
			return;

		if ( Rpc.Caller is null || Rpc.Caller.Id != GameObject.Network.OwnerId )
			return;

		if ( !ValidateStationDistance( stationId, out _ ) )
			return;

		var inv = Components.Get<ThornsInventory>();
		if ( !inv.IsValid() )
			return;

		var qty = Math.Clamp( quantity, 1, 999 );
		if ( !inv.TryGetHostSlot( inventorySlotIndex, out var slot ) || slot.IsEmpty || slot.Quantity <= 0 )
			return;

		var take = Math.Min( qty, slot.Quantity );
		var itemId = slot.ItemId;
		if ( ThornsRadioShopCatalog.IsMetalTradeBlockedFromRadioShop( itemId ) )
			return;

		var unitSell = ThornsRadioShopCatalog.HostComputeSellMetalForInventorySlot( inv, inventorySlotIndex );
		var payout = unitSell * take;
		if ( payout <= 0 )
			return;

		var currencyId = ThornsRadioShopCatalog.CurrencyItemId;
		if ( !inv.HostCanFitStackableResourceQuantity( currencyId, payout ) )
		{
			Log.Warning( $"[Thorns] Radio sell rejected: inventory_full cannot fit {payout} metal" );
			return;
		}

		var removed = inv.ServerRemoveItem( inventorySlotIndex, take );
		if ( removed != take )
		{
			Log.Warning( $"[Thorns] Radio sell invariant: removed={removed} expected={take}" );
			return;
		}

		var metalLeft = inv.ServerAddItem( currencyId, payout, suppressMilestoneRecord: true );
		if ( metalLeft > 0 )
		{
			inv.ServerAddItem( itemId, take, suppressMilestoneRecord: true );
			Log.Warning( $"[Thorns] Radio sell rollback: metalLeft={metalLeft} payout={payout}" );
			return;
		}
		Log.Info( $"[Thorns] Radio sell ok item={itemId}×{take} payout={payout}" );

		var sellNm = ThornsItemRegistry.TryGet( itemId, out var sd ) ? sd.DisplayName : itemId;
		ThornsGameShell.HostPushToastForPawnRoot(
			GameObject,
			$"Sold ×{take} {sellNm}\n+{payout} metal · thanks for the salvage.",
			3.4f,
			ThornsGameplayToastKind.Economy );
	}

	bool ValidateStationDistance( Guid stationId, out ThornsRadioStation station )
	{
		station = default;
		if ( !ThornsRadioStation.ActiveById.TryGetValue( stationId, out var s ) || !s.IsValid() )
			return false;

		if ( !s.HostIsInRange( GameObject.WorldPosition ) )
			return false;

		station = s;
		return true;
	}
}
