namespace Terraingen.Player;

using Sandbox.Network;
using Terraingen.GameData;
using Terraingen.Multiplayer;
using Terraingen.UI;
using Terraingen.World;

/// <summary>Consume and drop items from specific inventory slots.</summary>
public sealed partial class ThornsPlayerGameplay
{
	public void RequestConsumeFromSlot( ThornsContainerKind kind, int index )
	{
		if ( !IsLocalPlayer() )
			return;

		if ( Networking.IsActive && !Networking.IsHost )
			RpcConsumeFromSlot( (int)kind, index );
		else
			HostTryConsumeFromSlot( kind, index );
	}

	public void RequestDropFromSlot( ThornsContainerKind kind, int index, int count = 1 )
	{
		if ( !IsLocalPlayer() || count <= 0 )
			return;

		if ( Networking.IsActive && !Networking.IsHost )
			RpcDropFromSlot( (int)kind, index, count );
		else
			HostTryDropFromSlot( kind, index, count );
	}

	[Rpc.Host]
	void RpcConsumeFromSlot( int containerKind, int index )
	{
		if ( !ValidateCaller() )
			return;

		HostTryConsumeFromSlot( (ThornsContainerKind)containerKind, index );
	}

	[Rpc.Host]
	void RpcDropFromSlot( int containerKind, int index, int count )
	{
		if ( !ValidateCaller() )
			return;

		HostTryDropFromSlot( (ThornsContainerKind)containerKind, index, count );
	}

	public bool HostTryConsumeFromSlot( ThornsContainerKind kind, int index )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || HostIsDead() )
			return false;

		if ( kind is ThornsContainerKind.Head or ThornsContainerKind.Chest or ThornsContainerKind.Legs
		     or ThornsContainerKind.WorldLoot )
			return false;

		index = NormalizeIndex( kind, index );
		var stack = _inventory.GetSlot( kind, index );
		if ( stack.IsEmpty || !ThornsSurvivalConsumables.IsConsumable( stack.ItemId ) )
			return false;

		var itemId = stack.ItemId;
		if ( !HostApplyConsumable( itemId ) )
			return false;

		stack.Count--;
		if ( stack.Count <= 0 )
			stack = ThornsItemStack.EmptyStack;

		MarkInventorySyncDirty();
		_inventory.SetSlot( kind, index, stack );
		ThornsMilestoneTracker.OnInventoryChanged( this );
		PushInventoryToOwner();
		HostRefreshVitals( forceShowHealth: IsMedicalConsumable( itemId ), forceSync: true );
		HostPersistPlayerState();
		return true;
	}

	public bool HostTryDropFromSlot( ThornsContainerKind kind, int index, int count = 1 )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || count <= 0 || HostIsDead() )
			return false;

		if ( kind is ThornsContainerKind.Head or ThornsContainerKind.Chest or ThornsContainerKind.Legs
		     or ThornsContainerKind.WorldLoot )
			return false;

		index = NormalizeIndex( kind, index );
		var stack = _inventory.GetSlot( kind, index );
		if ( stack.IsEmpty )
			return false;

		var originalStack = stack;
		count = Math.Min( count, stack.Count );
		var dropStack = ThornsInventoryWeaponState.CopyStackWithCount( stack, count );

		if ( ThornsItemRegistry.TryGet( dropStack.ItemId, out var def ) )
		{
			var combatId = ThornsInventoryWeaponState.ResolveCombatId( def, dropStack.ItemId );
			ThornsInventoryWeaponState.EnsureWeaponRowInitialized( ref dropStack, combatId );
			ThornsInventoryWeaponState.EnsureToolDurabilityInitialized( ref dropStack, def );
		}

		stack.Count -= count;
		if ( stack.Count <= 0 )
			stack = ThornsItemStack.EmptyStack;

		MarkInventorySyncDirty();
		_inventory.SetSlot( kind, index, stack );

		if ( ThornsDeathCrateWorldService.Instance?.HostTrySpawnPlayerDrop( GameObject, dropStack ) != true )
		{
			_inventory.SetSlot( kind, index, originalStack );
			if ( IsLocalPlayer() )
				ThornsNotificationBus.Push( "Could not drop item here.", "warning" );
			return false;
		}

		ThornsMilestoneTracker.OnInventoryChanged( this );
		PushInventoryToOwner();
		HostPersistPlayerState();
		return true;
	}

	static bool IsMedicalConsumable( string itemId ) =>
		itemId.Equals( "bandage", StringComparison.OrdinalIgnoreCase )
		|| itemId.Equals( "medkit", StringComparison.OrdinalIgnoreCase )
		|| itemId.Equals( "morphine_pen", StringComparison.OrdinalIgnoreCase );
}
