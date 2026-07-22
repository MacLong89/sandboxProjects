namespace Fauna2;

/// <summary>
/// Host-side cash flow. Guests generate revenue; staff, feed and maintenance
/// drain it. Net $/min starts negative and climbs toward profit as the zoo grows.
/// </summary>
public sealed class EconomySystem : Component
{
	public static EconomySystem Instance { get; private set; }

	/// <summary>Net cash flow ($/min), synced for HUD and stats.</summary>
	[Sync( SyncFlags.FromHost )] public float IncomePerMinute { get; set; }

	[Sync( SyncFlags.FromHost )] public float RevenuePerMinute { get; set; }
	[Sync( SyncFlags.FromHost )] public float ExpensePerMinute { get; set; }

	private float _fractionalRevenue;
	private float _fractionalExpense;
	private TimeUntil _nextTick;

	protected override void OnAwake() => Instance = this;
	protected override void OnDestroy() { if ( Instance == this ) Instance = null; }

	protected override void OnFixedUpdate()
	{
		if ( IsProxy || !_nextTick ) return;
		_nextTick = 1f;

		var clock = DebugStats.StartTimer();
		Tick( 1f );
		DebugStats.StopTimer( "Economy", clock );
	}

	private void Tick( float deltaSeconds )
	{
		var guests = GuestSystem.Instance;
		var state = ZooState.Instance;
		if ( !guests.IsValid() || !state.IsValid() ) return;

		var revenue = GuestRevenue.PerSecond( guests.GuestCount, guests.Satisfaction )
			* (SanctuaryEventSystem.Instance?.IncomeMultiplier ?? 1f)
			* deltaSeconds;
		// Starter layout opens guest access immediately. Defer ops until the sanctuary
		// has a resident or paying guests so the first minutes are not a false drain.
		var operationsOpen = PathNetwork.HasGuestAccess
			&& (AnimalRegistry.Count > 0 || guests.GuestCount > 0);
		var expense = operationsOpen
			? (ZooOperatingCosts.PerSecond() + (StaffSystem.Instance?.PayrollPerMinute ?? 0) / 60f) * deltaSeconds
			: 0f;

		ApplyRevenue( state, revenue );
		ApplyExpense( state, expense );

		var instantRevenue = revenue / deltaSeconds * 60f;
		var instantExpense = expense / deltaSeconds * 60f;
		var instantNet = instantRevenue - instantExpense;

		RevenuePerMinute = RevenuePerMinute.LerpTo( instantRevenue, 0.12f );
		ExpensePerMinute = ExpensePerMinute.LerpTo( instantExpense, 0.12f );
		IncomePerMinute = IncomePerMinute.LerpTo( instantNet, 0.12f );
	}

	private void ApplyRevenue( ZooState state, float amount )
	{
		if ( amount <= 0f ) return;

		_fractionalRevenue += amount;
		if ( _fractionalRevenue < 1f ) return;

		var whole = (int)_fractionalRevenue;
		_fractionalRevenue -= whole;
		state.AddMoney( whole, isIncome: true );
	}

	private void ApplyExpense( ZooState state, float amount )
	{
		if ( amount <= 0f ) return;

		_fractionalExpense += amount;
		if ( _fractionalExpense < 1f ) return;

		var whole = (int)_fractionalExpense;
		_fractionalExpense -= whole;
		state.ApplyOperatingExpense( whole );
	}
}
