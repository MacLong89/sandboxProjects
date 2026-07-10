namespace Sandbox;

using System.Collections.Generic;

public sealed class ThornsInventoryReplicationService
{
	IThornsInventoryReplicationHost _host;

	ThornsInventorySlotNet[] _lastSentOwnerMirror;

	ThornsInventorySlotNet[] _clientMirror;

	int _clientMirrorVersion;

	public int ClientMirrorRevision => _clientMirrorVersion;

	public void Bind( IThornsInventoryReplicationHost host ) => _host = host;

	public void ResetLastSentOwnerMirror() => _lastSentOwnerMirror = null;

	public bool TryGetClientMirrorSlot( int index, out ThornsInventorySlotNet slot )
	{
		slot = default;
		if ( _clientMirror is null || index < 0 || index >= _host.TotalSlots )
			return false;

		slot = _clientMirror[index];
		return true;
	}

	public int ClientMirrorCountItemId( string itemId )
	{
		if ( _clientMirror is null || string.IsNullOrEmpty( itemId ) )
			return 0;

		var n = 0;
		for ( var i = 0; i < _host.TotalSlots; i++ )
		{
			var s = _clientMirror[i];
			if ( string.IsNullOrEmpty( s.ItemId ) || s.ItemId != itemId || s.Quantity <= 0 )
				continue;
			n += s.Quantity;
		}

		return n;
	}

	public void ApplyInventoryClientMirror( ThornsInventorySlotNet[] slots )
	{
		if ( slots is null || slots.Length != _host.TotalSlots )
			return;

		_clientMirror = slots;
		_clientMirrorVersion++;
		Log.Info(
			$"[Thorns] UI: inventory mirror updated (revision={_clientMirrorVersion}) len={slots.Length}" );

		_host.OnMirrorUpdated();
	}

	public void PushSnapshotToOwner()
	{
		if ( !Networking.IsHost )
			return;

		var payload = BuildOwnerMirrorPayload();
		TryPushOwnerMirror( payload );
	}

	ThornsInventorySlotNet[] BuildOwnerMirrorPayload()
	{
		var payload = new ThornsInventorySlotNet[_host.TotalSlots];
		for ( var i = 0; i < _host.TotalSlots; i++ )
			payload[i] = _host.BuildSlotNet( _host.HostGetSlotRef( i ) );
		return payload;
	}

	void TryPushOwnerMirror( ThornsInventorySlotNet[] payload )
	{
		if ( payload is null || payload.Length != _host.TotalSlots )
			payload = BuildOwnerMirrorPayload();
		if ( _lastSentOwnerMirror is null || _lastSentOwnerMirror.Length != _host.TotalSlots )
		{
			PushFullOwnerMirror( payload );
			return;
		}

		var changes = new List<ThornsInventorySlotChangeNet>( 8 );
		for ( var i = 0; i < _host.TotalSlots; i++ )
		{
			if ( !SlotNetEquals( _lastSentOwnerMirror[i], payload[i] ) )
				changes.Add( new ThornsInventorySlotChangeNet { SlotIndex = i, Slot = payload[i] } );
		}

		if ( changes.Count == 0 )
			return;

		if ( changes.Count > ThornsPerformanceBudgets.InventoryDeltaMaxSlotsBeforeFullSnapshot )
		{
			PushFullOwnerMirror( payload );
			return;
		}

		ThornsReplicationDiagnostics.WarnIfHeavyInventoryOwnerDelta(
			_host.GameObject.Name,
			_host.GameObject.Network.OwnerId,
			changes );

		if ( _host.HostSnapshotTargetsListenServerLocalOwner() )
			_host.ApplyClientMirrorDelta( changes );
		else
		{
			var delta = new ThornsInventorySlotChangeNet[changes.Count];
			for ( var i = 0; i < changes.Count; i++ )
				delta[i] = changes[i];
			_host.RpcClientReceiveDelta( delta );
		}

		for ( var c = 0; c < changes.Count; c++ )
		{
			var ch = changes[c];
			_lastSentOwnerMirror[ch.SlotIndex] = ch.Slot;
		}
	}

	void PushFullOwnerMirror( ThornsInventorySlotNet[] payload )
	{
		if ( payload is null || payload.Length != _host.TotalSlots )
			payload = BuildOwnerMirrorPayload();

		ThornsReplicationDiagnostics.WarnIfHeavyInventoryOwnerSnapshot(
			_host.GameObject.Name,
			_host.GameObject.Network.OwnerId,
			payload );

		if ( _host.HostSnapshotTargetsListenServerLocalOwner() )
			_host.ApplyClientMirror( payload );
		else
			_host.RpcClientReceiveSnapshot( payload );

		_lastSentOwnerMirror = new ThornsInventorySlotNet[_host.TotalSlots];
		for ( var i = 0; i < _host.TotalSlots; i++ )
			_lastSentOwnerMirror[i] = payload[i];
	}

	static bool SlotNetEquals( ThornsInventorySlotNet a, ThornsInventorySlotNet b ) =>
		a.Quantity == b.Quantity
		&& a.HasDurability == b.HasDurability
		&& MathF.Abs( a.Durability - b.Durability ) < 0.001f
		&& a.WeaponLoadedAmmo == b.WeaponLoadedAmmo
		&& string.Equals( a.ItemId ?? "", b.ItemId ?? "", StringComparison.Ordinal )
		&& string.Equals( a.WeaponInstanceId ?? "", b.WeaponInstanceId ?? "", StringComparison.Ordinal )
		&& string.Equals( a.WeaponRollPayload ?? "", b.WeaponRollPayload ?? "", StringComparison.Ordinal )
		&& string.Equals( a.ArmorRollPayload ?? "", b.ArmorRollPayload ?? "", StringComparison.Ordinal );

	public void ApplyInventoryClientMirrorDelta( IReadOnlyList<ThornsInventorySlotChangeNet> changes )
	{
		if ( changes is null || changes.Count == 0 )
			return;

		if ( _clientMirror is null || _clientMirror.Length != _host.TotalSlots )
		{
			_clientMirror = new ThornsInventorySlotNet[_host.TotalSlots];
			for ( var i = 0; i < _host.TotalSlots; i++ )
				_clientMirror[i] = default;
		}

		for ( var c = 0; c < changes.Count; c++ )
		{
			var ch = changes[c];
			if ( ch.SlotIndex < 0 || ch.SlotIndex >= _host.TotalSlots )
				continue;
			_clientMirror[ch.SlotIndex] = ch.Slot;
		}

		_clientMirrorVersion++;
		Log.Info(
			$"[Thorns] UI: inventory mirror delta updated (revision={_clientMirrorVersion}) changes={changes.Count}" );

		_host.OnMirrorUpdated();
	}
}
