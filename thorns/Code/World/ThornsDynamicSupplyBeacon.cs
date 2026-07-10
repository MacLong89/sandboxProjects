namespace Sandbox;

/// <summary>
/// Host-only lifecycle for a contested supply marker: minimap POI + paired <see cref="ThornsLootCrate"/>.
/// When the crate is emptied (destroyed), this root is removed and POI JSON is rebuilt (THORNS dynamic POI).
/// </summary>
[Title( "Thorns — Dynamic supply beacon" )]
[Category( "Thorns/World" )]
[Icon( "local_shipping" )]
public sealed class ThornsDynamicSupplyBeacon : Component
{
	ThornsLootCrate _crate;

	public void HostBindCrate( ThornsLootCrate crate ) => _crate = crate;

	protected override void OnStart()
	{
		if ( Networking.IsHost || !Networking.IsActive )
			ThornsDynamicSupplyBeaconPopulation.HostRegister( this );
	}

	protected override void OnDestroy() =>
		ThornsDynamicSupplyBeaconPopulation.HostUnregister( this );

	/// <summary>Host: 3D airdrop arrival sting for all clients in range.</summary>
	public void HostPlaySpawnSting()
	{
		if ( !Networking.IsHost || !GameObject.IsValid() )
			return;

		var worldEmit = GameObject.WorldPosition;
		if ( Networking.IsActive )
			RpcBroadcastAirdropSpawnSting( worldEmit );
		else
			ThornsGameplaySfx.PlayAirdropSpawnAt( worldEmit );
	}

	[Rpc.Broadcast]
	void RpcBroadcastAirdropSpawnSting( Vector3 worldEmit ) =>
		ThornsGameplaySfx.PlayAirdropSpawnAt( worldEmit );

	protected override void OnFixedUpdate()
	{
		if ( Networking.IsActive && !Networking.IsHost )
			return;

		if ( _crate is { IsValid: true } )
			return;

		HostCleanupAndDestroy();
	}

	void HostCleanupAndDestroy()
	{
		if ( !GameObject.IsValid() )
			return;

		GameObject.Destroy();
		ThornsPoiAuthority.Instance?.HostRebuildFromSceneMarkers();
	}
}
