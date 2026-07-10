namespace FinalOutpost;

public sealed class LeaderboardDisplayEntry
{
	public int Rank { get; set; }
	public string Name { get; set; }
	public int Nights { get; set; }
	public bool IsLocal { get; set; }
}

public static class LeaderboardService
{
	/// <summary>Submit a finished run to the global stat board (aggregates to each player's best).</summary>
	public static void SubmitRun( int nights )
	{
		if ( nights < 1 ) return;

		try
		{
			Sandbox.Services.Stats.SetValue( GameConstants.LeaderboardStat, nights );
			Sandbox.Services.Stats.Flush();
		}
		catch ( Exception e )
		{
			Log.Warning( $"[FinalOutpost] Leaderboard submit failed: {e.Message}" );
		}
	}

	/// <summary>Keep a local history entry and push the run to the global board.</summary>
	public static void RecordAndSubmitRun( SaveData save, int nights )
	{
		if ( save is null || nights < 1 ) return;

		save.RecordCompletedRun( nights );
		SubmitRun( nights );
	}

	public static async Task<IReadOnlyList<Sandbox.Services.Leaderboards.Board2.Entry>> FetchGlobalAsync( int maxEntries = 100 )
	{
		try
		{
			var board = Sandbox.Services.Leaderboards.GetFromStat( GameConstants.LeaderboardStat );
			board.SetAggregationMax();
			board.SetSortDescending();
			board.MaxEntries = maxEntries;
			await board.Refresh();
			return board.Entries ?? Array.Empty<Sandbox.Services.Leaderboards.Board2.Entry>();
		}
		catch ( Exception e )
		{
			Log.Warning( $"[FinalOutpost] Leaderboard fetch failed: {e.Message}" );
			return Array.Empty<Sandbox.Services.Leaderboards.Board2.Entry>();
		}
	}

	/// <summary>
	/// Build the display board: every completed local run is its own row; other players appear once (their best).
	/// In-progress runs are never included.
	/// </summary>
	public static async Task<IReadOnlyList<LeaderboardDisplayEntry>> FetchDisplayAsync( SaveData save, int maxEntries = 100 )
	{
		var localName = Connection.Local?.DisplayName;
		if ( string.IsNullOrWhiteSpace( localName ) )
			localName = "You";

		var history = save?.RunHistory;
		var hasLocalHistory = history is { Count: > 0 };
		var rows = new List<(int Nights, long SortTime, string Name, bool IsLocal)>();

		if ( hasLocalHistory )
		{
			foreach ( var run in history )
			{
				if ( run.Nights < 1 ) continue;
				rows.Add( (run.Nights, run.CompletedUnix, localName, true) );
			}
		}

		var localDisplayName = Connection.Local?.DisplayName;
		var global = await FetchGlobalAsync( maxEntries );
		foreach ( var e in global )
		{
			var name = string.IsNullOrWhiteSpace( e.DisplayName ) ? "Unknown" : e.DisplayName;
			if ( hasLocalHistory
				&& !string.IsNullOrWhiteSpace( localDisplayName )
				&& string.Equals( name, localDisplayName, StringComparison.OrdinalIgnoreCase ) )
				continue;

			var sortTime = e.Timestamp.ToUnixTimeSeconds();
			rows.Add( ((int)e.Value, sortTime, name, false) );
		}

		var ordered = rows
			.OrderByDescending( r => r.Nights )
			.ThenByDescending( r => r.SortTime )
			.ThenBy( r => r.Name, StringComparer.OrdinalIgnoreCase )
			.Take( maxEntries )
			.ToList();

		var result = new List<LeaderboardDisplayEntry>( ordered.Count );
		for ( var i = 0; i < ordered.Count; i++ )
		{
			var row = ordered[i];
			result.Add( new LeaderboardDisplayEntry
			{
				Rank = i + 1,
				Name = row.Name,
				Nights = row.Nights,
				IsLocal = row.IsLocal
			} );
		}

		return result;
	}
}
