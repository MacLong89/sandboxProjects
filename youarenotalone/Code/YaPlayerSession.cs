namespace Sandbox;

[Title( "YouAreNotAlone — Player session" )]
[Category( "YouAreNotAlone" )]
[Icon( "person" )]
public sealed class YaPlayerSession : Component, Component.INetworkSpawn
{
	public Connection OwnerConnection { get; private set; }
	public YaPawn ControlledPawn { get; private set; }

	protected override void OnAwake() => TryBindPawn();

	void TryBindPawn()
	{
		if ( ControlledPawn.IsValid() )
			return;
		ControlledPawn = GameObject.Components.GetInDescendantsOrSelf<YaPawn>();
	}

	public void OnNetworkSpawn( Connection owner )
	{
		OwnerConnection = owner;
		TryBindPawn();
		Log.Info( $"[YA] Player session owner='{owner?.DisplayName}' id={owner?.Id}" );
	}
}
