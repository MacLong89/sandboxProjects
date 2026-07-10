namespace Terraingen.Player;

using Sandbox.Network;
using Terraingen.Buildings;
using Terraingen.Combat;
using Terraingen.GameData;
using Terraingen.Multiplayer;
using Terraingen.UI;
using Terraingen.UI.Core;
using Terraingen.World;

/// <summary>Open world loot containers and move items between player inventory and external slots.</summary>
public sealed partial class ThornsPlayerGameplay
{
	string _openWorldContainerKey = "";

	public bool HasOpenWorldContainer => !string.IsNullOrWhiteSpace( _openWorldContainerKey );

	/// <summary>Local client pressed Use before container UI snapshot arrives (network/host open in flight).</summary>
	public bool IsAwaitingWorldContainerUi { get; private set; }

	public void RequestOpenWorldContainer( string containerKey )
	{
		if ( !ThornsLocalPlayer.IsLocallyControlledPawn( GameObject ) || string.IsNullOrWhiteSpace( containerKey ) )
			return;

		IsAwaitingWorldContainerUi = true;
		ThornsPlayerLocomotion.StopMovementImmediate( GameObject );
		ThornsPlayerLocomotion.SetOverlayInputBlocked( GameObject, true );

		if ( Networking.IsActive && !Networking.IsHost )
			RpcOpenWorldContainer( containerKey );
		else if ( !HostOpenWorldContainer( containerKey ) )
			ClearAwaitingWorldContainerUi();
	}

	public void RequestCloseWorldContainer()
	{
		if ( !ThornsLocalPlayer.IsLocallyControlledPawn( GameObject ) )
			return;

		ClearAwaitingWorldContainerUi();

		if ( Networking.IsActive && !Networking.IsHost )
			RpcCloseWorldContainer();
		else
			HostCloseWorldContainer();
	}

	void ClearAwaitingWorldContainerUi()
	{
		if ( !IsAwaitingWorldContainerUi )
			return;

		IsAwaitingWorldContainerUi = false;

		if ( HasOpenWorldContainer || ThornsMenuHost.IsWorldContainerOpen )
			return;

		if ( !ThornsMenuHost.IsOpen && !ThornsMenuHost.IsRadioShopOpen && !ThornsMenuHost.IsResearchOpen )
			ThornsPlayerLocomotion.SetOverlayInputBlocked( GameObject, false );
	}

	[Rpc.Host]
	void RpcOpenWorldContainer( string containerKey )
	{
		if ( !ValidateCaller() )
			return;

		if ( !HostOpenWorldContainer( containerKey ) )
			RpcSyncExternalContainerJson( Json.Serialize( new ThornsExternalContainerSnapshotDto() ) );
	}

	[Rpc.Host]
	void RpcCloseWorldContainer()
	{
		if ( !ValidateCaller() )
			return;

		HostCloseWorldContainer();
	}

	public bool HostOpenWorldContainer( string containerKey )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || HostIsDead() || string.IsNullOrWhiteSpace( containerKey ) )
			return false;

		if ( !ThornsPlayerContainerUse.HostPlayerCanAccessContainer( GameObject, containerKey ) )
			return false;

		var service = ThornsWorldLootContainerService.Instance;
		if ( service is null || !service.IsValid() )
			return false;

		service.HostEnsureRegistered( containerKey );
		if ( !service.TryGet( containerKey, out _ ) )
		{
			if ( containerKey.StartsWith( "furn:", StringComparison.Ordinal ) )
				ThornsBuildingLootWorldService.Instance?.HostSyncFurnitureContainers();

			service.HostEnsureRegistered( containerKey );
			if ( !service.TryGet( containerKey, out _ ) )
				return false;
		}

		if ( HasOpenRadioShop )
			HostCloseRadioShop();

		service.HostEnsureLootReady( containerKey );
		_openWorldContainerKey = containerKey;

		if ( containerKey.StartsWith( "air:", StringComparison.Ordinal ) )
			HostTryFireMilestoneEventOnce( "loot_airdrop" );

		if ( ThornsPlayerContainerUse.TryGetContainerWorldPosition( containerKey, out var openPos ) )
			ThornsGameplaySfx.PlayNetworkedWorldInteraction( openPos, ThornsGameplaySfx.ContainerOpen );

		PushExternalContainerToOwner();
		return true;
	}

	public void HostCloseWorldContainer()
	{
		if ( string.IsNullOrWhiteSpace( _openWorldContainerKey ) )
			return;

		_openWorldContainerKey = "";
		PushExternalContainerToOwner( closed: true );
	}

	public void PushExternalContainerToOwner( bool closed = false )
	{
		var dto = closed || string.IsNullOrWhiteSpace( _openWorldContainerKey )
			? new ThornsExternalContainerSnapshotDto()
			: ThornsWorldLootContainerService.Instance?.HostBuildSnapshot( _openWorldContainerKey )
			  ?? new ThornsExternalContainerSnapshotDto();

		if ( IsLocalPlayer() )
		{
			ThornsUiClientState.ApplyPartialExternalContainer( dto );
			ClearAwaitingWorldContainerUi();
		}
		else if ( Networking.IsActive )
			RpcSyncExternalContainerJson( Json.Serialize( dto ) );
		else
			ThornsUiClientState.ApplyPartialExternalContainer( dto );
	}

	[Rpc.Owner]
	void RpcSyncExternalContainerJson( string json )
	{
		if ( ThornsNetAuthority.TryDeserializeJson( json, ThornsNetAuthority.DefaultOwnerJsonMaxBytes, out ThornsExternalContainerSnapshotDto dto ) )
			ThornsUiClientState.ApplyPartialExternalContainer( dto );

		ClearAwaitingWorldContainerUi();
	}

	bool HostMoveUsesWorldContainer( ThornsMoveItemRequest req )
	{
		if ( req.FromContainer == ThornsContainerKind.WorldLoot || req.ToContainer == ThornsContainerKind.WorldLoot )
			return true;

		if ( !HasOpenWorldContainer )
			return false;

		return req.Mode is ThornsMoveItemMode.QuickTransfer or ThornsMoveItemMode.DoubleClickTransfer
		       || req.ShiftHeld;
	}

	public void HostMoveItemWithWorldContainer( ThornsMoveItemRequest req )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || req is null || HostIsDead() )
			return;

		MarkInventorySyncDirty();

		var key = _openWorldContainerKey;
		if ( string.IsNullOrWhiteSpace( key ) )
			return;

		if ( !string.IsNullOrWhiteSpace( req.WorldContainerKey )
		     && !string.Equals( req.WorldContainerKey, key, StringComparison.Ordinal ) )
			Log.Warning( $"[Thorns Container] Ignoring stale container key '{req.WorldContainerKey}' (open='{key}')." );

		if ( !ThornsPlayerContainerUse.HostPlayerCanAccessContainer( GameObject, key ) )
		{
			HostCloseWorldContainer();
			return;
		}

		var service = ThornsWorldLootContainerService.Instance;
		if ( service is null || !service.IsValid() )
			return;

		if ( req.Mode == ThornsMoveItemMode.QuickTransfer || req.ShiftHeld )
		{
			var tookLoot = req.FromContainer == ThornsContainerKind.WorldLoot;
			HostQuickTransferWorld( req.FromContainer, req.FromIndex, req.ToContainer, key, service );
			if ( tookLoot )
				HostNotifyContainerLootTaken( key );
			ThornsMilestoneTracker.OnInventoryChanged( this );
			PushInventoryToOwner();
			PushExternalContainerToOwner();
			HostPersistPlayerState();
			return;
		}

		if ( req.Mode == ThornsMoveItemMode.DoubleClickTransfer )
		{
			var tookLoot = req.FromContainer == ThornsContainerKind.WorldLoot;
			HostQuickTransferWorld( req.FromContainer, req.FromIndex, req.ToContainer, key, service );
			if ( tookLoot )
				HostNotifyContainerLootTaken( key );
			ThornsMilestoneTracker.OnInventoryChanged( this );
			PushInventoryToOwner();
			PushExternalContainerToOwner();
			HostPersistPlayerState();
			return;
		}

		if ( req.Mode == ThornsMoveItemMode.SplitHalf )
		{
			if ( req.FromContainer == ThornsContainerKind.WorldLoot )
			{
				if ( !ThornsContainerItemMoves.TrySplitHalf(
					    i => service.HostGetSlot( key, i ),
					    ( i, s ) => service.HostSetSlot( key, i, s ),
					    req.FromIndex,
					    i => _inventory.GetSlot( req.ToContainer, NormalizeIndex( req.ToContainer, i ) ),
					    ( i, s ) => _inventory.SetSlot( req.ToContainer, NormalizeIndex( req.ToContainer, i ), s ),
					    req.ToIndex ) )
					return;
			}
			else if ( req.ToContainer == ThornsContainerKind.WorldLoot )
			{
				if ( !ThornsContainerItemMoves.TrySplitHalf(
					    i => _inventory.GetSlot( req.FromContainer, NormalizeIndex( req.FromContainer, i ) ),
					    ( i, s ) => _inventory.SetSlot( req.FromContainer, NormalizeIndex( req.FromContainer, i ), s ),
					    req.FromIndex,
					    i => service.HostGetSlot( key, i ),
					    ( i, s ) => service.HostSetSlot( key, i, s ),
					    req.ToIndex ) )
					return;
			}
		}
		else
		{
			var fromIsWorld = req.FromContainer == ThornsContainerKind.WorldLoot;
			var fromIndex = fromIsWorld ? req.FromIndex : NormalizeIndex( req.FromContainer, req.FromIndex );
			var toIndex = req.ToContainer == ThornsContainerKind.WorldLoot ? req.ToIndex : NormalizeIndex( req.ToContainer, req.ToIndex );

			if ( fromIsWorld )
			{
				ThornsContainerItemMoves.TrySwapOrMerge(
					i => service.HostGetSlot( key, i ),
					( i, s ) => service.HostSetSlot( key, i, s ),
					fromIndex,
					i => _inventory.GetSlot( req.ToContainer, i ),
					( i, s ) => _inventory.SetSlot( req.ToContainer, i, s ),
					toIndex,
					ThornsContainerItemMoves.MapEquipSlot( req.ToContainer ) );
				HostNotifyContainerLootTaken( key );
			}
			else
			{
				ThornsContainerItemMoves.TrySwapOrMerge(
					i => _inventory.GetSlot( req.FromContainer, i ),
					( i, s ) => _inventory.SetSlot( req.FromContainer, i, s ),
					fromIndex,
					i => service.HostGetSlot( key, i ),
					( i, s ) => service.HostSetSlot( key, i, s ),
					toIndex );
			}
		}

		ThornsMilestoneTracker.OnInventoryChanged( this );
		PushInventoryToOwner();
		PushExternalContainerToOwner();
		HostPersistPlayerState();
	}

	void HostQuickTransferWorld(
		ThornsContainerKind from,
		int fromIndex,
		ThornsContainerKind toHint,
		string key,
		ThornsWorldLootContainerService service )
	{
		_ = toHint;
		if ( !service.TryGet( key, out var record ) )
			return;

		if ( from == ThornsContainerKind.WorldLoot )
		{
			var stack = service.HostGetSlot( key, fromIndex );
			if ( stack.IsEmpty )
				return;

			TryQuickDepositWorldToPlayer( key, fromIndex, service );
			return;
		}

		var idx = NormalizeIndex( from, fromIndex );
		var playerStack = _inventory.GetSlot( from, idx );
		if ( playerStack.IsEmpty )
			return;

		TryQuickDepositPlayerToWorld( key, from, idx, service );
	}

	void TryQuickDepositWorldToPlayer(
		string key,
		int worldIndex,
		ThornsWorldLootContainerService service )
	{
		ThornsInventoryQuickTransfer.TryQuickTransferToPlayerStorage(
			i => service.HostGetSlot( key, i ),
			( i, s ) => service.HostSetSlot( key, i, s ),
			worldIndex,
			( container, slotIndex ) => _inventory.GetSlot( container, slotIndex ),
			( container, slotIndex, stack ) => _inventory.SetSlot( container, slotIndex, stack ) );
	}

	void TryQuickDepositPlayerToWorld(
		string key,
		ThornsContainerKind fromKind,
		int fromIndex,
		ThornsWorldLootContainerService service )
	{
		if ( !service.TryGet( key, out var record ) )
			return;

		ThornsInventoryQuickTransfer.TryQuickTransferStack(
			i => _inventory.GetSlot( fromKind, i ),
			( i, s ) => _inventory.SetSlot( fromKind, i, s ),
			fromIndex,
			i => service.HostGetSlot( key, i ),
			( i, s ) => service.HostSetSlot( key, i, s ),
			record.SlotCount );
	}
}
