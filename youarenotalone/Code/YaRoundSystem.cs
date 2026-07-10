using System;
using System.Linq;

namespace Sandbox;

/// <summary>Host authority: round start/end, respawns, loadouts, win polling. Timer for round length uses <see cref="YaServerTimerSystem"/>.</summary>
[Title( "YouAreNotAlone — Round system" )]
[Category( "YouAreNotAlone" )]
[Icon( "sports_martial_arts" )]
[Order( 12 )]
public sealed class YaRoundSystem : Component
{
	[Property] public float RoundDurationSeconds { get; set; } = 180f;

	[Property] public float WinConditionPollInterval { get; set; } = 0.35f;

	YaServerTimerSystem _timers;
	YaGameStateSystem _flow;
	YaPracticeModeSystem _practice;

	double _nextPoll;
	Guid _aloneId;
	bool _roundActive;

	protected override void OnAwake()
	{
		TryCacheSiblings();
	}

	void TryCacheSiblings()
	{
		if ( !_timers.IsValid() )
			_timers = Components.Get<YaServerTimerSystem>( FindMode.EnabledInSelf );
		if ( !_flow.IsValid() )
			_flow = Components.Get<YaGameStateSystem>( FindMode.EnabledInSelf );
		if ( !_practice.IsValid() )
			_practice = Components.Get<YaPracticeModeSystem>( FindMode.EnabledInSelf );
	}

	/// <summary>Host: pick Alone, assign roles, loadouts, respawn, start round timer. Returns false if the round could not start.</summary>
	public bool HostStartRound()
	{
		if ( !Networking.IsHost )
			return false;

		TryCacheSiblings();

		_practice?.HostEnsureNoPracticeBotsBeforePvPRound();

		var scene = GameObject.Scene;
		var roots = YaTeamSystem.EnumeratePlayerRoots( scene ).ToArray();
		if ( roots.Length == 0 )
		{
			Log.Warning( "[YA] Round start aborted: no players." );
			return false;
		}

		var pick = roots[Random.Shared.Next( roots.Length )];
		_aloneId = pick.Network.OwnerId;

		foreach ( var root in roots )
		{
			var role = root.Network.OwnerId == _aloneId ? YaPlayerRole.Alone : YaPlayerRole.NotAlone;
			var pr = root.Components.Get<YaPlayerRoleComponent>( FindMode.EnabledInSelf );
			if ( pr.IsValid() )
				pr.HostSetRole( role );

			YaLoadoutSystem.HostApplyRoleLoadout( root, role );
			HostRespawnPlayer( root, role );

			var hb = root.Components.Get<YaHotbarEquipment>( FindMode.EnabledInSelf );
			if ( hb.IsValid() )
				hb.HostApplyHotbarSlot( 0, requireAlive: true );
		}

		_roundActive = true;
		_nextPoll = Time.Now + WinConditionPollInterval;

		if ( _timers.IsValid() )
		{
			var duration = RoundDurationSeconds;
			var mut = YaWeeklyMutatorSystem.Instance;
			if ( mut is { IsValid: true } && mut.RoundDurationMul > 0.01f )
				duration *= mut.RoundDurationMul;
			_timers.HostBegin( YaTimerPurpose.Round, Math.Max( 30f, duration ) );
		}

		foreach ( var root in roots )
			YaPlayerStats.HostResetRoundKills( root );

		_flow?.HostSetAloneConnectionId( _aloneId );
		_flow?.HostClearRoundMvp();
		Log.Info( $"[YA] Round started. Alone connection={_aloneId}, players={roots.Length}" );
		YaGameEvents.InvokeHostRoundStarted();
		return true;
	}

	/// <summary>Host: end round, strip players, clear roles, notify flow.</summary>
	public void HostEndRound( YaRoundEndReason reason )
	{
		if ( !Networking.IsHost )
			return;

		TryCacheSiblings();

		_roundActive = false;
		_timers?.HostStop();

		var scene = GameObject.Scene;
		HostAwardRoundWins( scene, reason );
		foreach ( var root in YaTeamSystem.EnumeratePlayerRoots( scene ) )
		{
			var pr = root.Components.Get<YaPlayerRoleComponent>( FindMode.EnabledInSelf );
			pr?.HostClearRole();
			YaLoadoutSystem.HostStripToNeutral( root );
		}

		_aloneId = default;
		_flow?.HostSetAloneConnectionId( default );
		_flow?.HostClearParanoiaDebuff();
		Log.Info( $"[YA] Round ended: {reason}" );
		YaGameEvents.InvokeHostRoundEnded( reason );
	}

	/// <summary>Host: heal, clear death, neutral spawn — intermission free-play after roles are stripped.</summary>
	public void HostPrepareAllPlayersForIntermission()
	{
		if ( !Networking.IsHost )
			return;

		TryCacheSiblings();

		var scene = GameObject.Scene;
		if ( scene is null || !scene.IsValid() )
			return;

		foreach ( var root in YaTeamSystem.EnumeratePlayerRoots( scene ) )
		{
			var health = root.Components.Get<YaPlayerHealth>( FindMode.EnabledInSelf );
			if ( !health.IsValid() )
				continue;

			var spawn = _flow is { IsValid: true }
				? _flow.GetSpawnTransformForPlayer( root, YaPlayerRole.Unassigned )
				: root.WorldTransform;

			health.HostRespawnFull( spawn );
			root.Components.Get<YaPawnMovement>( FindMode.EnabledInSelf )?.HostApplyRespawnSnap();
		}

		Log.Info( "[YA] Intermission — players reset for free-move (no combat)." );
	}

	void HostAwardRoundWins( Scene scene, YaRoundEndReason reason )
	{
		if ( scene is null || !scene.IsValid() )
			return;

		var aloneWon = reason == YaRoundEndReason.AllNotAloneEliminated
		               || reason == YaRoundEndReason.TimeExpired;
		var notAloneWon = reason == YaRoundEndReason.AloneEliminated;
		if ( !aloneWon && !notAloneWon )
			return;

		foreach ( var root in YaTeamSystem.EnumeratePlayerRoots( scene ) )
		{
			var isAlone = root.Network.OwnerId == _aloneId;
			var won = (aloneWon && isAlone) || (notAloneWon && !isAlone);
			if ( won )
				YaPlayerStats.HostRecordRoundWin( root );
			else
				YaPlayerStats.HostResetWinStreak( root );
		}
	}

	void HostRespawnPlayer( GameObject playerRoot, YaPlayerRole roundSpawnRole )
	{
		var health = playerRoot.Components.Get<YaPlayerHealth>( FindMode.EnabledInSelf );
		var move = playerRoot.Components.Get<YaPawnMovement>( FindMode.EnabledInSelf );
		if ( !health.IsValid() )
		{
			Log.Warning( $"[YA] HostRespawnPlayer: no YaPlayerHealth on '{playerRoot.Name}' — cannot place spawn." );
			return;
		}

		var spawn = _flow is not null && _flow.IsValid()
			? _flow.GetSpawnTransformForPlayer( playerRoot, roundSpawnRole )
			: playerRoot.WorldTransform;

		health.HostRespawnFull( spawn );
		move?.HostApplyRespawnSnap();
	}

	protected override void OnUpdate()
	{
		TryCacheSiblings();

		if ( !Networking.IsHost || !_roundActive || _flow is null || !_flow.IsValid() )
			return;

		if ( _flow.CurrentState != YaGameState.InRound )
			return;

		if ( Time.Now < _nextPoll )
			return;
		_nextPoll = Time.Now + WinConditionPollInterval;

		HostEvaluateWinConditions();
	}

	void HostEvaluateWinConditions()
	{
		var scene = GameObject.Scene;
		var aloneRoot = FindRootByConnectionId( scene, _aloneId );
		var aloneHealth = aloneRoot?.Components.Get<YaPlayerHealth>( FindMode.EnabledInSelf );

		var notAloneRoots = YaTeamSystem.EnumeratePlayerRoots( scene )
			.Where( r => YaTeamSystem.GetRole( r ) == YaPlayerRole.NotAlone )
			.ToArray();

		if ( _flow is null || !_flow.IsValid() )
			return;

		if ( aloneRoot is null || !aloneRoot.IsValid() )
		{
			_flow.HostNotifyRoundShouldEnd( YaRoundEndReason.AloneEliminated );
			return;
		}

		if ( aloneHealth is { IsValid: true } && (!aloneHealth.IsAlive || aloneHealth.IsDeadState) )
		{
			_flow.HostNotifyRoundShouldEnd( YaRoundEndReason.AloneEliminated );
			return;
		}

		var anyHunterAlive = notAloneRoots.Any( r =>
		{
			var h = r.Components.Get<YaPlayerHealth>( FindMode.EnabledInSelf );
			return h is { IsValid: true, IsAlive: true } && !h.IsDeadState;
		} );

		if ( notAloneRoots.Length > 0 && !anyHunterAlive )
		{
			_flow.HostNotifyRoundShouldEnd( YaRoundEndReason.AllNotAloneEliminated );
			return;
		}
	}

	static GameObject FindRootByConnectionId( Scene scene, Guid id )
	{
		if ( id == default )
			return null;

		foreach ( var root in YaTeamSystem.EnumeratePlayerRoots( scene ) )
		{
			if ( root.Network.OwnerId == id )
				return root;
		}

		return null;
	}

	/// <summary>Called from <see cref="YaGameStateSystem"/> when the round timer expires.</summary>
	internal void HostOnRoundTimerExpired()
	{
		if ( !Networking.IsHost || !_roundActive )
			return;

		TryCacheSiblings();
		_flow?.HostNotifyRoundShouldEnd( YaRoundEndReason.TimeExpired );
	}
}
