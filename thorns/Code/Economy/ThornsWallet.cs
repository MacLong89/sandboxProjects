namespace Sandbox;

/// <summary>Server-authoritative gold (legacy) + metal radio currency. Clients never mutate directly.</summary>
[Title( "Thorns — Wallet" )]
[Category( "Thorns" )]
[Icon( "payments" )]
[Order( 44 )]
public sealed class ThornsWallet : Component, Component.INetworkSpawn
{
	[Sync( SyncFlags.FromHost )] public int Gold { get; set; }

	/// <summary>Radio shop + future crafting currency (synced).</summary>
	[Sync( SyncFlags.FromHost )] public int Metal { get; set; }

	[Property] public int StartingGoldOnSpawn { get; set; } = 0;

	public void OnNetworkSpawn( Connection owner )
	{
		if ( !Networking.IsHost )
			return;

		if ( ThornsWorldPersistence.Instance is { } wp && wp.HostSpawnRestoreSkipsWalletStartingGold( owner ) )
			return;

		Gold = Math.Max( 0, StartingGoldOnSpawn );
		Metal = 0;
	}

	/// <summary>Host-only: spend gold if balance sufficient; never goes negative.</summary>
	public bool HostTrySpendGold( int amount )
	{
		if ( !Networking.IsHost || amount <= 0 )
			return false;

		if ( Gold < amount )
			return false;

		Gold -= amount;
		return true;
	}

	/// <summary>Host-only: grant gold (sales / refunds).</summary>
	public void HostAddGold( int amount )
	{
		if ( !Networking.IsHost || amount <= 0 )
			return;

		var sum = (long)Gold + amount;
		Gold = sum > int.MaxValue ? int.MaxValue : (int)sum;
	}

	/// <summary>Host-only: clamp after unusual paths.</summary>
	public void HostClampGoldNonNegative()
	{
		if ( !Networking.IsHost )
			return;

		if ( Gold < 0 )
		{
			Log.Warning( $"[Thorns] Wallet clamped negative gold → 0 pawn='{GameObject.Name}'" );
			Gold = 0;
		}
	}

	public bool HostTrySpendMetal( int amount )
	{
		if ( !Networking.IsHost || amount <= 0 )
			return false;

		if ( Metal < amount )
			return false;

		Metal -= amount;
		return true;
	}

	public void HostAddMetal( int amount )
	{
		if ( !Networking.IsHost || amount <= 0 )
			return;

		var sum = (long)Metal + amount;
		Metal = sum > int.MaxValue ? int.MaxValue : (int)sum;
	}

	public void HostClampMetalNonNegative()
	{
		if ( !Networking.IsHost )
			return;

		if ( Metal < 0 )
		{
			Log.Warning( $"[Thorns] Wallet clamped negative metal → 0 pawn='{GameObject.Name}'" );
			Metal = 0;
		}
	}
}
