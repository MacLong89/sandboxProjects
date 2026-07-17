namespace Fauna2;

/// <summary>
/// Social layer: zoo visiting, likes/favorites and visit rewards.
///
/// Visiting works through normal multiplayer — friends join the host's lobby
/// and walk the zoo (their presence replicates via PlayerState). Joining
/// grants the zoo a "visitor gift" once per player per day, giving both sides
/// a reason to host and to visit.
///
/// A leaderboard provider interface ships now so a backend (Sandbox.Services
/// stats, web API, ...) can slot in later without touching game code.
/// </summary>
public sealed class SocialSystem : Component
{
	public static SocialSystem Instance { get; private set; }

	/// <summary>Steam ids that liked this zoo (one like per account).</summary>
	[Sync( SyncFlags.FromHost )] public NetList<long> Likes { get; set; } = new();

	/// <summary>Steam ids that favorited this zoo.</summary>
	[Sync( SyncFlags.FromHost )] public NetList<long> Favorites { get; set; } = new();

	[Sync( SyncFlags.FromHost )] public int TotalVisitors { get; set; }
	[Sync( SyncFlags.FromHost )] public int WeeklyBestScore { get; set; }
	[Sync( SyncFlags.FromHost )] public string WeeklyTheme { get; set; } = "Popularity Sprint";

	/// <summary>steamId → last visit-bonus day (UTC day number). Host only; persisted.</summary>
	public Dictionary<long, int> VisitBonusDays { get; } = new();

	// AUDIT FIX B12: engage used to ++TotalVisitors with no rate limit (E spam /
	// forged RPC inflated weekly score). Per-caller cooldown below.
	// Revert: remove dictionary + checks in RequestVisitorEngaged.
	private readonly Dictionary<long, TimeSince> _lastEngageBySteamId = new();
	private const float VisitorEngageCooldownSeconds = 8f;

	public ILeaderboardProvider Leaderboards { get; set; } = new NullLeaderboardProvider();
	private TimeUntil _nextWeeklySubmit;

	protected override void OnAwake() => Instance = this;
	protected override void OnDestroy() { if ( Instance == this ) Instance = null; }

	protected override void OnFixedUpdate()
	{
		if ( IsProxy || GameManager.Instance?.GameStarted != true || !_nextWeeklySubmit ) return;
		_nextWeeklySubmit = 15f;
		UpdateWeeklyCompetition();
	}

	public bool HasLiked( long steamId ) => Likes.Contains( steamId );
	public bool HasFavorited( long steamId ) => Favorites.Contains( steamId );

	public int WeeklyScore => WeeklyTheme switch
	{
		"Rare Breeder Cup" => (CollectionSystem.Instance?.DiscoveredVariantCount ?? 0) * 120 + (ZooState.Instance?.TotalAnimalsBred ?? 0) * 15,
		"Guest Favorite Showcase" => (int)((GuestSystem.Instance?.ZooRating ?? 0f) * 100f) + Likes.Count * 40 + Favorites.Count * 60,
		"Conservation Week" => (ZooState.Instance?.TotalAnimalsCaught ?? 0) * 35 + (CollectionSystem.Instance?.DiscoveredSpeciesCount ?? 0) * 50,
		_ => Likes.Count * 50 + TotalVisitors * 10 + (GuestSystem.Instance?.PeakGuests ?? 0),
	};

	public string ShareSummary =>
		$"{ZooState.Instance?.ZooName ?? "Fauna Zoo"} · {GuestSystem.Instance?.ZooRating ?? 0f:0.0} stars · {CollectionSystem.Instance?.DiscoveredSpeciesCount ?? 0}/{Defs.Animals.Count()} species · {Likes.Count} likes";

	// ── Requests ────────────────────────────────────────────

	[Rpc.Host]
	public void RequestLike()
	{
		var steamId = Rpc.Caller?.SteamId.Value ?? 0;
		if ( steamId == 0 || Likes.Contains( steamId ) ) return;

		Likes.Add( steamId );
		ZooState.Instance?.Notify( $"{Rpc.Caller.DisplayName} liked the zoo! ({Likes.Count} likes)", "thumb_up" );
		Leaderboards.Submit( "likes", Likes.Count );
		UpdateWeeklyCompetition();
	}

	[Rpc.Host]
	public void RequestFavorite()
	{
		var steamId = Rpc.Caller?.SteamId.Value ?? 0;
		if ( steamId == 0 ) return;

		if ( Favorites.Contains( steamId ) )
			Favorites.Remove( steamId );
		else
			Favorites.Add( steamId );

		UpdateWeeklyCompetition();
	}

	// ── Visits (called by GameManager) ──────────────────────

	public void OnVisitorJoined( Connection channel )
	{
		if ( !Networking.IsHost || channel == Connection.Local ) return;

		TotalVisitors++;

		// Daily visit bonus: once per visitor per UTC day.
		var steamId = channel.SteamId.Value;
		var today = (int)(DateTime.UtcNow.Ticks / TimeSpan.TicksPerDay);

		if ( !VisitBonusDays.TryGetValue( steamId, out var lastDay ) || lastDay < today )
		{
			VisitBonusDays[steamId] = today;
			ZooState.Instance?.AddMoney( GameConstants.VisitorGiftAmount );
			ZooState.Instance?.AddXp( 40 );
			ZooState.Instance?.Notify(
				$"{channel.DisplayName} is visiting — visitor gift +${GameConstants.VisitorGiftAmount}!", "card_giftcard" );
		}
		else
		{
			ZooState.Instance?.Notify( $"{channel.DisplayName} is visiting the zoo!", "waving_hand" );
		}

		UpdateWeeklyCompetition();
	}

	public void OnVisitorLeft( Connection channel )
	{
		// AUDIT: clear engage cooldown entry so a quick rejoin doesn't inherit an
		// ancient TimeSince (TimeSince resets on remove/re-add anyway).
		if ( channel is not null )
			_lastEngageBySteamId.Remove( channel.SteamId.Value );
	}

	/// <summary>Visitors engaging with exhibits helps the host zoo socially.</summary>
	public void OnVisitorEngaged()
	{
		if ( SaveHost.CanStartSession )
			return;

		if ( Networking.IsHost )
			ApplyVisitorEngaged( Connection.Local?.SteamId.Value ?? 0 );
		else
			RequestVisitorEngaged();
	}

	[Rpc.Host]
	private void RequestVisitorEngaged()
	{
		var steamId = Rpc.Caller?.SteamId.Value ?? 0;
		ApplyVisitorEngaged( steamId );
	}

	/// <summary>AUDIT FIX B12: rate-limited engage credit (not unique-visitor join).</summary>
	private void ApplyVisitorEngaged( long steamId )
	{
		if ( !Networking.IsHost ) return;
		if ( steamId == 0 ) return;

		if ( _lastEngageBySteamId.TryGetValue( steamId, out var since )
			&& since < VisitorEngageCooldownSeconds )
			return;

		_lastEngageBySteamId[steamId] = 0f;
		TotalVisitors++;
		UpdateWeeklyCompetition();
	}

	private void UpdateWeeklyCompetition()
	{
		if ( !Networking.IsHost ) return;

		WeeklyTheme = ThemeForWeek();
		WeeklyBestScore = Math.Max( WeeklyBestScore, WeeklyScore );
		Leaderboards.Submit( $"weekly:{WeeklyTheme}", WeeklyBestScore );
	}

	private static string ThemeForWeek()
	{
		var day = WeatherSeasonSystem.Instance?.Day ?? 1;
		var week = ((day - 1) / 7) % 4;
		return week switch
		{
			0 => "Popularity Sprint",
			1 => "Rare Breeder Cup",
			2 => "Guest Favorite Showcase",
			_ => "Conservation Week",
		};
	}
}

// ── Leaderboard framework ───────────────────────────────────

public readonly struct LeaderboardEntry
{
	public string PlayerName { get; init; }
	public double Value { get; init; }
	public int Rank { get; init; }
}

/// <summary>
/// Future-proof leaderboard seam. Implementations could use Sandbox.Services
/// stats, a community web API, or local files — game code stays unchanged.
/// </summary>
public interface ILeaderboardProvider
{
	void Submit( string stat, double value );
	Task<IReadOnlyList<LeaderboardEntry>> GetTop( string stat, int count );
}

/// <summary>Placeholder provider until a backend is wired up.</summary>
public sealed class NullLeaderboardProvider : ILeaderboardProvider
{
	public void Submit( string stat, double value ) { }

	public Task<IReadOnlyList<LeaderboardEntry>> GetTop( string stat, int count ) =>
		Task.FromResult<IReadOnlyList<LeaderboardEntry>>( new List<LeaderboardEntry>() );
}
