using System.Text.Json;

namespace Sandbox;

public sealed class AimboxAimLeaderboardEntry
{
	public string Id { get; set; } = Guid.NewGuid().ToString( "N" );
	public string AccountId { get; set; }
	public string DisplayName { get; set; }
	public int Score { get; set; }
	public DateTime AchievedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class AimboxAimLeaderboardSubmitResult
{
	public int Score { get; init; }
	public int PreviousPersonalBest { get; init; }
	public int PersonalBest { get; init; }
	public bool IsNewPersonalBest { get; init; }
	public int LeaderboardRank { get; init; }
}

sealed class AimboxAimLeaderboardFile
{
	public Dictionary<string, List<AimboxAimLeaderboardEntry>> Boards { get; set; } = new();
}

public sealed class AimboxAimLeaderboardStore
{
	const int MaxEntriesPerMode = 100;

	static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
	readonly string _path;
	AimboxAimLeaderboardFile _data = new();

	public AimboxAimLeaderboardStore( string path = "data/aimbox/aim-leaderboards.json" )
	{
		_path = path.Trim( '/' );
		Load();
	}

	public IReadOnlyList<AimboxAimLeaderboardEntry> GetTop( AimboxGameMode mode, int count = 25 )
	{
		if ( !AimboxAimModeRules.IsAimMode( mode ) || count <= 0 )
			return [];

		var board = GetBoard( mode );
		return board.Take( count ).ToList();
	}

	public AimboxAimLeaderboardSubmitResult Submit(
		AimboxGameMode mode,
		AimboxPlayerData data,
		string displayName,
		int score )
	{
		if ( !AimboxAimModeRules.IsAimMode( mode ) || data is null || score < 0 )
			return new AimboxAimLeaderboardSubmitResult();

		var previousBest = data.AimModeBestScores.GetValueOrDefault( mode );
		var isNewPersonalBest = score > previousBest;
		if ( isNewPersonalBest )
			data.AimModeBestScores[mode] = score;

		var personalBest = Math.Max( previousBest, score );
		var board = GetBoard( mode );
		var entry = new AimboxAimLeaderboardEntry
		{
			AccountId = data.AccountId,
			DisplayName = displayName,
			Score = score,
			AchievedAtUtc = DateTime.UtcNow
		};

		board.Add( entry );
		SortBoard( board );
		TrimBoard( board );
		SetBoard( mode, board );
		Save();

		var rank = board.FindIndex( x => x.Id == entry.Id );

		return new AimboxAimLeaderboardSubmitResult
		{
			Score = score,
			PreviousPersonalBest = previousBest,
			PersonalBest = personalBest,
			IsNewPersonalBest = isNewPersonalBest,
			LeaderboardRank = rank >= 0 ? rank + 1 : 0
		};
	}

	List<AimboxAimLeaderboardEntry> GetBoard( AimboxGameMode mode )
	{
		if ( !_data.Boards.TryGetValue( ModeKey( mode ), out var board ) || board is null )
			return [];

		EnsureEntryIds( board );
		return board;
	}

	void SetBoard( AimboxGameMode mode, List<AimboxAimLeaderboardEntry> board ) =>
		_data.Boards[ModeKey( mode )] = board;

	static string ModeKey( AimboxGameMode mode ) => mode.ToString();

	static void EnsureEntryIds( List<AimboxAimLeaderboardEntry> board )
	{
		foreach ( var entry in board )
		{
			if ( string.IsNullOrWhiteSpace( entry.Id ) )
				entry.Id = Guid.NewGuid().ToString( "N" );
		}
	}

	static void SortBoard( List<AimboxAimLeaderboardEntry> board )
	{
		board.Sort( ( a, b ) =>
		{
			var scoreCompare = b.Score.CompareTo( a.Score );
			if ( scoreCompare != 0 )
				return scoreCompare;

			var timeCompare = a.AchievedAtUtc.CompareTo( b.AchievedAtUtc );
			if ( timeCompare != 0 )
				return timeCompare;

			return string.Compare( a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase );
		} );
	}

	static void TrimBoard( List<AimboxAimLeaderboardEntry> board )
	{
		if ( board.Count <= MaxEntriesPerMode )
			return;

		board.RemoveRange( MaxEntriesPerMode, board.Count - MaxEntriesPerMode );
	}

	void Load()
	{
		try
		{
			if ( !FileSystem.Data.FileExists( _path ) )
				return;

			_data = JsonSerializer.Deserialize<AimboxAimLeaderboardFile>( FileSystem.Data.ReadAllText( _path ), Options )
			       ?? new AimboxAimLeaderboardFile();

			foreach ( var board in _data.Boards.Values )
				EnsureEntryIds( board );
		}
		catch ( Exception ex )
		{
			Log.Warning( $"[Aimbox] Failed to load AIM leaderboards: {ex.Message}" );
			_data = new AimboxAimLeaderboardFile();
		}
	}

	void Save()
	{
		try
		{
			var directory = _path.Contains( '/' ) ? _path[.._path.LastIndexOf( '/' )] : "";
			if ( !string.IsNullOrWhiteSpace( directory ) )
				FileSystem.Data.CreateDirectory( directory );

			FileSystem.Data.WriteAllText( _path, JsonSerializer.Serialize( _data, Options ) );
		}
		catch ( Exception ex )
		{
			Log.Warning( $"[Aimbox] Failed to save AIM leaderboards: {ex.Message}" );
		}
	}
}
