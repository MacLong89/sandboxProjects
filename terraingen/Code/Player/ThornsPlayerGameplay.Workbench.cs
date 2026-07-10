namespace Terraingen.Player;

using Sandbox.Network;
using Terraingen.Buildings;
using Terraingen.GameData;
using Terraingen.Multiplayer;
using Terraingen.Progression;
using Terraingen.UI;

/// <summary>Workbench overlay — timed repair and tier upgrades at a placed or world workbench.</summary>
public sealed partial class ThornsPlayerGameplay
{
	ThornsWorkbenchSnapshotDto _workbench = new();
	TimeSince _workbenchPushDebounce;

	public bool HasOpenWorkbench => _workbench.IsOpen;

	public void RequestOpenWorkbench( string instanceKey, bool isWorldFurniture = false )
	{
		if ( !IsLocalPlayer() )
			return;

		if ( !isWorldFurniture && string.IsNullOrWhiteSpace( instanceKey ) )
			return;

		if ( Networking.IsActive && !Networking.IsHost )
			RpcOpenWorkbench( instanceKey, isWorldFurniture );
		else
			HostOpenWorkbench( instanceKey, isWorldFurniture );
	}

	public void RequestCloseWorkbench()
	{
		if ( !IsLocalPlayer() )
			return;

		if ( Networking.IsActive && !Networking.IsHost )
			RpcCloseWorkbench();
		else
			HostCloseWorkbench();
	}

	public void RequestSelectWorkbenchItem( ThornsContainerKind container, int index )
	{
		if ( !IsLocalPlayer() || index < 0 )
			return;

		if ( Networking.IsActive && !Networking.IsHost )
			RpcSelectWorkbenchItem( container, index );
		else
			HostSelectWorkbenchItem( container, index );
	}

	public void RequestStartWorkbenchRepair()
	{
		if ( !IsLocalPlayer() )
			return;

		if ( Networking.IsActive && !Networking.IsHost )
			RpcStartWorkbenchRepair();
		else
			HostStartWorkbenchRepair();
	}

	[Rpc.Host]
	void RpcOpenWorkbench( string instanceKey, bool isWorldFurniture )
	{
		if ( !ValidateCaller() )
			return;

		HostOpenWorkbench( instanceKey, isWorldFurniture );
	}

	[Rpc.Host]
	void RpcCloseWorkbench()
	{
		if ( !ValidateCaller() )
			return;

		HostCloseWorkbench();
	}

	[Rpc.Host]
	void RpcSelectWorkbenchItem( ThornsContainerKind container, int index )
	{
		if ( !ValidateCaller() )
			return;

		HostSelectWorkbenchItem( container, index );
	}

	[Rpc.Host]
	void RpcStartWorkbenchRepair()
	{
		if ( !ValidateCaller() )
			return;

		HostStartWorkbenchRepair();
	}

	public void HostOpenWorkbench( string instanceKey, bool isWorldFurniture = false )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline )
		{
			PushWorkbenchToOwner( closed: true );
			return;
		}

		if ( isWorldFurniture )
		{
			if ( !ValidateWorldWorkbenchInFront() )
			{
				PushWorkbenchToOwner( closed: true );
				return;
			}
		}
		else if ( !ValidateWorkbenchStructure( instanceKey ) )
		{
			PushWorkbenchToOwner( closed: true );
			return;
		}

		if ( HasOpenWorldContainer )
			HostCloseWorldContainer();
		if ( HasOpenRadioShop )
			HostCloseRadioShop();
		if ( HasOpenResearch )
			HostCloseResearchStation();
		if ( HasOpenCampfire )
			HostCloseCampfire();

		_workbench.IsOpen = true;
		_workbench.IsWorldFurniture = isWorldFurniture;
		_workbench.InstanceKey = isWorldFurniture ? "" : instanceKey;
		_nearestStation = ThornsCraftStationKind.Workbench;
		HostEnsureWorkbenchSelection();
		PushWorkbenchToOwner();
		PushInventoryToOwner();
	}

	public void HostCloseWorkbench()
	{
		if ( !_workbench.IsOpen )
			return;

		_workbench.IsOpen = false;
		if ( !_workbench.RepairInProgress )
			HostReturnWorkbenchStationToInventory();

		PushWorkbenchToOwner( closed: true );
	}

	public void HostTickWorkbenchRepair( float delta )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || delta <= 0f || !_workbench.RepairInProgress )
			return;

		if ( _workbench.IsOpen && !HostValidateOpenWorkbench() )
		{
			HostCloseWorkbench();
			return;
		}

		_workbench.RepairSecondsRemaining = Math.Max( 0f, _workbench.RepairSecondsRemaining - delta );
		if ( _workbench.RepairSecondsRemaining > 0f )
		{
			if ( _workbench.IsOpen && _workbenchPushDebounce > 0.12f )
			{
				_workbenchPushDebounce = 0;
				PushWorkbenchToOwner();
			}

			return;
		}

		HostCompleteWorkbenchRepair();
	}

	public void HostSelectWorkbenchItem( ThornsContainerKind container, int index )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || _workbench.RepairInProgress )
			return;

		if ( !HostValidateOpenWorkbench() )
			return;

		if ( container is ThornsContainerKind.WorldLoot )
			return;

		if ( !HostTryResolveWorkbenchSlot( container, index, out var stack, out var def ) )
			return;

		if ( !ThornsItemTier.IsWorkbenchServiceable( stack, def ) )
			return;

		_workbench.SelectedContainer = container;
		_workbench.SelectedIndex = index;
		_workbench.SelectedItemId = stack.ItemId;
		PushWorkbenchToOwner();
	}

	void HostEnsureWorkbenchSelection()
	{
		if ( _workbench.RepairInProgress )
			return;

		if ( _workbench.SelectedIndex >= 0
		     && HostTryResolveWorkbenchSlot( _workbench.SelectedContainer, _workbench.SelectedIndex, out var current, out var currentDef )
		     && ThornsItemTier.IsWorkbenchServiceable( current, currentDef ) )
		{
			_workbench.SelectedItemId = current.ItemId;
			return;
		}

		foreach ( var entry in HostEnumerateWorkbenchCandidates() )
		{
			_workbench.SelectedContainer = entry.Container;
			_workbench.SelectedIndex = entry.Index;
			_workbench.SelectedItemId = entry.ItemId;
			return;
		}

		_workbench.SelectedContainer = default;
		_workbench.SelectedIndex = -1;
		_workbench.SelectedItemId = "";
	}

	void HostStartWorkbenchRepair()
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || HostIsDead() || _workbench.RepairInProgress )
			return;

		if ( !HostValidateOpenWorkbench() )
			return;

		var itemStack = _workbenchStation[ThornsWorkbenchStationSlots.Item];
		if ( itemStack.IsEmpty || !ThornsItemRegistry.TryGet( itemStack.ItemId, out var def ) )
			return;

		if ( !ThornsItemTier.CanRepair( itemStack, def ) )
			return;

		var costs = ThornsItemTier.GetRepairCost( itemStack, def );
		if ( !ThornsStationSlotRules.HasMaterialsInSlots(
			    _workbenchStation,
			    ThornsWorkbenchStationSlots.FirstPart,
			    ThornsWorkbenchStationSlots.PartCount,
			    costs ) )
			return;

		ThornsStationSlotRules.ConsumeMaterialsFromSlots(
			_workbenchStation,
			ThornsWorkbenchStationSlots.FirstPart,
			ThornsWorkbenchStationSlots.PartCount,
			costs );

		_workbench.RepairInProgress = true;
		_workbench.RepairSecondsRemaining = ThornsWorkbenchRepair.SecondsPerJob;
		_workbench.SelectedItemId = itemStack.ItemId;

		MarkInventorySyncDirty();
		PushInventoryToOwner();
		PushWorkbenchToOwner();
		HostPersistPlayerState();
	}

	void HostCompleteWorkbenchRepair()
	{
		if ( !_workbench.RepairInProgress )
			return;

		MarkInventorySyncDirty();
		var stack = _workbenchStation[ThornsWorkbenchStationSlots.Item];
		if ( !stack.IsEmpty
		     && ThornsItemRegistry.TryGet( stack.ItemId, out var def )
		     && ThornsItemTier.CanRepair( stack, def ) )
		{
			ThornsItemTier.ApplyRepair( ref stack, def );
			_workbenchStation[ThornsWorkbenchStationSlots.Item] = ThornsItemStack.EmptyStack;
			HostDepositStackToStationSlot( _workbenchStation, ThornsWorkbenchStationSlots.Output, stack );
			ThornsMilestoneTracker.OnInventoryChanged( this );
		}

		_workbench.RepairInProgress = false;
		_workbench.RepairSecondsRemaining = 0f;
		_workbench.SelectedItemId = _workbenchStation[ThornsWorkbenchStationSlots.Item].IsEmpty
			? _workbenchStation[ThornsWorkbenchStationSlots.Output].ItemId ?? ""
			: _workbenchStation[ThornsWorkbenchStationSlots.Item].ItemId;

		PushInventoryToOwner();
		PushWorkbenchToOwner();
		HostPersistPlayerState();
	}

	public ThornsWorkbenchSnapshotDto HostBuildWorkbenchSnapshot() => new()
	{
		IsOpen = _workbench.IsOpen,
		InstanceKey = _workbench.InstanceKey ?? "",
		IsWorldFurniture = _workbench.IsWorldFurniture,
		SelectedContainer = _workbench.SelectedContainer,
		SelectedIndex = _workbench.SelectedIndex,
		SelectedItemId = _workbenchStation[ThornsWorkbenchStationSlots.Item].IsEmpty
			? _workbenchStation[ThornsWorkbenchStationSlots.Output].ItemId ?? ""
			: _workbenchStation[ThornsWorkbenchStationSlots.Item].ItemId,
		RepairInProgress = _workbench.RepairInProgress,
		RepairSecondsRemaining = Math.Max( 0f, _workbench.RepairSecondsRemaining ),
		RepairSecondsPerJob = ThornsWorkbenchRepair.SecondsPerJob,
		StationSlots = ThornsStationSlotRules.BuildSlotDtos( ThornsContainerKind.WorkbenchStation, _workbenchStation )
	};

	bool ValidateWorkbenchStructure( string instanceKey )
	{
		if ( string.IsNullOrWhiteSpace( instanceKey ) )
			return false;

		if ( !ThornsPlacedBuildStructure.TryFindByInstanceKey( instanceKey, out var placed ) || !placed.IsValid() )
			return false;

		if ( !string.Equals( placed.StructureId, "workbench", StringComparison.OrdinalIgnoreCase ) )
			return false;

		return Vector3.DistanceBetween( GameObject.WorldPosition, placed.GameObject.WorldPosition )
		       <= ThornsPlacedStructureInteraction.UseRange + 60f;
	}

	bool ValidateWorldWorkbenchInFront()
	{
		if ( !ThornsPlacedStructureInteraction.TryPickCraftStationInFront( GameObject, out var placed, out var station )
		     || station != ThornsCraftStationKind.Workbench )
			return false;

		if ( placed.IsValid() )
			return ValidateWorkbenchStructure( placed.InstanceKey );

		return false;
	}

	bool HostTryResolveWorkbenchSlot(
		ThornsContainerKind container,
		int index,
		out ThornsItemStack stack,
		out ThornsItemDefinition def )
	{
		stack = default;
		def = null;

		if ( container is ThornsContainerKind.WorldLoot )
			return false;

		var idx = NormalizeIndex( container, index );
		stack = _inventory.GetSlot( container, idx );
		if ( stack.IsEmpty || !ThornsItemRegistry.TryGet( stack.ItemId, out def ) )
			return false;

		return true;
	}

	IEnumerable<(ThornsContainerKind Container, int Index, string ItemId)> HostEnumerateWorkbenchCandidates()
	{
		foreach ( var kind in new[]
		         {
			         ThornsContainerKind.Hotbar,
			         ThornsContainerKind.Inventory,
			         ThornsContainerKind.Head,
			         ThornsContainerKind.Chest,
			         ThornsContainerKind.Legs
		         } )
		{
			var max = kind switch
			{
				ThornsContainerKind.Inventory => ThornsInventoryContainer.InventorySlotCount,
				ThornsContainerKind.Hotbar => ThornsInventoryContainer.HotbarSlotCount,
				_ => 1
			};

			for ( var i = 0; i < max; i++ )
			{
				if ( !HostTryResolveWorkbenchSlot( kind, i, out var stack, out var def ) )
					continue;

				if ( !ThornsItemTier.IsWorkbenchServiceable( stack, def ) )
					continue;

				yield return ( kind, i, stack.ItemId );
			}
		}
	}

	void PushWorkbenchToOwner( bool closed = false )
	{
		if ( closed )
			_workbench.IsOpen = false;

		var snap = HostBuildWorkbenchSnapshot();
		if ( closed )
		{
			snap.IsOpen = false;
			snap.InstanceKey = "";
			snap.IsWorldFurniture = false;
		}

		if ( IsLocalPlayer() )
			ThornsUiClientState.ApplyPartialWorkbench( snap );
		else if ( Networking.IsActive )
			RpcSyncWorkbenchJson( Json.Serialize( snap ) );
	}

	[Rpc.Owner]
	void RpcSyncWorkbenchJson( string json )
	{
		if ( !ThornsNetAuthority.TryDeserializeJson( json, ThornsNetAuthority.DefaultOwnerJsonMaxBytes, out ThornsWorkbenchSnapshotDto snap ) )
			return;

		ThornsUiClientState.ApplyPartialWorkbench( snap );
	}

	bool HostValidateOpenWorkbench()
	{
		if ( !_workbench.IsOpen && !_workbench.RepairInProgress )
			return false;

		if ( _workbench.IsWorldFurniture )
			return ValidateWorldWorkbenchInFront();

		if ( ValidateWorkbenchStructure( _workbench.InstanceKey ) )
			return true;

		if ( _workbench.RepairInProgress )
		{
			_workbench.RepairInProgress = false;
			_workbench.RepairSecondsRemaining = 0f;
			PushWorkbenchToOwner();
		}

		HostCloseWorkbench();
		return false;
	}
}
