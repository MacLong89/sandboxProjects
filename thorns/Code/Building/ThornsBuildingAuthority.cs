namespace Sandbox;

/// <summary>Host-only Base Core tracking — one core per connection; position cached for future raid anchoring (THORNS_EVERYTHING_DOCUMENT §3, §12).</summary>
public static class ThornsBuildingAuthority
{
	static readonly Dictionary<Guid, Vector3> _baseCoreWorldPositionByOwner = new();

	public static bool HostHasBaseCore( Guid ownerConnectionId ) =>
		Networking.IsHost && _baseCoreWorldPositionByOwner.ContainsKey( ownerConnectionId );

	/// <summary>Call only after a Base Core entity was successfully spawned (keeps registry aligned with world).</summary>
	public static void HostRegisterPlacedBaseCore( Guid ownerConnectionId, Vector3 worldPosition )
	{
		if ( !Networking.IsHost )
			return;

		_baseCoreWorldPositionByOwner[ownerConnectionId] = worldPosition;
		Log.Info( $"[Thorns] Base Core registered for raids (owner={ownerConnectionId} pos={worldPosition})" );
	}

	public static bool HostTryGetBaseCoreWorldPosition( Guid ownerConnectionId, out Vector3 position ) =>
		_baseCoreWorldPositionByOwner.TryGetValue( ownerConnectionId, out position );

	/// <summary>When a Base Core piece is removed from the world (demolish / future decay).</summary>
	public static void HostUnregisterPlacedBaseCore( Guid ownerConnectionId )
	{
		if ( !Networking.IsHost )
			return;

		if ( _baseCoreWorldPositionByOwner.Remove( ownerConnectionId ) )
			Log.Info( $"[Thorns] Base Core unregistered (owner={ownerConnectionId})" );
	}
}
