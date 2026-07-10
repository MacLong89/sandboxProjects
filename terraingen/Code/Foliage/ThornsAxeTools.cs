namespace Terraingen.Foliage;

using Terraingen.GameData;
using Terraingen.Multiplayer;
using Terraingen.Player;
using Terraingen.UI;

/// <summary>Detects hatchet tools for tree harvesting.</summary>
public static class ThornsAxeTools
{
	public static bool IsAxeItemId( string itemId )
	{
		if ( string.IsNullOrWhiteSpace( itemId ) )
			return false;

		var id = ThornsItemIdAliases.Canonicalize( itemId ).ToLowerInvariant();
		if ( id.Contains( "pickaxe" ) || id.Contains( "_pick" ) )
			return false;

		return id.Contains( "hatchet" ) || id.Contains( "axe" );
	}

	public static bool PlayerHasAxeEquipped( GameObject playerRoot )
	{
		if ( !playerRoot.IsValid() )
			return false;

		return TryGetEquippedItemId( playerRoot, out var itemId ) && IsAxeItemId( itemId );
	}

	public static bool TryGetEquippedItemId( GameObject playerRoot, out string itemId )
	{
		itemId = "";
		if ( !playerRoot.IsValid() )
			return false;

		var gameplay = playerRoot.Components.Get<ThornsPlayerGameplay>( FindMode.EnabledInSelf );
		if ( !gameplay.IsValid() )
			gameplay = playerRoot.Components.Get<ThornsPlayerGameplay>( FindMode.EverythingInSelfAndDescendants );

		if ( gameplay.IsValid() )
		{
			if ( ThornsMultiplayer.IsHostOrOffline && gameplay.HostTryGetActiveHotbarItemId( out itemId ) )
				return true;

			if ( gameplay.TryGetActiveHotbarItemId( out itemId ) )
				return true;
		}

		if ( ThornsPlayerGameplay.Local.IsValid()
		     && ThornsPlayerGameplay.Local.GameObject == playerRoot
		     && ThornsUiClientState.HasSnapshot )
		{
			var inventory = ThornsUiClientState.Snapshot?.Inventory;
			var slots = inventory?.Slots;
			if ( slots is { Count: > 0 } )
			{
				var idx = Math.Clamp( inventory.ActiveHotbarIndex, 0, ThornsInventoryContainer.HotbarSlotCount - 1 );
				var slot = slots.FirstOrDefault( s =>
					s is not null && s.Container == ThornsContainerKind.Hotbar && s.Index == idx );

				if ( slot is not null && !string.IsNullOrWhiteSpace( slot.ItemId ) )
				{
					itemId = slot.ItemId;
					return true;
				}
			}
		}

		return false;
	}
}
