using System.Collections.Generic;
using System.Linq;

namespace Sandbox;

[Title( "YouAreNotAlone — Practice mode" )]
[Category( "YouAreNotAlone" )]
[Icon( "smart_toy" )]
/// <summary>Runs before <see cref="YaGameStateSystem"/> so solo practice tears down before lobby→intermission when a 2nd player joins.</summary>
[Order( 8 )]
public sealed class YaPracticeModeSystem : Component
{
	const int PracticeTotalPlayers = 8;

	public static YaPracticeModeSystem Instance { get; private set; }

	[Sync( SyncFlags.FromHost )] public bool AwaitingSideChoice { get; private set; }
	[Sync( SyncFlags.FromHost )] public bool PracticeActive { get; private set; }
	[Sync( SyncFlags.FromHost )] public YaPlayerRole HumanChosenRole { get; private set; } = YaPlayerRole.Unassigned;
	[Sync( SyncFlags.FromHost )] public float SoloPracticeAutoStartSecondsRemaining { get; private set; }

	readonly List<GameObject> _practiceBots = new();
	YaGameStateSystem _flow;
	YaGameManager _gm;
	YaServerTimerSystem _timers;
	YaRoundSystem _rounds;
	Guid _practiceHumanConnectionId;
	double _soloLobbyAutoPracticeAt;

	protected override void OnAwake() => Instance = this;

	protected override void OnDestroy()
	{
		if ( Instance == this )
			Instance = null;
	}

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost )
			return;

		_flow ??= Components.Get<YaGameStateSystem>( FindMode.EnabledInSelf );
		_gm ??= GameObject.Parent.Components.Get<YaGameManager>( FindMode.EnabledInSelf );
		if ( !_flow.IsValid() || !_gm.IsValid() )
			return;

		var connected = YaTeamSystem.CountConnectedPlayers( GameObject.Scene );
		if ( connected != 1 )
		{
			AwaitingSideChoice = false;
			_soloLobbyAutoPracticeAt = 0;
			SoloPracticeAutoStartSecondsRemaining = 0f;
			if ( PracticeActive )
				HostStopPracticeAndCleanup( enterNormalIntermission: true );
			return;
		}

		if ( PracticeActive )
		{
			_soloLobbyAutoPracticeAt = 0;
			SoloPracticeAutoStartSecondsRemaining = 0f;
			// Elimination win must run before human-death cleanup so a trade-kill / same-frame wipe
			// cannot skip the victory banner; once RoundVictory is active, death teardown is deferred.
			if ( HostTryPracticeEliminationVictoryFromBotCounts() )
				return;

			HostCheckPracticeRoundReset();
			return;
		}

		if ( _flow.CurrentState == YaGameState.Lobby && !PracticeActive )
		{
			AwaitingSideChoice = true;
			if ( _soloLobbyAutoPracticeAt <= 0 )
				_soloLobbyAutoPracticeAt = Time.Now + 15.0;
			SoloPracticeAutoStartSecondsRemaining = (float)Math.Max( 0.0, _soloLobbyAutoPracticeAt - Time.Now );
			if ( Time.Now >= _soloLobbyAutoPracticeAt )
				HostAutoStartDefaultPractice();
		}
		else
			SoloPracticeAutoStartSecondsRemaining = 0f;
	}

	void HostAutoStartDefaultPractice()
	{
		_soloLobbyAutoPracticeAt = 0;
		SoloPracticeAutoStartSecondsRemaining = 0f;
		var scene = GameObject.Scene;
		if ( scene is null || !scene.IsValid() )
			return;

		var humanSession = YaTeamSystem.EnumerateSessions( scene ).FirstOrDefault();
		if ( humanSession is null || !humanSession.IsValid() || humanSession.OwnerConnection is null )
			return;

		HostStartPracticeRound( humanSession.OwnerConnection, YaPlayerRole.NotAlone );
	}

	[Rpc.Host]
	public void RequestChoosePracticeRole( int roleValue )
	{
		if ( !Networking.IsHost )
			return;

		_flow ??= Components.Get<YaGameStateSystem>( FindMode.EnabledInSelf );
		_gm ??= GameObject.Parent.Components.Get<YaGameManager>( FindMode.EnabledInSelf );
		if ( !_flow.IsValid() || !_gm.IsValid() )
			return;
		if ( _flow.CurrentState != YaGameState.Lobby || PracticeActive )
			return;
		if ( YaTeamSystem.CountConnectedPlayers( GameObject.Scene ) != 1 )
			return;

		var caller = Rpc.Caller;
		if ( caller is null )
			return;

		var chosen = roleValue == (int)YaPlayerRole.Alone ? YaPlayerRole.Alone : YaPlayerRole.NotAlone;
		HostStartPracticeRound( caller, chosen );
	}

	void HostStartPracticeRound( Connection humanConnection, YaPlayerRole chosenRole )
	{
		var scene = GameObject.Scene;
		var humanSession = YaTeamSystem.EnumerateSessions( scene )
			.FirstOrDefault( s => s.OwnerConnection == humanConnection );
		if ( humanSession is null || !humanSession.IsValid() )
			return;

		HostDestroyAllPracticeBotsInScene();
		PracticeActive = true;
		AwaitingSideChoice = false;
		HumanChosenRole = chosenRole;
		_practiceHumanConnectionId = humanConnection.Id;

		_flow ??= Components.Get<YaGameStateSystem>( FindMode.EnabledInSelf );

		var aloneSpawn = _gm.GetRoundSpawnTransformForRole( YaPlayerRole.Alone );
		var notAloneSpawn = _gm.GetRoundSpawnTransformForRole( YaPlayerRole.NotAlone );

		var humanRoot = humanSession.GameObject;
		var humanSpawn = chosenRole == YaPlayerRole.NotAlone
			? _gm.GetRoundSpawnTransformForRole( YaPlayerRole.NotAlone, 0 )
			: (_flow.IsValid()
				? _flow.GetSpawnTransformForPlayer( humanRoot, chosenRole )
				: aloneSpawn);
		AssignRoleAndRespawnAt( humanRoot, chosenRole, humanSpawn );

		SpawnPracticeRosterForChoice( chosenRole, humanConnection, aloneSpawn, notAloneSpawn );

		_timers ??= Components.Get<YaServerTimerSystem>( FindMode.EnabledInSelf );
		_rounds ??= Components.Get<YaRoundSystem>( FindMode.EnabledInSelf );

		_flow.CurrentState = YaGameState.InRound;
		if ( _timers.IsValid() && _rounds.IsValid() )
			_timers.HostBegin( YaTimerPurpose.Round, Math.Max( 1f, _rounds.RoundDurationSeconds ) );

		YaGameEvents.InvokeHostRoundStarted();
	}

	/// <summary>Host: called after <see cref="YaRoundSystem.HostEndRound"/> when the replicated round timer hits zero.</summary>
	public void HostFinalizePracticeAfterTimedRoundEnd()
	{
		if ( !Networking.IsHost )
			return;

		_flow ??= Components.Get<YaGameStateSystem>( FindMode.EnabledInSelf );
		_gm ??= GameObject.Parent.Components.Get<YaGameManager>( FindMode.EnabledInSelf );
		if ( !_flow.IsValid() || !_gm.IsValid() )
			return;

		_flow.HostCancelRoundVictoryCountdownAndBanner();
		_timers ??= Components.Get<YaServerTimerSystem>( FindMode.EnabledInSelf );
		_timers?.HostStop();

		PracticeActive = false;
		HumanChosenRole = YaPlayerRole.Unassigned;
		_practiceHumanConnectionId = default;
		HostDestroyAllPracticeBotsInScene();

		foreach ( var s in YaTeamSystem.EnumerateSessions( GameObject.Scene ) )
			ResetPlayerRootToHostLobbySpawn( s.GameObject );

		_flow.HostSetAloneConnectionId( default );
		_flow.CurrentState = YaGameState.Lobby;
		AwaitingSideChoice = true;
	}

	void SpawnPracticeRosterForChoice( YaPlayerRole chosenRole, Connection humanConnection, Transform aloneSpawn, Transform notAloneSpawn )
	{
		if ( chosenRole == YaPlayerRole.Alone )
		{
			var hunterBots = PracticeTotalPlayers - 1;
			for ( var i = 0; i < hunterBots; i++ )
				SpawnPracticeBot( YaPlayerRole.NotAlone, _gm.GetRoundSpawnTransformForRole( YaPlayerRole.NotAlone, i ) );
			_flow.HostSetAloneConnectionId( humanConnection.Id );
			return;
		}

		SpawnPracticeBot( YaPlayerRole.Alone, aloneSpawn );
		var extraHunters = PracticeTotalPlayers - 2;
		for ( var i = 0; i < extraHunters; i++ )
			SpawnPracticeBot( YaPlayerRole.NotAlone, _gm.GetRoundSpawnTransformForRole( YaPlayerRole.NotAlone, i + 1 ) );
		_flow.HostSetAloneConnectionId( default );
	}

	void SpawnPracticeBot( YaPlayerRole role, Transform spawn )
	{
		var displayName = YaBotDisplayNames.Reserve();
		var root = _gm.CreateNonNetworkedPlayerRoot( displayName, spawn );
		if ( !root.IsValid() )
		{
			YaBotDisplayNames.Release( displayName );
			return;
		}

		root.Tags.Add( "ya_practice_bot" );
		if ( !root.Tags.Has( "player" ) )
			root.Tags.Add( "player" );

		AssignRoleAndRespawnAt( root, role, spawn );

		var brain = root.Components.Get<YaBotBrain>( FindMode.EnabledInSelf );
		if ( !brain.IsValid() )
			brain = root.Components.Create<YaBotBrain>();
		brain.BotRole = role;

		_practiceBots.Add( root );
	}

	void AssignRoleAndRespawnAt( GameObject root, YaPlayerRole role, Transform spawn )
	{
		var pr = root.Components.Get<YaPlayerRoleComponent>( FindMode.EnabledInSelf );
		pr?.HostSetRole( role );
		YaLoadoutSystem.HostApplyRoleLoadout( root, role );

		var health = root.Components.Get<YaPlayerHealth>( FindMode.EnabledInSelf );
		var move = root.Components.Get<YaPawnMovement>( FindMode.EnabledInSelf );
		if ( health.IsValid() )
			health.HostRespawnFull( spawn );
		move?.HostApplyRespawnSnap();

		var hb = root.Components.Get<YaHotbarEquipment>( FindMode.EnabledInSelf );
		if ( hb.IsValid() )
			hb.HostApplyHotbarSlot( 0, requireAlive: true );
	}

	/// <summary>Host: multiplayer match dropped mid-phase — lone player returns to neutral loadout/host spawn like first join (solo practice picker).</summary>
	public void HostPrepareSoloLobbyAfterMidMatchDisconnect()
	{
		if ( !Networking.IsHost )
			return;

		_gm ??= GameObject.Parent.Components.Get<YaGameManager>( FindMode.EnabledInSelf );
		if ( !_gm.IsValid() )
			return;

		PracticeActive = false;
		HumanChosenRole = YaPlayerRole.Unassigned;
		_practiceHumanConnectionId = default;
		HostDestroyAllPracticeBotsInScene();

		foreach ( var s in YaTeamSystem.EnumerateSessions( GameObject.Scene ) )
			ResetPlayerRootToHostLobbySpawn( s.GameObject );

		AwaitingSideChoice = true;
	}

	static void ResetPlayerRootToHostLobbySpawn( YaGameManager gm, GameObject root )
	{
		if ( root is null || !root.IsValid() || !gm.IsValid() )
			return;

		var role = root.Components.Get<YaPlayerRoleComponent>( FindMode.EnabledInSelf );
		role?.HostClearRole();
		YaLoadoutSystem.HostStripToNeutral( root );

		var health = root.Components.Get<YaPlayerHealth>( FindMode.EnabledInSelf );
		if ( health.IsValid() )
			health.HostRespawnFull( gm.GetHostSpawnTransform() );
	}

	void ResetPlayerRootToHostLobbySpawn( GameObject root ) => ResetPlayerRootToHostLobbySpawn( _gm, root );

	public void HostStopPracticeAndCleanup( bool enterNormalIntermission )
	{
		_flow ??= Components.Get<YaGameStateSystem>( FindMode.EnabledInSelf );
		_flow?.HostCancelRoundVictoryCountdownAndBanner();

		_timers ??= Components.Get<YaServerTimerSystem>( FindMode.EnabledInSelf );
		_timers?.HostStop();

		_flow?.HostClearParanoiaDebuff();

		var humanId = _practiceHumanConnectionId;
		PracticeActive = false;
		HumanChosenRole = YaPlayerRole.Unassigned;
		_practiceHumanConnectionId = default;
		HostDestroyAllPracticeBotsInScene();
		HostResetHumanToInitialLobbyState( humanId );
		if ( _flow.IsValid() )
		{
			_flow.HostSetAloneConnectionId( default );
			if ( enterNormalIntermission )
				_flow.HostBeginNormalIntermissionNow();
			else
			{
				_flow.CurrentState = YaGameState.Lobby;
				if ( YaTeamSystem.CountConnectedPlayers( GameObject.Scene ) == 1 )
					AwaitingSideChoice = true;
			}
		}
	}

	void HostResetHumanToInitialLobbyState( Guid humanId )
	{
		if ( humanId == default || !_gm.IsValid() )
			return;

		var scene = GameObject.Scene;
		if ( scene is null || !scene.IsValid() )
			return;

		var session = YaTeamSystem.EnumerateSessions( scene )
			.FirstOrDefault( s => s.OwnerConnection is { Id: var id } && id == humanId );
		if ( session is null || !session.IsValid() || !session.GameObject.IsValid() )
			return;

		ResetPlayerRootToHostLobbySpawn( session.GameObject );
	}

	void HostCheckPracticeRoundReset()
	{
		if ( _practiceHumanConnectionId == default )
			return;

		var scene = GameObject.Scene;
		if ( scene is null || !scene.IsValid() )
			return;

		_flow ??= Components.Get<YaGameStateSystem>( FindMode.EnabledInSelf );
		if ( _flow is { IsValid: true, CurrentState: YaGameState.RoundVictory } )
			return;

		var humanSession = YaTeamSystem.EnumerateSessions( scene )
			.FirstOrDefault( s => s.OwnerConnection is { Id: var id } && id == _practiceHumanConnectionId );
		if ( humanSession is null || !humanSession.IsValid() )
			return;

		var hp = humanSession.GameObject.Components.Get<YaPlayerHealth>( FindMode.EnabledInSelf );
		if ( hp is { IsValid: true, IsDeadState: true } )
		{
			HostStopPracticeAndCleanup( enterNormalIntermission: false );
			AwaitingSideChoice = true;
		}
	}

	/// <summary>Host: if practice win-by-elimination conditions are met, enter <see cref="YaGameState.RoundVictory"/> and return true.</summary>
	bool HostTryPracticeEliminationVictoryFromBotCounts()
	{
		var scene = GameObject.Scene;
		if ( scene is null || !scene.IsValid() )
			return false;

		var aliveAloneBots = CountAliveBotsByRole( scene, YaPlayerRole.Alone );
		var aliveNotAloneBots = CountAliveBotsByRole( scene, YaPlayerRole.NotAlone );

		// Practice round ends when bot roster of either side is fully eliminated.
		var shouldReset = false;
		if ( HumanChosenRole == YaPlayerRole.Alone )
		{
			// In Alone practice there are no allied Alone bots; only enemy hunters matter.
			shouldReset = aliveNotAloneBots == 0;
		}
		else if ( HumanChosenRole == YaPlayerRole.NotAlone )
		{
			// In Not Alone practice, either bot side being wiped ends practice.
			shouldReset = aliveAloneBots == 0 || aliveNotAloneBots == 0;
		}

		if ( !shouldReset )
			return false;

		_flow ??= Components.Get<YaGameStateSystem>( FindMode.EnabledInSelf );
		var endReason = HostResolvePracticeEliminationReason( HumanChosenRole, aliveAloneBots, aliveNotAloneBots );
		_flow?.HostNotifyPracticeEliminationVictory( endReason );
		AwaitingSideChoice = true;
		return true;
	}

	static YaRoundEndReason HostResolvePracticeEliminationReason( YaPlayerRole humanChosenRole, int aliveAloneBots, int aliveNotAloneBots )
	{
		if ( humanChosenRole == YaPlayerRole.Alone )
			return YaRoundEndReason.AllNotAloneEliminated;

		if ( aliveAloneBots == 0 )
			return YaRoundEndReason.AloneEliminated;

		return YaRoundEndReason.AllNotAloneEliminated;
	}

	static int CountAliveBotsByRole( Scene scene, YaPlayerRole role )
	{
		var n = 0;
		foreach ( var brain in scene.GetAllComponents<YaBotBrain>() )
		{
			if ( !brain.IsValid() || brain.BotRole != role )
				continue;
			var hp = brain.GameObject.Components.Get<YaPlayerHealth>( FindMode.EnabledInSelf );
			if ( hp is { IsValid: true, IsAlive: true } && !hp.IsDeadState )
				n++;
		}

		return n;
	}

	void HostDestroyPracticeBots()
	{
		foreach ( var bot in _practiceBots )
		{
			if ( bot.IsValid() )
				bot.Destroy();
		}
		_practiceBots.Clear();
	}

	void HostDestroyAllPracticeBotsInScene()
	{
		HostDestroyPracticeBots();
		var scene = GameObject.Scene;
		if ( scene is null || !scene.IsValid() )
			return;

		// Safety sweep: remove any leftover bot roots from prior practice runs.
		foreach ( var brain in scene.GetAllComponents<YaBotBrain>() )
		{
			if ( brain.IsValid() && brain.GameObject.IsValid() )
				brain.GameObject.Destroy();
		}
	}

	/// <summary>Host: PvP round starting — practice bots must never persist or respawn outside solo practice.</summary>
	public void HostEnsureNoPracticeBotsBeforePvPRound()
	{
		if ( !Networking.IsHost )
			return;

		PracticeActive = false;
		HumanChosenRole = YaPlayerRole.Unassigned;
		_practiceHumanConnectionId = default;
		AwaitingSideChoice = false;
		HostDestroyAllPracticeBotsInScene();
	}
}
