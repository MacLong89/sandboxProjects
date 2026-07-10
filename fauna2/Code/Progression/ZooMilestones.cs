namespace Fauna2;

/// <summary>
/// Passive milestone rewards for guests, codex, habitats, and economy.
/// Runs continuously in the background — no player action required.
/// </summary>
public sealed class ZooMilestones : Component
{
	public static ZooMilestones Instance { get; private set; }

	[Sync( SyncFlags.FromHost )] public int GuestMilestoneFlags { get; set; }
	[Sync( SyncFlags.FromHost )] public int CodexTierFlags { get; set; }
	[Sync( SyncFlags.FromHost )] public int HabitatTierFlags { get; set; }
	[Sync( SyncFlags.FromHost )] public bool ProfitableNotified { get; set; }
	[Sync( SyncFlags.FromHost )] public bool EconomyTutorialShown { get; set; }

	private TimeUntil _nextCheck;

	protected override void OnAwake()
	{
		Instance = this;
		GameEvents.LevelUp += OnLevelUp;
	}

	protected override void OnDestroy()
	{
		GameEvents.LevelUp -= OnLevelUp;
		if ( Instance == this ) Instance = null;
	}

	protected override void OnFixedUpdate()
	{
		if ( IsProxy || !_nextCheck ) return;
		_nextCheck = 2f;

		TryEconomyTutorial();
		TryGuestMilestones();
		TryCodexTiers();
		TryHabitatTiers();
		TryProfitable();
	}

	private void OnLevelUp( int level )
	{
		if ( IsProxy ) return;

		var unlocks = ProgressionHelper.UpcomingUnlocks( 3 ).ToList();
		var teaser = unlocks.Count > 0 ? $" Next up: {unlocks[0].Name}" : "";
		UI.UiState.ShowCelebration( $"Level {level}!", $"New animals and buildables unlocked.{teaser}", "military_tech" );
	}

	private void TryEconomyTutorial()
	{
		if ( EconomyTutorialShown ) return;

		var state = ZooState.Instance;
		if ( !state.IsValid() ) return;

		if ( !PathNetwork.HasGuestAccess )
		{
			if ( (EconomySystem.Instance?.IncomePerMinute ?? 0f) >= 0f )
				return;

			EconomyTutorialShown = true;
			state.Notify(
				"Setup phase — operating costs are normal. Ticket revenue starts once paths connect to your entrance.",
				"info" );
			return;
		}

		if ( (EconomySystem.Instance?.IncomePerMinute ?? 0f) >= -50f ) return;
		if ( (state.TotalSpent) < 200 ) return;

		EconomyTutorialShown = true;
		state.Notify(
			"Guest revenue grows as you add animals, paths, and amenities — early zoos often break even before they profit.",
			"info" );
	}

	private void TryGuestMilestones()
	{
		var guests = GuestSystem.Instance?.GuestCount ?? 0;
		GuestMilestoneFlags = ApplyFlag( GuestMilestoneFlags, 1, guests >= 10, "10 guests arrived!", 200, 30 );
		GuestMilestoneFlags = ApplyFlag( GuestMilestoneFlags, 2, guests >= 25, "25 guests arrived!", 300, 40 );
		GuestMilestoneFlags = ApplyFlag( GuestMilestoneFlags, 4, guests >= 50, "50 guests — your zoo is buzzing!", 500, 60 );
		GuestMilestoneFlags = ApplyFlag( GuestMilestoneFlags, 8, guests >= 100, "100 guests — record attendance!", 1000, 120 );
		GuestMilestoneFlags = ApplyFlag( GuestMilestoneFlags, 16, guests >= 200, "200 guests — mega sanctuary!", 1800, 200 );
	}

	private void TryCodexTiers()
	{
		var pct = CollectionSystem.Instance?.CompletionPercent ?? 0f;
		CodexTierFlags = ApplyFlag( CodexTierFlags, 1, pct >= 10f, "Codex 10% — great start!", 250, 35 );
		CodexTierFlags = ApplyFlag( CodexTierFlags, 2, pct >= 25f, "Codex 25% — nice progress!", 400, 50 );
		CodexTierFlags = ApplyFlag( CodexTierFlags, 4, pct >= 50f, "Codex half full!", 800, 100 );
		CodexTierFlags = ApplyFlag( CodexTierFlags, 8, pct >= 75f, "Codex 75% — almost there!", 1200, 150 );
		CodexTierFlags = ApplyFlag( CodexTierFlags, 16, pct >= 99f, "Codex complete — legendary collection!", 2500, 300 );
	}

	private void TryHabitatTiers()
	{
		if ( HabitatRegistry.Count == 0 ) return;
		var max = HabitatRegistry.All.Max( h => h.Score );
		HabitatTierFlags = ApplyFlag( HabitatTierFlags, 1, max >= 55f, "Quality habitat (55+) — breeding unlocked!", 300, 45 );
		HabitatTierFlags = ApplyFlag( HabitatTierFlags, 2, max >= 70f, "Great habitat (70+) — guests love it!", 500, 70 );
		HabitatTierFlags = ApplyFlag( HabitatTierFlags, 4, max >= 90f, "Pristine habitat (90+) — world class!", 900, 110 );
	}

	private void TryProfitable()
	{
		if ( ProfitableNotified ) return;
		if ( (EconomySystem.Instance?.IncomePerMinute ?? 0f) <= 0f ) return;

		ProfitableNotified = true;
		var state = ZooState.Instance;
		state.AddMoney( 750 );
		state.AddXp( 80 );
		state.Notify( "Profitable zoo! Net cash flow is positive (+$750 bonus)", "trending_up" );
		UI.UiState.ShowCelebration( "Profitable!", "Your zoo earns more than it spends.", "paid" );
	}

	private int ApplyFlag( int flags, int bit, bool condition, string message, int money, int xp )
	{
		if ( !condition || (flags & bit) != 0 ) return flags;

		flags |= bit;
		var state = ZooState.Instance;
		if ( !state.IsValid() ) return flags;

		state.AddMoney( money );
		state.AddXp( xp );
		state.Notify( message, "celebration" );
		return flags;
	}

	public void Restore( int guest, int codex, int habitat, bool profitable, bool economyTutorial )
	{
		if ( !Networking.IsHost ) return;
		GuestMilestoneFlags = guest;
		CodexTierFlags = codex;
		HabitatTierFlags = habitat;
		ProfitableNotified = profitable;
		EconomyTutorialShown = economyTutorial;
	}
}
