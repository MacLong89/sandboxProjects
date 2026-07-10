namespace Terraingen.Player;

using Sandbox.Network;
using Terraingen.Buildings;
using Terraingen.GameData;
using Terraingen.Multiplayer;
using Terraingen.Progression;
using Terraingen.UI;
using Terraingen.UI.Core;

/// <summary>Campfire smelting — ore to ingots via dedicated UI (not craft recipes).</summary>
public sealed partial class ThornsPlayerGameplay
{
	ThornsCampfireSnapshotDto _campfire = new();
	TimeSince _campfirePushDebounce;

	public bool HasOpenCampfire => _campfire.IsOpen;

	public void RequestOpenCampfire( string instanceKey )
	{
		if ( !IsLocalPlayer() || string.IsNullOrWhiteSpace( instanceKey ) )
			return;

		if ( Networking.IsActive && !Networking.IsHost )
			RpcOpenCampfire( instanceKey );
		else
			HostOpenCampfire( instanceKey );
	}

	public void RequestCloseCampfire()
	{
		if ( !IsLocalPlayer() )
			return;

		if ( Networking.IsActive && !Networking.IsHost )
			RpcCloseCampfire();
		else
			HostCloseCampfire();
	}

	public void RequestStartCampfireSmelt( int batchCount )
	{
		if ( !IsLocalPlayer() || batchCount <= 0 )
			return;

		if ( Networking.IsActive && !Networking.IsHost )
			RpcStartCampfireSmelt( batchCount );
		else
			HostStartCampfireSmelt( batchCount );
	}

	[Rpc.Host]
	void RpcOpenCampfire( string instanceKey )
	{
		if ( !ValidateCaller() )
			return;

		HostOpenCampfire( instanceKey );
	}

	[Rpc.Host]
	void RpcCloseCampfire()
	{
		if ( !ValidateCaller() )
			return;

		HostCloseCampfire();
	}

	[Rpc.Host]
	void RpcStartCampfireSmelt( int batchCount )
	{
		if ( !ValidateCaller() )
			return;

		HostStartCampfireSmelt( batchCount );
	}

	public void HostOpenCampfire( string instanceKey )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || string.IsNullOrWhiteSpace( instanceKey ) )
		{
			PushCampfireToOwner( closed: true );
			return;
		}

		if ( !ValidateCampfireStructure( instanceKey ) )
		{
			PushCampfireToOwner( closed: true );
			return;
		}

		if ( HasOpenWorldContainer )
			HostCloseWorldContainer();
		if ( HasOpenRadioShop )
			HostCloseRadioShop();
		if ( HasOpenResearch )
			HostCloseResearchStation();
		if ( HasOpenWorkbench )
			HostCloseWorkbench();

		_campfire.IsOpen = true;
		_campfire.InstanceKey = instanceKey;
		_nearestStation = ThornsCraftStationKind.Campfire;
		PushCampfireToOwner();
		PushInventoryToOwner();
	}

	public void HostCloseCampfire()
	{
		if ( !_campfire.IsOpen )
			return;

		_campfire.IsOpen = false;
		_campfire.InstanceKey = "";
		if ( _campfire.SmeltBatchesRemaining <= 0 )
			HostReturnCampfireStationToInventory();

		PushCampfireToOwner( closed: true );
	}

	public void HostTickCampfireSmelt( float delta )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || delta <= 0f || _campfire.SmeltBatchesRemaining <= 0 )
			return;

		if ( _campfire.IsOpen && !ValidateCampfireStructure( _campfire.InstanceKey ) )
		{
			HostCloseCampfire();
			return;
		}

		_campfire.SmeltSecondsRemaining = Math.Max( 0f, _campfire.SmeltSecondsRemaining - delta );
		if ( _campfire.SmeltSecondsRemaining > 0f )
		{
			if ( _campfire.IsOpen && _campfirePushDebounce > 0.12f )
			{
				_campfirePushDebounce = 0;
				PushCampfireToOwner();
			}

			return;
		}

		HostCompleteCampfireSmeltBatch();
	}

	void HostStartCampfireSmelt( int batchCount )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || HostIsDead() || batchCount <= 0 )
			return;

		if ( !_campfire.IsOpen || string.IsNullOrWhiteSpace( _campfire.InstanceKey ) )
			return;

		if ( !ValidateCampfireStructure( _campfire.InstanceKey ) )
		{
			HostCloseCampfire();
			return;
		}

		var woodAvailable = _campfireStation[ThornsCampfireStationSlots.Wood].Count;
		var oreAvailable = _campfireStation[ThornsCampfireStationSlots.Ore].Count;
		var batches = ThornsCampfireSmelt.ClampBatchCount( batchCount, oreAvailable, woodAvailable );
		if ( batches <= 0
		     || oreAvailable < ThornsCampfireSmelt.OrePerIngot
		     || woodAvailable < ThornsCampfireSmelt.WoodPerBatch )
			return;

		var oreCost = batches * ThornsCampfireSmelt.OrePerIngot;
		var woodCost = batches * ThornsCampfireSmelt.WoodPerBatch;
		HostRemoveFromStationSlot( _campfireStation, ThornsCampfireStationSlots.Ore, oreCost );
		HostRemoveFromStationSlot( _campfireStation, ThornsCampfireStationSlots.Wood, woodCost );

		if ( _campfire.SmeltBatchesRemaining <= 0 )
			_campfire.SmeltSecondsRemaining = ThornsCampfireSmelt.SecondsPerBatch;

		_campfire.SmeltBatchesRemaining += batches;
		MarkInventorySyncDirty();
		PushInventoryToOwner();
		PushCampfireToOwner();
		HostPersistPlayerState();
	}

	void HostRemoveFromStationSlot( ThornsItemStack[] station, int index, int count )
	{
		if ( index < 0 || index >= station.Length || count <= 0 )
			return;

		ref var stack = ref station[index];
		if ( stack.IsEmpty )
			return;

		stack.Count = Math.Max( 0, stack.Count - count );
		if ( stack.Count <= 0 )
			stack = ThornsItemStack.EmptyStack;
	}

	void HostCompleteCampfireSmeltBatch()
	{
		if ( _campfire.SmeltBatchesRemaining <= 0 )
			return;

		HostDepositStackToStationSlot(
			_campfireStation,
			ThornsCampfireStationSlots.Output,
			new ThornsItemStack { ItemId = ThornsCampfireSmelt.IngotItemId, Count = ThornsCampfireSmelt.IngotPerBatch } );

		ThornsMilestoneTracker.OnCrafted( this, ThornsCampfireSmelt.IngotItemId, ThornsCampfireSmelt.IngotPerBatch );
		ThornsMilestoneTracker.OnInventoryChanged( this );

		_campfire.SmeltBatchesRemaining--;
		if ( _campfire.SmeltBatchesRemaining > 0 )
			_campfire.SmeltSecondsRemaining = ThornsCampfireSmelt.SecondsPerBatch;
		else
			_campfire.SmeltSecondsRemaining = 0f;

		PushInventoryToOwner();
		PushCampfireToOwner();
		HostPersistPlayerState();
	}

	public ThornsCampfireSnapshotDto HostBuildCampfireSnapshot()
		=> BuildCampfireSnapshot( _campfire.IsOpen, _campfire.InstanceKey );

	ThornsCampfireSnapshotDto BuildCampfireSnapshot( bool isOpen, string instanceKey ) =>
		new()
		{
			IsOpen = isOpen,
			InstanceKey = instanceKey ?? "",
			SmeltBatchesRemaining = Math.Max( 0, _campfire.SmeltBatchesRemaining ),
			SmeltSecondsRemaining = Math.Max( 0f, _campfire.SmeltSecondsRemaining ),
			SmeltSecondsPerBatch = ThornsCampfireSmelt.SecondsPerBatch,
			OrePerIngot = ThornsCampfireSmelt.OrePerIngot,
			WoodPerBatch = ThornsCampfireSmelt.WoodPerBatch,
			IngotPerBatch = ThornsCampfireSmelt.IngotPerBatch,
			StationSlots = ThornsStationSlotRules.BuildSlotDtos( ThornsContainerKind.CampfireStation, _campfireStation )
		};

	bool ValidateCampfireStructure( string instanceKey )
	{
		if ( string.IsNullOrWhiteSpace( instanceKey ) )
			return false;

		if ( !ThornsPlacedBuildStructure.TryFindByInstanceKey( instanceKey, out var placed ) || !placed.IsValid() )
			return false;

		if ( !string.Equals( placed.StructureId, "campfire", StringComparison.OrdinalIgnoreCase ) )
			return false;

		return Vector3.DistanceBetween( GameObject.WorldPosition, placed.GameObject.WorldPosition )
		       <= ThornsPlacedStructureInteraction.UseRange + 60f;
	}

	void PushCampfireToOwner( bool closed = false )
	{
		if ( closed )
		{
			_campfire.IsOpen = false;
			_campfire.InstanceKey = "";
		}

		var snap = closed
			? BuildCampfireSnapshot( false, "" )
			: HostBuildCampfireSnapshot();

		if ( IsLocalPlayer() )
			ThornsUiClientState.ApplyPartialCampfire( snap );
		else if ( Networking.IsActive )
			RpcSyncCampfireJson( Json.Serialize( snap ) );
	}

	[Rpc.Owner]
	void RpcSyncCampfireJson( string json )
	{
		if ( !ThornsNetAuthority.TryDeserializeJson( json, ThornsNetAuthority.DefaultOwnerJsonMaxBytes, out ThornsCampfireSnapshotDto snap ) )
			return;

		ThornsUiClientState.ApplyPartialCampfire( snap );
	}
}
