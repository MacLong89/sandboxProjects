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
	/// <summary>Editor-only. Ignored when not running in the s&amp;box editor.</summary>
	[Property, Title( "Dev only — unlock all weapons, perks, and compatible attachments" )]
	public bool DebugUnlockAllProgression { get; set; }
	/// <summary>Editor-only. Ignored when not running in the s&amp;box editor.</summary>
	[Property, Title( "Dev only — wipe local player + gun mastery save on next boot" )]
	public bool ResetProgressOnBoot { get; set; }
	[Property, Title( "Dev only — start with hitbox overlay enabled (H toggles anytime)" )]
	public bool EnableHitboxDebug { get; set; }
	[Property, Title( "Dev only — in-game optic ADS tuning (bracket key) and M700 PiP tuning (O key)" )]
	public bool EnableOpticAdsTuners { get; set; }
	[Property, Title( "Dev only — log M700 draw calls at ADS + PiP vs lens on fire" )]
	public bool EnableM700ScopeInvestigation { get; set; }

	public bool DevUnlockAllProgressionActive => Application.IsEditor && DebugUnlockAllProgression;
	public bool DevResetProgressOnBootActive => Application.IsEditor && ResetProgressOnBoot;
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
	public AimboxAimLeaderboardStore AimLeaderboards { get; private set; }
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
	public int HumanPlayerCount => _players.Count( IsLivePlayer );
	/// <summary>
	/// AUDIT FIX H2 (2026-07-13): previously counted only `!IsProxy`, so each peer saw ≤1 human
	/// and UsesLobbyMapVote never enabled with remotes. Count all registered live player pawns
	/// (proxies on host ARE remote humans). Lobby vote Sync is still incomplete — this only
	/// unlocks the multi-human vote code path on hosts that actually have ≥2 pawns.
	/// </summary>
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
	bool _localPlayerSpawnInFlight;

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
		AimLeaderboards = new AimboxAimLeaderboardStore();
		Leaderboards.Initialize( AimLeaderboards );
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

		if ( DevResetProgressOnBootActive )
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
		TickHostProxyWeaponClocks();

		// AUDIT FIX C2: only the host decides ShouldEnd when networked. Clients wait for RpcEndMatch.
		// Offline (no Networking) still ends locally as before.
		if ( Match.ShouldEnd() )
		{
			if ( !Networking.IsActive || Networking.IsHost )
				EndMatch();
		}
	}

	/// <summary>
	/// AUDIT FIX C1/H1: host proxies never hit UpdateWeaponInput, so weapon _cooldown never drained
	/// after EnsureHostAuthorityWeapon built a runtime. Tick them once per Playing frame.
	/// </summary>
	void TickHostProxyWeaponClocks()
	{
		if ( !Networking.IsActive || !Networking.IsHost )
			return;

		var dt = Time.Delta;
		foreach ( var player in _players )
		{
			if ( player is null || !player.IsValid() || !player.IsProxy )
				continue;

			player.TickHostProxyCombat( dt );
		}
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

		if ( _duelRoundResetStarted < AimboxArenaConfig.DuelRoundResetSeconds )
			return;

		// AUDIT FIX H4 follow-up: joiners also receive _duelRoundResetPending (IsCombatLocked).
		// Clearing must happen on ALL peers. Only the host respawns + starts freeze (which
		// RpcBeginMatchFreeze's to joiners). Do NOT early-out joiners before clearing — that
		// used to softlock combat forever after a duel round.
		_duelRoundResetPending = false;

		if ( Networking.IsActive && !Networking.IsHost )
			return;

		RespawnDuelPair();
		BeginMatchFreeze( AimboxArenaConfig.DuelRoundFreezeSeconds, "ROUND START" );
	}

	public void BeginMatchFreeze( float seconds, string label = "FREEZE TIME" )
	{
		ApplyMatchFreezeLocal( seconds, label );

		// AUDIT FIX H4: broadcast freeze so joiners lock combat with the host.
		// Host already applied locally; RPC skips host via Networking.IsHost check inside.
		if ( Networking.IsActive && Networking.IsHost && seconds > 0f )
			RpcBeginMatchFreeze( seconds, label );
	}

	void ApplyMatchFreezeLocal( float seconds, string label )
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

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void RpcBeginMatchFreeze( float seconds, string label )
	{
		if ( Networking.IsHost )
			return;

		ApplyMatchFreezeLocal( seconds, label );
	}

	public void OnDuelRoundEnded( IAimboxCombatActor attacker, IAimboxCombatActor victim )
	{
		if ( Match.Mode != AimboxGameMode.Duel || Match.ShouldEnd() || _duelRoundResetPending )
			return;

		ApplyDuelRoundResetLocal();
		Log.Info( $"[Aimbox] Duel round {Match.DuelRound} won by {attacker.CombatId}." );

		// AUDIT FIX H4: joiners previously never saw _duelRoundResetPending / freeze.
		if ( Networking.IsActive && Networking.IsHost )
			RpcDuelRoundReset();
	}

	void ApplyDuelRoundResetLocal()
	{
		_duelRoundResetPending = true;
		_duelRoundResetStarted = 0;
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void RpcDuelRoundReset()
	{
		if ( Networking.IsHost )
			return;

		ApplyDuelRoundResetLocal();
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
		if ( Scene is null || !Scene.IsValid() )
		{
			Log.Warning( "[Aimbox] StartMatch aborted — scene unavailable." );
			return;
		}

		// AUDIT FIX M5: StartMatch had no phase guard and could re-enter while Playing
		// (legacy SetMode / AutoStart). Host-driven StartNextMatch still works from Intermission.
		if ( Phase == AimboxSessionPhase.Playing )
		{
			Log.Warning( $"[Aimbox] StartMatch ignored — already Playing ({mode})." );
			return;
		}

		SanitizeRegisteredActors();
		TrySpawnLocalPlayer();

		EnsureValidatedLobbyMap();
		EnsureArena( forMode: mode );

		Phase = AimboxSessionPhase.Starting;
		NextMode = mode;
		Match.Start( mode );

		if ( authoritative )
			PrepareActorsForMode( mode );

		foreach ( var player in Scene.GetAllComponents<AimboxPlayerController>() )
		{
			if ( !IsLivePlayer( player ) || player.IsProxy )
				continue;

			player.BeginMatch();
		}

		if ( authoritative )
		{
			if ( mode == AimboxGameMode.Survival )
			{
				Respawns.BeginSpreadAssignment();
				foreach ( var player in _players )
				{
					if ( !IsLivePlayer( player ) )
						continue;

					player.Respawn();
				}
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

		AimboxOnboardingTips.NotifyMatchStarted();
		AimboxMetaNavigation.ApplyPresentationState();
		LogMatchSpawnState();
	}

	public void EndMatch()
	{
		if ( Phase != AimboxSessionPhase.Playing )
			return;

		// AUDIT FIX C2: when networked, joiners must not unilaterally Finish/enter intermission
		// from a divergent Match snapshot — host owns end, then broadcasts.
		var endingAsHost = !Networking.IsActive || Networking.IsHost;

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

		var winnerSet = new HashSet<string>( winners, StringComparer.OrdinalIgnoreCase );
		foreach ( var player in _players )
		{
			var won = winnerSet.Contains( player.AccountId );
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

		// AUDIT FIX C3 (ranked path): was `_players.Where(!IsProxy)` which never yields two humans
		// on a listen server. Rank two live humans with highest kill counts from Match.
		if ( Match.Mode == AimboxGameMode.Duel && endingAsHost )
		{
			var ordered = _players
				.Where( p => p is not null && p.IsValid() && p.Data is not null )
				.OrderByDescending( p => Match.PlayerKills.GetValueOrDefault( p.AccountId ) )
				.ToList();
			if ( ordered.Count >= 2 )
				Ranked.ApplyDuelResult( ordered[0].Data, ordered[1].Data );
		}

		if ( endingAsHost && Networking.IsActive )
			RpcEndMatch( winners.ToArray() );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void RpcEndMatch( string[] winnerAccountIds )
	{
		if ( Networking.IsHost )
			return;

		if ( Phase != AimboxSessionPhase.Playing )
			return;

		// Joiners apply Finish to stop local clock, then drive intermission using host winners
		// (local ResolveWinners may be empty / wrong because scores never synced).
		LastScoreboard = AimboxScoreboardBuilder.Snapshot( this );
		Match.Finish( _players );

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

		var winnerSet = new HashSet<string>( winnerAccountIds ?? [], StringComparer.OrdinalIgnoreCase );
		foreach ( var player in _players )
		{
			if ( player.IsProxy )
				continue;

			var won = winnerSet.Contains( player.AccountId );
			player.FinishMatch( won );
			AimboxMetaNavigation.EnterIntermission( player.BuildMatchSummary( won ) );
		}

		Log.Info( $"[Aimbox] Match ended by host. Winners: {string.Join( ", ", winnerSet )}." );
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
			{
				if ( !IsLivePlayer( player ) )
					continue;

				player.Team = AimboxTeam.None;
			}

			foreach ( var bot in _bots )
			{
				if ( !IsLiveBot( bot ) )
					continue;

				bot.Team = AimboxTeam.None;
			}

			return;
		}

		var redCount = 0;
		var blueCount = 0;
		var unassignedPlayers = new List<AimboxPlayerController>();
		var unassignedBots = new List<AimboxBotController>();

		foreach ( var player in _players )
		{
			if ( !IsLivePlayer( player ) )
				continue;

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
			if ( !IsLiveBot( bot ) )
				continue;

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

		SanitizeRegisteredActors();
		TrySpawnLocalPlayer();

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
		{
			if ( !IsLivePlayer( player ) )
				continue;

			player.Respawn();
		}

		foreach ( var bot in Scene.GetAllComponents<AimboxBotController>() )
		{
			if ( !IsLiveBot( bot ) )
				continue;

			bot.Respawn();
		}

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
			_ => ResolveDefaultBotCount()
		};
	}

	int ResolveDefaultBotCount()
	{
		var local = _players.FirstOrDefault( p => p is not null && p.IsValid() && !p.IsProxy );
		if ( local?.Data is not null && AimboxXpSystem.IsFirstSession( local.Data ) )
			return Math.Clamp( Math.Min( BotCount, 2 ), 0, BotCount );

		return BotCount;
	}

	public void RegisterPlayer( AimboxPlayerController player )
	{
		if ( !IsLivePlayer( player ) )
			return;

		if ( !player.IsProxy && FindLocalHumanPlayer( player ) is not null )
		{
			Log.Warning( $"[Aimbox] Duplicate local player blocked — destroying '{player.GameObject.Name}'." );
			player.GameObject.Destroy();
			return;
		}

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

		if ( _localPlayerSpawnInFlight && connection == Connection.Local )
			return true;

		foreach ( var player in Scene.GetAllComponents<AimboxPlayerController>() )
		{
			if ( !IsLivePlayer( player ) )
				continue;

			if ( player.GameObject.Network.Owner == connection )
				return true;

			// Offline/editor pawns often never get a network owner assigned.
			if ( connection == Connection.Local && !player.IsProxy )
				return true;
		}

		return false;
	}

	void SpawnPlayerForConnection( Connection connection )
	{
		if ( HasPlayerForConnection( connection ) )
			return;

		_localPlayerSpawnInFlight = true;
		try
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
		finally
		{
			_localPlayerSpawnInFlight = false;
		}
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

		// AUDIT FIX C1: proxies never built an inventory on the host — CurrentWeapon was null
		// and every joiner shot no-op'd. Ensure a host-side runtime for the claimed weapon.
		var weapon = attacker.EnsureHostAuthorityWeapon( weaponId );
		if ( weapon is null || weapon.Definition.Id != weaponId )
			return;

		// AUDIT FIX H1: host must consume ammo/ROF itself. Client already consumed for UX;
		// reject spam / empty mags here. aimOrigin is reserved for future lag-comp — today we
		// still resolve from attacker.EyePosition (synced transform).
		_ = aimOrigin;
		if ( !weapon.TryConsumeShot() )
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
		if ( attacker is null )
			return;

		// Prefer existing runtime; on joiners the owning client already has inventory.
		var weapon = attacker.CurrentWeapon;
		if ( weapon is null || weapon.Definition.Id != weaponId )
			weapon = attacker.EnsureHostAuthorityWeapon( weaponId );

		if ( weapon is null || weapon.Definition.Id != weaponId )
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
		string attackerCombatId,
		string victimAccountId,
		AimboxWeaponId weapon,
		bool headshot,
		float distance )
	{
		var victim = FindPlayerByAccountId( victimAccountId );
		if ( victim is null )
			return;

		// AUDIT FIX C4: attacker may be a player AccountId OR bot CombatId (bot_XXX).
		var attackerPlayer = FindPlayerByAccountId( attackerCombatId );
		var attackerBot = FindBotById( attackerCombatId );
		IAimboxCombatActor attacker = attackerPlayer ?? (IAimboxCombatActor)attackerBot;
		if ( attacker is null )
		{
			Log.Warning( $"[Aimbox] Kill broadcast dropped — unknown attacker '{attackerCombatId}'." );
			return;
		}

		KillFeed.Record( attacker, victim, weapon, headshot );

		// Every peer must update its local Match copy — scores are not [Sync].
		// Host-only gating left joiners with empty scoreboards for the whole match.
		Match.RegisterKillScores( attacker, victim );

		// Only human attackers award local XP / mastery (bots use ConfirmKill → RegisterKill offline;
		// here scores already registered above — avoid double RegisterKill for bots).
		if ( attackerPlayer is not null && !attackerPlayer.IsProxy )
			attackerPlayer.ConfirmKill( victim, weapon, headshot, distance );

		if ( !victim.IsProxy )
			victim.RegisterNetworkDeath();
	}

	public AimboxBotController FindBotById( string botId )
	{
		if ( string.IsNullOrWhiteSpace( botId ) )
			return null;

		return _bots.FirstOrDefault( b => b.BotId == botId )
			?? Scene.GetAllComponents<AimboxBotController>().FirstOrDefault( b => b.BotId == botId );
	}

	/// <summary>
	/// AUDIT FIX (bot victim MP): same shape as RpcBroadcastPlayerKill but victim is a bot.
	/// Host registers scores; owning attacker client runs ConfirmKill for XP.
	/// </summary>
	[Rpc.Broadcast( NetFlags.HostOnly )]
	public void RpcBroadcastBotVictimKill(
		string attackerCombatId,
		string botId,
		AimboxWeaponId weapon,
		bool headshot,
		float distance )
	{
		var bot = FindBotById( botId );
		if ( bot is null )
			return;

		var attackerPlayer = FindPlayerByAccountId( attackerCombatId );
		var attackerBot = FindBotById( attackerCombatId );
		IAimboxCombatActor attacker = attackerPlayer ?? (IAimboxCombatActor)attackerBot;
		if ( attacker is null )
			return;

		KillFeed.Record( attacker, bot, weapon, headshot );

		// Mirror player-kill broadcast: keep every peer's Match scores in sync.
		Match.RegisterKillScores( attacker, bot );

		if ( attackerPlayer is not null && !attackerPlayer.IsProxy )
			attackerPlayer.ConfirmKill( bot, weapon, headshot, distance );
	}

	public void UnregisterPlayer( AimboxPlayerController player )
	{
		if ( player is null )
			return;

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
		{
			// AUDIT FIX H3: mid-match joiners previously returned here with Team.None, then
			// spawn FilterForMode could fall back to ALL candidates (wrong side). Auto-balance
			// only when still unassigned; leave deliberate lobby picks alone.
			if ( Phase == AimboxSessionPhase.Playing && player.Team == AimboxTeam.None )
			{
				player.Team = PickBalancedTeam();
				Log.Info( $"[Aimbox] Late-join assigned {player.AccountId} → {player.Team}." );
			}

			return;
		}

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
		{
			if ( Phase == AimboxSessionPhase.Playing && bot.Team == AimboxTeam.None )
				bot.Team = PickBalancedTeam();

			return;
		}

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

	static bool IsLivePlayer( AimboxPlayerController player ) =>
		player?.GameObject is { IsValid: true };

	static bool IsLiveBot( AimboxBotController bot ) =>
		bot?.GameObject is { IsValid: true };

	void SanitizeRegisteredActors()
	{
		_players.RemoveAll( player => !IsLivePlayer( player ) );
		_bots.RemoveAll( bot => !IsLiveBot( bot ) );
		DeduplicateLocalHumanPlayers();
	}

	AimboxPlayerController FindLocalHumanPlayer( AimboxPlayerController exclude = null ) =>
		Scene.GetAllComponents<AimboxPlayerController>()
			.FirstOrDefault( p => IsLivePlayer( p ) && !p.IsProxy && p != exclude );

	void DeduplicateLocalHumanPlayers()
	{
		var locals = Scene.GetAllComponents<AimboxPlayerController>()
			.Where( p => IsLivePlayer( p ) && !p.IsProxy )
			.ToList();

		if ( locals.Count <= 1 )
			return;

		var keeper = locals.FirstOrDefault( p => _players.Contains( p ) ) ?? locals[0];
		foreach ( var duplicate in locals )
		{
			if ( duplicate == keeper )
				continue;

			Log.Warning( $"[Aimbox] Removing duplicate local player '{duplicate.GameObject.Name}'." );
			UnregisterPlayer( duplicate );
			duplicate.GameObject.Destroy();
		}
	}

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
