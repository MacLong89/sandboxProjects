namespace Fauna2;

/// <summary>Player-carried animals and catch tools (host sync for owner).</summary>
public sealed class PlayerInventory : Component
{
	public static PlayerInventory Local { get; private set; }

	public const int MaxCarry = 2;

	// AUDIT FIX: inventory is mutated on the host (catch / shop RPCs). FromHost
	// prevents a connection-owner client from forging bait/carry via bare Sync.
	// Revert hint: if catch rewards stop appearing on the owner HUD, confirm the
	// host writes these fields (ResolveCatchHost / shop hosts) and that the
	// inventory component lives on the zoo-owner player object.
	[Sync( SyncFlags.FromHost )] public int CarriedCount { get; set; }
	[Sync( SyncFlags.FromHost )] public string CarriedSpecies0 { get; set; } = "";
	[Sync( SyncFlags.FromHost )] public string CarriedSpecies1 { get; set; } = "";
	[Sync( SyncFlags.FromHost )] public bool HasNet { get; set; } = true;
	[Sync( SyncFlags.FromHost )] public int BaitCount { get; set; } = 3;
	[Sync( SyncFlags.FromHost )] public int TranquilizerCount { get; set; }

	protected override void OnStart()
	{
		if ( !IsProxy )
			Local = this;
	}

	protected override void OnDestroy()
	{
		if ( Local == this ) Local = null;
	}

	public bool CanCarryMore => CarriedCount < MaxCarry;

	/// <summary>First queued species for interact UI (slot 0, then slot 1).</summary>
	public string FirstCarriedSpecies()
	{
		NormalizeCarried();
		if ( CarriedCount <= 0 ) return null;
		return !string.IsNullOrEmpty( CarriedSpecies0 ) ? CarriedSpecies0 : CarriedSpecies1;
	}

	/// <summary>Keep count in sync with non-empty species slots after load or desync.</summary>
	public void NormalizeCarried()
	{
		var species = new List<string>( MaxCarry );
		if ( !string.IsNullOrEmpty( CarriedSpecies0 ) ) species.Add( CarriedSpecies0 );
		if ( !string.IsNullOrEmpty( CarriedSpecies1 ) ) species.Add( CarriedSpecies1 );

		CarriedCount = Math.Min( species.Count, MaxCarry );
		CarriedSpecies0 = CarriedCount > 0 ? species[0] : "";
		CarriedSpecies1 = CarriedCount > 1 ? species[1] : "";
	}

	public bool TryAddCatch( string speciesId )
	{
		if ( !CanCarryMore || string.IsNullOrEmpty( speciesId ) ) return false;

		if ( CarriedCount == 0 )
			CarriedSpecies0 = speciesId;
		else
			CarriedSpecies1 = speciesId;

		CarriedCount++;

		// AUDIT: catch path previously skipped NormalizeCarried (place/load call it).
		// Keeps count ↔ species strings consistent if Sync arrived partially ordered.
		NormalizeCarried();
		return true;
	}

	public string GetCarriedAt( int slot )
	{
		NormalizeCarried();
		return slot switch
		{
			0 => CarriedSpecies0,
			1 => CarriedSpecies1,
			_ => null,
		};
	}

	public bool TryTakeCarriedAt( int slot, out string speciesId )
	{
		speciesId = GetCarriedAt( slot );
		if ( string.IsNullOrEmpty( speciesId ) )
			return false;

		if ( slot == 0 )
		{
			CarriedSpecies0 = CarriedSpecies1;
			CarriedSpecies1 = "";
		}
		else if ( slot == 1 )
		{
			CarriedSpecies1 = "";
		}
		else
		{
			speciesId = null;
			return false;
		}

		CarriedCount = (string.IsNullOrEmpty( CarriedSpecies0 ) ? 0 : 1)
			+ (string.IsNullOrEmpty( CarriedSpecies1 ) ? 0 : 1);
		return true;
	}

	public string TakeNextCarried()
	{
		NormalizeCarried();
		if ( !TryTakeCarriedAt( 0, out var species ) && !TryTakeCarriedAt( 1, out species ) )
			return null;

		return species;
	}

	public bool ConsumeBait()
	{
		if ( BaitCount <= 0 ) return false;
		BaitCount--;
		return true;
	}

	public bool ConsumeTranquilizer()
	{
		if ( TranquilizerCount <= 0 ) return false;
		TranquilizerCount--;
		return true;
	}

	public void ResetToStarterKit()
	{
		CarriedCount = 0;
		CarriedSpecies0 = "";
		CarriedSpecies1 = "";
		HasNet = true;
		BaitCount = 3;
		TranquilizerCount = 0;
	}

	public void TryBuyNet()
	{
		if ( Networking.IsHost )
			BuyNetHost();
		else
			RequestBuyNet();
	}

	public void TryBuyBaitPack()
	{
		if ( Networking.IsHost )
			BuyBaitPackHost();
		else
			RequestBuyBaitPack();
	}

	public void TryBuyTranquilizer()
	{
		if ( Networking.IsHost )
			BuyTranquilizerHost();
		else
			RequestBuyTranquilizer();
	}

	[Rpc.Host]
	private void RequestBuyNet()
	{
		if ( !RpcAuthorization.IsOwnerCaller() ) return;
		BuyNetHost();
	}

	[Rpc.Host]
	private void RequestBuyBaitPack()
	{
		if ( !RpcAuthorization.IsOwnerCaller() ) return;
		BuyBaitPackHost();
	}

	[Rpc.Host]
	private void RequestBuyTranquilizer()
	{
		if ( !RpcAuthorization.IsOwnerCaller() ) return;
		BuyTranquilizerHost();
	}

	private void BuyNetHost()
	{
		if ( !IsZooOwnerInventory() ) return;

		if ( HasNet )
		{
			ZooState.Instance?.Notify( "You already have a net.", "block" );
			return;
		}

		var cost = GameConstants.NetCost;
		if ( !ZooState.Instance?.TrySpend( cost ) ?? true )
		{
			ZooState.Instance?.Notify( $"Need ${cost:n0} for a net.", "block" );
			return;
		}

		HasNet = true;
		ZooState.Instance?.Notify( "Net purchased — ready to catch!", "pets" );
		SaveSystem.Instance?.RequestSave();
	}

	private void BuyBaitPackHost()
	{
		if ( !IsZooOwnerInventory() ) return;

		var cost = GameConstants.BaitCost * GameConstants.BaitPackSize;
		if ( !ZooState.Instance?.TrySpend( cost ) ?? true )
		{
			ZooState.Instance?.Notify( $"Need ${cost:n0} for bait.", "block" );
			return;
		}

		BaitCount += GameConstants.BaitPackSize;
		ZooState.Instance?.Notify( $"Bought {GameConstants.BaitPackSize} bait.", "restaurant" );
		SaveSystem.Instance?.RequestSave();
	}

	private void BuyTranquilizerHost()
	{
		if ( !IsZooOwnerInventory() ) return;

		var cost = GameConstants.TranquilizerCost;
		if ( !ZooState.Instance?.TrySpend( cost ) ?? true )
		{
			ZooState.Instance?.Notify( $"Need ${cost:n0} for a tranquilizer.", "block" );
			return;
		}

		TranquilizerCount++;
		ZooState.Instance?.Notify( "Tranquilizer stocked.", "medication" );
		SaveSystem.Instance?.RequestSave();
	}

	private bool IsZooOwnerInventory() =>
		GameObject.Components.Get<PlayerState>()?.IsZooOwner == true;
}
