namespace Sandbox;

using System.Collections.Generic;

public interface IThornsInventoryReplicationHost
{
	Guid OwnerConnectionId { get; }
	int TotalSlots { get; }
	bool HostSnapshotTargetsListenServerLocalOwner();
	void ApplyClientMirror( ThornsInventorySlotNet[] slots );
	void ApplyClientMirrorDelta( IReadOnlyList<ThornsInventorySlotChangeNet> changes );
	void RpcClientReceiveSnapshot( ThornsInventorySlotNet[] slots );
	void RpcClientReceiveDelta( ThornsInventorySlotChangeNet[] delta );
	void OnMirrorUpdated();
	ThornsInventorySlotNet BuildSlotNet( ThornsInventorySlot slot );
	ref ThornsInventorySlot HostGetSlotRef( int index );
	GameObject GameObject { get; }
}
