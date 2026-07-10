namespace Sandbox;

public sealed class ThornsInventoryStorageService
{
	IThornsInventoryStorageHost _host;

	public void Bind( IThornsInventoryStorageHost host ) => _host = host;

	public void RequestOpenStorageChest( string structureInstanceIdD )
	{
		if ( !Networking.IsHost )
			return;

		if ( !_host.ValidateRpcCallerOwnsPawn() )
			return;

		if ( _host.IsPlayerDead() )
			return;

		if ( string.IsNullOrWhiteSpace( structureInstanceIdD )
		     || !Guid.TryParse( structureInstanceIdD, out var sid ) )
			return;

		if ( ThornsStorageChest.TryGetForStructure( sid, out var chest ) && chest.IsValid() )
		{
			if ( !ThornsPlacedStructure.ActiveByInstanceId.TryGetValue( sid, out var ps )
			     || !ps.IsValid()
			     || ps.StructureDefId != "storage_chest" )
				return;

			if ( !ThornsStorageChest.HostValidatePlayerUseAllowed( _host.GameObject, chest, ThornsStorageChest.InteractionRange ) )
				return;

			_host.HostNotifyOpenBuildSfx( ps.GameObject.WorldPosition );
			chest.HostPushSnapshotToOwner( _host.Inventory );
			return;
		}

		if ( !ThornsFurnitureContainer.TryGet( sid, out var furniture ) || !furniture.IsValid() )
			return;

		if ( furniture.IsProcLootSync )
		{
			if ( !ThornsFurnitureContainer.HostValidatePlayerUseAllowed(
				     _host.GameObject, furniture, ThornsFurnitureContainer.InteractionRange ) )
				return;

			_host.HostNotifyOpenBuildSfx( furniture.GameObject.WorldPosition );
			furniture.HostPushSnapshotToOwner( _host.Inventory );
			return;
		}

		if ( !ThornsPlacedStructure.ActiveByInstanceId.TryGetValue( sid, out var placed )
		     || !placed.IsValid() )
			return;

		if ( !ThornsFurnitureContainer.HostValidatePlayerUseAllowed(
			     _host.GameObject, furniture, ThornsFurnitureContainer.InteractionRange ) )
			return;

		_host.HostNotifyOpenBuildSfx( placed.GameObject.WorldPosition );
		furniture.HostPushSnapshotToOwner( _host.Inventory );
	}

	public void RequestStorageChestTransfer(
		string structureInstanceIdD,
		bool fromChest,
		int fromIdx,
		bool toChest,
		int toIdx )
	{
		if ( !Networking.IsHost )
			return;

		if ( !_host.ValidateRpcCallerOwnsPawn() )
			return;

		if ( _host.IsPlayerDead() )
			return;

		if ( string.IsNullOrWhiteSpace( structureInstanceIdD )
		     || !Guid.TryParse( structureInstanceIdD, out var sid ) )
			return;

		if ( ThornsStorageChest.TryGetForStructure( sid, out var chest ) && chest.IsValid() )
		{
			if ( !ThornsStorageChest.HostValidatePlayerUseAllowed( _host.GameObject, chest, ThornsStorageChest.InteractionRange ) )
				return;

			if ( !chest.HostApplyTransfer( fromChest, fromIdx, toChest, toIdx, _host.Inventory ) )
				return;

			chest.HostPushSnapshotToOwner( _host.Inventory );
			return;
		}

		if ( !ThornsFurnitureContainer.TryGet( sid, out var furniture ) || !furniture.IsValid() )
			return;

		if ( !ThornsFurnitureContainer.HostValidatePlayerUseAllowed(
			     _host.GameObject, furniture, ThornsFurnitureContainer.InteractionRange ) )
			return;

		if ( !furniture.HostApplyTransfer( fromChest, fromIdx, toChest, toIdx, _host.Inventory ) )
			return;

		furniture.HostPushSnapshotToOwner( _host.Inventory );
	}

	public void RequestStorageChestQuickTransfer( string structureInstanceIdD, bool fromChest, int fromIdx )
	{
		if ( !Networking.IsHost )
			return;

		if ( !_host.ValidateRpcCallerOwnsPawn() )
			return;

		if ( _host.IsPlayerDead() )
			return;

		if ( string.IsNullOrWhiteSpace( structureInstanceIdD )
		     || !Guid.TryParse( structureInstanceIdD, out var sid ) )
			return;

		if ( ThornsStorageChest.TryGetForStructure( sid, out var chest ) && chest.IsValid() )
		{
			if ( !ThornsStorageChest.HostValidatePlayerUseAllowed( _host.GameObject, chest, ThornsStorageChest.InteractionRange ) )
				return;

			if ( !chest.HostTryQuickTransfer( fromChest, fromIdx, _host.Inventory ) )
				return;

			chest.HostPushSnapshotToOwner( _host.Inventory );
			return;
		}

		if ( !ThornsFurnitureContainer.TryGet( sid, out var furniture ) || !furniture.IsValid() )
			return;

		if ( !ThornsFurnitureContainer.HostValidatePlayerUseAllowed(
			     _host.GameObject, furniture, ThornsFurnitureContainer.InteractionRange ) )
			return;

		if ( !furniture.HostTryQuickTransfer( fromChest, fromIdx, _host.Inventory ) )
			return;

		furniture.HostPushSnapshotToOwner( _host.Inventory );
	}

	public void RequestOpenCampfire( string structureInstanceIdD )
	{
		if ( !Networking.IsHost )
			return;

		if ( !_host.ValidateRpcCallerOwnsPawn() )
			return;

		if ( _host.IsPlayerDead() )
			return;

		if ( string.IsNullOrWhiteSpace( structureInstanceIdD )
		     || !Guid.TryParse( structureInstanceIdD, out var sid ) )
			return;

		if ( !ThornsPlacedStructure.ActiveByInstanceId.TryGetValue( sid, out var ps )
		     || !ps.IsValid()
		     || !string.Equals( ps.StructureDefId, "campfire", StringComparison.OrdinalIgnoreCase ) )
			return;

		if ( !ThornsCampfire.TryGetForStructure( sid, out var fire ) || !fire.IsValid() )
			return;

		if ( !ThornsCampfire.HostValidatePlayerUseAllowed( _host.GameObject, fire, ThornsCampfire.InteractionRange ) )
			return;

		_host.HostNotifyOpenBuildSfx( ps.GameObject.WorldPosition );
		fire.HostPushSnapshotToOwner( _host.Inventory, presentOverlay: true );
	}

	public void RequestNotifyCampfireUiClosed( string structureInstanceIdD )
	{
		if ( !Networking.IsHost )
			return;

		if ( !_host.ValidateRpcCallerOwnsPawn() )
			return;

		if ( string.IsNullOrWhiteSpace( structureInstanceIdD )
		     || !Guid.TryParse( structureInstanceIdD, out var sid ) )
			return;

		if ( !ThornsCampfire.TryGetForStructure( sid, out var fire ) || !fire.IsValid() )
			return;

		fire.HostClearLastInteractInventoryIf( _host.Inventory );
	}

	public void RequestCampfireTransfer(
		string structureInstanceIdD,
		bool fromCampfire,
		int fromIdx,
		bool toCampfire,
		int toIdx )
	{
		if ( !Networking.IsHost )
			return;

		if ( !_host.ValidateRpcCallerOwnsPawn() )
			return;

		if ( _host.IsPlayerDead() )
			return;

		if ( string.IsNullOrWhiteSpace( structureInstanceIdD )
		     || !Guid.TryParse( structureInstanceIdD, out var sid ) )
			return;

		if ( !ThornsCampfire.TryGetForStructure( sid, out var fire ) || !fire.IsValid() )
			return;

		if ( !ThornsCampfire.HostValidatePlayerUseAllowed( _host.GameObject, fire, ThornsCampfire.InteractionRange ) )
			return;

		if ( !fire.HostApplyTransfer( fromCampfire, fromIdx, toCampfire, toIdx, _host.Inventory ) )
			return;

		fire.HostPushSnapshotToOwner( _host.Inventory, presentOverlay: false );
	}

	public void RequestCampfireQuickTransfer( string structureInstanceIdD, bool fromCampfire, int fromIdx )
	{
		if ( !Networking.IsHost )
			return;

		if ( !_host.ValidateRpcCallerOwnsPawn() )
			return;

		if ( _host.IsPlayerDead() )
			return;

		if ( string.IsNullOrWhiteSpace( structureInstanceIdD )
		     || !Guid.TryParse( structureInstanceIdD, out var sid ) )
			return;

		if ( !ThornsCampfire.TryGetForStructure( sid, out var fire ) || !fire.IsValid() )
			return;

		if ( !ThornsCampfire.HostValidatePlayerUseAllowed( _host.GameObject, fire, ThornsCampfire.InteractionRange ) )
			return;

		if ( !fire.HostTryQuickTransfer( fromCampfire, fromIdx, _host.Inventory ) )
			return;

		fire.HostPushSnapshotToOwner( _host.Inventory, presentOverlay: false );
	}

	public void RequestOpenWorkbench( string structureInstanceIdD )
	{
		if ( !Networking.IsHost )
			return;

		if ( !_host.ValidateRpcCallerOwnsPawn() )
			return;

		if ( _host.IsPlayerDead() )
			return;

		if ( string.IsNullOrWhiteSpace( structureInstanceIdD )
		     || !Guid.TryParse( structureInstanceIdD, out var sid ) )
			return;

		if ( !ThornsPlacedStructure.ActiveByInstanceId.TryGetValue( sid, out var ps )
		     || !ps.IsValid()
		     || !string.Equals( ps.StructureDefId, "workbench", StringComparison.OrdinalIgnoreCase ) )
			return;

		if ( !ThornsWorkbench.TryGetForStructure( sid, out var wb ) || !wb.IsValid() )
			return;

		if ( !ThornsWorkbench.HostValidatePlayerUseAllowed( _host.GameObject, wb, ThornsWorkbench.InteractionRange ) )
			return;

		_host.HostNotifyOpenBuildSfx( ps.GameObject.WorldPosition );
		wb.HostPushSnapshotToOwner( _host.Inventory );
	}

	public void RequestWorkbenchTransfer(
		string structureInstanceIdD,
		bool fromBench,
		int fromIdx,
		bool toBench,
		int toIdx )
	{
		if ( !Networking.IsHost )
			return;

		if ( !_host.ValidateRpcCallerOwnsPawn() )
			return;

		if ( _host.IsPlayerDead() )
			return;

		if ( string.IsNullOrWhiteSpace( structureInstanceIdD )
		     || !Guid.TryParse( structureInstanceIdD, out var sid ) )
			return;

		if ( !ThornsWorkbench.TryGetForStructure( sid, out var wb ) || !wb.IsValid() )
			return;

		if ( !ThornsWorkbench.HostValidatePlayerUseAllowed( _host.GameObject, wb, ThornsWorkbench.InteractionRange ) )
			return;

		if ( !wb.HostApplyTransfer( fromBench, fromIdx, toBench, toIdx, _host.Inventory ) )
			return;

		wb.HostPushSnapshotToOwner( _host.Inventory );
	}

	public void RequestWorkbenchQuickTransfer( string structureInstanceIdD, bool fromBench, int fromIdx )
	{
		if ( !Networking.IsHost )
			return;

		if ( !_host.ValidateRpcCallerOwnsPawn() )
			return;

		if ( _host.IsPlayerDead() )
			return;

		if ( string.IsNullOrWhiteSpace( structureInstanceIdD )
		     || !Guid.TryParse( structureInstanceIdD, out var sid ) )
			return;

		if ( !ThornsWorkbench.TryGetForStructure( sid, out var wb ) || !wb.IsValid() )
			return;

		if ( !ThornsWorkbench.HostValidatePlayerUseAllowed( _host.GameObject, wb, ThornsWorkbench.InteractionRange ) )
			return;

		if ( !wb.HostTryQuickTransfer( fromBench, fromIdx, _host.Inventory ) )
			return;

		wb.HostPushSnapshotToOwner( _host.Inventory );
	}
}
