namespace Terraingen.Player;

using Sandbox.Network;
using Terraingen.GameData;
using Terraingen.Multiplayer;
using Terraingen.NpcGuild;
using Terraingen.UI;
using Terraingen.UI.Presenters;
using Terraingen.Victory;

/// <summary>Host-side guild state. Activity feed is real; announcements stubbed.</summary>
public sealed class ThornsGuildWorldService : Component
{
	public static ThornsGuildWorldService Instance { get; private set; }

	readonly Dictionary<string, ThornsGuildState> _guilds = new();

	public static ThornsGuildWorldService EnsureInstance()
	{
		if ( Instance is not null && Instance.IsValid() )
			return Instance;

		var hostObject = Game.ActiveScene?.GetAllComponents<ThornsWorldPersistence>().FirstOrDefault()?.GameObject
		                 ?? Game.ActiveScene?.GetAllComponents<ThornsNetworkGameManager>().FirstOrDefault()?.GameObject;
		if ( hostObject is null || !hostObject.IsValid() )
			return null;

		return hostObject.Components.Get<ThornsGuildWorldService>()
		       ?? hostObject.Components.Create<ThornsGuildWorldService>();
	}
	readonly Dictionary<string, string> _accountToGuild = new();

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

	void TryImportPendingSave()
	{
		var persistence = ThornsWorldPersistence.Instance;
		if ( persistence is null )
			return;

		persistence.HostEnsureInitialized();
		persistence.SyncGuildsToWorldService();
	}

	public void ImportFromSave( ThornsPersistentWorldDto dto )
	{
		_guilds.Clear();
		_accountToGuild.Clear();

		if ( dto is null )
			return;

		if ( dto.Guilds is not null )
		{
			foreach ( var saved in dto.Guilds )
			{
				if ( saved is null || string.IsNullOrWhiteSpace( saved.GuildId ) )
					continue;

				_guilds[saved.GuildId] = ThornsGuildState.FromPersistent( saved );
			}
		}

		if ( dto.AccountGuildIds is not null )
		{
			foreach ( var (accountKey, guildId) in dto.AccountGuildIds )
			{
				if ( string.IsNullOrWhiteSpace( accountKey ) || string.IsNullOrWhiteSpace( guildId ) )
					continue;

				_accountToGuild[accountKey] = guildId;
			}
		}

		RefreshMemberOnlineStates();
		HostPurgeLegacyShowcaseGuilds();
		HostSeedNpcGuildsIfNeeded();
		ThornsWorldPersistence.Instance?.HostSyncGuildStateToLive();
		Log.Info( $"[Thorns Guild] Restored {_guilds.Count} guild(s) from world save." );
	}

	/// <summary>Removes showcase NPC factions and orphan guild rows left in old saves.</summary>
	void HostPurgeLegacyShowcaseGuilds()
	{
		if ( !ThornsMultiplayer.IsHostOrOffline )
			return;

		var removed = new List<string>();
		foreach ( var guildId in _guilds.Keys.ToList() )
		{
			if ( ShouldRetainGuildRecord( guildId ) )
				continue;

			_guilds.Remove( guildId );
			removed.Add( guildId );
		}

		if ( removed.Count == 0 )
			return;

		foreach ( var accountKey in _accountToGuild.Where( kv => removed.Contains( kv.Value ) ).Select( kv => kv.Key ).ToList() )
			_accountToGuild.Remove( accountKey );

		ThornsVictoryManager.EnsureInstance()?.HostRemoveGuildProgress( removed );
		ThornsVictoryManager.EnsureInstance()?.HostPurgeObsoleteGuildProgress();
		PersistGuilds();
		NotifyGuildMembers();
		Log.Info( $"[Thorns Guild] Removed legacy showcase guild(s): {string.Join( ", ", removed )}." );
	}

	/// <summary>Player guilds with a linked account, plus catalog NPC rivals that exist in-world.</summary>
	bool ShouldRetainGuildRecord( string guildId )
	{
		if ( string.IsNullOrWhiteSpace( guildId ) || !_guilds.ContainsKey( guildId ) )
			return false;

		if ( ThornsNpcGuildCatalog.IsAuthorizedNpcGuildId( guildId ) )
			return true;

		if ( IsNpcGuild( guildId ) || ThornsNpcGuildCatalog.IsNpcGuildId( guildId ) )
			return false;

		return HasLinkedPlayerAccount( guildId );
	}

	bool HasLinkedPlayerAccount( string guildId )
	{
		foreach ( var linkedGuildId in _accountToGuild.Values )
		{
			if ( string.Equals( linkedGuildId, guildId, StringComparison.OrdinalIgnoreCase ) )
				return true;
		}

		return false;
	}

	/// <summary>Player guilds with members and catalog NPC rivals — excludes legacy showcase factions.</summary>
	public bool ShouldIncludeInServerRankings( string guildId ) => ShouldRetainGuildRecord( guildId );

	public void HostEnsureAllCatalogNpcGuilds()
	{
		if ( !ThornsMultiplayer.IsHostOrOffline )
			return;

		var seededAny = false;
		foreach ( var template in ThornsNpcGuildCatalog.All )
			seededAny |= HostEnsureNpcGuildRecord( template.GuildId );

		if ( seededAny )
		{
			PersistGuilds();
			ThornsWorldPersistence.Instance?.HostSyncGuildStateToLive();
		}
	}

	bool HostEnsureNpcGuildRecord( string guildId )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || string.IsNullOrWhiteSpace( guildId ) )
			return false;

		if ( _guilds.ContainsKey( guildId ) )
			return false;

		var template = ThornsNpcGuildCatalog.TryGet( guildId );
		if ( template is null )
			return false;

		var guild = new ThornsGuildState
		{
			GuildId = template.GuildId,
			GuildName = template.GuildName,
			Motto = template.Motto,
			GuildLevel = template.GuildLevel,
			GuildXp = template.GuildXp,
			IsNpcGuild = true,
			Announcement = template.Announcement,
			AnnouncementAuthor = template.AnnouncementAuthor,
			AnnouncementTimestampUtc = DateTime.UtcNow.AddHours( -6 ).ToString( "o" )
		};
		guild.Members.Add( new ThornsGuildMemberDto
		{
			AccountKey = $"{template.GuildId}_leader",
			DisplayName = template.AnnouncementAuthor,
			Rank = "Leader",
			IsOnline = false
		} );
		_guilds[template.GuildId] = guild;
		return true;
	}

	void HostSeedNpcGuildsIfNeeded() => HostEnsureAllCatalogNpcGuilds();

	public IReadOnlyList<string> GetAllGuildIds() => _guilds.Keys.ToList();

	public bool IsNpcGuild( string guildId )
		=> _guilds.TryGetValue( guildId, out var guild ) && guild.IsNpcGuild;

	public bool IsNpcGuildEliminated( string guildId )
		=> _guilds.TryGetValue( guildId, out var guild ) && guild.IsNpcGuild && guild.IsEliminated;

	public int GetMemberCount( string guildId )
		=> _guilds.TryGetValue( guildId, out var guild ) ? guild.Members.Count : 0;

	public string TryResolveGuildDisplayName( string guildId )
	{
		if ( string.IsNullOrWhiteSpace( guildId ) )
			return null;

		return _guilds.TryGetValue( guildId, out var guild ) ? guild.GuildName : null;
	}

	public IReadOnlyList<ThornsGuildActivityDto> BuildWorldActivityFeed( int maxEntries = 24, string exceptGuildId = null )
	{
		return _guilds.Values
			.Where( g => ShouldIncludeInServerRankings( g.GuildId ) )
			.Where( g => string.IsNullOrWhiteSpace( exceptGuildId )
			             || !string.Equals( g.GuildId, exceptGuildId, StringComparison.OrdinalIgnoreCase ) )
			.SelectMany( g => g.Activity )
			.OrderByDescending( a => a.TimestampUtc ?? "" )
			.Take( maxEntries )
			.ToList();
	}

	public void EnrichGuildSnapshot( ThornsGuildSnapshotDto snap, ThornsVictorySnapshot victory )
	{
		if ( snap is null || !snap.InGuild )
			return;

		snap.RivalNpcGuilds = ThornsNpcGuildWorldService.Instance?.BuildAllRivalSnapshots()
		                      ?? new List<ThornsNpcGuildRivalDto>();
		snap.RivalNpcGuild = snap.RivalNpcGuilds.FirstOrDefault( r => r.HasRival && !r.IsEliminated )
		                     ?? snap.RivalNpcGuilds.FirstOrDefault( r => r.HasRival )
		                     ?? new ThornsNpcGuildRivalDto();

		var comparison = victory?.GuildComparisonRows?
			.FirstOrDefault( r => string.Equals( r.GuildId, snap.GuildId, StringComparison.OrdinalIgnoreCase ) );

		snap.Overview = ThornsGuildCommandSnapshotBuilder.BuildOverview( snap, comparison, victory, snap.RivalNpcGuild );
		snap.Command = ThornsGuildCommandSnapshotBuilder.Build( snap.GuildId, victory );

		snap.Command.WorldActivity = BuildMergedActivityFeed( snap, victory );
	}

	static List<ThornsGuildActivityDto> BuildMergedActivityFeed( ThornsGuildSnapshotDto snap, ThornsVictorySnapshot victory )
	{
		var seen = new HashSet<string>( StringComparer.Ordinal );
		var merged = new List<ThornsGuildActivityDto>();

		void TryAdd( ThornsGuildActivityDto entry )
		{
			if ( entry is null )
				return;

			var key = !string.IsNullOrWhiteSpace( entry.EntryId )
				? entry.EntryId
				: $"{entry.Message}|{entry.TimestampUtc}";
			if ( !seen.Add( key ) )
				return;

			merged.Add( entry );
		}

		foreach ( var entry in snap.Activity )
			TryAdd( entry );

		foreach ( var entry in Instance?.BuildWorldActivityFeed( 16, snap.GuildId ) ?? Array.Empty<ThornsGuildActivityDto>() )
			TryAdd( entry );

		if ( victory?.RecentLeadershipChanges is not null )
		{
			foreach ( var change in victory.RecentLeadershipChanges.Where( c => c.Scope == ThornsVictoryScope.Guild ).Take( 8 ) )
			{
				TryAdd( new ThornsGuildActivityDto
				{
					EntryId = $"leadership_{change.PathId}_{change.TimestampUtc}",
					Kind = "victory_shift",
					Message = ThornsVictoryUiPresenter.FormatLeadershipChange( change ),
					TimestampUtc = change.TimestampUtc
				} );
			}
		}

		return merged
			.OrderByDescending( a => a.TimestampUtc ?? "" )
			.Take( 24 )
			.ToList();
	}

	public void HostEnsureNpcRivalGuild( string guildId )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || string.IsNullOrWhiteSpace( guildId ) )
			return;

		if ( HostEnsureNpcGuildRecord( guildId ) )
			PersistGuilds();
	}

	public void HostMarkNpcGuildEliminated( string guildId )
	{
		if ( !_guilds.TryGetValue( guildId, out var guild ) || !guild.IsNpcGuild )
			return;

		guild.IsEliminated = true;
		guild.Announcement = "ELIMINATED — this rival guild has been driven from the wasteland.";
		guild.AnnouncementAuthor = "Wasteland Chronicle";
		guild.AnnouncementTimestampUtc = DateTime.UtcNow.ToString( "o" );
		guild.AddActivity( "dominion", $"{guild.GuildName} has been eliminated." );
		PersistGuilds();
		NotifyGuildMembers();
	}

	public void HostSetNpcOutpostCount( string guildId, int count, int target, bool hasDominionVictory, bool isEliminated )
	{
		if ( !_guilds.TryGetValue( guildId, out var guild ) || !guild.IsNpcGuild )
			return;

		guild.NpcOutpostCount = count;
		guild.NpcOutpostTarget = target;
		guild.HasDominionVictory = hasDominionVictory;
		guild.IsEliminated = isEliminated;

		if ( hasDominionVictory && !isEliminated )
		{
			guild.Announcement = $"DOMINION VICTORY — {count}/{target} outposts secured.";
			guild.AnnouncementAuthor = guild.Members.FirstOrDefault()?.DisplayName ?? "Ragnar";
			guild.AnnouncementTimestampUtc = DateTime.UtcNow.ToString( "o" );
		}
	}

	public void HostAddNpcGuildActivity( string guildId, string kind, string message, bool broadcastWorldAlert = true )
	{
		if ( !_guilds.TryGetValue( guildId, out var guild ) || !guild.IsNpcGuild )
			return;

		guild.AddActivity( kind, message );
		PersistGuilds();
		if ( broadcastWorldAlert )
			HostBroadcastWorldEvent( message );
	}

	public void HostBroadcastOutpostGrowth( string guildName, int count, int target )
	{
		ThornsWorldEventHudBus.PushOutpostGrowth( guildName, count, target );

		if ( Networking.IsActive && Networking.IsHost )
			RpcOutpostGrowthAlert( guildName, count, target );
	}

	[Rpc.Broadcast]
	void RpcOutpostGrowthAlert( string guildName, int count, int target )
	{
		if ( ThornsNetAuthority.RejectClientBroadcastOrigin() )
			return;

		if ( ThornsMultiplayer.IsHostOrOffline )
			return;

		ThornsWorldEventHudBus.PushOutpostGrowth( guildName, count, target );
	}

	void HostBroadcastWorldEvent( string message )
	{
		if ( string.IsNullOrWhiteSpace( message ) )
			return;

		ThornsWorldEventHudBus.PushWorldEvent( message, 8f );

		if ( Networking.IsActive && Networking.IsHost )
			RpcWorldEventFeed( message );
	}

	[Rpc.Broadcast]
	void RpcWorldEventFeed( string message )
	{
		if ( ThornsNetAuthority.RejectClientBroadcastOrigin() )
			return;

		if ( ThornsMultiplayer.IsHostOrOffline )
			return;

		ThornsWorldEventHudBus.PushWorldEvent( message, 8f );
	}

	public static void RefreshAllGuildSnapshotsFromWorld()
	{
		Instance?.NotifyGuildMembers();
		ThornsVictoryManager.EnsureInstance()?.HostFlushSnapshotRefresh();
	}

	public void ExportToSave( ThornsPersistentWorldDto dto )
	{
		if ( dto is null )
			return;

		dto.Guilds = _guilds.Values.Select( g => g.ToPersistent() ).ToList();
		dto.AccountGuildIds = new Dictionary<string, string>( _accountToGuild, StringComparer.Ordinal );
	}

	/// <summary>Online players who are not already in <paramref name="guildId"/>.</summary>
	public IReadOnlyList<(string AccountKey, string DisplayName)> ListInvitableOnlinePlayers( string guildId )
	{
		if ( string.IsNullOrWhiteSpace( guildId ) || Scene is null )
			return Array.Empty<(string, string)>();

		var members = _guilds.TryGetValue( guildId, out var guild )
			? new HashSet<string>( guild.Members.Select( m => m.AccountKey ), StringComparer.Ordinal )
			: new HashSet<string>( StringComparer.Ordinal );

		var list = new List<(string, string)>();
		foreach ( var gameplay in Scene.GetAllComponents<ThornsPlayerGameplay>() )
		{
			if ( !gameplay.IsValid() || string.IsNullOrWhiteSpace( gameplay.AccountKey ) )
				continue;

			if ( members.Contains( gameplay.AccountKey ) )
				continue;

			list.Add( (gameplay.AccountKey, ResolvePlayerDisplayName( gameplay )) );
		}

		return list.OrderBy( p => p.Item2 ).ToList();
	}

	/// <summary>Every player belongs to a guild — create a solo guild named after them when missing.</summary>
	public void HostEnsurePersonalGuild( ThornsPlayerGameplay player )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || player is null || string.IsNullOrWhiteSpace( player.AccountKey ) )
			return;

		if ( _accountToGuild.TryGetValue( player.AccountKey, out var guildId )
		     && _guilds.ContainsKey( guildId ) )
			return;

		if ( _accountToGuild.ContainsKey( player.AccountKey ) )
			_accountToGuild.Remove( player.AccountKey );

		HostCreatePersonalGuild( player );
	}

	public bool TryGetAccountGuildId( string accountKey, out string guildId )
	{
		guildId = "";
		return !string.IsNullOrWhiteSpace( accountKey ) && _accountToGuild.TryGetValue( accountKey, out guildId );
	}

	public ThornsGuildSnapshotDto GetGuildSnapshotFor( string accountKey )
	{
		if ( string.IsNullOrEmpty( accountKey ) || !_accountToGuild.TryGetValue( accountKey, out var guildId ) )
			return new ThornsGuildSnapshotDto();

		if ( !_guilds.TryGetValue( guildId, out var guild ) )
			return new ThornsGuildSnapshotDto();

		return guild.ToSnapshot();
	}

	public void HostCreateGuild( ThornsPlayerGameplay player, string name )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || player is null || string.IsNullOrWhiteSpace( name ) )
			return;

		if ( _accountToGuild.ContainsKey( player.AccountKey ) )
			return;

		HostCreateGuildInternal( player, name.Trim(), $"{ResolvePlayerDisplayName( player )} created the guild." );
	}

	void HostCreatePersonalGuild( ThornsPlayerGameplay player )
	{
		var displayName = ResolvePlayerDisplayName( player );
		HostCreateGuildInternal( player, ResolvePersonalGuildName( player ), $"{displayName} founded their guild." );
	}

	void HostCreateGuildInternal( ThornsPlayerGameplay player, string guildName, string activityMessage )
	{
		if ( string.IsNullOrWhiteSpace( guildName ) )
			return;

		guildName = EnsureUniqueGuildName( guildName.Trim(), exceptGuildId: null );
		if ( guildName.Length > 48 )
			guildName = guildName[..48];

		var guild = new ThornsGuildState
		{
			GuildId = Guid.NewGuid().ToString( "N" )[..8],
			GuildName = guildName,
			Motto = ThornsGuildUiPresenter.DefaultMotto( isNpcGuild: false ),
			Announcement = "Invite allies from Guild Management when you are ready."
		};
		guild.Members.Add( new ThornsGuildMemberDto
		{
			AccountKey = player.AccountKey,
			DisplayName = ResolvePlayerDisplayName( player ),
			Rank = "Leader",
			IsOnline = true
		} );
		guild.AddActivity( "joined", activityMessage );

		_guilds[guild.GuildId] = guild;
		_accountToGuild[player.AccountKey] = guild.GuildId;
		Log.Info( $"[Thorns Guild] Registered '{guild.GuildName}' ({guild.Members.Count} member(s)) for {ResolvePlayerDisplayName( player )}." );
		FinishGuildMutation( player );
	}

	public void HostJoinGuild( ThornsPlayerGameplay player, string guildId )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || player is null || string.IsNullOrEmpty( guildId ) )
			return;

		if ( !_guilds.TryGetValue( guildId, out var guild ) )
			return;

		if ( guild.IsNpcGuild )
			return;

		if ( _accountToGuild.TryGetValue( player.AccountKey, out var currentGuildId ) && currentGuildId == guildId )
			return;

		HostLeaveGuild( player.AccountKey, createPersonalGuildIfHomeless: false );

		var member = new ThornsGuildMemberDto
		{
			AccountKey = player.AccountKey,
			DisplayName = ResolvePlayerDisplayName( player ),
			IsOnline = true
		};
		guild.Members.Add( member );
		guild.AddActivity( "joined", $"{member.DisplayName} joined the guild." );
		_accountToGuild[player.AccountKey] = guildId;
		FinishGuildMutation( player );
	}

	public void HostRequestLeaveGuild( ThornsPlayerGameplay player )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || player is null || string.IsNullOrWhiteSpace( player.AccountKey ) )
			return;

		HostLeaveGuild( player.AccountKey, createPersonalGuildIfHomeless: true );
		FinishGuildMutation( player );
		player.PushClientToastToOwner( "You left the guild.", "info" );
	}

	public void HostInviteToGuild( ThornsPlayerGameplay inviter, string targetAccountKey )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || inviter is null || string.IsNullOrWhiteSpace( targetAccountKey ) )
			return;

		if ( string.Equals( inviter.AccountKey, targetAccountKey, StringComparison.Ordinal ) )
			return;

		if ( !_accountToGuild.TryGetValue( inviter.AccountKey, out var guildId ) )
			return;

		if ( !_guilds.TryGetValue( guildId, out var guild ) )
			return;

		var inviterMember = guild.Members.FirstOrDefault( m => m.AccountKey == inviter.AccountKey );
		if ( inviterMember is null || !string.Equals( inviterMember.Rank, "Leader", StringComparison.OrdinalIgnoreCase ) )
			return;

		if ( guild.Members.Any( m => m.AccountKey == targetAccountKey ) )
			return;

		var target = FindGameplayByAccount( targetAccountKey );
		if ( target is null )
			return;

		HostJoinGuild( target, guildId );
		if ( !_guilds.TryGetValue( guildId, out guild ) )
			return;

		guild.AddActivity( "invited", $"{ResolvePlayerDisplayName( inviter )} invited {ResolvePlayerDisplayName( target )}." );
		PersistGuilds();
		NotifyGuildMembers();
	}

	void HostLeaveGuild( string accountKey, bool createPersonalGuildIfHomeless )
	{
		if ( string.IsNullOrWhiteSpace( accountKey ) || !_accountToGuild.TryGetValue( accountKey, out var guildId ) )
			return;

		if ( !_guilds.TryGetValue( guildId, out var guild ) )
		{
			_accountToGuild.Remove( accountKey );
			return;
		}

		var member = guild.Members.FirstOrDefault( m => m.AccountKey == accountKey );
		if ( member is not null )
		{
			guild.Members.Remove( member );
			guild.AddActivity( "left", $"{member.DisplayName} left the guild." );
		}

		_accountToGuild.Remove( accountKey );

		if ( guild.Members.Count == 0 )
			_guilds.Remove( guildId );

		if ( createPersonalGuildIfHomeless )
		{
			var player = FindGameplayByAccount( accountKey );
			if ( player is not null )
				HostEnsurePersonalGuild( player );
		}
	}

	public void HostKick( string actorAccountKey, string targetAccountKey )
	{
		if ( !_accountToGuild.TryGetValue( targetAccountKey, out var guildId ) )
			return;

		if ( !_guilds.TryGetValue( guildId, out var guild ) )
			return;

		var removed = guild.Members.FirstOrDefault( m => m.AccountKey == targetAccountKey );
		guild.Members.RemoveAll( m => m.AccountKey == targetAccountKey );
		guild.AddActivity( "kicked", removed is not null ? $"{removed.DisplayName} was removed." : $"{targetAccountKey} was removed." );
		_accountToGuild.Remove( targetAccountKey );

		if ( guild.Members.Count == 0 )
			_guilds.Remove( guildId );

		var kickedPlayer = FindGameplayByAccount( targetAccountKey );
		if ( kickedPlayer is not null )
			HostEnsurePersonalGuild( kickedPlayer );

		PersistGuilds();
		NotifyGuildMembers();
	}

	public void HostRenameGuild( ThornsPlayerGameplay player, string name )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || player is null || string.IsNullOrWhiteSpace( name ) )
			return;

		if ( !_accountToGuild.TryGetValue( player.AccountKey, out var guildId ) )
			return;

		if ( !_guilds.TryGetValue( guildId, out var guild ) )
			return;

		var member = guild.Members.FirstOrDefault( m => m.AccountKey == player.AccountKey );
		if ( member is null || !string.Equals( member.Rank, "Leader", StringComparison.OrdinalIgnoreCase ) )
			return;

		var trimmed = name.Trim();
		if ( trimmed.Length > 48 )
			trimmed = trimmed[..48];

		if ( string.Equals( guild.GuildName, trimmed, StringComparison.Ordinal ) )
			return;

		if ( IsGuildNameTaken( trimmed, guildId ) )
			return;

		guild.GuildName = trimmed;
		guild.AddActivity( "renamed", $"{member.DisplayName} renamed the guild to \"{trimmed}\"." );
		FinishGuildMutation( player );
	}

	public void HostUpdateAnnouncement( ThornsPlayerGameplay player, string message )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || player is null )
			return;

		if ( !_accountToGuild.TryGetValue( player.AccountKey, out var guildId ) )
			return;

		if ( !_guilds.TryGetValue( guildId, out var guild ) )
			return;

		var member = guild.Members.FirstOrDefault( m => m.AccountKey == player.AccountKey );
		if ( member is null || !string.Equals( member.Rank, "Leader", StringComparison.OrdinalIgnoreCase ) )
			return;

		var trimmed = (message ?? "").Trim();
		if ( trimmed.Length > 512 )
			trimmed = trimmed[..512];

		guild.Announcement = trimmed;
		guild.AnnouncementAuthor = member.DisplayName;
		guild.AnnouncementTimestampUtc = DateTime.UtcNow.ToString( "o" );
		guild.AddActivity( "notice", $"{member.DisplayName} updated the guild notice." );
		FinishGuildMutation( player );
	}

	public void HostPromote( string targetAccountKey, string rank )
	{
		if ( !_accountToGuild.TryGetValue( targetAccountKey, out var guildId ) )
			return;

		if ( !_guilds.TryGetValue( guildId, out var guild ) )
			return;

		var m = guild.Members.FirstOrDefault( x => x.AccountKey == targetAccountKey );
		if ( m is null )
			return;

		m.Rank = rank;
		guild.AddActivity( "promoted", $"{m.DisplayName} promoted to {rank}." );
		PersistGuilds();
	}

	void FinishGuildMutation( ThornsPlayerGameplay player )
	{
		RefreshMemberOnlineStates();
		NotifyGuildMembers();
		PersistGuilds();
		player?.RefreshGuildFromWorld();
	}

	void PersistGuilds() => ThornsWorldPersistence.RequestSave();

	void RefreshMemberOnlineStates()
	{
		if ( Scene is null )
			return;

		var onlineKeys = new HashSet<string>( StringComparer.Ordinal );
		foreach ( var gameplay in Scene.GetAllComponents<ThornsPlayerGameplay>() )
		{
			if ( gameplay.IsValid() && !string.IsNullOrEmpty( gameplay.AccountKey ) )
				onlineKeys.Add( gameplay.AccountKey );
		}

		foreach ( var guild in _guilds.Values )
		{
			foreach ( var member in guild.Members )
			{
				member.IsOnline = onlineKeys.Contains( member.AccountKey );
				var gameplay = FindGameplayByAccount( member.AccountKey );
				if ( gameplay is not null )
					member.DisplayName = ResolvePlayerDisplayName( gameplay );
			}
		}
	}

	static string ResolvePersonalGuildName( ThornsPlayerGameplay player )
	{
		_ = player;
		return "Survivor Company";
	}

	bool IsGuildNameTaken( string guildName, string exceptGuildId )
	{
		if ( string.IsNullOrWhiteSpace( guildName ) )
			return false;

		foreach ( var guild in _guilds.Values )
		{
			if ( !string.IsNullOrWhiteSpace( exceptGuildId )
			     && string.Equals( guild.GuildId, exceptGuildId, StringComparison.OrdinalIgnoreCase ) )
				continue;

			if ( string.Equals( guild.GuildName, guildName, StringComparison.OrdinalIgnoreCase ) )
				return true;
		}

		return false;
	}

	string EnsureUniqueGuildName( string desiredName, string exceptGuildId )
	{
		var trimmed = desiredName.Trim();
		if ( string.IsNullOrWhiteSpace( trimmed ) )
			trimmed = "Survivor";

		if ( !IsGuildNameTaken( trimmed, exceptGuildId ) )
			return trimmed;

		for ( var i = 2; i < 100; i++ )
		{
			var suffix = $" ({i})";
			var maxBase = Math.Max( 1, 48 - suffix.Length );
			var candidate = trimmed.Length > maxBase ? trimmed[..maxBase] + suffix : trimmed + suffix;
			if ( !IsGuildNameTaken( candidate, exceptGuildId ) )
				return candidate;
		}

		var token = Guid.NewGuid().ToString( "N" )[..4];
		var fallbackMax = Math.Max( 1, 48 - token.Length - 1 );
		return $"{trimmed[..Math.Min( trimmed.Length, fallbackMax )]}_{token}";
	}

	ThornsPlayerGameplay FindGameplayByAccount( string accountKey )
	{
		if ( Scene is null || string.IsNullOrWhiteSpace( accountKey ) )
			return null;

		foreach ( var gameplay in Scene.GetAllComponents<ThornsPlayerGameplay>() )
		{
			if ( gameplay.IsValid() && string.Equals( gameplay.AccountKey, accountKey, StringComparison.Ordinal ) )
				return gameplay;
		}

		return null;
	}

	void NotifyGuildMembers()
	{
		if ( Scene is null )
			return;

		foreach ( var gameplay in Scene.GetAllComponents<ThornsPlayerGameplay>() )
		{
			if ( !gameplay.IsValid() || string.IsNullOrEmpty( gameplay.AccountKey ) )
				continue;

			if ( !_accountToGuild.TryGetValue( gameplay.AccountKey, out var guildId ) )
				continue;

			if ( _guilds.ContainsKey( guildId ) )
				gameplay.RefreshGuildFromWorld();
		}
	}

	static string ResolvePlayerDisplayName( ThornsPlayerGameplay player )
	{
		if ( player is null || !player.IsValid() )
			return "Player";

		return player.Network?.Owner?.DisplayName ?? "Player";
	}

	sealed class ThornsGuildState
	{
		public string GuildId { get; set; } = "";
		public string GuildName { get; set; } = "";
		public int GuildLevel { get; set; } = 1;
		public float GuildXp { get; set; }
		public string Motto { get; set; } = "";
		public bool IsNpcGuild { get; set; }
		public bool IsEliminated { get; set; }
		public bool HasDominionVictory { get; set; }
		public int NpcOutpostCount { get; set; }
		public int NpcOutpostTarget { get; set; } = 10;
		public string Announcement { get; set; } = "";
		public string AnnouncementAuthor { get; set; } = "";
		public string AnnouncementTimestampUtc { get; set; } = "";
		public List<ThornsGuildMemberDto> Members { get; set; } = new();
		public List<ThornsGuildActivityDto> Activity { get; set; } = new();

		public static ThornsGuildState FromPersistent( ThornsPersistentGuildDto dto ) => new()
		{
			GuildId = dto.GuildId,
			GuildName = dto.GuildName,
			GuildLevel = dto.GuildLevel,
			GuildXp = dto.GuildXp,
			Motto = dto.Motto ?? "",
			IsNpcGuild = dto.IsNpcGuild,
			IsEliminated = dto.IsEliminated,
			HasDominionVictory = dto.HasDominionVictory,
			NpcOutpostCount = dto.NpcOutpostCount,
			NpcOutpostTarget = dto.NpcOutpostTarget,
			Announcement = dto.Announcement ?? "",
			AnnouncementAuthor = dto.AnnouncementAuthor ?? "",
			AnnouncementTimestampUtc = dto.AnnouncementTimestampUtc ?? "",
			Members = dto.Members?.ToList() ?? new List<ThornsGuildMemberDto>(),
			Activity = dto.Activity?.ToList() ?? new List<ThornsGuildActivityDto>()
		};

		public ThornsPersistentGuildDto ToPersistent() => new()
		{
			GuildId = GuildId,
			GuildName = GuildName,
			GuildLevel = GuildLevel,
			GuildXp = GuildXp,
			Motto = Motto,
			IsNpcGuild = IsNpcGuild,
			IsEliminated = IsEliminated,
			HasDominionVictory = HasDominionVictory,
			NpcOutpostCount = NpcOutpostCount,
			NpcOutpostTarget = NpcOutpostTarget,
			Announcement = Announcement,
			AnnouncementAuthor = AnnouncementAuthor,
			AnnouncementTimestampUtc = AnnouncementTimestampUtc,
			Members = Members.ToList(),
			Activity = Activity.ToList()
		};

		public void AddActivity( string kind, string message )
		{
			Activity.Insert( 0, new ThornsGuildActivityDto
			{
				EntryId = Guid.NewGuid().ToString( "N" ),
				Kind = kind,
				Message = message,
				TimestampUtc = DateTime.UtcNow.ToString( "o" )
			} );

			if ( Activity.Count > 40 )
				Activity.RemoveAt( Activity.Count - 1 );
		}

		public ThornsGuildSnapshotDto ToSnapshot() => new()
		{
			InGuild = true,
			GuildId = GuildId,
			GuildName = GuildName,
			GuildLevel = GuildLevel,
			GuildXp = GuildXp,
			GuildXpToNext = 12000f,
			Motto = string.IsNullOrWhiteSpace( Motto ) ? ThornsGuildUiPresenter.DefaultMotto( IsNpcGuild ) : Motto,
			IsNpcGuild = IsNpcGuild,
			BannerIconPath = ThornsGuildUiCatalog.GuildEmblemPath,
			Announcement = Announcement,
			Notice = new ThornsGuildNoticeDto
			{
				Message = Announcement,
				AuthorName = AnnouncementAuthor,
				TimestampUtc = AnnouncementTimestampUtc
			},
			Members = Members.ToList(),
			Activity = Activity.ToList()
		};
	}
}
