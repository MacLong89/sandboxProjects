namespace UnderPressure;

/// <summary>Syncs lifetime earnings to cloud stats and fetches the global board.</summary>
public static class LeaderboardService
{
	public const string StatName = "lifetime_earned";

	public static void SubmitEarned( double amount )
	{
		if ( amount <= 0 ) return;

		try
		{
			Sandbox.Services.Stats.Increment( StatName, (float)amount );
		}
		catch ( Exception e )
		{
			Log.Warning( $"[UnderPressure] Failed to submit earnings stat: {e.Message}" );
		}
	}

	/// <summary>One-time push of local lifetime totals for saves created before cloud stats existed.</summary>
	public static async Task MigrateLifetimeTotal( SaveData save )
	{
		if ( save.LeaderboardMigrated ) return;

		save.LeaderboardMigrated = true;

		try
		{
			var total = save.LifetimeEarned;
			var cloud = Sandbox.Services.Stats.LocalPlayer.Get( StatName );
			total = Math.Max( total, cloud.Sum );

			if ( total > 0 )
				Sandbox.Services.Stats.SetValue( StatName, (float)total );

			await Sandbox.Services.Stats.FlushAsync();
		}
		catch ( Exception e )
		{
			Log.Warning( $"[UnderPressure] Failed to migrate lifetime earnings stat: {e.Message}" );
		}
	}

	public static Sandbox.Services.Leaderboards.Board2 CreateBoard()
	{
		var board = Sandbox.Services.Leaderboards.GetFromStat( GameConstants.PackageIdent, StatName );
		board.SetAggregationSum();
		board.SetSortDescending();
		board.CenterOnMe();
		board.MaxEntries = 50;
		return board;
	}

	public static void Flush()
	{
		try
		{
			Sandbox.Services.Stats.Flush();
		}
		catch ( Exception e )
		{
			Log.Warning( $"[UnderPressure] Failed to flush stats: {e.Message}" );
		}
	}
}
