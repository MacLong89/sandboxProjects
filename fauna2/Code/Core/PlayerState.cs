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

	// AUDIT FIX B1 (2026-07): Was bare [Sync] written every frame by the object
	// owner via TryBindLocal (IsZooOwner = SaveHost.CanStartSession).
	// Cheating clients could set IsZooOwner=true on their own PlayerState and
	// pass RpcAuthorization.IsOwnerCaller() for every owner Host RPC
	// (build/catch/spend/clear/collect).
	//
	// Now: FromHost only. GameManager stamps this when NetworkSpawn'ing the
	// player (lobby host = zoo owner). TryBindLocal must NEVER write this.
	// Revert hint: if host can't build after joining their own lobby, check
	// GameManager.OnActive still calls StampZooOwnership after NetworkSpawn.
	[Sync( SyncFlags.FromHost )] public bool IsZooOwner { get; set; }

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

	/// <summary>
	/// Host-only stamp right after NetworkSpawn. Listen-server model: lobby host
	/// is the zoo operator / save owner. Visitors never receive IsZooOwner=true.
	/// </summary>
	public void StampZooOwnership( bool isZooOwner )
	{
		if ( !Networking.IsHost ) return;
		IsZooOwner = isZooOwner;
	}

	private void TryBindLocal()
	{
		if ( IsProxy ) return;

		PlayerName = Connection.Local?.DisplayName ?? PlayerName;
		SteamId = Connection.Local?.SteamId.Value ?? SteamId;

		// AUDIT FIX B1: deliberately do NOT set IsZooOwner here anymore.
		// Old line was: IsZooOwner = SaveHost.CanStartSession;

		if ( Local != this )
			Local = this;
	}

	protected override void OnDestroy()
	{
		if ( Local == this ) Local = null;
	}
}
