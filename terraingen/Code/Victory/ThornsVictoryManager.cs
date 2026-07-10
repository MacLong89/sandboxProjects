namespace Terraingen.Victory;

using Terraingen;
using Terraingen.Core;
using Terraingen.GameData;
using Terraingen.Multiplayer;
using Terraingen.Player;

/// <summary>Host-authoritative world victory paths — progress, competition, and snapshots.</summary>
[Title( "Thorns Victory Manager" )]
[Category( "Thorns/Victory" )]
[Icon( "emoji_events" )]
public sealed class ThornsVictoryManager : Component
{
	public const int MaxLeaderboardRows = 10;
	public const int MaxLeadershipHistory = 24;

	public static ThornsVictoryManager Instance { get; private set; }

	readonly Dictionary<string, Dictionary<string, long>> _playerProgress = new( StringComparer.Ordinal );
	readonly Dictionary<string, Dictionary<string, long>> _guildProgress = new( StringComparer.Ordinal );
	readonly Dictionary<string, Dictionary<string, long>> _worldProgress = new( StringComparer.Ordinal );
	readonly Dictionary<string, string> _lastPlayerLeader = new( StringComparer.OrdinalIgnoreCase );
	readonly Dictionary<string, string> _lastGuildLeader = new( StringComparer.OrdinalIgnoreCase );
	readonly List<ThornsVictoryLeadershipChangeDto> _leadershipChanges = new();

	bool _dirty;
	TimeSince _pushDebounce;

	protected override void OnStart()
	{
		Instance = this;
		TryImportPendingSave();
	}

	protected override void OnDestroy()
	{
		if ( Instance == this )
			Instance = null;
	}

	protected override void OnFixedUpdate()
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || !Game.IsPlaying )
			return;

		if ( !_dirty || _pushDebounce < 0.12f )
			return;

		_pushDebounce = 0;
		_dirty = false;
		HostPushAllVictorySnapshots();
	}

	public void HostFlushSnapshotRefresh()
	{
		if ( !ThornsMultiplayer.IsHostOrOffline )
			return;

		_dirty = false;
		_pushDebounce = 0;
		HostPushAllVictorySnapshots();
	}

	public static ThornsVictoryManager EnsureInstance()
	{
		if ( Instance is not null && Instance.IsValid )
			return Instance;

		var hostObject = Game.ActiveScene?.GetAllComponents<ThornsWorldPersistence>().FirstOrDefault()?.GameObject
		                 ?? Game.ActiveScene?.GetAllComponents<ThornsNetworkGameManager>().FirstOrDefault()?.GameObject;
		if ( hostObject is null || !hostObject.IsValid() )
			return null;

		return hostObject.Components.Get<ThornsVictoryManager>() ?? hostObject.Components.Create<ThornsVictoryManager>();
	}

	void TryImportPendingSave()
	{
		var persistence = ThornsWorldPersistence.Instance;
		if ( persistence is null )
			return;

		persistence.HostEnsureInitialized();
		ImportPersistent( persistence.Live?.VictoryState );
	}

	public void ImportPersistent( ThornsVictoryPersistentStateDto dto )
	{
		_playerProgress.Clear();
		_guildProgress.Clear();
		_worldProgress.Clear();
		_lastPlayerLeader.Clear();
		_lastGuildLeader.Clear();
		_leadershipChanges.Clear();

		if ( dto is null )
			return;

		CopyNested( dto.PlayerProgressByAccount, _playerProgress );
		CopyNested( dto.GuildProgressByGuildId, _guildProgress );
		HostPurgeObsoleteGuildProgress();
		if ( dto.WorldProgressByPath is not null && dto.WorldProgressByPath.Count > 0 )
		{
			var world = new Dictionary<string, long>( StringComparer.OrdinalIgnoreCase );
			foreach ( var (pathId, value) in dto.WorldProgressByPath )
				world[pathId] = Math.Max( 0, value );
			_worldProgress["world"] = world;
		}

		if ( dto.LastPlayerLeaderByPath is not null )
		{
			foreach ( var (pathId, key) in dto.LastPlayerLeaderByPath )
				_lastPlayerLeader[pathId] = key ?? "";
		}

		if ( dto.LastGuildLeaderByPath is not null )
		{
			foreach ( var (pathId, key) in dto.LastGuildLeaderByPath )
				_lastGuildLeader[pathId] = key ?? "";
		}

		if ( dto.LeadershipChanges is not null )
		{
			foreach ( var row in dto.LeadershipChanges )
			{
				if ( row is null || string.IsNullOrWhiteSpace( row.PathId ) )
					continue;

				_leadershipChanges.Add( new ThornsVictoryLeadershipChangeDto
				{
					PathId = row.PathId,
					PathDisplayName = ResolvePathName( row.PathId ),
					Scope = (ThornsVictoryScope)row.Scope,
					NewLeaderScopeKey = row.NewLeaderScopeKey ?? "",
					PreviousLeaderScopeKey = row.PreviousLeaderScopeKey ?? "",
					NewLeaderName = ResolveLeaderName( (ThornsVictoryScope)row.Scope, row.NewLeaderScopeKey ),
					PreviousLeaderName = ResolveLeaderName( (ThornsVictoryScope)row.Scope, row.PreviousLeaderScopeKey ),
					TimestampUtc = row.TimestampUtc ?? ""
				} );
			}
		}

		Log.Info( $"[Thorns Victory] Restored progress for {_playerProgress.Count} player(s), {_guildProgress.Count} guild(s)." );
	}

	public ThornsVictoryPersistentStateDto ExportPersistent()
	{
		return new ThornsVictoryPersistentStateDto
		{
			PlayerProgressByAccount = CloneNested( _playerProgress ),
			GuildProgressByGuildId = CloneNested( _guildProgress ),
			WorldProgressByPath = _worldProgress.TryGetValue( "world", out var world )
				? new Dictionary<string, long>( world, StringComparer.OrdinalIgnoreCase )
				: new Dictionary<string, long>( StringComparer.OrdinalIgnoreCase ),
			LastPlayerLeaderByPath = new Dictionary<string, string>( _lastPlayerLeader, StringComparer.OrdinalIgnoreCase ),
			LastGuildLeaderByPath = new Dictionary<string, string>( _lastGuildLeader, StringComparer.OrdinalIgnoreCase ),
			LeadershipChanges = _leadershipChanges
				.TakeLast( MaxLeadershipHistory )
				.Select( c => new ThornsVictoryLeadershipChangePersistentDto
				{
					PathId = c.PathId,
					Scope = (byte)c.Scope,
					NewLeaderScopeKey = c.NewLeaderScopeKey,
					PreviousLeaderScopeKey = c.PreviousLeaderScopeKey,
					TimestampUtc = c.TimestampUtc
				} )
				.ToList()
		};
	}

	public void HostReportSource( string accountKey, string sourceKey, int amount = 1 )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || string.IsNullOrWhiteSpace( accountKey ) || amount <= 0 )
			return;

		var guildId = ThornsGuildWorldService.Instance?.TryGetAccountGuildId( accountKey, out var gid ) == true ? gid : "";

		foreach ( var (pathId, points) in ThornsVictoryPathCatalog.ResolveSources( sourceKey ) )
		{
			var delta = (long)points * amount;
			AddProgress( pathId, ThornsVictoryScope.Player, accountKey, delta );
			if ( !string.IsNullOrEmpty( guildId ) )
				AddProgress( pathId, ThornsVictoryScope.Guild, guildId, delta );
			AddProgress( pathId, ThornsVictoryScope.World, "world", delta );
		}
	}

	void AddProgress( string pathId, ThornsVictoryScope scope, string scopeKey, long delta )
	{
		if ( delta <= 0 || string.IsNullOrWhiteSpace( scopeKey ) )
			return;

		var map = GetMap( scope );
		if ( !map.TryGetValue( scopeKey, out var perPath ) )
		{
			perPath = new Dictionary<string, long>( StringComparer.OrdinalIgnoreCase );
			map[scopeKey] = perPath;
		}

		perPath.TryGetValue( pathId, out var current );
		perPath[pathId] = current + delta;
		_dirty = true;
		TryRecordLeadershipChange( pathId, scope );
	}

	public ThornsVictorySnapshot BuildSnapshotFor( string accountKey, string selectedPathId = null )
	{
		var guildId = "";
		if ( !string.IsNullOrEmpty( accountKey ) )
			ThornsGuildWorldService.Instance?.TryGetAccountGuildId( accountKey, out guildId );

		selectedPathId = NormalizePathId( selectedPathId );
		var snap = new ThornsVictorySnapshot { SelectedPathId = selectedPathId };

		foreach ( var def in ThornsVictoryPathCatalog.All )
			snap.PathCards.Add( BuildPathCard( def, accountKey, guildId ) );

		snap.SelectedDetail = BuildPathDetail( selectedPathId, accountKey, guildId );
		snap.TopPlayersOverall = BuildTopPlayersOverall();
		snap.TopGuildsOverall = BuildTopGuildsOverall();
		snap.RecentLeadershipChanges = _leadershipChanges.TakeLast( MaxLeadershipHistory ).Reverse().ToList();
		snap.CurrentLeadersByPath = BuildCurrentLeaders();
		if ( !string.IsNullOrEmpty( guildId ) )
			snap.GuildSummary = BuildGuildSummaryFor( guildId );

		snap.GuildComparisonRows = BuildGuildComparisonRows();
		return snap;
	}

	public ThornsVictoryGuildSummaryDto BuildGuildSummaryFor( string guildId )
	{
		var summary = new ThornsVictoryGuildSummaryDto();
		if ( string.IsNullOrWhiteSpace( guildId ) )
			return summary;

		string bestPath = "";
		long bestProgress = -1;

		foreach ( var def in ThornsVictoryPathCatalog.All )
		{
			var progress = GetProgress( ThornsVictoryScope.Guild, guildId, def.PathId );
			var rank = ComputeRank( ThornsVictoryScope.Guild, guildId, def.PathId );
			var leader = GetLeader( ThornsVictoryScope.Guild, def.PathId );
			summary.PathRows.Add( new ThornsVictoryGuildPathRowDto
			{
				PathId = def.PathId,
				DisplayName = def.DisplayName,
				PercentComplete = Percent( progress, def.TargetProgress ),
				GuildRank = rank,
				PathLeaderName = leader.displayName
			} );

			if ( progress > bestProgress )
			{
				bestProgress = progress;
				bestPath = def.PathId;
			}
		}

		if ( !string.IsNullOrEmpty( bestPath ) )
		{
			var leader = GetLeader( ThornsVictoryScope.Guild, bestPath );
			summary.ServerLeaderPathName = ResolvePathName( bestPath );
			summary.ServerLeaderGuildName = leader.displayName;
		}

		return summary;
	}

	public ThornsGuildVictoryProgressSnapshot BuildGuildVictoryProgressSnapshot( string guildId, string selectedPathId = null )
	{
		var snapshot = new ThornsGuildVictoryProgressSnapshot();
		if ( string.IsNullOrWhiteSpace( guildId ) )
			return snapshot;

		foreach ( var def in ThornsVictoryPathCatalog.All )
		{
			var progress = GetProgress( ThornsVictoryScope.Guild, guildId, def.PathId );
			var rank = progress > 0 ? ComputeRank( ThornsVictoryScope.Guild, guildId, def.PathId ) : 0;
			var leader = GetLeader( ThornsVictoryScope.Guild, def.PathId );
			var currentMilestone = CurrentMilestone( def, progress );
			var nextMilestone = NextMilestone( def, progress );
			var pct = Percent( progress, def.TargetProgress );

			snapshot.Paths.Add( new ThornsGuildVictoryPathEntryDto
			{
				PathId = def.PathId,
				DisplayName = def.DisplayName,
				Summary = def.Summary,
				IconPath = def.IconPath,
				PercentComplete = pct,
				GuildRank = rank,
				CurrentMilestoneTitle = currentMilestone?.Title ?? ( progress > 0 ? "In progress" : "Not started" ),
				NextMilestoneTitle = nextMilestone?.Title ?? "Victory",
				NextMilestoneRewardPreview = nextMilestone?.RewardPreview ?? "Server prestige",
				RewardPreviewItems = SplitRewardPreviewItems( nextMilestone?.RewardPreview ),
				StatusLabel = ResolveGuildPathStatus( rank, pct, leader.scopeKey == guildId ),
				PathLeaderGuildName = progress > 0 && leader.scopeKey != guildId && leader.displayName != "—"
					? leader.displayName
					: "",
				GuildProgress = progress,
				TargetProgress = def.TargetProgress
			} );
		}

		_ = selectedPathId;
		return snapshot;
	}

	public void HostApplyGuildPathProgress( string guildId, IReadOnlyDictionary<string, long> pathProgress )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || string.IsNullOrWhiteSpace( guildId ) || pathProgress is null )
			return;

		if ( !_guildProgress.TryGetValue( guildId, out var perPath ) )
		{
			perPath = new Dictionary<string, long>( StringComparer.OrdinalIgnoreCase );
			_guildProgress[guildId] = perPath;
		}

		foreach ( var (pathId, value) in pathProgress )
		{
			if ( string.IsNullOrWhiteSpace( pathId ) || value <= 0 )
				continue;

			perPath[pathId] = Math.Max( perPath.GetValueOrDefault( pathId ), value );
		}

		_dirty = true;
	}

	/// <summary>Sets one guild path to an exact value (used for live NPC Dominion sync).</summary>
	public void HostSetGuildPathProgress( string guildId, string pathId, long value )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || string.IsNullOrWhiteSpace( guildId ) || string.IsNullOrWhiteSpace( pathId ) )
			return;

		if ( value <= 0 )
		{
			if ( _guildProgress.TryGetValue( guildId, out var existing ) )
			{
				existing.Remove( pathId );
				if ( existing.Count == 0 )
					_guildProgress.Remove( guildId );
			}

			_dirty = true;
			return;
		}

		if ( !_guildProgress.TryGetValue( guildId, out var perPath ) )
		{
			perPath = new Dictionary<string, long>( StringComparer.OrdinalIgnoreCase );
			_guildProgress[guildId] = perPath;
		}

		perPath[pathId] = value;
		_dirty = true;
		TryRecordLeadershipChange( pathId, ThornsVictoryScope.Guild );
	}

	public void HostRemoveGuildProgress( IEnumerable<string> guildIds )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || guildIds is null )
			return;

		var removedAny = false;
		foreach ( var guildId in guildIds )
		{
			if ( string.IsNullOrWhiteSpace( guildId ) )
				continue;

			if ( _guildProgress.Remove( guildId ) )
				removedAny = true;
		}

		if ( removedAny )
			_dirty = true;
	}

	public void HostPurgeObsoleteGuildProgress()
	{
		if ( !ThornsMultiplayer.IsHostOrOffline )
			return;

		var removed = _guildProgress.Keys
			.Where( id => ThornsGuildWorldService.Instance?.ShouldIncludeInServerRankings( id ) != true )
			.ToList();

		if ( removed.Count == 0 )
			return;

		HostRemoveGuildProgress( removed );
	}

	static ThornsVictoryMilestoneDefinition CurrentMilestone( ThornsVictoryPathDefinition def, long progress )
	{
		ThornsVictoryMilestoneDefinition current = null;
		foreach ( var milestone in def.Milestones.OrderBy( m => m.Threshold ) )
		{
			if ( progress >= milestone.Threshold )
				current = milestone;
			else
				break;
		}

		return current;
	}

	static string ResolveGuildPathStatus( int rank, float percent, bool isPathLeader )
	{
		if ( percent <= 0f )
			return "";

		if ( isPathLeader )
			return "Leading";
		if ( rank > 0 && rank <= 3 )
			return "Contested";
		if ( percent >= 50f )
			return "Rising";
		return "Behind";
	}

	static List<string> SplitRewardPreviewItems( string rewardPreview )
	{
		var list = new List<string>();
		if ( string.IsNullOrWhiteSpace( rewardPreview ) )
			return list;

		if ( rewardPreview.Contains( '&', StringComparison.Ordinal ) )
		{
			list.AddRange( rewardPreview.Split( '&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries ) );
			return list;
		}

		list.Add( rewardPreview );
		return list;
	}

	ThornsVictoryPathCardDto BuildPathCard( ThornsVictoryPathDefinition def, string accountKey, string guildId )
	{
		var playerProgress = GetProgress( ThornsVictoryScope.Player, accountKey, def.PathId );
		var guildProgress = string.IsNullOrEmpty( guildId ) ? 0 : GetProgress( ThornsVictoryScope.Guild, guildId, def.PathId );
		var worldProgress = GetProgress( ThornsVictoryScope.World, "world", def.PathId );
		var leader = GetLeader( ThornsVictoryScope.Player, def.PathId );
		var next = NextMilestone( def, playerProgress );

		return new ThornsVictoryPathCardDto
		{
			PathId = def.PathId,
			DisplayName = def.DisplayName,
			Summary = def.Summary,
			IconPath = def.IconPath,
			PercentComplete = Percent( playerProgress, def.TargetProgress ),
			PlayerRank = ComputeRank( ThornsVictoryScope.Player, accountKey, def.PathId ),
			GuildRank = string.IsNullOrEmpty( guildId ) ? 0 : ComputeRank( ThornsVictoryScope.Guild, guildId, def.PathId ),
			CurrentLeaderName = leader.displayName,
			CurrentLeaderScope = leader.scopeKey,
			NextMilestoneTitle = next?.Title ?? "Victory",
			NextMilestoneThreshold = next?.Threshold ?? def.TargetProgress,
			NextMilestoneRewardPreview = next?.RewardPreview ?? "Server prestige",
			PlayerProgress = playerProgress,
			GuildProgress = guildProgress,
			WorldProgress = worldProgress,
			TargetProgress = def.TargetProgress
		};
	}

	ThornsVictoryPathDetailDto BuildPathDetail( string pathId, string accountKey, string guildId )
	{
		if ( !ThornsVictoryPathCatalog.TryGet( pathId, out var def ) )
			def = ThornsVictoryPathCatalog.All[0];

		var detail = new ThornsVictoryPathDetailDto
		{
			PathId = def.PathId,
			DisplayName = def.DisplayName,
			Summary = def.Summary,
			Player = BuildProgressEntry( def, ThornsVictoryScope.Player, accountKey ),
			Guild = BuildProgressEntry( def, ThornsVictoryScope.Guild, guildId ),
			World = BuildProgressEntry( def, ThornsVictoryScope.World, "world" ),
			TopPlayers = BuildLeaderboard( ThornsVictoryScope.Player, def.PathId ),
			TopGuilds = BuildLeaderboard( ThornsVictoryScope.Guild, def.PathId )
		};

		var playerProgress = detail.Player.CurrentProgress;
		foreach ( var milestone in def.Milestones )
		{
			detail.Milestones.Add( new ThornsVictoryMilestoneRowDto
			{
				MilestoneId = milestone.MilestoneId,
				Title = milestone.Title,
				Threshold = milestone.Threshold,
				RewardPreview = milestone.RewardPreview,
				Reached = playerProgress >= milestone.Threshold
			} );
		}

		return detail;
	}

	ThornsVictoryProgressEntry BuildProgressEntry( ThornsVictoryPathDefinition def, ThornsVictoryScope scope, string scopeKey )
	{
		if ( string.IsNullOrWhiteSpace( scopeKey ) )
			scopeKey = "";

		var progress = string.IsNullOrEmpty( scopeKey ) ? 0 : GetProgress( scope, scopeKey, def.PathId );
		var leader = GetLeader( scope, def.PathId );
		var topGuild = GetLeader( ThornsVictoryScope.Guild, def.PathId );
		var topPlayer = GetLeader( ThornsVictoryScope.Player, def.PathId );

		return new ThornsVictoryProgressEntry
		{
			PathId = def.PathId,
			Scope = scope,
			ScopeKey = scopeKey,
			CurrentProgress = progress,
			TotalProgress = def.TargetProgress,
			PercentComplete = Percent( progress, def.TargetProgress ),
			Rank = string.IsNullOrEmpty( scopeKey ) ? 0 : ComputeRank( scope, scopeKey, def.PathId ),
			LeaderDisplayName = leader.displayName,
			LeaderScopeKey = leader.scopeKey,
			TopGuildName = topGuild.displayName,
			TopPlayerName = topPlayer.displayName
		};
	}

	List<ThornsVictoryLeaderboardRowDto> BuildLeaderboard( ThornsVictoryScope scope, string pathId )
	{
		var rows = new List<(string Key, long Progress)>();
		var map = GetMap( scope );
		foreach ( var (scopeKey, perPath) in map )
		{
			if ( perPath is null || !perPath.TryGetValue( pathId, out var progress ) || progress <= 0 )
				continue;
			rows.Add( (scopeKey, progress) );
		}

		rows.Sort( (a, b) => b.Progress.CompareTo( a.Progress ) );
		var result = new List<ThornsVictoryLeaderboardRowDto>();
		var rank = 1;
		ThornsVictoryPathCatalog.TryGet( pathId, out var def );
		var target = def?.TargetProgress ?? 10_000;

		foreach ( var row in rows.Take( MaxLeaderboardRows ) )
		{
			result.Add( new ThornsVictoryLeaderboardRowDto
			{
				Rank = rank++,
				DisplayName = ResolveLeaderName( scope, row.Key ),
				ScopeKey = row.Key,
				Progress = row.Progress,
				PercentComplete = Percent( row.Progress, target )
			} );
		}

		return result;
	}

	List<ThornsVictoryLeaderboardRowDto> BuildTopPlayersOverall()
	{
		var totals = new Dictionary<string, long>( StringComparer.Ordinal );
		foreach ( var (account, perPath) in _playerProgress )
		{
			if ( perPath is null )
				continue;
			totals[account] = perPath.Values.Sum();
		}

		return totals
			.OrderByDescending( kv => kv.Value )
			.Take( MaxLeaderboardRows )
			.Select( (kv, i) => new ThornsVictoryLeaderboardRowDto
			{
				Rank = i + 1,
				DisplayName = ResolveLeaderName( ThornsVictoryScope.Player, kv.Key ),
				ScopeKey = kv.Key,
				Progress = kv.Value,
				PercentComplete = 0
			} )
			.ToList();
	}

	List<ThornsVictoryGuildComparisonRowDto> BuildGuildComparisonRows( int maxRows = 16 )
	{
		var guildIds = CollectRankedGuildIds();
		var rows = new List<(string GuildId, long Total, List<ThornsVictoryGuildPathRowDto> Paths)>();

		foreach ( var guildId in guildIds )
		{
			_guildProgress.TryGetValue( guildId, out var perPath );
			perPath ??= new Dictionary<string, long>( StringComparer.OrdinalIgnoreCase );

			var pathRows = new List<ThornsVictoryGuildPathRowDto>();
			long total = 0;

			foreach ( var def in ThornsVictoryPathCatalog.All )
			{
				perPath.TryGetValue( def.PathId, out var progress );
				total += progress;
				pathRows.Add( new ThornsVictoryGuildPathRowDto
				{
					PathId = def.PathId,
					DisplayName = def.DisplayName,
					PercentComplete = Percent( progress, def.TargetProgress ),
					GuildRank = progress > 0 ? ComputeRank( ThornsVictoryScope.Guild, guildId, def.PathId ) : 0,
					PathLeaderName = GetLeader( ThornsVictoryScope.Guild, def.PathId ).displayName
				} );
			}

			rows.Add( (guildId, total, pathRows) );
		}

		return rows
			.Where( r => ThornsGuildWorldService.Instance?.ShouldIncludeInServerRankings( r.GuildId ) == true )
			.OrderByDescending( r => r.Total )
			.ThenBy( r => ResolveLeaderName( ThornsVictoryScope.Guild, r.GuildId ), StringComparer.OrdinalIgnoreCase )
			.Take( maxRows )
			.Select( (row, index) => new ThornsVictoryGuildComparisonRowDto
			{
				GuildId = row.GuildId,
				GuildName = ResolveLeaderName( ThornsVictoryScope.Guild, row.GuildId ),
				IsNpcGuild = ThornsGuildWorldService.Instance?.IsNpcGuild( row.GuildId ) == true,
				IsEliminated = ThornsGuildWorldService.Instance?.IsNpcGuildEliminated( row.GuildId ) == true,
				MemberCount = ThornsGuildWorldService.Instance?.GetMemberCount( row.GuildId ) ?? 0,
				OverallRank = row.Total > 0 ? index + 1 : 0,
				TotalScore = row.Total,
				PathRows = row.Paths
			} )
			.ToList();
	}

	List<ThornsVictoryLeaderboardRowDto> BuildTopGuildsOverall()
	{
		var guildIds = CollectRankedGuildIds();
		var totals = new List<(string GuildId, long Total)>();
		foreach ( var guildId in guildIds )
		{
			long total = 0;
			if ( _guildProgress.TryGetValue( guildId, out var perPath ) && perPath is not null )
				total = perPath.Values.Sum();
			totals.Add( (guildId, total) );
		}

		return totals
			.Where( kv => ThornsGuildWorldService.Instance?.ShouldIncludeInServerRankings( kv.GuildId ) == true )
			.OrderByDescending( kv => kv.Total )
			.ThenBy( kv => ResolveLeaderName( ThornsVictoryScope.Guild, kv.GuildId ), StringComparer.OrdinalIgnoreCase )
			.Take( MaxLeaderboardRows )
			.Select( (kv, i) => new ThornsVictoryLeaderboardRowDto
			{
				Rank = kv.Total > 0 ? i + 1 : 0,
				DisplayName = ResolveLeaderName( ThornsVictoryScope.Guild, kv.GuildId ),
				ScopeKey = kv.GuildId,
				IsNpcGuild = ThornsGuildWorldService.Instance?.IsNpcGuild( kv.GuildId ) == true,
				Progress = kv.Total,
				PercentComplete = 0
			} )
			.ToList();
	}

	List<ThornsVictoryPathLeaderDto> BuildCurrentLeaders()
	{
		var list = new List<ThornsVictoryPathLeaderDto>();
		foreach ( var def in ThornsVictoryPathCatalog.All )
		{
			var guildLeader = GetLeader( ThornsVictoryScope.Guild, def.PathId );
			var guildProgress = string.IsNullOrWhiteSpace( guildLeader.scopeKey )
				? 0
				: GetProgress( ThornsVictoryScope.Guild, guildLeader.scopeKey, def.PathId );
			var world = GetProgress( ThornsVictoryScope.World, "world", def.PathId );

			list.Add( new ThornsVictoryPathLeaderDto
			{
				PathId = def.PathId,
				PathDisplayName = def.DisplayName,
				PlayerLeaderName = GetLeader( ThornsVictoryScope.Player, def.PathId ).displayName,
				GuildLeaderName = guildProgress > 0 ? guildLeader.displayName : "—",
				GuildProgress = guildProgress,
				GuildPercentComplete = Percent( guildProgress, def.TargetProgress ),
				WorldProgress = world,
				WorldPercentComplete = Percent( world, def.TargetProgress )
			} );
		}

		return list;
	}

	void TryRecordLeadershipChange( string pathId, ThornsVictoryScope scope )
	{
		var leader = GetLeader( scope, pathId );
		var lastMap = scope == ThornsVictoryScope.Player ? _lastPlayerLeader : _lastGuildLeader;
		lastMap.TryGetValue( pathId, out var previousKey );

		if ( string.Equals( previousKey, leader.scopeKey, StringComparison.Ordinal ) )
			return;

		if ( !string.IsNullOrEmpty( previousKey ) && !string.IsNullOrEmpty( leader.scopeKey ) )
		{
			_leadershipChanges.Add( new ThornsVictoryLeadershipChangeDto
			{
				PathId = pathId,
				PathDisplayName = ResolvePathName( pathId ),
				Scope = scope,
				NewLeaderScopeKey = leader.scopeKey,
				PreviousLeaderScopeKey = previousKey,
				NewLeaderName = leader.displayName,
				PreviousLeaderName = ResolveLeaderName( scope, previousKey ),
				TimestampUtc = DateTime.UtcNow.ToString( "o" )
			} );

			while ( _leadershipChanges.Count > MaxLeadershipHistory )
				_leadershipChanges.RemoveAt( 0 );
		}

		lastMap[pathId] = leader.scopeKey ?? "";
	}

	(string displayName, string scopeKey) GetLeader( ThornsVictoryScope scope, string pathId )
	{
		var map = GetMap( scope );
		string bestKey = "";
		long best = -1;

		foreach ( var (scopeKey, perPath) in map )
		{
			if ( perPath is null || !perPath.TryGetValue( pathId, out var value ) )
				continue;
			if ( value > best )
			{
				best = value;
				bestKey = scopeKey;
			}
		}

		if ( string.IsNullOrEmpty( bestKey ) )
			return ("—", "");

		return (ResolveLeaderName( scope, bestKey ), bestKey);
	}

	int ComputeRank( ThornsVictoryScope scope, string scopeKey, string pathId )
	{
		if ( string.IsNullOrWhiteSpace( scopeKey ) )
			return 0;

		var progress = GetProgress( scope, scopeKey, pathId );
		var map = GetMap( scope );
		var rank = 1;
		foreach ( var (_, perPath) in map )
		{
			if ( perPath is null || !perPath.TryGetValue( pathId, out var other ) )
				continue;
			if ( other > progress )
				rank++;
		}

		return rank;
	}

	long GetProgress( ThornsVictoryScope scope, string scopeKey, string pathId )
	{
		if ( string.IsNullOrWhiteSpace( scopeKey ) )
			return 0;

		var map = GetMap( scope );
		if ( !map.TryGetValue( scopeKey, out var perPath ) || perPath is null )
			return 0;

		return perPath.TryGetValue( pathId, out var value ) ? Math.Max( 0, value ) : 0;
	}

	Dictionary<string, Dictionary<string, long>> GetMap( ThornsVictoryScope scope ) => scope switch
	{
		ThornsVictoryScope.Player => _playerProgress,
		ThornsVictoryScope.Guild => _guildProgress,
		_ => _worldProgress
	};

	static ThornsVictoryMilestoneDefinition NextMilestone( ThornsVictoryPathDefinition def, long progress )
	{
		foreach ( var milestone in def.Milestones.OrderBy( m => m.Threshold ) )
		{
			if ( progress < milestone.Threshold )
				return milestone;
		}

		return def.Milestones.LastOrDefault();
	}

	static float Percent( long current, long total )
	{
		if ( total <= 0 )
			return 0f;
		return Math.Clamp( current / (float)total * 100f, 0f, 100f );
	}

	static HashSet<string> CollectRankedGuildIds()
	{
		ThornsGuildWorldService.EnsureInstance()?.HostEnsureAllCatalogNpcGuilds();

		var guildIds = new HashSet<string>( StringComparer.OrdinalIgnoreCase );
		foreach ( var template in ThornsNpcGuildCatalog.All )
			guildIds.Add( template.GuildId );

		foreach ( var guildId in ThornsGuildWorldService.Instance?.GetAllGuildIds() ?? Array.Empty<string>() )
		{
			if ( string.IsNullOrWhiteSpace( guildId ) )
				continue;

			if ( ThornsGuildWorldService.Instance?.ShouldIncludeInServerRankings( guildId ) != true )
				continue;

			guildIds.Add( guildId );
		}

		if ( Instance is not null )
		{
			foreach ( var guildId in Instance._guildProgress.Keys )
			{
				if ( string.IsNullOrWhiteSpace( guildId ) )
					continue;

				if ( ThornsGuildWorldService.Instance?.ShouldIncludeInServerRankings( guildId ) != true )
					continue;

				guildIds.Add( guildId );
			}
		}

		return guildIds;
	}

	static string NormalizePathId( string pathId )
	{
		if ( ThornsVictoryPathCatalog.TryGet( pathId, out _ ) )
			return pathId;
		return ThornsVictoryPathIds.Dominion;
	}

	string ResolvePathName( string pathId ) =>
		ThornsVictoryPathCatalog.TryGet( pathId, out var def ) ? def.DisplayName : pathId;

	string ResolveLeaderName( ThornsVictoryScope scope, string scopeKey )
	{
		if ( string.IsNullOrWhiteSpace( scopeKey ) )
			return "—";

		if ( scope == ThornsVictoryScope.World )
			return "The Wasteland";

		if ( scope == ThornsVictoryScope.Guild )
		{
			var liveName = ThornsGuildWorldService.Instance?.TryResolveGuildDisplayName( scopeKey );
			if ( !string.IsNullOrWhiteSpace( liveName ) )
				return liveName;

			var guilds = ThornsWorldPersistence.Instance?.Live?.Guilds;
			if ( guilds is not null )
			{
				for ( var i = 0; i < guilds.Count; i++ )
				{
					var guild = guilds[i];
					if ( guild is null || !string.Equals( guild.GuildId, scopeKey, StringComparison.Ordinal ) )
						continue;

					if ( !string.IsNullOrWhiteSpace( guild.GuildName ) )
						return guild.GuildName;
				}
			}

			return scopeKey;
		}

		var players = ThornsWorldPersistence.Instance?.Live?.PlayersByAccountKey;
		if ( players is not null && players.TryGetValue( scopeKey, out var player ) && !string.IsNullOrWhiteSpace( player.DisplayName ) )
			return player.DisplayName;

		var liveRoot = ThornsPlayerRootCache.TryGetByAccountKey( Scene, scopeKey );
		if ( liveRoot.IsValid() )
		{
			var gameplay = liveRoot.Components.Get<ThornsPlayerGameplay>( FindMode.EnabledInSelf );
			if ( gameplay.IsValid() )
				return gameplay.Network?.Owner?.DisplayName ?? "Player";
		}

		return scopeKey;
	}

	void HostPushAllVictorySnapshots()
	{
		ThornsPlayerRootCache.RefreshIfStale( Scene );

		foreach ( var root in ThornsPlayerRootCache.RootsReadOnly )
		{
			if ( !root.IsValid() )
				continue;

			var gameplay = root.Components.Get<ThornsPlayerGameplay>( FindMode.EnabledInSelf );
			if ( !gameplay.IsValid() || string.IsNullOrWhiteSpace( gameplay.AccountKey ) )
				continue;

			gameplay.HostPushVictorySnapshot();
		}
	}

	static void CopyNested( Dictionary<string, Dictionary<string, long>> source, Dictionary<string, Dictionary<string, long>> dest )
	{
		if ( source is null )
			return;

		foreach ( var (key, inner) in source )
		{
			if ( string.IsNullOrWhiteSpace( key ) || inner is null )
				continue;
			dest[key] = new Dictionary<string, long>( inner, StringComparer.OrdinalIgnoreCase );
		}
	}

	static Dictionary<string, Dictionary<string, long>> CloneNested( Dictionary<string, Dictionary<string, long>> source )
	{
		var clone = new Dictionary<string, Dictionary<string, long>>( StringComparer.Ordinal );
		CopyNested( source, clone );
		return clone;
	}
}
