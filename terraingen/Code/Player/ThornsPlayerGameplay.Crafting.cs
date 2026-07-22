namespace Terraingen.Player;

using Sandbox.Network;
using Terraingen.Buildings;
using Terraingen.GameData;
using Terraingen.Multiplayer;
using Terraingen.UI;

/// <summary>Crafting queue, recipes, station checks, and craft UI state (extracted module).</summary>
public sealed partial class ThornsPlayerGameplay
{
	public void RequestCraft( ThornsCraftRequest req )
	{
		if ( !IsLocalPlayer() )
			return;

		if ( Networking.IsActive && !Networking.IsHost )
			RpcCraft( req );
		else
			HostCraft( req );
	}

	[Rpc.Host]
	void RpcCraft( ThornsCraftRequest req )
	{
		if ( !ValidateCaller() )
			return;

		HostCraft( req );
	}

	public void HostCraft( ThornsCraftRequest req )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || req is null )
			return;

		if ( HostIsDead() )
		{
			PushOwnerNotification( "Cannot craft while dead.", "warning" );
			return;
		}

		var recipe = ThornsDefinitionRegistry.GetRecipe( req.RecipeId );
		if ( recipe is null || req.Quantity <= 0 )
		{
			PushOwnerNotification( "Unknown recipe.", "warning" );
			return;
		}

		if ( !HostMeetsCraftTier( recipe ) )
		{
			PushOwnerNotification( "Skill tier too low for this recipe.", "warning" );
			return;
		}

		if ( !HostHasIngredients( recipe, req.Quantity ) )
		{
			PushOwnerNotification( "Missing ingredients.", "warning" );
			return;
		}

		if ( !HostStationAllows( recipe ) )
		{
			PushOwnerNotification( "Need the right crafting station nearby.", "warning" );
			return;
		}

		if ( !HostCanGuaranteeCraftDelivery( recipe, req.Quantity ) )
		{
			PushOwnerNotification( "Inventory full — free space or drop items before crafting.", "warning" );
			return;
		}

		HostConsumeIngredients( recipe, req.Quantity );
		_craftQueue.Enqueue( recipe, req.Quantity );
		MarkInventorySyncDirty();
		PushInventoryToOwner();
		HostPersistPlayerState();
	}

	bool HostCanGuaranteeCraftDelivery( ThornsRecipeDefinition recipe, int batches )
	{
		if ( recipe is null || batches <= 0 )
			return false;

		var needed = recipe.OutputCount * batches;
		if ( needed <= 0 )
			return true;

		// Enough free inventory capacity, or a world drop crate can be spawned as fallback.
		if ( HostCountFreeInventoryCapacity( recipe.OutputItemId ) >= needed )
			return true;

		return Terraingen.World.ThornsDeathCrateWorldService.Instance is { IsValid: true };
	}

	int HostCountFreeInventoryCapacity( string itemId )
	{
		if ( string.IsNullOrWhiteSpace( itemId ) || !ThornsItemRegistry.TryGet( itemId, out var def ) || def is null )
			return 0;

		var free = 0;
		for ( var i = 0; i < ThornsInventoryContainer.InventorySlotCount; i++ )
		{
			var s = _inventory.GetSlot( ThornsContainerKind.Inventory, i );
			if ( s.IsEmpty )
			{
				free += def.MaxStack;
				continue;
			}

			if ( s.ItemId != itemId )
				continue;

			free += Math.Max( 0, def.MaxStack - s.Count );
		}

		return free;
	}

	bool HostMeetsCraftTier( ThornsRecipeDefinition recipe )
		=> ThornsCraftProgression.MeetsCraftTier( HostGetCraftTier(), recipe );

	public int HostGetCraftTier()
	{
		HostEnsureSkillsRanks();
		return ThornsCraftProgression.ResolveCraftTier( _skills );
	}

	bool HostHasIngredients( ThornsRecipeDefinition recipe, int batches )
	{
		foreach ( var ing in recipe.Ingredients )
		{
			if ( HostCountItem( ing.ItemId ) < ing.Count * batches )
				return false;
		}

		return true;
	}

	bool HostStationAllows( ThornsRecipeDefinition recipe )
	{
		if ( recipe.Station == ThornsCraftStationKind.Hand )
			return true;

		return recipe.Station <= _nearestStation;
	}

	void HostConsumeIngredients( ThornsRecipeDefinition recipe, int batches )
	{
		foreach ( var ing in recipe.Ingredients )
			HostRemoveItemCount( ing.ItemId, ing.Count * batches );
	}

	public void HostGrantRecipeOutput( string recipeId )
	{
		var recipe = ThornsDefinitionRegistry.GetRecipe( recipeId );
		if ( recipe is null )
			return;

		// AUDIT FIX: HostAddItem previously discarded overflow silently after ingredients were
		// already consumed at queue time. Now: add what fits, drop leftovers as a world crate,
		// and notify the owner. Revert: call HostAddItem only and remove overflow handling.
		var granted = HostTryAddItem( recipe.OutputItemId, recipe.OutputCount, out var remaining );
		var droppedOverflow = false;
		if ( remaining > 0 )
		{
			var overflow = new ThornsItemStack { ItemId = recipe.OutputItemId, Count = remaining };
			if ( ThornsItemRegistry.TryGet( recipe.OutputItemId, out var overflowDef ) )
			{
				if ( ThornsItemTier.SupportsTiering( overflowDef ) )
					ThornsItemTier.ApplyCraftDefaults( ref overflow, overflowDef );
			}

			droppedOverflow = Terraingen.World.ThornsDeathCrateWorldService.Instance
				?.HostTrySpawnPlayerDrop( GameObject, overflow ) == true;

			if ( !droppedOverflow )
			{
				// Hold overflow on the player until inventory space opens — never silently destroy paid crafts.
				HostEnqueuePendingCraftOverflow( overflow );
				PushOwnerNotification(
					$"Inventory full — crafted items held until you free space ({remaining}).",
					"warning" );
			}
			else
			{
				PushOwnerNotification(
					$"Inventory full — crafted items dropped nearby ({remaining}).",
					"warning" );
			}

			Log.Warning(
				$"[Thorns Craft] Grant overflow itemId={recipe.OutputItemId} remaining={remaining} dropped={droppedOverflow} held={!droppedOverflow} account={AccountKey}" );
		}

		// Still credit milestones if anything was granted, dropped, or held (ingredients already spent).
		if ( granted > 0 || droppedOverflow || remaining == 0 || remaining > 0 )
		{
			ThornsMilestoneTracker.OnCrafted( this, recipe.OutputItemId, recipe.OutputCount );
			ThornsMilestoneTracker.OnInventoryChanged( this );
			ThornsJourneyProgression.NotifySurvivalArmamentAcquired( this );
			if ( ThornsItemRegistry.TryGet( recipe.OutputItemId, out var itemDef ) )
				PushCraftCompleteToOwner( itemDef.DisplayName );
		}

		MarkInventorySyncDirty();
		PushInventoryToOwner();
		HostPersistPlayerState();
	}

	void HostEnqueuePendingCraftOverflow( ThornsItemStack stack )
	{
		if ( stack.IsEmpty )
			return;

		_pendingCraftOverflow.Add( stack );
	}

	void HostTickPendingCraftOverflow()
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || _pendingCraftOverflow.Count == 0 || HostIsDead() )
			return;

		var changed = false;
		for ( var i = _pendingCraftOverflow.Count - 1; i >= 0; i-- )
		{
			var stack = _pendingCraftOverflow[i];
			if ( stack.IsEmpty )
			{
				_pendingCraftOverflow.RemoveAt( i );
				continue;
			}

			var granted = HostTryAddItem( stack.ItemId, stack.Count, out var remaining );
			if ( granted <= 0 && remaining >= stack.Count )
				continue;

			changed = true;
			if ( remaining <= 0 )
			{
				_pendingCraftOverflow.RemoveAt( i );
				continue;
			}

			stack.Count = remaining;
			_pendingCraftOverflow[i] = stack;
		}

		if ( !changed )
			return;

		MarkInventorySyncDirty();
		PushInventoryToOwner();
		HostPersistPlayerState();
		PushOwnerNotification( "Recovered crafted items into inventory.", "success" );
	}

	public void SetCraftUiState( bool expanded, string category, string recipeId )
	{
		if ( !IsLocalPlayer() )
			return;

		ApplyLocalCraftUiPreview( expanded, category, recipeId );

		if ( Networking.IsActive && !Networking.IsHost )
		{
			RpcSetCraftUiState( expanded, category ?? "", recipeId ?? "" );
			return;
		}

		HostSetCraftUiState( expanded, category, recipeId );
	}

	static void ApplyLocalCraftUiPreview( bool expanded, string category, string recipeId )
	{
		if ( !ThornsUiClientState.HasSnapshot )
			return;

		var inv = ThornsUiClientState.Snapshot.Inventory;
		inv.CraftPanelExpanded = expanded;

		if ( !string.IsNullOrEmpty( category ) )
			inv.ActiveCraftCategoryId = ThornsCraftCatalog.NormalizeCraftCategoryId( category );

		if ( !string.IsNullOrEmpty( recipeId ) )
			inv.SelectedRecipeId = recipeId;

		UiRevisionBus.Publish( UiRevisionChannel.Craft );
	}

	[Rpc.Host]
	void RpcSetCraftUiState( bool expanded, string category, string recipeId )
	{
		if ( !ValidateCaller() )
			return;

		HostSetCraftUiState( expanded, category, recipeId );
	}

	void HostSetCraftUiState( bool expanded, string category, string recipeId )
	{
		_craftPanelExpanded = expanded;

		var categoryChanged = false;
		if ( !string.IsNullOrEmpty( category ) )
		{
			categoryChanged = !string.Equals( _craftCategory, category, StringComparison.OrdinalIgnoreCase );
			_craftCategory = ThornsCraftCatalog.NormalizeCraftCategoryId( category );
		}

		if ( !string.IsNullOrEmpty( recipeId ) )
		{
			_selectedRecipeId = recipeId;
		}
		else if ( categoryChanged || !string.IsNullOrEmpty( category ) )
		{
			ThornsRecipeDefinition firstInCategory = null;
			foreach ( var recipe in ThornsCraftCatalog.EnumerateRecipesForCategory( _craftCategory ) )
			{
				if ( firstInCategory is null
				     || string.Compare( recipe.DisplayName, firstInCategory.DisplayName, StringComparison.OrdinalIgnoreCase ) < 0 )
					firstInCategory = recipe;
			}

			if ( firstInCategory is not null )
				_selectedRecipeId = firstInCategory.Id;
		}

		MarkInventorySyncDirty();
		PushInventoryToOwner();
	}

	public void SetNearestStation( ThornsCraftStationKind station )
	{
		if ( !IsLocalPlayer() )
			return;

		if ( Networking.IsActive && !Networking.IsHost )
			RpcSetNearestStation( station );
		else
			HostSetNearestStation( station );
	}

	[Rpc.Host]
	void RpcSetNearestStation( ThornsCraftStationKind station )
	{
		if ( !ValidateCaller() )
			return;

		HostSetNearestStation( station );
	}

	void HostSetNearestStation( ThornsCraftStationKind station )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline )
			return;

		if ( station == ThornsCraftStationKind.Hand )
		{
			_nearestStation = station;
			MarkInventorySyncDirty();
			PushInventoryToOwner();
			return;
		}

		if ( !ThornsPlacedStructureInteraction.TryPickCraftStationInFront( GameObject, out _, out var inFront )
		     || inFront != station )
			return;

		_nearestStation = station;
		MarkInventorySyncDirty();
		PushInventoryToOwner();
	}
}
