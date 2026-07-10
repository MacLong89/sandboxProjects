namespace Fauna2;

/// <summary>
/// Per-player networked avatar. Host spawns one per connection; local player
/// walks with WASD and is shown as a pixel zookeeper.
/// </summary>
public sealed class PlayerState : Component
{
	public static PlayerState Local { get; private set; }

	[Sync] public string PlayerName { get; set; } = "Visitor";
	[Sync] public long SteamId { get; set; }
	[Sync] public bool IsZooOwner { get; set; }

	public Vector3 FeetPosition =>
		Components.Get<ZooPlayerController>()?.FeetPosition ?? GameObject.WorldPosition;

	protected override void OnStart()
	{
		TryBindLocal();

		GameObject.GetOrAddComponent<ZooPlayerController>();
		GameObject.GetOrAddComponent<ZooPlayerVisual>();
		if ( IsZooOwner )
			GameObject.GetOrAddComponent<PlayerInventory>();
	}

	protected override void OnUpdate()
	{
		TryBindLocal();
	}

	private void TryBindLocal()
	{
		if ( IsProxy ) return;

		PlayerName = Connection.Local?.DisplayName ?? PlayerName;
		SteamId = Connection.Local?.SteamId.Value ?? SteamId;
		IsZooOwner = SaveHost.CanStartSession;

		if ( Local != this )
			Local = this;
	}

	protected override void OnDestroy()
	{
		if ( Local == this ) Local = null;
	}
}
