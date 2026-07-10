namespace Sandbox;

/// <summary>
/// Per-connection session: lives on the <b>same networked GameObject</b> as <see cref="ThornsPawn"/> and <see cref="ThornsPawnMovement"/> so root transform replicates.
/// </summary>
[Title( "Thorns — Player (session)" )]
[Category( "Thorns" )]
[Icon( "person" )]
public sealed class ThornsPlayer : Component, Component.INetworkSpawn
{
	/// <summary>Connection this session belongs to (assigned on network spawn).</summary>
	public Connection OwnerConnection { get; private set; }

	/// <summary>Set once on <see cref="OnNetworkSpawn"/> — used for disk saves when <see cref="Connection.All"/> is empty at shutdown.</summary>
	public string HostPersistenceAccountKey { get; private set; } = "";

	/// <summary>Resolved child pawn; null until after spawn wiring.</summary>
	public ThornsPawn ControlledPawn { get; private set; }

	protected override void OnAwake()
	{
		TryBindPawn();
	}

	void TryBindPawn()
	{
		if ( ControlledPawn.IsValid() )
			return;

		ControlledPawn = GameObject.Components.GetInDescendantsOrSelf<ThornsPawn>();
	}

	public void OnNetworkSpawn( Connection owner )
	{
		OwnerConnection = owner;
		TryBindPawn();

		HostPersistenceAccountKey = owner is not null ? ThornsPersistenceIdentity.GetStableAccountKey( owner ) : "";

		Log.Info( $"[Thorns] Ownership assigned on ThornsPlayer: owner='{owner?.DisplayName}' id={owner?.Id}, pawn={(ControlledPawn.IsValid() ? ControlledPawn.GameObject.Name : "MISSING")}" );

		if ( Networking.IsHost
		     && !string.IsNullOrEmpty( HostPersistenceAccountKey )
		     && ThornsPlayerBedSpawn.HostTryGetBedMinimapWorldXy( HostPersistenceAccountKey, out var bedXy ) )
		{
			var mm = GameObject.Components.Get<ThornsMinimapHud>( FindMode.EnabledInSelf );
			if ( mm.IsValid() )
				mm.HostSetOwnedBedMinimapBlip( bedXy );
		}
	}
}
