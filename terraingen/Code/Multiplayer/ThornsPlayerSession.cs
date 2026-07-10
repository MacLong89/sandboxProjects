namespace Terraingen.Multiplayer;

using Sandbox.Network;

/// <summary>Lightweight per-connection marker used by host persistence.</summary>
public sealed class ThornsPlayerSession : Component, Component.INetworkSpawn
{
	public Connection OwnerConnection { get; private set; }
	public string HostPersistenceAccountKey { get; private set; } = "";

	public void OnNetworkSpawn( Connection owner )
	{
		OwnerConnection = owner ?? Connection.Find( GameObject.Network.OwnerId );
		HostEnsurePersistenceKey( OwnerConnection );
	}

	/// <summary>Offline / terrain-explorer spawns without <see cref="OnNetworkSpawn"/>.</summary>
	public void HostEnsurePersistenceKey( Connection connection = null )
	{
		if ( !GameObject.IsValid() )
			return;

		if ( !string.IsNullOrEmpty( HostPersistenceAccountKey ) )
			return;

		connection ??= OwnerConnection ?? Connection.Local;
		if ( connection is not null )
			OwnerConnection = connection;

		var key = ThornsPersistenceIdentity.GetStableAccountKey( connection );
		if ( string.IsNullOrEmpty( key ) )
			key = $"local:{GameObject.Id:N}";

		HostPersistenceAccountKey = key;
	}
}
