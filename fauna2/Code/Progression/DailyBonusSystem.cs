namespace Fauna2;

/// <summary>Daily login bonus with streak escalation — rewards coming back often.</summary>
public sealed class DailyBonusSystem : Component
{
	public static DailyBonusSystem Instance { get; private set; }

	[Sync( SyncFlags.FromHost )] public long LastBonusUnixDay { get; set; }
	[Sync( SyncFlags.FromHost )] public int LoginStreak { get; set; }

	private bool _checked;
	private bool _bonusPresentationDeferred;
	private int _deferredStreak;
	private int _deferredBonus;

	protected override void OnAwake() => Instance = this;

	protected override void OnDestroy()
	{
		if ( Instance == this ) Instance = null;
	}

	protected override void OnFixedUpdate()
	{
		if ( IsProxy || _checked ) return;
		if ( GameManager.Instance is null || !GameManager.Instance.GameStarted ) return;

		_checked = true;
		TryGrantDailyBonus();
	}

	public void TryGrantDailyBonus()
	{
		if ( !Networking.IsHost ) return;

		var today = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 86400;
		if ( LastBonusUnixDay >= today ) return;

		var streakBroken = LastBonusUnixDay > 0 && today - LastBonusUnixDay > 1;
		LoginStreak = streakBroken ? 1 : Math.Max( 1, LoginStreak + 1 );
		LastBonusUnixDay = today;

		var state = ZooState.Instance;
		if ( state is null ) return;

		var bonus = 400 + state.Level * 75 + state.Prestige * 10 + LoginStreak * 100;
		state.AddMoney( bonus );

		if ( UI.UiState.StartupToastsSuppressed )
		{
			_bonusPresentationDeferred = true;
			_deferredStreak = LoginStreak;
			_deferredBonus = bonus;
			return;
		}

		PresentBonus( LoginStreak, bonus );
	}

	public void PresentDeferredBonus()
	{
		if ( !_bonusPresentationDeferred )
			return;

		_bonusPresentationDeferred = false;
		PresentBonus( _deferredStreak, _deferredBonus );
	}

	private static void PresentBonus( int streak, int bonus )
	{
		UI.UiState.ShowCelebration( $"Day {streak} bonus!", $"+${bonus:n0} for returning to your zoo.", "redeem" );
	}

	public static string NextBonusCountdown()
	{
		var today = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 86400;
		var nextDayStart = (today + 1) * 86400;
		var seconds = nextDayStart - DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		if ( seconds <= 0 ) return "Available now";

		var hours = seconds / 3600;
		var mins = (seconds % 3600) / 60;
		return hours > 0 ? $"{hours}h {mins}m" : $"{mins}m";
	}
}
