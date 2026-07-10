namespace Fauna2;

/// <summary>
/// The shared, host-authoritative zoo state: treasury, XP, level and prestige.
/// Lives on the networked ZooCore object so every player (including late
/// joiners) sees the same numbers. All mutation happens on the host.
/// </summary>
public sealed class ZooState : Component
{
	public static ZooState Instance { get; private set; }

	[Sync( SyncFlags.FromHost )] public string ZooName { get; set; } = "New Sanctuary";
	[Sync( SyncFlags.FromHost )] public int Money { get; set; }
	[Sync( SyncFlags.FromHost )] public int Xp { get; set; }
	[Sync( SyncFlags.FromHost )] public int Level { get; set; } = 1;
	[Sync( SyncFlags.FromHost )] public int Prestige { get; set; }
	[Sync( SyncFlags.FromHost )] public long TotalEarned { get; set; }
	[Sync( SyncFlags.FromHost )] public long TotalSpent { get; set; }
	[Sync( SyncFlags.FromHost )] public bool TutorialAnimalClaimed { get; set; }
	[Sync( SyncFlags.FromHost )] public int TotalAnimalsBought { get; set; }
	[Sync( SyncFlags.FromHost )] public int TotalAnimalsBred { get; set; }
	[Sync( SyncFlags.FromHost )] public int TotalAnimalsCaught { get; set; }

	[Sync( SyncFlags.FromHost )] public string StarterProfileId { get; set; } = "";
	[Sync( SyncFlags.FromHost )] public Biome StarterBiome { get; set; } = Biome.Grassland;
	[Sync( SyncFlags.FromHost )] public float GuestAppealModifier { get; set; }
	[Sync( SyncFlags.FromHost )] public float NativeBiomeHappinessBonus { get; set; }
	[Sync( SyncFlags.FromHost )] public float NativeGuestAppealBonus { get; set; }
	[Sync( SyncFlags.FromHost )] public int GuestCapBonus { get; set; }

	protected override void OnAwake()
	{
		Instance = this;
	}

	protected override void OnDestroy()
	{
		if ( Instance == this ) Instance = null;
	}

	/// <summary>Host only. Set a brand new zoo up (default grassland starter).</summary>
	public void SetNewGameDefaults() =>
		ApplyStarterProfile( ZooStarterProfiles.All[0] );

	/// <summary>Host only. Configure a fresh zoo from a main-menu starter pack.</summary>
	public void ApplyStarterProfile( ZooStarterProfile profile )
	{
		if ( !Networking.IsHost || profile is null ) return;

		ZooName = $"{Connection.Local?.DisplayName ?? "My"}'s Zoo";
		Money = profile.NetStartingMoney;
		Xp = 0;
		Level = 1;
		Prestige = 0;
		TotalEarned = 0;
		TotalSpent = profile.SetupCost;
		TutorialAnimalClaimed = false;
		TotalAnimalsBought = 0;
		TotalAnimalsBred = 0;
		TotalAnimalsCaught = 0;

		StarterProfileId = profile.Id;
		StarterBiome = profile.Biome;
		GuestAppealModifier = profile.GuestAppealBonus;
		NativeBiomeHappinessBonus = profile.NativeAnimalHappinessBonus;
		NativeGuestAppealBonus = profile.NativeGuestAppealBonus;
		GuestCapBonus = profile.GuestCapBonus;
	}

	/// <summary>Host only. Deducts recurring operating costs (feed, staff, upkeep).</summary>
	public void ApplyOperatingExpense( int amount )
	{
		if ( !Networking.IsHost || amount <= 0 ) return;

		Money = Math.Max( 0, Money - amount );
		TotalSpent += amount;
	}

	// ── Money ───────────────────────────────────────────────

	public bool CanAfford( int amount ) => Money >= amount;

	/// <summary>Host only. Adds money. Earned income also feeds XP trickle.</summary>
	public void AddMoney( int amount, bool isIncome = false )
	{
		if ( !Networking.IsHost || amount == 0 ) return;

		Money = Math.Max( 0, Money + amount );

		if ( amount != 0 && (!isIncome || amount >= 50) )
			UI.UiState.PushFloat( amount > 0 ? $"+${amount:n0}" : $"−${Math.Abs( amount ):n0}", "money" );

		if ( isIncome && amount > 0 )
		{
			TotalEarned += amount;
			AddXp( (int)MathF.Floor( amount * GameConstants.AtGamePace( GameConstants.XpPerDollarEarned ) ) );
		}

		if ( amount > 0 && !isIncome )
			GameEvents.RaiseEconomyGain( amount );
	}

	/// <summary>Host only. Returns false (and charges nothing) if we can't afford it.</summary>
	public bool TrySpend( int amount )
	{
		if ( !Networking.IsHost ) return false;
		if ( amount < 0 ) return false;
		if ( Money < amount ) return false;

		Money -= amount;
		TotalSpent += amount;
		SaveSystem.Instance?.RequestSave();
		return true;
	}

	// ── XP / Levels ─────────────────────────────────────────

	/// <summary>XP required to advance *from* the given level to the next.</summary>
	public static int XpForLevel( int level )
	{
		return (int)(100 * MathF.Pow( level, 1.35f ));
	}

	/// <summary>Host only.</summary>
	public void AddXp( int amount )
	{
		if ( !Networking.IsHost || amount <= 0 ) return;

		Xp += amount;
		UI.UiState.PulseXp();
		UI.UiState.PushFloat( $"+{amount} XP", "xp" );

		while ( Level < GameConstants.MaxLevel && Xp >= XpForLevel( Level ) )
		{
			Xp -= XpForLevel( Level );
			Level++;
			AddPrestige( GameConstants.PrestigeLevelUp );
			GameEvents.RaiseLevelUp( Level );
			Notify( $"Zoo reached Level {Level}!", "military_tech" );
		}
	}

	/// <summary>Host only.</summary>
	public void AddPrestige( int amount )
	{
		if ( !Networking.IsHost || amount <= 0 ) return;
		Prestige += amount;
	}

	/// <summary>Host only. Rename the zoo.</summary>
	[Rpc.Host]
	public void RequestRename( string name )
	{
		if ( !RpcAuthorization.IsOwnerCaller() ) return;
		if ( string.IsNullOrWhiteSpace( name ) ) return;
		ZooName = name.Trim().Truncate( 32 );
	}

	// ── Notifications ───────────────────────────────────────

	/// <summary>Host only. Shows a toast on every player's screen.</summary>
	public void Notify( string message, string icon = "info" )
	{
		if ( !Networking.IsHost ) return;
		BroadcastNotify( message, icon );
	}

	[Rpc.Broadcast]
	private void BroadcastNotify( string message, string icon )
	{
		UI.UiState.PushToast( message, icon );
	}
}
