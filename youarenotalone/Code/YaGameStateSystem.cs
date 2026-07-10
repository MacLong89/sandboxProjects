using System;
using System.Collections.Generic;
using System.Linq;

namespace Sandbox;

/// <summary>Host-driven lobby → intermission → in-round loop. Replicates state + timers for clients.</summary>
[Title( "YouAreNotAlone — Game state" )]
[Category( "YouAreNotAlone" )]
[Icon( "flag" )]
[Order( 10 )]
public sealed class YaGameStateSystem : Component
{
	public static YaGameStateSystem Instance { get; private set; }

	[Property] public int MinPlayers { get; set; } = 2;

	[Property] public float IntermissionSeconds { get; set; } = 5f;

	[Property] public float RoundVictoryBannerDurationSeconds { get; set; } = 6f;

	[Property] public bool AllowSinglePlayerDebug { get; set; }

	[Sync( SyncFlags.FromHost )] public YaGameState CurrentState { get; set; } = YaGameState.Lobby;

	/// <summary>Replicated countdown for the active <see cref="YaTimerPurpose"/> (intermission or round).</summary>
	[Sync( SyncFlags.FromHost )] public float SyncedPhaseSecondsRemaining { get; set; }

	[Sync( SyncFlags.FromHost )] public Guid AloneConnectionId { get; set; }

	[Sync( SyncFlags.FromHost )] public YaRoundEndReason LastRoundEndReason { get; set; }

	/// <summary>Non-empty while the post-round win line is shown (replicated).</summary>
	[Sync( SyncFlags.FromHost )] public string RoundVictoryBannerHeadline { get; set; } = "";

	/// <summary>Seconds left on the win banner; host counts down, clients mirror for UI.</summary>
	[Sync( SyncFlags.FromHost )] public float RoundVictoryBannerSecondsRemaining { get; set; }

	/// <summary>Seconds left on Not Alone vision/hearing debuff (host-authoritative, replicated).</summary>
	[Sync( SyncFlags.FromHost )] public float ParanoiaDebuffSecondsRemaining { get; set; }

	[Sync( SyncFlags.FromHost )] public int IntermissionReadyCount { get; set; }

	[Sync( SyncFlags.FromHost )] public int IntermissionPlayerCount { get; set; }

	[Sync( SyncFlags.FromHost )] public string RoundMvpDisplayName { get; set; } = "";

	[Sync( SyncFlags.FromHost )] public int RoundMvpKillCount { get; set; }

	[Property] public float ParanoiaDebuffDurationSeconds { get; set; } = 8f;

	YaServerTimerSystem _timers;
	YaRoundSystem _rounds;
	YaGameManager _gm;
	YaPracticeModeSystem _practice;

	bool _roundEnding;
	bool _hostVictoryBannerCountdownActive;
	bool _hostPostVictoryPracticeCleanup;
	bool _hostHasStartedFirstMatchRound;
	int _lastConnectedCount = -1;
	readonly HashSet<Guid> _intermissionReadyIds = new();

	protected override void OnAwake()
	{
		Instance = this;
		TryCacheSiblings();
	}

	/// <summary>
	/// Sibling components may not exist yet during our <see cref="OnAwake"/> (creation order).
	/// </summary>
	void TryCacheSiblings()
	{
		if ( !_timers.IsValid() )
			_timers = Components.Get<YaServerTimerSystem>( FindMode.EnabledInSelf );
		if ( !_rounds.IsValid() )
			_rounds = Components.Get<YaRoundSystem>( FindMode.EnabledInSelf );
		if ( !_practice.IsValid() )
			_practice = Components.Get<YaPracticeModeSystem>( FindMode.EnabledInSelf );
	}

	protected override void OnDestroy()
	{
		TryCacheSiblings();
		if ( _timers.IsValid() )
			_timers.HostExpired -= OnHostTimerExpired;
		if ( Instance == this )
			Instance = null;
	}

	protected override void OnStart()
	{
		TryCacheSiblings();
		TryResolveGameManager();
		if ( _timers.IsValid() )
			_timers.HostExpired += OnHostTimerExpired;
	}

	/// <summary>
	/// Match director starts under <see cref="YaGameManager"/>; scene queries can miss on first frame — always resolve before round spawn placement.
	/// </summary>
	YaGameManager TryResolveGameManager()
	{
		if ( _gm is { IsValid: true } )
			return _gm;

		_gm = null;

		var parent = GameObject.Parent;
		if ( parent.IsValid() )
		{
			var onParent = parent.Components.Get<YaGameManager>();
			if ( onParent.IsValid() )
				return _gm = onParent;
		}

		_gm = Scene.GetAllComponents<YaGameManager>().FirstOrDefault( g => g.IsValid() );
		return _gm;
	}

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost )
			return;

		TryCacheSiblings();

		if ( ParanoiaDebuffSecondsRemaining > 0f )
			ParanoiaDebuffSecondsRemaining = Math.Max( 0f, ParanoiaDebuffSecondsRemaining - Time.Delta );

		var need = AllowSinglePlayerDebug ? 1 : Math.Max( 1, MinPlayers );
		var count = YaTeamSystem.CountConnectedPlayers( GameObject.Scene );

		if ( _lastConnectedCount >= 2 && count == 1
		     && CurrentState is YaGameState.InRound or YaGameState.Intermission or YaGameState.RoundVictory )
		{
			HostAbortToSoloPracticeLobbyAfterDisconnect();
		}

		_lastConnectedCount = count;

		if ( CurrentState == YaGameState.Lobby && count >= need && !HostShouldSkipLobbyAutoIntermission() )
			EnterIntermissionFromLobby();

		HostRecoverOrphanRoundVictoryState();

		// Mirror after lobby/intermission transitions so the same frame reflects HostBegin().
		if ( _timers.IsValid() )
			SyncedPhaseSecondsRemaining = _timers.SyncedRemaining;

		if ( Time.Now >= _nextHostFlowLog )
		{
			_nextHostFlowLog = Time.Now + 8.0;
			var tp = _timers.IsValid() ? _timers.ActivePurpose : YaTimerPurpose.None;
			var tr = _timers.IsValid() ? _timers.SyncedRemaining : 0f;
			Log.Info( $"[YA] Host match flow: state={CurrentState} syncedPhaseSec={SyncedPhaseSecondsRemaining:F2} timerPurpose={tp} timerRem={tr:F2} players={count}" );
		}

		if ( _hostVictoryBannerCountdownActive )
		{
			RoundVictoryBannerSecondsRemaining = Math.Max( 0f, RoundVictoryBannerSecondsRemaining - (float)Time.Delta );
			if ( RoundVictoryBannerSecondsRemaining <= 0f )
			{
				_hostVictoryBannerCountdownActive = false;
				HostClearRoundVictoryBanner();
				if ( _hostPostVictoryPracticeCleanup )
				{
					_hostPostVictoryPracticeCleanup = false;
					_practice?.HostStopPracticeAndCleanup( enterNormalIntermission: false );
				}
				else
					HostCommitIntermissionAfterRound();
			}
		}
	}

	double _nextHostFlowLog;

	void HostAbortToSoloPracticeLobbyAfterDisconnect()
	{
		TryCacheSiblings();

		_hostVictoryBannerCountdownActive = false;
		_hostPostVictoryPracticeCleanup = false;
		HostClearRoundVictoryBanner();

		LastRoundEndReason = YaRoundEndReason.Aborted;
		_rounds?.HostEndRound( YaRoundEndReason.Aborted );
		_timers?.HostStop();
		HostClearParanoiaDebuff();

		SyncedPhaseSecondsRemaining = 0f;
		CurrentState = YaGameState.Lobby;

		_practice?.HostPrepareSoloLobbyAfterMidMatchDisconnect();

		Log.Info( "[YA] Player count dropped mid-match → solo lobby / practice picker." );
	}

	bool HostShouldSkipLobbyAutoIntermission()
	{
		// Solo practice picker lives in Lobby; AllowSinglePlayerDebug must not yank the player into PvP intermission.
		if ( _practice is { IsValid: true, AwaitingSideChoice: true } )
			return true;
		if ( _practice is { IsValid: true, PracticeActive: true } )
			return true;
		return false;
	}

	/// <summary>Host: victory banner was cancelled without a follow-up transition (disconnect / cleanup race).</summary>
	void HostRecoverOrphanRoundVictoryState()
	{
		if ( CurrentState != YaGameState.RoundVictory || _hostVictoryBannerCountdownActive )
			return;

		Log.Warning( "[YA] RoundVictory with no active banner countdown — recovering match flow." );
		if ( _hostPostVictoryPracticeCleanup )
		{
			_hostPostVictoryPracticeCleanup = false;
			_practice?.HostStopPracticeAndCleanup( enterNormalIntermission: false );
		}
		else
			HostCommitIntermissionAfterRound();
	}

	void EnterIntermissionFromLobby()
	{
		TryCacheSiblings();
		_hostVictoryBannerCountdownActive = false;
		_hostPostVictoryPracticeCleanup = false;
		HostClearRoundVictoryBanner();
		_practice?.HostEnsureNoPracticeBotsBeforePvPRound();

		if ( !_hostHasStartedFirstMatchRound && HostTryStartFirstRoundFromLobby() )
			return;

		if ( !_timers.IsValid() )
		{
			Log.Error( "[YA] YaServerTimerSystem not resolved on match director — cannot start intermission (check sibling components)." );
			CurrentState = YaGameState.Lobby;
			return;
		}

		CurrentState = YaGameState.Intermission;
		_rounds?.HostPrepareAllPlayersForIntermission();
		HostClearIntermissionReady();
		var preRoundWait = Math.Max( 3f, IntermissionSeconds );
		Log.Info( "[YA] Entering intermission (from lobby)." );
		_timers.HostBegin( YaTimerPurpose.Intermission, preRoundWait );
	}

	/// <summary>Host: skip the first lobby intermission — jump straight into round 1 when enough players are present.</summary>
	bool HostTryStartFirstRoundFromLobby()
	{
		var need = AllowSinglePlayerDebug ? 1 : Math.Max( 1, MinPlayers );
		if ( YaTeamSystem.CountConnectedPlayers( GameObject.Scene ) < need )
			return false;

		TryResolveGameManager();
		if ( _rounds is not { IsValid: true } || !_rounds.HostStartRound() )
			return false;

		_hostHasStartedFirstMatchRound = true;
		CurrentState = YaGameState.InRound;
		Log.Info( "[YA] First match round started immediately (skipped initial intermission)." );
		return true;
	}

	/// <summary>Host: immediately enter normal match intermission countdown.</summary>
	public void HostBeginNormalIntermissionNow()
	{
		if ( !Networking.IsHost )
			return;

		EnterIntermissionFromLobby();
	}

	/// <summary>Host: round subsystem requests ending (win or timer).</summary>
	public void HostNotifyRoundShouldEnd( YaRoundEndReason reason )
	{
		TryCacheSiblings();
		if ( !Networking.IsHost || _roundEnding )
			return;
		if ( CurrentState != YaGameState.InRound )
			return;

		_roundEnding = true;
		LastRoundEndReason = reason;
		HostComputeRoundMvp();
		_rounds?.HostEndRound( reason );

		if ( TryGetVictoryHeadline( reason, out var headline ) )
		{
			CurrentState = YaGameState.RoundVictory;
			RoundVictoryBannerHeadline = headline;
			RoundVictoryBannerSecondsRemaining = Math.Max( 3f, RoundVictoryBannerDurationSeconds );
			_hostVictoryBannerCountdownActive = true;
			Log.Info( $"[YA] Round win banner: '{headline}' ({RoundVictoryBannerSecondsRemaining:F0}s) then intermission." );
		}
		else
			HostCommitIntermissionAfterRound();

		_roundEnding = false;
	}

	/// <summary>Host: solo practice ended by elimination — same win line as PvP, then return to role picker (no <see cref="YaRoundSystem.HostEndRound"/>).</summary>
	public void HostNotifyPracticeEliminationVictory( YaRoundEndReason reason )
	{
		TryCacheSiblings();
		if ( !Networking.IsHost )
			return;
		if ( _practice is not { IsValid: true, PracticeActive: true } )
			return;
		if ( CurrentState == YaGameState.RoundVictory )
			return;
		if ( CurrentState != YaGameState.InRound )
			return;
		if ( !TryGetVictoryHeadline( reason, out var headline ) )
			return;

		LastRoundEndReason = reason;
		HostComputeRoundMvp();
		_timers?.HostStop();
		CurrentState = YaGameState.RoundVictory;
		RoundVictoryBannerHeadline = headline;
		RoundVictoryBannerSecondsRemaining = Math.Max( 3f, RoundVictoryBannerDurationSeconds );
		_hostVictoryBannerCountdownActive = true;
		_hostPostVictoryPracticeCleanup = true;
		Log.Info( $"[YA] Practice elimination — win banner '{headline}' ({RoundVictoryBannerSecondsRemaining:F0}s) then solo picker." );
	}

	static bool TryGetVictoryHeadline( YaRoundEndReason reason, out string headline )
	{
		headline = "";
		if ( reason == YaRoundEndReason.AllNotAloneEliminated )
		{
			headline = "Alone has won!";
			return true;
		}

		if ( reason == YaRoundEndReason.AloneEliminated )
		{
			headline = "Not Alone has won!";
			return true;
		}

		if ( reason == YaRoundEndReason.TimeExpired )
		{
			headline = "Alone has won! — survived the hunt";
			return true;
		}

		return false;
	}

	void HostClearRoundVictoryBanner()
	{
		RoundVictoryBannerHeadline = "";
		RoundVictoryBannerSecondsRemaining = 0f;
	}

	/// <summary>Host: stop victory countdown and clear banner sync (e.g. practice teardown mid-banner or a second player joining).</summary>
	public void HostCancelRoundVictoryCountdownAndBanner()
	{
		if ( !Networking.IsHost )
			return;

		_hostVictoryBannerCountdownActive = false;
		_hostPostVictoryPracticeCleanup = false;
		HostClearRoundVictoryBanner();
	}

	void HostCommitIntermissionAfterRound()
	{
		TryCacheSiblings();
		_hostVictoryBannerCountdownActive = false;
		_hostPostVictoryPracticeCleanup = false;
		HostClearRoundVictoryBanner();
		if ( !_timers.IsValid() )
		{
			Log.Error( "[YA] Cannot start post-round intermission — timer missing; returning to lobby." );
			CurrentState = YaGameState.Lobby;
			return;
		}

		CurrentState = YaGameState.Intermission;
		_rounds?.HostPrepareAllPlayersForIntermission();
		HostClearIntermissionReady();
		_timers.HostBegin( YaTimerPurpose.Intermission, Math.Max( 3f, IntermissionSeconds ) );
		Log.Info( $"[YA] Intermission after round ({LastRoundEndReason})." );
	}

	void OnHostTimerExpired( YaTimerPurpose purpose )
	{
		TryCacheSiblings();
		if ( !Networking.IsHost )
			return;

		if ( purpose == YaTimerPurpose.Intermission )
		{
			if ( HostShouldSkipLobbyAutoIntermission() )
			{
				CurrentState = YaGameState.Lobby;
				_timers?.HostStop();
				Log.Info( "[YA] Intermission ended — solo practice picker active, back to lobby." );
				return;
			}

			var need = AllowSinglePlayerDebug ? 1 : Math.Max( 1, MinPlayers );
			if ( YaTeamSystem.CountConnectedPlayers( GameObject.Scene ) < need )
			{
				CurrentState = YaGameState.Lobby;
				_timers?.HostStop();
				Log.Info( "[YA] Intermission ended — not enough players, back to lobby." );
				return;
			}

			TryResolveGameManager();
			if ( _rounds is not { IsValid: true } || !_rounds.HostStartRound() )
			{
				Log.Warning( "[YA] Round start failed — staying in intermission and retrying countdown." );
				CurrentState = YaGameState.Intermission;
				_rounds?.HostPrepareAllPlayersForIntermission();
				_timers?.HostBegin( YaTimerPurpose.Intermission, Math.Max( 3f, IntermissionSeconds ) );
				return;
			}

			_hostHasStartedFirstMatchRound = true;
			CurrentState = YaGameState.InRound;
			return;
		}

		if ( purpose == YaTimerPurpose.Round )
		{
			if ( _practice is { IsValid: true, PracticeActive: true }
			     && CurrentState == YaGameState.InRound )
			{
				LastRoundEndReason = YaRoundEndReason.TimeExpired;
				_rounds?.HostEndRound( YaRoundEndReason.TimeExpired );
				if ( TryGetVictoryHeadline( YaRoundEndReason.TimeExpired, out var practiceHeadline ) )
				{
					CurrentState = YaGameState.RoundVictory;
					RoundVictoryBannerHeadline = practiceHeadline;
					RoundVictoryBannerSecondsRemaining = Math.Max( 3f, RoundVictoryBannerDurationSeconds );
					_hostVictoryBannerCountdownActive = true;
					_hostPostVictoryPracticeCleanup = true;
				}
				else
					_practice.HostFinalizePracticeAfterTimedRoundEnd();

				return;
			}

			_rounds?.HostOnRoundTimerExpired();
		}
	}

	public void HostSetAloneConnectionId( Guid id ) => AloneConnectionId = id;

	public void HostClearRoundMvp()
	{
		if ( !Networking.IsHost )
			return;

		RoundMvpDisplayName = "";
		RoundMvpKillCount = 0;
	}

	/// <summary>Host: Alone ability — impair all hunters for <see cref="ParanoiaDebuffDurationSeconds"/>.</summary>
	public void HostApplyParanoiaDebuff()
	{
		if ( !Networking.IsHost )
			return;

		var d = Math.Max( 0.5f, ParanoiaDebuffDurationSeconds );
		ParanoiaDebuffSecondsRemaining = Math.Max( ParanoiaDebuffSecondsRemaining, d );
	}

	/// <summary>Host: clear paranoia (e.g. round end).</summary>
	public void HostClearParanoiaDebuff()
	{
		if ( !Networking.IsHost )
			return;

		ParanoiaDebuffSecondsRemaining = 0f;
	}

	/// <summary>Spawn position for a player — pass <paramref name="roundSpawnRole"/> on the host when role is already known (avoids <see cref="Sync"/> delay on the same frame).</summary>
	public Transform GetSpawnTransformForPlayer( GameObject playerRoot, YaPlayerRole roundSpawnRole )
	{
		var gm = TryResolveGameManager();
		if ( gm is { IsValid: true } )
		{
			if ( roundSpawnRole == YaPlayerRole.Alone || roundSpawnRole == YaPlayerRole.NotAlone )
				return gm.GetRoundSpawnTransformForPlayer( playerRoot, roundSpawnRole );
			return gm.GetHostSpawnTransform();
		}

		Log.Warning( "[YA] GetSpawnTransformForPlayer: YaGameManager not found — spawn may not use role markers." );
		var t = playerRoot.WorldTransform;
		t.Position += Vector3.Up * 8f;
		return t;
	}

	void HostClearIntermissionReady()
	{
		_intermissionReadyIds.Clear();
		IntermissionReadyCount = 0;
		IntermissionPlayerCount = YaTeamSystem.CountConnectedPlayers( GameObject.Scene );
	}

	/// <summary>Client: mark ready during intermission (R when weapons are gated off).</summary>
	[Rpc.Host]
	public void RequestMarkIntermissionReady()
	{
		if ( !Networking.IsHost || CurrentState != YaGameState.Intermission )
			return;

		var caller = Rpc.Caller;
		if ( caller is null )
			return;

		_intermissionReadyIds.Add( caller.Id );
		IntermissionPlayerCount = Math.Max( 1, YaTeamSystem.CountConnectedPlayers( GameObject.Scene ) );
		IntermissionReadyCount = _intermissionReadyIds.Count;

		if ( IntermissionReadyCount >= IntermissionPlayerCount )
			HostSkipIntermissionEarly();
	}

	void HostSkipIntermissionEarly()
	{
		if ( !Networking.IsHost || CurrentState != YaGameState.Intermission )
			return;

		var need = AllowSinglePlayerDebug ? 1 : Math.Max( 1, MinPlayers );
		if ( YaTeamSystem.CountConnectedPlayers( GameObject.Scene ) < need )
			return;

		_timers?.HostStop();
		TryResolveGameManager();
		if ( _rounds is not { IsValid: true } || !_rounds.HostStartRound() )
			return;

		_hostHasStartedFirstMatchRound = true;
		CurrentState = YaGameState.InRound;
		HostClearIntermissionReady();
		Log.Info( "[YA] Intermission skipped — all players ready." );
	}

	void HostComputeRoundMvp()
	{
		RoundMvpDisplayName = "";
		RoundMvpKillCount = 0;

		var scene = GameObject.Scene;
		if ( scene is null || !scene.IsValid() )
			return;

		var bestName = "";
		var bestKills = 0;
		foreach ( var root in YaTeamSystem.EnumeratePlayerRoots( scene ) )
		{
			var stats = root.Components.Get<YaPlayerStats>( FindMode.EnabledInSelf );
			if ( !stats.IsValid() || stats.RoundKills <= 0 )
				continue;

			if ( stats.RoundKills > bestKills )
			{
				bestKills = stats.RoundKills;
				bestName = stats.DisplayName;
			}
		}

		if ( bestKills > 0 )
		{
			RoundMvpDisplayName = bestName;
			RoundMvpKillCount = bestKills;
		}
	}

	[Rpc.Broadcast]
	public void RpcPushKillFeedEntry( string killer, int killerRole, string victim, int victimRole )
	{
		YaKillFeed.EnqueueLocal( new YaKillFeed.Entry
		{
			Killer = killer,
			KillerRole = (YaPlayerRole)Math.Clamp( killerRole, 0, (int)YaPlayerRole.NotAlone ),
			Victim = victim,
			VictimRole = (YaPlayerRole)Math.Clamp( victimRole, 0, (int)YaPlayerRole.NotAlone )
		} );
	}

	[Rpc.Broadcast]
	public void RpcPushKillFeedInfo( string message ) => YaKillFeed.EnqueueLocalInfo( message );
}
