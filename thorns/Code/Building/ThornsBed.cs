namespace Sandbox;

/// <summary>Player-placed <c>bed</c> — registers owner respawn on the host when spawned.</summary>
[Title( "Thorns — Bed" )]
[Category( "Thorns/Building" )]
[Icon( "hotel" )]
[Order( 42 )]
public sealed class ThornsBed : Component
{
	ThornsPlacedStructure _structure;

	protected override void OnStart()
	{
		_structure = Components.Get<ThornsPlacedStructure>();
		if ( !Networking.IsHost || !_structure.IsValid() )
			return;

		ThornsPlayerBedSpawn.HostOnBedPlaced( _structure );
	}

	protected override void OnDestroy()
	{
		if ( !Networking.IsHost || !_structure.IsValid() )
			return;

		ThornsPlayerBedSpawn.HostOnBedRemoved( _structure.InstanceId );
	}
}
