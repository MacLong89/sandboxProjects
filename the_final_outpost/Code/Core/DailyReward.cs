namespace FinalOutpost;

/// <summary>Preview of the pending daily login bonus.</summary>
public struct DailyState
{
	public bool Available;
	public int Streak;      // the streak the player WOULD be on if they claim now
	public double Reward;
}

/// <summary>
/// Daily login bonus with an escalating streak. Rewards the habit of returning each day — one of the
/// most effective retention levers there is. Streak grows on consecutive days and resets if a day is
/// skipped.
/// </summary>
public static class DailyReward
{
	public static double RewardForStreak( int streak )
	{
		var s = Math.Clamp( streak, 1, GameConstants.DailyStreakCap );
		return GameConstants.DailyBaseReward + (s - 1) * GameConstants.DailyRewardPerStreak;
	}

	public static DailyState Evaluate( SaveData save )
	{
		var state = new DailyState();
		if ( save is null ) return state;

		var today = DateTimeOffset.UtcNow.UtcDateTime.Date;

		if ( save.LastDailyClaimUnix <= 0 )
		{
			state.Available = true;
			state.Streak = 1;
			state.Reward = RewardForStreak( 1 );
			return state;
		}

		var lastDate = DateTimeOffset.FromUnixTimeSeconds( save.LastDailyClaimUnix ).UtcDateTime.Date;
		if ( today <= lastDate )
		{
			// Already claimed today (or clock skew) — nothing pending.
			state.Available = false;
			state.Streak = save.DailyStreak;
			return state;
		}

		var consecutive = today == lastDate.AddDays( 1 );
		state.Available = true;
		state.Streak = consecutive ? save.DailyStreak + 1 : 1;
		state.Reward = RewardForStreak( state.Streak );
		return state;
	}

	/// <summary>Applies the daily reward if one is pending. Returns the amount granted (0 if none).</summary>
	public static double Claim( GameCore core )
	{
		var save = core?.Save;
		var state = Evaluate( save );
		if ( !state.Available ) return 0;

		save.DailyStreak = state.Streak;
		save.LastDailyClaimUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		core.Wallet.Earn( state.Reward );
		core.SaveManagerTouch();
		return state.Reward;
	}
}
