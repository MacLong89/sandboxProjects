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
		if ( !ThornsMultiplayer.IsHostOrOffline || req is null || HostIsDead() )
			return;

		var recipe = ThornsDefinitionRegistry.GetRecipe( req.RecipeId );
		if ( recipe is null || req.Quantity <= 0 )
			return;

		if ( !HostMeetsCraftTier( recipe ) )
			return;

		if ( !HostHasIngredients( recipe, req.Quantity ) )
			return;

		if ( !HostStationAllows( recipe ) )
			return;

		HostConsumeIngredients( recipe, req.Quantity );
		_craftQueue.Enqueue( recipe, req.Quantity );
		MarkInventorySyncDirty();
		PushInventoryToOwner();
		HostPersistPlayerState();
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

			PushOwnerNotification(
				droppedOverflow
					? $"Inventory full — crafted items dropped nearby ({remaining})."
					: $"Inventory full — lost {remaining} crafted item(s).",
				droppedOverflow ? "warning" : "error" );

			Log.Warning(
				$"[Thorns Craft] Grant overflow itemId={recipe.OutputItemId} remaining={remaining} dropped={droppedOverflow} account={AccountKey}" );
		}

		// Still credit milestones if anything was granted or safely dropped (ingredients already spent).
		if ( granted > 0 || droppedOverflow || remaining == 0 )
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
