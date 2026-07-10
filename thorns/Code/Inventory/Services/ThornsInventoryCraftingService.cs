namespace Sandbox;

public sealed class ThornsInventoryCraftingService
{
	IThornsInventoryCraftingHost _host;

	public void Bind( IThornsInventoryCraftingHost host ) => _host = host;

	/// <summary>Host-only: whether craft output can fully fit (stack rules + empty slot for weapon/armor).</summary>
	public bool HostCanAcceptCraftOutput( string itemId, int quantity )
	{
		if ( !Networking.IsHost || quantity <= 0 )
			return false;

		if ( !ThornsItemRegistry.TryGet( itemId, out var def ) )
			return false;

		if ( def.ItemType == ThornsItemType.Weapon || def.ItemType == ThornsItemType.Tool || def.ItemType == ThornsItemType.Armor )
		{
			if ( quantity != 1 )
				return false;
			return _host.FindFirstEmptySlot() >= 0;
		}

		return _host.HostCanFitStackableResourceQuantity( itemId, quantity );
	}

	public void RequestCraftRecipe( string recipeId )
	{
		Log.Info( $"[Thorns] Craft request received recipe='{recipeId}' caller={_host.RpcCaller?.Id}" );

		if ( !Networking.IsHost )
			return;

		if ( !_host.ValidateRpcCallerOwnsPawn() )
		{
			Log.Warning( "[Thorns] Craft rejected: not owner" );
			_host.ClientCraftResultNotify( "rejected", "not_owner" );
			return;
		}

		if ( _host.IsPlayerDead() )
		{
			Log.Warning( "[Thorns] Craft rejected: dead" );
			_host.ClientCraftResultNotify( "rejected", "dead" );
			return;
		}

		if ( !ThornsCraftingRecipes.TryGet( recipeId, out var recipe ) )
		{
			Log.Warning( $"[Thorns] Craft rejected: unknown recipe '{recipeId}'" );
			_host.ClientCraftResultNotify( "rejected", "invalid_recipe" );
			return;
		}

		Log.Info( $"[Thorns] Craft recipe validated id={recipe.Id} → {recipe.OutputItemId}×{recipe.OutputQuantity} tier≥{recipe.RequiredCraftingTier}" );

		var upgrades = _host.GetPlayerUpgrades();
		var craftTier = upgrades.IsValid() ? upgrades.GetEffectiveCraftingTier() : 1;
		if ( craftTier < recipe.RequiredCraftingTier )
		{
			Log.Warning( $"[Thorns] Craft rejected: tier need={recipe.RequiredCraftingTier} have={craftTier}" );
			_host.ClientCraftResultNotify( "rejected", "tier" );
			return;
		}

		foreach ( var ing in recipe.Ingredients )
		{
			if ( _host.ServerCountItemId( ing.ItemId ) < ing.Quantity )
			{
				Log.Warning( $"[Thorns] Craft rejected: missing material {ing.ItemId} need={ing.Quantity} have={_host.ServerCountItemId( ing.ItemId )}" );
				_host.ClientCraftResultNotify( "rejected", $"missing:{ing.ItemId}" );
				return;
			}
		}

		if ( !HostCanAcceptCraftOutput( recipe.OutputItemId, recipe.OutputQuantity ) )
		{
			Log.Warning( "[Thorns] Craft rejected: output cannot fit (inventory_full / no slot)" );
			_host.ClientCraftResultNotify( "rejected", "inventory_full" );
			return;
		}

		foreach ( var ing in recipe.Ingredients )
		{
			var removed = _host.ServerRemoveItemId( ing.ItemId, ing.Quantity, suppressOwnerSnapshot: true );
			if ( removed < ing.Quantity )
			{
				Log.Error( $"[Thorns] Craft critical: removed {removed} < need {ing.Quantity} for {ing.ItemId}" );
				_host.PushSnapshotToOwner();
				_host.ClientCraftResultNotify( "rejected", "invariant" );
				return;
			}

			Log.Info( $"[Thorns] Craft ingredient removed item={ing.ItemId} qty={removed}" );
		}

		var leftover = _host.ServerAddItem( recipe.OutputItemId, recipe.OutputQuantity, suppressOwnerSnapshot: true );
		if ( leftover > 0 )
		{
			Log.Warning( $"[Thorns] Craft output failed leftover={leftover} — restoring ingredients" );
			foreach ( var ing in recipe.Ingredients )
				_host.ServerAddItem( ing.ItemId, ing.Quantity, suppressOwnerSnapshot: true, suppressMilestoneRecord: true );

			_host.PushSnapshotToOwner();
			_host.ClientCraftResultNotify( "rejected", "inventory_full" );
			return;
		}

		Log.Info( $"[Thorns] Craft output created item={recipe.OutputItemId} qty={recipe.OutputQuantity}" );
		_host.PushSnapshotToOwner();
		Log.Info( "[Thorns] Craft complete — inventory snapshot pushed (owner)" );
		_host.HostRecordRecipeCrafted( recipe.Id );

		_host.ClientCraftResultNotify( "ok", recipe.Id );
	}

	public void ClientCraftResultNotify( string status, string detail )
	{
		Log.Info( $"[Thorns] Craft result (owner mirror): status={status} detail={detail}" );
		if ( status != "ok" || string.IsNullOrWhiteSpace( detail ) )
			return;

		var shell = _host.GetComponent<ThornsGameShell>();
		if ( !shell.IsValid() )
			return;

		if ( !ThornsCraftingRecipes.TryGet( detail, out var recipe ) )
			return;

		var outNm = ThornsItemRegistry.TryGet( recipe.OutputItemId, out var outDef )
			? outDef.DisplayName
			: recipe.OutputItemId;
		var qty = recipe.OutputQuantity;
		shell.PushGameplayToast(
			$"Crafted ×{qty} {outNm}\nNice work — output sent to your inventory.",
			3.4f,
			ThornsGameplayToastKind.Positive );
	}
}
