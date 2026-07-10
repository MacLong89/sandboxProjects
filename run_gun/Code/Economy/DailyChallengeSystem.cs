namespace RunGun;

public enum DailyModifier
{
	None,
	DoubleEnemyHp,
	CritGatesOnly,
	FastEnemies,
	BonusCoins,
	HardMode,
}

public sealed class DailyChallengeSystem
{
	private readonly SaveData _save;

	public DailyChallengeSystem( SaveData save ) => _save = save;

	public int TodaySeed => (int)(DateTime.UtcNow.Date.ToUniversalTime() - DateTime.UnixEpoch).TotalDays;

	public DailyModifier ActiveModifier =>
		_save.DailyChallengeDate == TodaySeed
			? _save.DailyChallengeModifier
			: (DailyModifier)(TodaySeed % 5 + 1);

	public bool CompletedToday => _save.DailyChallengeDate == TodaySeed && _save.DailyChallengeCompleted;

	public string ModifierName => ActiveModifier switch
	{
		DailyModifier.DoubleEnemyHp => "Iron Brutes",
		DailyModifier.CritGatesOnly => "Sharpshooter Gates",
		DailyModifier.FastEnemies => "Rush Hour",
		DailyModifier.BonusCoins => "Payday",
		DailyModifier.HardMode => "Nightmare",
		_ => "Standard",
	};

	public string ModifierDescription => ActiveModifier switch
	{
		DailyModifier.DoubleEnemyHp => "Enemies have double HP.",
		DailyModifier.CritGatesOnly => "Gates only offer crit and damage.",
		DailyModifier.FastEnemies => "Enemies move 50% faster.",
		DailyModifier.BonusCoins => "Earn 2x coins this run.",
		DailyModifier.HardMode => "Enemies tougher and faster.",
		_ => "No modifier today.",
	};

	public double CompletionReward => 1500 * (1 + _save.PrestigeLevel * 0.25);

	public void BeginRun()
	{
		if ( _save.DailyChallengeDate != TodaySeed )
		{
			_save.DailyChallengeDate = TodaySeed;
			_save.DailyChallengeModifier = (DailyModifier)(TodaySeed % 5 + 1);
			_save.DailyChallengeCompleted = false;
		}
	}

	public bool TryComplete( float distance )
	{
		if ( _save.DailyChallengeDate != TodaySeed )
		{
			_save.DailyChallengeDate = TodaySeed;
			_save.DailyChallengeModifier = (DailyModifier)(TodaySeed % 5 + 1);
			_save.DailyChallengeCompleted = false;
		}

		if ( _save.DailyChallengeCompleted || distance < 400f ) return false;
		_save.DailyChallengeCompleted = true;
		return true;
	}
}
