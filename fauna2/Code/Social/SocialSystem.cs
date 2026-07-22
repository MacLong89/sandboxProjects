namespace Fauna2;

/// <summary>
/// Social layer: zoo visiting, likes/favorites and visit rewards.
///
/// Visiting works through normal multiplayer — friends join the host's lobby
/// and walk the zoo (their presence replicates via PlayerState). Joining
/// grants the zoo a "visitor gift" once per player per day, giving both sides
/// a reason to host and to visit.
///
/// Weekly Showcase is a personal best tracked on this zoo's save — not a
/// global ranked leaderboard.
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

	private TimeUntil _nextWeeklyUpdate;

	protected override void OnAwake() => Instance = this;
	protected override void OnDestroy() { if ( Instance == this ) Instance = null; }

	protected override void OnFixedUpdate()
	{
		if ( IsProxy || GameManager.Instance?.GameStarted != true || !_nextWeeklyUpdate ) return;
		_nextWeeklyUpdate = 15f;
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

	public string ZooSummary =>
		$"{ZooState.Instance?.ZooName ?? "Fauna Zoo"} · {GuestSystem.Instance?.ZooRating ?? 0f:0.0} stars · {CollectionSystem.Instance?.DiscoveredSpeciesCount ?? 0}/{Defs.Animals.Count()} species · {Likes.Count} likes";

	/// <summary>Legacy alias used by older UI bindings.</summary>
	public string ShareSummary => ZooSummary;

	// ── Requests ────────────────────────────────────────────

	[Rpc.Host]
	public void RequestLike()
	{
		var steamId = Rpc.Caller?.SteamId.Value ?? 0;
		if ( steamId == 0 || Likes.Contains( steamId ) ) return;

		Likes.Add( steamId );
		ZooState.Instance?.Notify( $"{Rpc.Caller.DisplayName} liked the zoo! ({Likes.Count} likes)", "thumb_up" );
		UpdateWeeklyCompetition();
	}

	[Rpc.Host]
	public void RequestFavorite()
	{
		var steamId = Rpc.Caller?.SteamId.Value ?? 0;
		if ( steamId == 0 ) return;

		if ( Favorites.Contains( steamId ) )
		{
			Favorites.Remove( steamId );
			ZooState.Instance?.Notify( $"{Rpc.Caller.DisplayName} unfavorited the zoo.", "star_border" );
		}
		else
		{
			Favorites.Add( steamId );
			ZooState.Instance?.Notify( $"{Rpc.Caller.DisplayName} favorited the zoo!", "star" );
		}

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
			GrantVisitorReturnCredit( steamId, GameConstants.VisitorGiftAmount );
		}
		else
		{
			ZooState.Instance?.Notify( $"{channel.DisplayName} is visiting the zoo!", "waving_hand" );
		}

		UpdateWeeklyCompetition();
	}

	[Rpc.Broadcast]
	private void GrantVisitorReturnCredit( long visitorSteamId, int amount )
	{
		if ( Connection.Local?.SteamId.Value != visitorSteamId || amount <= 0 )
			return;

		var today = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 86400;
		if ( GameSettings.Current.LastVisitorRewardUnixDay >= today )
			return;

		GameSettings.Current.LastVisitorRewardUnixDay = today;
		GameSettings.Current.PendingVisitorCredits += amount;
		GameSettings.Save();
		UI.UiState.PushToast( $"Visit reward saved: +${amount:n0} for your zoo next time you host.", "card_giftcard" );
	}

	/// <summary>Claim locally earned visit credits when this player next hosts a zoo.</summary>
	public static void ClaimPendingVisitorCredits()
	{
		if ( !SaveHost.CanStartSession ) return;

		var amount = Math.Max( 0, GameSettings.Current.PendingVisitorCredits );
		if ( amount <= 0 || !ZooState.Instance.IsValid() ) return;

		GameSettings.Current.PendingVisitorCredits = 0;
		GameSettings.Save();
		ZooState.Instance.AddMoney( amount );
		ZooState.Instance.Notify( $"Community visit reward claimed: +${amount:n0}.", "card_giftcard" );
		SaveSystem.Instance?.RequestSave();
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
