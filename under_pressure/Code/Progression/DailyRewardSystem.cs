namespace UnderPressure;

/// <summary>Escalating login-streak bonus. Missing a day resets the streak.</summary>
public static class DailyRewardSystem
{
	public readonly struct Result
	{
		public bool Granted { get; init; }
		public int Streak { get; init; }
		public double Amount { get; init; }
	}

	public static Result Apply( SaveData save, PlayerWallet wallet, PrestigeSystem prestige )
	{
		var today = DateTime.UtcNow.ToString( "yyyy-MM-dd" );
		if ( save.LastDailyDate == today )
			return default;

		var yesterday = DateTime.UtcNow.AddDays( -1 ).ToString( "yyyy-MM-dd" );
		save.DailyStreak = save.LastDailyDate == yesterday ? save.DailyStreak + 1 : 1;
		save.LastDailyDate = today;

		var streakForReward = Math.Min( save.DailyStreak, GameConstants.DailyMaxStreakReward );
		var amount = (GameConstants.DailyBaseReward + (streakForReward - 1) * GameConstants.DailyPerStreak)
			* prestige.Multiplier;

		wallet.Earn( amount );
		return new Result { Granted = true, Streak = save.DailyStreak, Amount = amount };
	}
}
