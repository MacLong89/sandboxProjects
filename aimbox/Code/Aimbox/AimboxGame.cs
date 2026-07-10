namespace Sandbox;

// Rebuild stamp: 2026-07-08 — forces s&box editor to recompile after AIM mode changes.

[Title( "Aimbox Game" )]
[Category( "Aimbox" )]
public sealed class AimboxGame : Component, Component.INetworkListener
{
	public static AimboxGame Instance { get; private set; }

	[Property] public AimboxGameMode DefaultMode { get; set; }
	[Property] public bool AutoCreateLocalPlayer { get; set; } = true;
	[Property] public int BotCount { get; set; } = 3;
	[Property] public bool SkipMetaMenu { get; set; }
	[Property] public bool SkipBots { get; set; }
	[Property] public bool AutoStartMatch { get; set; }
	[Property] public bool DebugSandboxLoadout { get; set; }
	[Property, Title( "Gun builder scene — skip loadout; Aimbox Gun Builder drives weapon/attachments" )]
	public bool GunBuilderScene { get; set; }
	[Property, Title( "TP weapon lab scene — dummy + inspector tuning for third-person world guns" )]
	public bool ThirdPersonWeaponLabScene { get; set; }
	[Property, Title( "Dev only — unlock all weapons, perks, and compatible attachments" )]
	public bool DebugUnlockAllProgression { get; set; }
	[Property, Title( "Dev only — wipe local player + gun mastery save on next boot" )]
	public bool ResetProgressOnBoot { get; set; }
	[Property, Title( "Dev only — start with hitbox overlay enabled (H toggles anytime)" )]
	public bool EnableHitboxDebug { get; set; }
	[Property, Title( "Dev only — in-game optic ADS tuning (bracket key) and M700 PiP tuning (O key)" )]
	public bool EnableOpticAdsTuners { get; set; }
	[Property, Title( "Dev only — log M700 draw calls at ADS + PiP vs lens on fire" )]
	public bool EnableM700ScopeInvestigation { get; set; }
	[Property] public AimboxArenaMap ActiveArenaMap { get; set; }

	public AimboxMatchSystem Match { get; } = new();
	public AimboxScoreboardView LastScoreboard { get; private set; }
	public AimboxRespawnSystem Respawns { get; } = new();
	public AimboxHitscanSystem Hitscan { get; } = new();
	public AimboxDamageSystem Damage { get; } = new();
	public AimboxXpSystem Xp { get; } = new();
	public AimboxWeaponProgressionSystem WeaponProgression { get; } = new();
	public AimboxAttachmentUnlockSystem AttachmentUnlocks { get; } = new();
	public AimboxChallengeSystem Challenges { get; } = new();
	public AimboxMedalSystem Medals { get; } = new();
	public AimboxRankedSystem Ranked { get; } = new();
	public AimboxLeaderboardSystem Leaderboards { get; } = new();
	public AimboxKillstreakSystem Killstreaks { get; } = new();
	public AimboxGrenadeSystem Grenades { get; } = new();
	public AimboxKillFeedSystem KillFeed { get; } = new();
	public IAimboxDatabase PlayerData { get; private set; }
	public AimboxPlayerDataService PlayerDataService { get; private set; }
	public AimboxLoadoutPersistenceService Loadouts { get; } = new();
	public AimboxRankedPersistenceService RankedPersistence { get; } = new();
	public AimboxChallengePersistenceService ChallengePersistence { get; } = new();
	public AimboxStatsPersistenceService StatsPersistence { get; } = new();
	public AimboxMatchHistoryService MatchHistory { get; } = new();
	public AimboxSaveQueueSystem Saves { get; private set; }
	public IReadOnlyList<AimboxPlayerController> Players => _players;
	public IReadOnlyList<AimboxBotController> Bots => _bots;
	public int HumanPlayerCount => _players.Count( p => !p.IsProxy );
	public bool UsesLobbyMapVote => HumanPlayerCount > 1;
	public bool IsLobbyHost => AimboxLobbyAuthority.IsHost;
	public bool IsLobbyJoiner => AimboxLobbyAuthority.IsJoiner;
	public string LobbyHostAccountId => AimboxLobbyAuthority.HostAccountId;

	public AimboxLobbyState Lobby { get; } = new();
	public AimboxSessionPhase Phase { get; private set; } = AimboxSessionPhase.Intermission;
	public AimboxGameMode NextMode { get; private set; } = AimboxGameMode.FreeForAll;
	public float IntermissionTimeRemaining { get; private set; }
	public bool IsLocalReady { get; private set; }
	public float ReadyCountdownRemaining { get; private set; }
	public bool AwaitingFirstMatch { get; private set; }
	public bool IsMetaUiActive => _metaUiObject.IsValid() && _metaUiObject.Enabled;
	public bool IsMatchFrozen => _freezeActive;
	public bool IsDuelRoundResetPending => _duelRoundResetPending;
	public bool IsCombatLocked => _freezeActive || _duelRoundResetPending;
	public bool IsAttachmentLabScene => GunBuilderScene || ThirdPersonWeaponLabScene;
	public float FreezeTimeRemaining { get; private set; }
	public string FreezeLabel { get; private set; } = "FREEZE TIME";

	readonly List<AimboxPlayerController> _players = [];
	readonly List<AimboxBotController> _bots = [];
	GameObject _metaUiObject;
	GameObject _arenaAnchorGo;
	AimboxArenaMap _builtArenaMap = (AimboxArenaMap)(-1);
	int _builtArenaLayoutSignature;
	bool _builtAimRoom;
	int _metaUiComponentGeneration = 0;
	const int MetaUiComponentGeneration = 6;
	TimeSince _lastSaveFlush;
	TimeSince _intermissionStarted;
	TimeSince _readyCountdownStarted;
	bool _readyCountdownActive;
	bool _pendingAutoStart;
	bool _freezeActive;
	float _freezeDuration;
	TimeSince _freezeStarted;
	bool _duelRoundResetPending;
	TimeSince _duelRoundResetStarted;
	readonly AimboxLocalCombatAuthority _combatAuthority = new();
	bool _weaponPackagesReady;
	bool _weaponPackagesMounting;

	static readonly string[] WeaponPackageIdents =
	[
		"facepunch.sboxweapons",
		"facepunch.reddotrmr",
		"facepunch.reddotrmrraised",
		"facepunch.sightholographic",
		"facepunch.suppressor9mm",
		"facepunch.foregripstraight",
	];

	public bool WeaponPackagesReady => _weaponPackagesReady;

	protected override void OnStart()
	{
		Instance = this;
		StartWeaponPackageMount();
		Log.Info( "[Aimbox] Game OnStart: booting core systems." );
		PlayerData = new JsonFileAimboxDatabase();
		PlayerDataService = new AimboxPlayerDataService( PlayerData );
		Saves = new AimboxSaveQueueSystem( PlayerData );
		EnsureHitboxDebug();
		AimboxCombatTracerService.EnsureForScene( Scene );
		AimboxOpticAdsTuner.TunerEnabled = EnableOpticAdsTuners;
		AimboxM700ScopePipTuner.TunerEnabled = EnableOpticAdsTuners;
		AimboxM700ScopeInvestigationDebug.Enabled = EnableM700ScopeInvestigation;
		NextMode = DefaultMode;
		AwaitingFirstMatch = true;
		Phase = AimboxSessionPhase.Intermission;
		IsLocalReady = false;
		IntermissionTimeRemaining = 0f;

		ResetMetaUiOnBoot();
		EnsureMetaUi();
		Log.Info( "[Aimbox] Lobby map vote: YARD, DOCKS, VAULT, JUNCTION, STACK, CANAL." );
		if ( SkipMetaMenu )
		{
			AimboxMetaNavigation.LeaveIntermission();
			if ( _metaUiObject.IsValid() )
				_metaUiObject.Enabled = false;
		}
		else
		{
			Lobby.ResetForLobby();
			ApplyLobbyBotPresence();
			AimboxMetaNavigation.EnterLobby();
		}

		EnsureValidatedLobbyMap();
		EnsureArena();

		_ = FinishBootAfterWeaponPackagesAsync();

		if ( ResetProgressOnBoot )
			ResetLocalProgress();
	}

	async Task FinishBootAfterWeaponPackagesAsync()
	{
		await WaitForWeaponPackagesAsync();

		TrySpawnLocalPlayer();
		SyncViewCamera();

		if ( GunBuilderScene || ThirdPersonWeaponLabScene )
		{
			_pendingAutoStart = false;
			Phase = AimboxSessionPhase.Playing;
			HandoffToGameplayCamera();
			Log.Info( $"[Aimbox] Attachment lab boot — phase={Phase}, meta skipped={SkipMetaMenu}." );
		}
		else if ( !SkipBots && BotCount > 0 && ( !Networking.IsActive || IsLobbyHost ) )
			AimboxBotSpawner.EnsureBots( Scene, BotCount );

		if ( AutoStartMatch && !IsAttachmentLabScene )
			_pendingAutoStart = true;

		if ( SkipMetaMenu && !IsAttachmentLabScene )
			Log.Info( $"[Aimbox] Tuning scene boot. Match phase={Phase}. Meta menu skipped." );
		else if ( GunBuilderScene )
			Log.Info( "[Aimbox] Gun builder scene — use Aimbox Gun Builder in the inspector to swap weapons and attachments." );
		else if ( ThirdPersonWeaponLabScene )
			Log.Info( "[Aimbox] TP weapon lab — select TP Weapon Lab, swap weapon and edit hand-attach transform live." );
		else
			Log.Info( $"[Aimbox] Match lobby open. Default mode is {DefaultMode}. Scene has {Scene.GetAllComponents<PanelComponent>().Count()} panel component(s)." );
	}

	protected override void OnDestroy()
	{
		Saves?.FlushAll();
		if ( Instance == this )
			Instance = null;
	}

	protected override void OnUpdate()
	{
		TickPendingAutoStart();
		AimboxMetaNavigation.ApplyPresentationState();
		SyncViewCamera();

		if ( _lastSaveFlush > 0.5f )
		{
			_lastSaveFlush = 0;
			Saves.FlushOne();
		}

		if ( Match.ConsumeSurvivalWaveAdvance() )
			OnSurvivalWaveAdvanced();

		if ( Phase == AimboxSessionPhase.Playing && Match.Mode == AimboxGameMode.Survival && Match.IsRunning && !Match.SurvivalComplete )
			TickSurvivalWaveClear();

		if ( Phase == AimboxSessionPhase.Intermission )
		{
			TickIntermission();
			return;
		}

		if ( Phase != AimboxSessionPhase.Playing )
			return;

		TickMatchFreeze();
		TickDuelRoundReset();

		if ( Match.ShouldEnd() )
			EndMatch();
	}

	protected override void OnPreRender()
	{
		AimboxCursor.Sync();
	}

	void TickMatchFreeze()
	{
		if ( !_freezeActive )
			return;

		FreezeTimeRemaining = MathF.Max( 0f, _freezeDuration - _freezeStarted.Relative );
		if ( _freezeStarted < _freezeDuration )
			return;

		_freezeActive = false;
		FreezeTimeRemaining = 0f;
		if ( !Match.IsClockRunning && Match.Mode is not AimboxGameMode.Range )
			Match.StartClock();
	}

	void TickDuelRoundReset()
	{
		if ( !_duelRoundResetPending )
			return;

		if ( Networking.IsActive && !Networking.IsHost )
			return;

		if ( _duelRoundResetStarted < AimboxArenaConfig.DuelRoundResetSeconds )
			return;

		_duelRoundResetPending = false;
		RespawnDuelPair();
		BeginMatchFreeze( AimboxArenaConfig.DuelRoundFreezeSeconds, "ROUND START" );
	}

	public void BeginMatchFreeze( float seconds, string label = "FREEZE TIME" )
	{
		if ( seconds <= 0f )
		{
			if ( !Match.IsClockRunning && Match.Mode is not AimboxGameMode.Range )
				Match.StartClock();
			return;
		}

		_freezeActive = true;
		_freezeDuration = seconds;
		_freezeStarted = 0;
		FreezeLabel = label;
		FreezeTimeRemaining = seconds;
		Match.StopClock();
	}

	public void OnDuelRoundEnded( IAimboxCombatActor attacker, IAimboxCombatActor victim )
	{
		if ( Match.Mode != AimboxGameMode.Duel || Match.ShouldEnd() || _duelRoundResetPending )
			return;

		_duelRoundResetPending = true;
		_duelRoundResetStarted = 0;
		Log.Info( $"[Aimbox] Duel round {Match.DuelRound} won by {attacker.CombatId}." );
	}

	public void OnSurvivalPlayerEliminated( AimboxPlayerController player )
	{
		if ( Match.Mode != AimboxGameMode.Survival || Match.ShouldEnd() || player is null || player.IsProxy )
			return;

		Match.NotifySurvivalFailed();
		Log.Info( $"[Aimbox] Survival failed on wave {Match.SurvivalWave} — {player.AccountId} eliminated." );
	}

	void RespawnDuelPair()
	{
		foreach ( var player in _players )
			player.Respawn();

		foreach ( var bot in _bots )
			bot.Respawn();
	}

	void TickPendingAutoStart()
	{
		if ( !_pendingAutoStart || Phase != AimboxSessionPhase.Intermission )
			return;

		if ( Networking.IsActive && !Networking.IsHost )
			return;

		foreach ( var player in Scene.GetAllComponents<AimboxPlayerController>() )
		{
			if ( player.IsProxy || player.Data is null )
				continue;

			_pendingAutoStart = false;
			StartMatch( DefaultMode );
			return;
		}
	}

	public void StartMatch( AimboxGameMode mode, bool authoritative = true )
	{
		EnsureValidatedLobbyMap();
		EnsureArena( forMode: mode );

		Phase = AimboxSessionPhase.Starting;
		NextMode = mode;
		Match.Start( mode );

		if ( authoritative )
			PrepareActorsForMode( mode );

		foreach ( var player in Scene.GetAllComponents<AimboxPlayerController>() )
		{
			if ( !player.IsProxy )
				player.BeginMatch();
		}

		if ( authoritative )
		{
			if ( mode == AimboxGameMode.Survival )
			{
				Respawns.BeginSpreadAssignment();
				foreach ( var player in _players )
					player.Respawn();
				Respawns.EndSpreadAssignment();
				SpawnSurvivalWave();
			}
			else
			{
				RespawnAllCombatActors();
			}
		}

		HandoffToGameplayCamera();
		AimboxMetaNavigation.LeaveIntermission();

		Phase = AimboxSessionPhase.Playing;
		ApplyArenaMatchPacing();
		BeginMatchFreeze( ResolveFreezeTimeSeconds() );

		AimboxMetaNavigation.ApplyPresentationState();
		LogMatchSpawnState();
	}

	public void EndMatch()
	{
		if ( Phase != AimboxSessionPhase.Playing )
			return;

		LastScoreboard = AimboxScoreboardBuilder.Snapshot( this );
		var winners = Match.Finish( _players );
		Log.Info( $"Aimbox match ended. Winners: {string.Join( ", ", winners )}" );

		_freezeActive = false;
		_duelRoundResetPending = false;
		FreezeTimeRemaining = 0f;

		Phase = AimboxSessionPhase.Intermission;
		NextMode = Match.Mode;
		AwaitingFirstMatch = false;
		IsLocalReady = false;
		_readyCountdownActive = false;
		ReadyCountdownRemaining = 0f;
		_intermissionStarted = 0;
		IntermissionTimeRemaining = AimboxMetaNavigation.IntermissionDurationSeconds;
		ResetLobbyTeamPicks();

		foreach ( var player in _players )
		{
			var won = winners.Contains( player.AccountId );
			player.FinishMatch( won );
			var summary = player.BuildMatchSummary( won );
			MatchHistory.Add( new AimboxMatchHistoryEntry
			{
				Mode = summary.Mode,
				AccountId = summary.AccountId,
				Won = summary.Won,
				Kills = summary.Kills,
				Deaths = summary.Deaths,
				XpEarned = summary.RankXpEarned
			} );

			if ( !player.IsProxy )
				AimboxMetaNavigation.EnterIntermission( summary );
		}

		if ( Match.Mode == AimboxGameMode.Duel && _players.Count >= 2 )
		{
			var ordered = _players.Where( p => !p.IsProxy )
				.OrderByDescending( p => Match.PlayerKills.GetValueOrDefault( p.AccountId ) )
				.ToList();
			if ( ordered.Count >= 2 )
				Ranked.ApplyDuelResult( ordered[0].Data, ordered[1].Data );
		}
	}

	public void SetNextMode( AimboxGameMode mode )
	{
		if ( Phase != AimboxSessionPhase.Intermission || !IsLobbyHost )
			return;

		NextMode = mode;
		ResetLobbyTeamPicks();
		ApplyLobbyBotPresence();
	}

	public void SelectLobbyTeam( AimboxTeam team )
	{
		if ( Phase != AimboxSessionPhase.Intermission )
			return;

		if ( !AimboxLobbyTeamRules.UsesTeamSelect( NextMode ) )
			return;

		var local = _players.FirstOrDefault( p => !p.IsProxy );
		if ( local is null )
			return;

		Lobby.SetTeamPick( local.AccountId, team );
		local.Team = team;
	}

	void ResetLobbyTeamPicks()
	{
		Lobby.ResetTeamPicks();

		foreach ( var player in _players )
			player.Team = AimboxTeam.None;

		foreach ( var bot in _bots )
			bot.Team = AimboxTeam.None;
	}

	void FinalizeLobbyTeams( AimboxGameMode mode )
	{
		if ( !AimboxLobbyTeamRules.UsesTeamSelect( mode ) )
		{
			foreach ( var player in _players )
				player.Team = AimboxTeam.None;

			foreach ( var bot in _bots )
				bot.Team = AimboxTeam.None;

			return;
		}

		var redCount = 0;
		var blueCount = 0;
		var unassignedPlayers = new List<AimboxPlayerController>();
		var unassignedBots = new List<AimboxBotController>();

		foreach ( var player in _players )
		{
			var pick = Lobby.GetTeamPick( player.AccountId );
			if ( pick is AimboxTeam.Red or AimboxTeam.Blue )
			{
				player.Team = pick;
				if ( pick == AimboxTeam.Red )
					redCount++;
				else
					blueCount++;
			}
			else
			{
				player.Team = AimboxTeam.None;
				unassignedPlayers.Add( player );
			}
		}

		foreach ( var bot in _bots )
		{
			var pick = Lobby.GetTeamPick( bot.BotId );
			if ( pick is AimboxTeam.Red or AimboxTeam.Blue )
			{
				bot.Team = pick;
				if ( pick == AimboxTeam.Red )
					redCount++;
				else
					blueCount++;
			}
			else
			{
				bot.Team = AimboxTeam.None;
				unassignedBots.Add( bot );
			}
		}

		foreach ( var player in unassignedPlayers )
		{
			var team = PickLobbyBalancedTeam( mode, redCount, blueCount );
			player.Team = team;

			if ( team == AimboxTeam.Red )
				redCount++;
			else
				blueCount++;
		}

		foreach ( var bot in unassignedBots )
		{
			var team = PickLobbyBalancedTeam( mode, redCount, blueCount );
			bot.Team = team;

			if ( team == AimboxTeam.Red )
				redCount++;
			else
				blueCount++;
		}
	}

	AimboxTeam PickLobbyBalancedTeam( AimboxGameMode mode, int redCount, int blueCount )
	{
		if ( mode == AimboxGameMode.TeamDeathmatch )
		{
			if ( redCount >= AimboxArenaConfig.TdmRosterPerTeam && blueCount < AimboxArenaConfig.TdmRosterPerTeam )
				return AimboxTeam.Blue;

			if ( blueCount >= AimboxArenaConfig.TdmRosterPerTeam && redCount < AimboxArenaConfig.TdmRosterPerTeam )
				return AimboxTeam.Red;
		}

		return redCount <= blueCount ? AimboxTeam.Red : AimboxTeam.Blue;
	}

	public void VoteLobbyMap( string mapId )
	{
		if ( Phase != AimboxSessionPhase.Intermission )
			return;

		mapId = AimboxPlayLobbyUiHelpers.NormalizeMapId( mapId );

		if ( !AimboxPlayLobbyUiHelpers.IsMapPlayable( mapId ) )
			return;

		if ( !UsesLobbyMapVote && !IsLobbyHost )
			return;

		var voter = _players.FirstOrDefault( p => !p.IsProxy )?.AccountId ?? "offline";
		var previousMap = ActiveArenaMap;
		if ( UsesLobbyMapVote )
			Lobby.CastMapVote( mapId, voter );
		else
			Lobby.ForceSelectedMap( mapId );

		ApplyLobbyArenaMap();

		var activeDef = AimboxMapCatalog.Get( ActiveArenaMap );
		if ( previousMap == ActiveArenaMap
		     && _builtArenaMap == ActiveArenaMap
		     && _builtArenaLayoutSignature == activeDef.LayoutSignature
		     && AimboxArenaWorld.CountArenaBlocks() > 0 )
			return;

		EnsureArena( forceRebuild: previousMap != ActiveArenaMap );
		Log.Info( UsesLobbyMapVote
			? $"[Aimbox] Lobby map vote: {mapId} -> {ActiveArenaMap}."
			: $"[Aimbox] Lobby map selected: {mapId} -> {ActiveArenaMap}." );
	}

	public void ApplyLobbyBotPresence()
	{
		if ( Phase != AimboxSessionPhase.Intermission || !IsLobbyHost )
			return;

		var count = Lobby.BotsEnabled && !SkipBots
			? Math.Max( 0, TargetBotCountForMode( NextMode ) )
			: 0;

		AimboxBotSpawner.SetBotCount( Scene, count );
	}

	public void SetLocalReady()
	{
		if ( Phase != AimboxSessionPhase.Intermission || IsLocalReady || !IsLobbyHost )
			return;

		BeginMatchCountdown();
		if ( Networking.IsActive )
			RpcNotifyMatchStarting( NextMode );
	}

	public void ContinueAfterPostMatch() => SetLocalReady();

	void BeginMatchCountdown()
	{
		IsLocalReady = true;
		_readyCountdownActive = true;
		_readyCountdownStarted = 0;
		ReadyCountdownRemaining = AimboxMetaNavigation.ReadyCountdownSeconds;
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void RpcNotifyMatchStarting( AimboxGameMode mode )
	{
		if ( IsLobbyHost || Phase != AimboxSessionPhase.Intermission || IsLocalReady )
			return;

		NextMode = mode;
		BeginMatchCountdown();
		AimboxMetaNavigation.OpenScreen( AimboxMetaScreen.PostMatch );
		Log.Info( $"[Aimbox] Host started match countdown: {AimboxGameModeLabels.Long( mode )}." );
	}

	public void StartNextMatch()
	{
		if ( Phase != AimboxSessionPhase.Intermission )
			return;

		LastScoreboard = null;
		IsLocalReady = false;
		_readyCountdownActive = false;
		ReadyCountdownRemaining = 0f;
		AwaitingFirstMatch = false;

		if ( Networking.IsActive && !Networking.IsHost )
			return;

		StartMatch( NextMode, authoritative: true );

		if ( Networking.IsActive )
			RpcBeginMatch( NextMode );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void RpcBeginMatch( AimboxGameMode mode )
	{
		if ( Networking.IsHost || Phase != AimboxSessionPhase.Intermission )
			return;

		LastScoreboard = null;
		IsLocalReady = false;
		_readyCountdownActive = false;
		ReadyCountdownRemaining = 0f;
		AwaitingFirstMatch = false;

		StartMatch( mode, authoritative: false );
		Log.Info( $"[Aimbox] Match started by host: {AimboxGameModeLabels.Long( mode )}." );
	}

	void RespawnAllCombatActors()
	{
		Respawns.BeginSpreadAssignment();
		foreach ( var player in Scene.GetAllComponents<AimboxPlayerController>() )
			player.Respawn();
		foreach ( var bot in Scene.GetAllComponents<AimboxBotController>() )
			bot.Respawn();
		Respawns.EndSpreadAssignment();
	}

	int TargetBotCountForMode( AimboxGameMode mode )
	{
		if ( SkipBots || !Lobby.BotsEnabled )
			return 0;

		return mode switch
		{
			AimboxGameMode.Duel => Math.Max( 0, 1 ),
			AimboxGameMode.Survival => 0,
			AimboxGameMode.Range => 0,
			_ when AimboxAimModeRules.IsAimMode( mode ) => 0,
			AimboxGameMode.TeamDeathmatch => Math.Max( 0, AimboxArenaConfig.TdmRosterPerTeam * 2 - _players.Count ),
			_ => BotCount
		};
	}

	public void RegisterPlayer( AimboxPlayerController player )
	{
		if ( !_players.Contains( player ) )
			_players.Add( player );

		if ( Phase == AimboxSessionPhase.Intermission )
		{
			player.Team = AimboxTeam.None;
			return;
		}

		AssignTeam( player );
		if ( Phase == AimboxSessionPhase.Playing )
			player.BeginMatch();

		if ( Phase == AimboxSessionPhase.Playing && AimboxNetworkCombat.ShouldApplyPlayerDamage )
			player.Respawn();
	}

	public void OnActive( Connection connection ) => TrySpawnPlayerForConnection( connection );

	public AimboxPlayerController FindPlayerByAccountId( string accountId )
	{
		if ( string.IsNullOrWhiteSpace( accountId ) )
			return null;

		return _players.FirstOrDefault( p => p.AccountId == accountId )
			?? Scene.GetAllComponents<AimboxPlayerController>().FirstOrDefault( p => p.AccountId == accountId );
	}

	void TrySpawnLocalPlayer()
	{
		if ( !AutoCreateLocalPlayer )
			return;

		if ( HasPlayerForConnection( Connection.Local ) )
			return;

		if ( Networking.IsActive && !Networking.IsHost )
			return;

		TrySpawnPlayerForConnection( Connection.Local );
	}

	void TrySpawnPlayerForConnection( Connection connection )
	{
		if ( !AutoCreateLocalPlayer || connection is null )
			return;

		if ( !Networking.IsHost )
			return;

		if ( HasPlayerForConnection( connection ) )
			return;

		if ( !_weaponPackagesReady )
		{
			_ = DeferSpawnPlayerForConnection( connection );
			return;
		}

		SpawnPlayerForConnection( connection );
	}

	async Task DeferSpawnPlayerForConnection( Connection connection )
	{
		await WaitForWeaponPackagesAsync();
		TrySpawnPlayerForConnection( connection );
	}

	void StartWeaponPackageMount()
	{
		if ( _weaponPackagesReady || _weaponPackagesMounting )
			return;

		_weaponPackagesMounting = true;
		_ = MountWeaponPackagesAsync();
	}

	public async Task WaitForWeaponPackagesAsync()
	{
		while ( !_weaponPackagesReady )
			await Task.DelayRealtimeSeconds( 0.05f );
	}

	async Task MountWeaponPackagesAsync()
	{
		Log.Info( "[Aimbox] Mounting weapon packages (required on host and joiners)..." );

		foreach ( var ident in WeaponPackageIdents )
		{
			var package = await Package.Fetch( ident, false );
			if ( package == null )
			{
				Log.Warning( $"[Aimbox] Weapon package not found: '{ident}'. Models from this package will be missing." );
				continue;
			}

			if ( !package.IsMounted() )
				await package.MountAsync( false );
		}

		_weaponPackagesReady = true;
		_weaponPackagesMounting = false;
		Log.Info( "[Aimbox] Weapon packages mounted." );
	}

	bool HasPlayerForConnection( Connection connection )
	{
		if ( connection is null )
			return false;

		return Scene.GetAllComponents<AimboxPlayerController>()
			.Any( p => p.IsValid() && p.GameObject.IsValid() && p.GameObject.Network.Owner == connection );
	}

	void SpawnPlayerForConnection( Connection connection )
	{
		var label = connection.DisplayName;
		if ( string.IsNullOrWhiteSpace( label ) )
			label = "Player";

		var go = new GameObject( true, $"Aimbox Player {label}" );
		go.NetworkMode = NetworkMode.Object;
		AimboxHitboxes.ConfigureCitizenCapsule( go.Components.Create<CapsuleCollider>() );
		go.Components.Create<AimboxPlayerController>();

		if ( Networking.IsActive )
			go.NetworkSpawn( connection );

		Log.Info( $"[Aimbox] Spawned player pawn for {label}." );
	}

	[Rpc.Host]
	public void RpcRequestPlayerFire(
		string attackerAccountId,
		AimboxWeaponId weaponId,
		Vector3 aimOrigin,
		Vector3 aimForward,
		bool adsHeld,
		bool moving,
		bool crouched,
		bool meleeHeavy )
	{
		if ( !Networking.IsHost )
			return;

		var attacker = FindPlayerByAccountId( attackerAccountId );
		if ( attacker is null || !attacker.IsAlive )
			return;

		var weapon = attacker.CurrentWeapon;
		if ( weapon is null || weapon.Definition.Id != weaponId )
			return;

		var request = new AimboxCombatShotRequest(
			attacker,
			weapon,
			aimForward.Normal,
			adsHeld,
			moving,
			crouched,
			meleeHeavy );

		var shot = _combatAuthority.ResolveShot( in request );
		_combatAuthority.ApplyDamage( attacker, weaponId, in shot, meleeHeavy );
		RpcBroadcastPlayerShotFx( attackerAccountId, weaponId, aimForward, shot.AnyHit, meleeHeavy );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void RpcBroadcastPlayerShotFx(
		string attackerAccountId,
		AimboxWeaponId weaponId,
		Vector3 aimForward,
		bool anyHit,
		bool meleeHeavy )
	{
		if ( Networking.IsHost || !anyHit )
			return;

		var attacker = FindPlayerByAccountId( attackerAccountId );
		var weapon = attacker?.CurrentWeapon;
		if ( attacker is null || weapon is null || weapon.Definition.Id != weaponId )
			return;

		var request = new AimboxCombatShotRequest(
			attacker,
			weapon,
			aimForward.Normal,
			false,
			false,
			false,
			meleeHeavy );

		var shot = _combatAuthority.ResolveShot( in request );
		_combatAuthority.SpawnTracers(
			in request,
			shot,
			new AimboxCombatPresentationContext( null, null, null, null ) );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	public void RpcBroadcastPlayerKill(
		string attackerAccountId,
		string victimAccountId,
		AimboxWeaponId weapon,
		bool headshot,
		float distance )
	{
		var attacker = FindPlayerByAccountId( attackerAccountId );
		var victim = FindPlayerByAccountId( victimAccountId );
		if ( attacker is null || victim is null )
			return;

		KillFeed.Record( attacker, victim, weapon, headshot );

		if ( Networking.IsHost )
			Match.RegisterKillScores( attacker, victim );

		if ( attacker is AimboxPlayerController localAttacker && !localAttacker.IsProxy )
			localAttacker.ConfirmKill( victim, weapon, headshot, distance );

		if ( victim is AimboxPlayerController localVictim && !localVictim.IsProxy )
			localVictim.RegisterNetworkDeath();
	}

	public void UnregisterPlayer( AimboxPlayerController player )
	{
		_players.Remove( player );
	}

	public void RegisterBot( AimboxBotController bot )
	{
		if ( !_bots.Contains( bot ) )
			_bots.Add( bot );

		if ( Phase == AimboxSessionPhase.Intermission )
		{
			bot.Team = AimboxTeam.None;
			return;
		}

		AssignTeam( bot );
		bot.Respawn();
	}

	public void UnregisterBot( AimboxBotController bot )
	{
		_bots.Remove( bot );
	}

	public List<IAimboxCombatActor> GetAllCombatActors()
	{
		var actors = new List<IAimboxCombatActor>( _players.Count + _bots.Count );
		actors.AddRange( _players );
		actors.AddRange( _bots );
		return actors;
	}

	public void QueueSave( AimboxPlayerData data )
	{
		if ( data is null )
			return;

		var local = AimboxLocalPlayer.Controller;
		if ( local is null || local.IsProxy || !string.Equals( local.AccountId, data.AccountId, StringComparison.OrdinalIgnoreCase ) )
			return;

		Saves.Enqueue( data );
	}

	public void ResetLocalProgress()
	{
		var player = AimboxLocalPlayer.Controller;
		if ( player is null )
		{
			Log.Warning( "[Aimbox] Progress reset skipped — no local player." );
			return;
		}

		Saves?.DiscardPending( player.AccountId );
		var data = PlayerDataService.ResetProgress( player.AccountId );
		player.ReloadPlayerData( data );
		Loadouts.ValidateAllLoadouts( player.Data );
		player.Respawn();
		Saves?.FlushAll();
		Log.Info( $"[Aimbox] Local progression reset complete. Rank {player.Data.PlayerLevel}, xp {player.Data.TotalXp}, weapons {player.Data.Weapons.Count}." );
	}

	public void ResetLocalXpProgression()
	{
		var player = AimboxLocalPlayer.Controller;
		if ( player?.Data is null )
		{
			Log.Warning( "[Aimbox] XP reset skipped — no local player." );
			return;
		}

		Saves?.DiscardPending( player.AccountId );
		AimboxPlayerData.ResetXpProgression( player.Data );
		AimboxUnlockService.EnforceSavedProgressionLocks( player.Data );
		Loadouts.ValidateAllLoadouts( player.Data );
		player.ReloadPlayerData( player.Data );
		player.Respawn();
		QueueSave( player.Data );
		Saves?.FlushAll();
		Log.Info( $"[Aimbox] Player rank and gun mastery XP reset. Rank {player.Data.PlayerLevel}, xp {player.Data.TotalXp}." );
	}

	[ConCmd( "aimbox_reset_progress" )]
	public static void ResetProgressConsole()
	{
		AimboxGame.Instance?.ResetLocalProgress();
	}

	[ConCmd( "aimbox_reset_xp" )]
	public static void ResetXpProgressionConsole()
	{
		AimboxGame.Instance?.ResetLocalXpProgression();
	}

	[ConCmd( "aimbox_enforce_locks" )]
	public static void EnforceLocksConsole()
	{
		var game = AimboxGame.Instance;
		var player = game?.Players.FirstOrDefault( x => !x.IsProxy ) ?? game?.Players.FirstOrDefault();
		if ( game is null || player?.Data is null )
			return;

		game.Saves?.DiscardPending( player.AccountId );
		AimboxUnlockService.EnforceSavedProgressionLocks( player.Data );
		game.Loadouts.ValidateAllLoadouts( player.Data );
		player.ReloadPlayerData( player.Data );
		player.Respawn();
		game.QueueSave( player.Data );
		game.Saves?.FlushAll();
		Log.Info( "[Aimbox] Progression locks enforced on current save." );
	}

	public void ResetIntermissionTimer()
	{
		if ( Phase != AimboxSessionPhase.Intermission )
			return;

		_intermissionStarted = 0;
		IntermissionTimeRemaining = AimboxMetaNavigation.IntermissionDurationSeconds;
	}

	void TickIntermission()
	{
		if ( _readyCountdownActive )
		{
			ReadyCountdownRemaining = MathF.Max( 0f, AimboxMetaNavigation.ReadyCountdownSeconds - _readyCountdownStarted.Relative );
			if ( _readyCountdownStarted >= AimboxMetaNavigation.ReadyCountdownSeconds )
				StartNextMatch();
			return;
		}

		if ( AwaitingFirstMatch )
			return;

		if ( !AimboxMetaNavigation.MatchRecapDismissed )
			return;

		if ( AimboxMetaNavigation.CurrentScreen != AimboxMetaScreen.PostMatch )
			return;

		IntermissionTimeRemaining = MathF.Max( 0f, AimboxMetaNavigation.IntermissionDurationSeconds - _intermissionStarted.Relative );
		if ( _intermissionStarted >= AimboxMetaNavigation.IntermissionDurationSeconds )
		{
			if ( !IsLobbyHost )
				return;

			SetLocalReady();
		}
	}

	void PrepareActorsForMode( AimboxGameMode mode )
	{
		AimboxBotSpawner.SetBotCount( Scene, TargetBotCountForMode( mode ) );

		if ( mode == AimboxGameMode.Range )
			AimboxRangeDummySpawner.Ensure( Scene );
		else
			AimboxRangeDummySpawner.Clear( Scene );

		if ( AimboxAimModeRules.IsAimMode( mode ) )
			AimboxAimDrillSpawner.Ensure( Scene, AimboxAimModeRules.ToDrill( mode ) );
		else
			AimboxAimDrillSpawner.Clear( Scene );

		FinalizeLobbyTeams( mode );

		if ( mode == AimboxGameMode.Duel )
			PrepareDuelPairing();

		if ( mode == AimboxGameMode.Survival )
			PrepareSurvivalTeams();
	}

	void PrepareSurvivalTeams()
	{
		foreach ( var player in _players )
			player.Team = AimboxTeam.Red;
	}

	void PrepareDuelPairing()
	{
		var humans = _players.Where( p => !p.IsProxy ).ToList();
		if ( humans.Count >= 2 )
		{
			foreach ( var duelist in humans )
			{
				var pick = Lobby.GetTeamPick( duelist.AccountId );
				if ( pick is AimboxTeam.Red or AimboxTeam.Blue )
					duelist.Team = pick;
			}

			if ( humans.All( h => h.Team is AimboxTeam.Red or AimboxTeam.Blue )
			     && humans[0].Team == humans[1].Team )
				humans[1].Team = humans[0].Team == AimboxTeam.Red ? AimboxTeam.Blue : AimboxTeam.Red;

			if ( humans.Any( h => h.Team is not (AimboxTeam.Red or AimboxTeam.Blue) ) )
			{
				humans[0].Team = AimboxTeam.Red;
				humans[1].Team = AimboxTeam.Blue;
			}

			return;
		}

		var human = humans.FirstOrDefault() ?? _players.FirstOrDefault();
		var bot = _bots.FirstOrDefault();
		if ( human is not null )
		{
			var pick = Lobby.GetTeamPick( human.AccountId );
			human.Team = pick is AimboxTeam.Red or AimboxTeam.Blue ? pick : AimboxTeam.Red;
		}

		if ( bot is not null )
			bot.Team = human?.Team == AimboxTeam.Red ? AimboxTeam.Blue : AimboxTeam.Red;
	}

	void TickSurvivalWaveClear()
	{
		if ( _bots.Count <= 0 || _bots.Any( x => x.IsAlive ) )
			return;

		Match.NotifySurvivalWaveCleared();
		RestoreSurvivalPlayers();
	}

	void RestoreSurvivalPlayers()
	{
		foreach ( var player in _players.Where( p => !p.IsProxy && p.IsAlive ) )
			player.Respawn();
	}

	void OnSurvivalWaveAdvanced()
	{
		if ( SkipBots || Match.SurvivalComplete )
			return;

		SpawnSurvivalWave();
		Log.Info( $"[Aimbox] Survival wave {Match.SurvivalWave} — {Match.SurvivalWaveBotTarget} enemies{(Match.SurvivalHardMode ? " (hard)" : "")}." );
	}

	void SpawnSurvivalWave()
	{
		var count = Match.SurvivalWaveBotTarget;
		AimboxBotSpawner.SetBotCount( Scene, count );
		Respawns.BeginSpreadAssignment();
		foreach ( var bot in _bots )
		{
			bot.Team = AimboxTeam.Blue;
			bot.ApplyWaveScaling( Match.SurvivalHardMode );
			bot.Respawn();
		}

		Respawns.EndSpreadAssignment();
	}

	void AssignTeam( AimboxPlayerController player )
	{
		if ( Match.Mode == AimboxGameMode.Survival )
		{
			player.Team = AimboxTeam.Red;
			return;
		}

		if ( Match.Mode == AimboxGameMode.Duel || AimboxLobbyTeamRules.UsesTeamSelect( Match.Mode ) )
			return;

		player.Team = AimboxTeam.None;
	}

	void AssignTeam( AimboxBotController bot )
	{
		if ( Match.Mode == AimboxGameMode.Survival )
		{
			bot.Team = AimboxTeam.Blue;
			return;
		}

		if ( Match.Mode == AimboxGameMode.Duel || AimboxLobbyTeamRules.UsesTeamSelect( Match.Mode ) )
			return;

		bot.Team = AimboxTeam.None;
	}

	AimboxTeam PickBalancedTeam()
	{
		var red = CountTeam( AimboxTeam.Red );
		var blue = CountTeam( AimboxTeam.Blue );
		if ( red >= AimboxArenaConfig.TdmRosterPerTeam && blue >= AimboxArenaConfig.TdmRosterPerTeam )
			return red <= blue ? AimboxTeam.Red : AimboxTeam.Blue;

		if ( red >= AimboxArenaConfig.TdmRosterPerTeam )
			return AimboxTeam.Blue;

		if ( blue >= AimboxArenaConfig.TdmRosterPerTeam )
			return AimboxTeam.Red;

		return red <= blue ? AimboxTeam.Red : AimboxTeam.Blue;
	}

	int CountTeam( AimboxTeam team ) =>
		_players.Count( x => x.Team == team ) + _bots.Count( x => x.Team == team );


	void ResetMetaUiOnBoot()
	{
		foreach ( var root in Scene.GetAllComponents<AimboxMetaRoot>().ToList() )
		{
			if ( root?.GameObject.IsValid() == true )
				root.GameObject.Destroy();
		}

		if ( _metaUiObject.IsValid() )
			_metaUiObject.Destroy();

		_metaUiObject = default;
		_metaUiComponentGeneration = 0;
	}

	void EnsureMetaUi()
	{
		if ( _metaUiObject.IsValid() )
		{
			EnsureMetaUiComponents( _metaUiObject );
			return;
		}

		var go = new GameObject( true, "Aimbox Meta UI" );
		go.Components.Create<ScreenPanel>().AutoScreenScale = true;
		go.Components.Create<AimboxMetaRoot>();
		EnsureMetaUiComponents( go );
		go.Components.Create<AimboxCursorGuard>();
		_metaUiObject = go;
	}

	void EnsureMetaUiComponents( GameObject go )
	{
		if ( _metaUiComponentGeneration >= MetaUiComponentGeneration )
			return;

		EnsureMetaScreen<AimboxMainMenu>( go, AimboxMetaUiFlags.UseRedesignedMainMenu, () => go.Components.Create<AimboxMainMenu>() );
		EnsureMetaScreen<AimboxLoadoutsScreen>( go, AimboxMetaUiFlags.UseRedesignedLoadouts, () => go.Components.Create<AimboxLoadoutsScreen>() );
		EnsureMetaScreen<AimboxInMatchLoadoutBar>( go, AimboxMetaUiFlags.UseRedesignedLoadouts, () => go.Components.Create<AimboxInMatchLoadoutBar>() );
		EnsureMetaScreen<AimboxProgressionScreen>( go, AimboxMetaUiFlags.UseRedesignedProgression, () => go.Components.Create<AimboxProgressionScreen>() );
		EnsureMetaScreen<AimboxScoreboardScreen>( go, AimboxMetaUiFlags.UseRedesignedScoreboard, () => go.Components.Create<AimboxScoreboardScreen>() );
		EnsureMetaScreen<AimboxPlayLobbyScreen>( go, AimboxMetaUiFlags.UseRedesignedPlayLobby, () => go.Components.Create<AimboxPlayLobbyScreen>() );
		EnsureMetaScreen<AimboxMatchStartPopup>( go, AimboxMetaUiFlags.UseRedesignedPlayLobby, () => go.Components.Create<AimboxMatchStartPopup>() );
		EnsureMetaScreen<AimboxChallengesScreen>( go, AimboxMetaUiFlags.UseRedesignedChallenges, () => go.Components.Create<AimboxChallengesScreen>() );
		EnsureMetaScreen<AimboxPauseMenuTabs>( go, AimboxPauseMenuTabsUi.Enabled, () => go.Components.Create<AimboxPauseMenuTabs>() );
		EnsureMetaScreen<AimboxScreenBackButton>( go, true, () => go.Components.Create<AimboxScreenBackButton>() );

		_metaUiComponentGeneration = MetaUiComponentGeneration;
	}

	static void EnsureMetaScreen<T>( GameObject go, bool enabled, Action create ) where T : Component
	{
		foreach ( var existing in go.Components.GetAll<T>( FindMode.EverythingInSelf ) )
			existing.Destroy();

		if ( enabled )
			create();
	}

	public void SyncMetaUiActive()
	{
		EnsureMetaUi();
		if ( !_metaUiObject.IsValid() )
			return;

		var active = AimboxMetaNavigation.RequiresMetaUiHost;
		if ( _metaUiObject.Enabled == active )
			return;

		_metaUiObject.Enabled = active;
		Log.Info( $"[Aimbox Cursor] Meta UI enabled={active} phase={Phase} screen={AimboxMetaNavigation.CurrentScreen} intermission={AimboxMetaNavigation.IsInIntermission} blocks={AimboxMetaNavigation.BlocksGameplay}." );
	}

	void EnsureArena( bool forceRebuild = false, AimboxGameMode? forMode = null )
	{
		PurgeLegacyArenaRoots();
		EnsureArenaAnchor();

		var useArena = !GunBuilderScene && !ThirdPersonWeaponLabScene;
		var arenaMode = forMode ?? ResolveArenaMode();
		var useAimRoom = ShouldUseAimRoom( arenaMode );
		if ( useAimRoom != _builtAimRoom )
			forceRebuild = true;

		var def = AimboxMapCatalog.Get( ActiveArenaMap );
		var layoutSignature = def.LayoutSignature;
		if ( useArena
		     && !forceRebuild
		     && useAimRoom
		     && _builtAimRoom
		     && AimboxArenaWorld.FindArenaRoot( AimboxAimRoomLayout.RootName ).IsValid() )
		{
			SyncSceneFloor( useArena );
			ApplyArenaGameplaySettings();
			return;
		}

		if ( useArena
		     && !forceRebuild
		     && !useAimRoom
		     && _builtArenaMap == ActiveArenaMap
		     && _builtArenaLayoutSignature == layoutSignature
		     && AimboxArenaWorld.CountArenaBlocks() > 0 )
		{
			SyncSceneFloor( useArena );
			EnsureMapSpawns( def.Layout );
			ApplyArenaGameplaySettings();
			return;
		}

		foreach ( var legacy in new[] { "Spawn A", "Spawn B", "Spawn C", "Spawn D" } )
		{
			var old = Scene.GetAllComponents<AimboxSpawnPoint>().FirstOrDefault( x => x.GameObject.Name == legacy );
			old?.GameObject.Destroy();
		}

		AimboxAimRoomBuilder.Destroy();
		AimboxArenaWorld.DestroyAllArenaRoots();

		if ( useArena )
		{
			AimboxArenaMaterials.ResetCache( forceRebuild ? "arena-rebuild" : "arena-build" );
			AimboxArenaGeometry.ResetCaches( forceRebuild ? "arena-rebuild" : "arena-build" );
			if ( useAimRoom )
				AimboxAimRoomBuilder.Ensure( Scene );
			else
				BuildActiveArena();
		}

		SyncSceneFloor( useArena );

		if ( !useArena )
			EnsureOpenSpawns();
		else if ( !useAimRoom )
			EnsureMapSpawns( def.Layout );

		ApplyArenaGameplaySettings();
		RemoveGameplayDummies();

		if ( useArena )
		{
			if ( useAimRoom )
			{
				_builtAimRoom = true;
				_builtArenaMap = (AimboxArenaMap)(-1);
				_builtArenaLayoutSignature = 0;
				Log.Info( $"[Aimbox] AIM trainer room ready, blocks={AimboxArenaWorld.CountArenaBlocks()}." );
			}
			else
			{
				_builtAimRoom = false;
				_builtArenaMap = ActiveArenaMap;
				_builtArenaLayoutSignature = layoutSignature;
				Log.Info( $"[Aimbox] Arena ready: {ActiveArenaMap}, blocks={AimboxArenaWorld.CountArenaBlocks()} ({AimboxArenaWorld.DescribeArenaInventory()})." );
			}

			AimboxArenaDiagnostics.LogWorldState( forceRebuild ? "arena-rebuild" : "arena-ready" );
		}
		else
		{
			_builtAimRoom = false;
			_builtArenaMap = (AimboxArenaMap)(-1);
			_builtArenaLayoutSignature = 0;
		}
	}

	static bool ShouldUseAimRoom( AimboxGameMode mode ) => AimboxAimModeRules.IsAimMode( mode );

	AimboxGameMode ResolveArenaMode() =>
		Phase is AimboxSessionPhase.Starting or AimboxSessionPhase.Playing
			? Match.Mode
			: NextMode;

	void BuildActiveArena() =>
		AimboxMapBuilder.Ensure( Scene, ActiveArenaMap, skip: false );

	public GameObject EnsureArenaAnchor()
	{
		if ( _arenaAnchorGo.IsValid() )
			return _arenaAnchorGo;

		_arenaAnchorGo = new GameObject( true, "Aimbox Arena Anchor" );
		_arenaAnchorGo.WorldPosition = Vector3.Zero;
		_arenaAnchorGo.WorldRotation = Rotation.Identity;
		_arenaAnchorGo.NetworkMode = NetworkMode.Never;
		return _arenaAnchorGo;
	}

	public GameObject ArenaAnchor => _arenaAnchorGo;

	void EnsureValidatedLobbyMap()
	{
		var mapId = AimboxPlayLobbyUiHelpers.NormalizeMapId( Lobby.SelectedMapId );
		if ( !AimboxPlayLobbyUiHelpers.IsMapPlayable( mapId ) )
			mapId = AimboxPlayLobbyUiHelpers.DefaultMapId;

		Lobby.ForceSelectedMap( mapId );
		ApplyLobbyArenaMap();
	}

	void PurgeLegacyArenaRoots()
	{
		foreach ( var child in GameObject.Children.ToArray() )
		{
			if ( !child.IsValid() )
				continue;

			foreach ( var rootName in AimboxArenaWorld.ArenaRootNames )
			{
				if ( string.Equals( child.Name, rootName, StringComparison.OrdinalIgnoreCase ) )
					child.Destroy();
			}
		}
	}

	void SyncViewCamera()
	{
		var sceneCamera = FindScenePreviewCamera();
		var localPlayer = Scene.GetAllComponents<AimboxPlayerController>().FirstOrDefault( x => !x.IsProxy );

		var useGameplayCamera = Phase is AimboxSessionPhase.Starting or AimboxSessionPhase.Playing;
		var useScenePreview = !useGameplayCamera
		                      && Phase == AimboxSessionPhase.Intermission
		                      && AimboxMetaNavigation.IsInIntermission
		                      && !SkipMetaMenu;

		if ( useGameplayCamera )
		{
			if ( sceneCamera is not null )
			{
				sceneCamera.Enabled = false;
				sceneCamera.IsMainCamera = false;
				sceneCamera.GameObject.Enabled = false;
			}

			DisableEditorOverlayCamera( Scene );
			localPlayer?.SetGameplayCameraActive( true );
			AimboxArenaDiagnostics.LogCameraHandoff( $"phase={Phase}", true );
			return;
		}

		if ( useScenePreview && sceneCamera is not null )
		{
			localPlayer?.SetGameplayCameraActive( false );
			sceneCamera.GameObject.Enabled = true;
			sceneCamera.Enabled = true;
			sceneCamera.IsMainCamera = true;
			sceneCamera.Priority = 32;
			AimboxArenaDiagnostics.LogCameraHandoff( "lobby-preview", false );
			return;
		}

		if ( sceneCamera is not null && !Scene.GetAllComponents<CameraComponent>().Any( x => x.Enabled && x.IsMainCamera ) )
		{
			sceneCamera.GameObject.Enabled = true;
			sceneCamera.Enabled = true;
			sceneCamera.IsMainCamera = true;
		}
	}

	static void DisableEditorOverlayCamera( Scene scene )
	{
		if ( scene is null || !scene.IsValid() )
			return;

		foreach ( var cam in scene.GetAllComponents<CameraComponent>() )
		{
			if ( !string.Equals( cam.GameObject.Name, "editor_camera", StringComparison.OrdinalIgnoreCase ) )
				continue;

			cam.Enabled = false;
			cam.IsMainCamera = false;
			cam.GameObject.Enabled = false;
		}
	}

	static CameraComponent FindScenePreviewCamera( Scene scene ) =>
		scene?.GetAllComponents<CameraComponent>()
			.FirstOrDefault( x => string.Equals( x.GameObject.Name, "Camera", StringComparison.OrdinalIgnoreCase ) );

	CameraComponent FindScenePreviewCamera() => FindScenePreviewCamera( Scene );

	void HandoffToGameplayCamera() => SyncViewCamera();

	void LogMatchSpawnState()
	{
		var local = Scene.GetAllComponents<AimboxPlayerController>().FirstOrDefault( x => !x.IsProxy );
		if ( local is null )
			return;

		Log.Info( $"[Aimbox] Match spawn: map={ActiveArenaMap}, mode={Match.Mode}, playerPos={local.WorldPosition}, arenaBlocks={AimboxArenaWorld.CountArenaBlocks()} ({AimboxArenaWorld.DescribeArenaInventory()})." );
		AimboxArenaDiagnostics.LogWorldState( "match-spawn" );
	}

	float ResolveFreezeTimeSeconds()
	{
		if ( IsAttachmentLabScene || Match.Mode == AimboxGameMode.Range || AimboxAimModeRules.IsAimMode( Match.Mode ) )
			return 0f;

		return AimboxMapDesignRules.FreezeTimeSeconds;
	}

	void ApplyArenaGameplaySettings()
	{
		Respawns.RespawnDelay = Match.Mode == AimboxGameMode.Range || AimboxAimModeRules.IsAimMode( Match.Mode )
			? 1f
			: AimboxMapDesignRules.RespawnDelaySeconds;
	}

	void ApplyLobbyArenaMap() =>
		ActiveArenaMap = AimboxMapCatalog.MapFromId( Lobby.SelectedMapId );

	void SyncSceneFloor( bool useArena )
	{
		foreach ( var renderer in Scene.GetAllComponents<ModelRenderer>() )
		{
			if ( renderer?.GameObject is not { IsValid: true } go )
				continue;

			if ( !string.Equals( go.Name, "Nuketown Lot", StringComparison.OrdinalIgnoreCase )
			     && !string.Equals( go.Name, "Arena Floor", StringComparison.OrdinalIgnoreCase ) )
				continue;

			go.Enabled = false;
		}
	}

	void EnsureMapSpawns( AimboxMapLayout cfg )
	{
		var spread = cfg.SpawnSpreadY;
		var ffaSpawns = AimboxMapDesignRules.CreateFfaSpawnPositions( cfg );
		EnsureSpawn( "FFA N", new Vector3( ffaSpawns[0].x, ffaSpawns[0].y, 0 ), Rotation.FromYaw( 180 ), AimboxTeam.None );
		EnsureSpawn( "FFA S", new Vector3( ffaSpawns[1].x, ffaSpawns[1].y, 0 ), Rotation.FromYaw( 0 ), AimboxTeam.None );
		EnsureSpawn( "FFA E", new Vector3( ffaSpawns[2].x, ffaSpawns[2].y, 0 ), Rotation.FromYaw( 270 ), AimboxTeam.None );
		EnsureSpawn( "FFA W", new Vector3( ffaSpawns[3].x, ffaSpawns[3].y, 0 ), Rotation.FromYaw( 90 ), AimboxTeam.None );
		EnsureSpawn( "FFA NE", new Vector3( ffaSpawns[4].x, ffaSpawns[4].y, 0 ), Rotation.FromYaw( 225 ), AimboxTeam.None );
		EnsureSpawn( "FFA NW", new Vector3( ffaSpawns[5].x, ffaSpawns[5].y, 0 ), Rotation.FromYaw( 135 ), AimboxTeam.None );
		EnsureSpawn( "FFA SE", new Vector3( ffaSpawns[6].x, ffaSpawns[6].y, 0 ), Rotation.FromYaw( 315 ), AimboxTeam.None );
		EnsureSpawn( "FFA SW", new Vector3( ffaSpawns[7].x, ffaSpawns[7].y, 0 ), Rotation.FromYaw( 45 ), AimboxTeam.None );

		for ( var i = 0; i < AimboxMapDesignRules.SpawnsPerTeam; i++ )
		{
			var t = (i - (AimboxMapDesignRules.SpawnsPerTeam - 1) * 0.5f) / MathF.Max( 1, AimboxMapDesignRules.SpawnsPerTeam - 1 );
			var y = t * spread;
			EnsureSpawn( $"TDM Red {i + 1}", new Vector3( cfg.RedSpawnX, y, 0 ), Rotation.FromYaw( 90 ), AimboxTeam.Red );
			EnsureSpawn( $"TDM Blue {i + 1}", new Vector3( cfg.BlueSpawnX, y, 0 ), Rotation.FromYaw( 270 ), AimboxTeam.Blue );
		}

		EnsureSpawn( "Duel Red", new Vector3( cfg.RedSpawnX, 0, 0 ), Rotation.FromYaw( 90 ), AimboxTeam.Red );
		EnsureSpawn( "Duel Blue", new Vector3( cfg.BlueSpawnX, 0, 0 ), Rotation.FromYaw( 270 ), AimboxTeam.Blue );
		EnsureSpawn( "Survival Player", new Vector3( cfg.RedSpawnX, -spread * 0.35f, 0 ), Rotation.FromYaw( 90 ), AimboxTeam.Red );
	}

	void ApplyArenaMatchPacing() => ApplyArenaGameplaySettings();

	void EnsureOpenSpawns()
	{
		var r = AimboxArenaConfig.SpawnRadius;
		var edge = r * 0.75f;

		EnsureSpawn( "FFA N", new Vector3( 0, r, 0 ), Rotation.FromYaw( 180 ), AimboxTeam.None );
		EnsureSpawn( "FFA S", new Vector3( 0, -r, 0 ), Rotation.FromYaw( 0 ), AimboxTeam.None );
		EnsureSpawn( "FFA E", new Vector3( r, 0, 0 ), Rotation.FromYaw( 270 ), AimboxTeam.None );
		EnsureSpawn( "FFA W", new Vector3( -r, 0, 0 ), Rotation.FromYaw( 90 ), AimboxTeam.None );
		EnsureSpawn( "FFA NE", new Vector3( edge, edge, 0 ), Rotation.FromYaw( 225 ), AimboxTeam.None );
		EnsureSpawn( "FFA NW", new Vector3( -edge, edge, 0 ), Rotation.FromYaw( 135 ), AimboxTeam.None );
		EnsureSpawn( "FFA SE", new Vector3( edge, -edge, 0 ), Rotation.FromYaw( 315 ), AimboxTeam.None );
		EnsureSpawn( "FFA SW", new Vector3( -edge, -edge, 0 ), Rotation.FromYaw( 45 ), AimboxTeam.None );

		for ( var i = 0; i < AimboxArenaConfig.TdmRosterPerTeam; i++ )
		{
			var t = (i - (AimboxArenaConfig.TdmRosterPerTeam - 1) * 0.5f) / AimboxArenaConfig.TdmRosterPerTeam;
			var y = t * r * 1.2f;
			EnsureSpawn( $"TDM Red {i + 1}", new Vector3( -r, y, 0 ), Rotation.FromYaw( 90 ), AimboxTeam.Red );
			EnsureSpawn( $"TDM Blue {i + 1}", new Vector3( r, y, 0 ), Rotation.FromYaw( 270 ), AimboxTeam.Blue );
		}

		EnsureSpawn( "Duel Red", new Vector3( -r, 0, 0 ), Rotation.FromYaw( 90 ), AimboxTeam.Red );
		EnsureSpawn( "Duel Blue", new Vector3( r, 0, 0 ), Rotation.FromYaw( 270 ), AimboxTeam.Blue );
		EnsureSpawn( "Survival Player", new Vector3( -r, 0, 0 ), Rotation.FromYaw( 90 ), AimboxTeam.Red );
	}

	public float GetSpawnFeetZ() => AimboxMapDesignRules.FloorWalkZ;

	void EnsureSpawn( string name, Vector3 position, Rotation rotation, AimboxTeam team )
	{
		position = position.WithZ( GetSpawnFeetZ() );
		var requested = position;
		position = AimboxSpawnClearance.ResolveClearFeetPosition( Scene, default, position );
		if ( position.WithZ( 0 ).Distance( requested.WithZ( 0 ) ) > 24f )
			Log.Info( $"[Aimbox] Spawn '{name}' adjusted from {requested} to {position} to avoid geometry." );

		var existing = Scene.GetAllComponents<AimboxSpawnPoint>().FirstOrDefault( x => x.GameObject.Name == name );
		if ( existing is not null )
		{
			existing.GameObject.WorldPosition = position;
			existing.GameObject.WorldRotation = rotation;
			existing.Team = team;
			return;
		}

		var go = new GameObject( true, name );
		go.WorldPosition = position;
		go.WorldRotation = rotation;
		go.Components.Create<AimboxSpawnPoint>().Team = team;
		Log.Info( $"[Aimbox] Spawn point '{name}' at {position}." );
	}

	void RemoveGameplayDummies()
	{
		foreach ( var dummy in Scene.GetAllComponents<AimboxDummyTarget>().ToArray() )
		{
			if ( dummy?.GameObject.IsValid() == true )
				dummy.GameObject.Destroy();
		}
	}

	void EnsureHitboxDebug()
	{
		if ( Scene.GetAllComponents<AimboxHitboxDebug>().Any() )
			return;

		var debug = GameObject.Components.Create<AimboxHitboxDebug>();
		debug.DrawOnStart = EnableHitboxDebug;
	}
}
