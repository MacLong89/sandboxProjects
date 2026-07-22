namespace Terraingen.Multiplayer;

using System.Threading.Tasks;
using Terraingen.Player;
using Terraingen.TerrainGen;
using Terraingen.UI.Core;
using Terraingen.UI.Menu;

/// <summary>Single owner for gameplay enter: pawn + terrain boot + minimal UI, then release overlay/input.</summary>
[Title( "Thorns Session Enter Controller" )]
[Category( "Thorns/Multiplayer" )]
public sealed class ThornsSessionEnterController : Component
{
	const float ForceCompleteAfterSeconds = 120f;

	public static ThornsSessionEnterController Instance { get; private set; }

	public static bool IsEnterComplete { get; private set; }

	public static bool IsAwaitingEnter => _awaitingEnter;

	static bool _awaitingEnter;
	static double _waitStartedRealtime = -1;

	public static void ResetForSession( string reason = "" )
	{
		if ( !string.IsNullOrWhiteSpace( reason ) )
			ThornsJoinFlowDebug.JoinWarn( $"ResetForSession ({reason}) — progress={ThornsMenuJoinFlow.IsProgressVisible} join={ThornsSessionBootstrap.IsJoiningRemoteLobby}" );

		IsEnterComplete = false;
		_awaitingEnter = false;
		_waitStartedRealtime = -1;
		ThornsJoinFlowDebug.ResetStatusThrottle();
	}

	/// <summary>Call when a join/host flow begins waiting for gameplay enter (resets timer even if driver already exists).</summary>
	public static void BeginAwaitEnter()
	{
		Ensure();
		IsEnterComplete = false;
		_awaitingEnter = true;
		_waitStartedRealtime = Time.Now;
		ThornsJoinFlowDebug.ResetStatusThrottle();
		ThornsJoinFlowDebug.LogMilestone( "BeginAwaitEnter" );
		ThornsJoinFlowDebug.JoinInfo( DescribeWaitState( "begin" ) );
	}

	public static void Ensure( Scene scene = null )
	{
		scene ??= Game.ActiveScene;
		if ( scene is null || !scene.IsValid )
			return;

		if ( Instance is not null && Instance.IsValid() )
			return;

		var root = scene.GetAllComponents<ThornsNetworkGameManager>().FirstOrDefault()?.GameObject
		           ?? scene.GetAllComponents<ThornsTerrainBootstrap>().FirstOrDefault()?.GameObject;
		if ( !root.IsValid() )
			return;

		if ( !root.Components.Get<ThornsSessionEnterController>().IsValid() )
		{
			root.Components.Create<ThornsSessionEnterController>();
			ThornsJoinFlowDebug.JoinInfo( $"Ensure created controller on '{root.Name}' scene={scene.Name}" );
		}
	}

	protected override void OnAwake()
	{
		if ( Instance is null || !Instance.IsValid() )
			Instance = this;
	}

	protected override void OnDestroy()
	{
		if ( Instance == this )
			Instance = null;
	}

	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying || IsEnterComplete )
			return;

		RearmEnterWaitIfNeeded( "OnUpdate" );
		if ( !_awaitingEnter )
			return;

		if ( _waitStartedRealtime < 0 )
			_waitStartedRealtime = Time.Now;

		ThornsJoinFlowDebug.LogPeriodicStatus( "Awaiting enter (game thread)" );

		var canEnter = CanEnterGameplay( out var blockReason );
		if ( canEnter )
			CompleteEnter( "tick" );
		else if ( !TryResolveLocalPawn( out _, out _ ) )
			ThornsJoinFlowDebug.LogPeriodicStatus( $"Pawn missing: {ThornsJoinFlowDebug.DescribePawnResolveFailure()}" );

		if ( IsEnterComplete )
			return;

		if ( Time.Now - _waitStartedRealtime < ForceCompleteAfterSeconds )
			return;

		ThornsJoinFlowDebug.JoinWarn( $"Enter wait hit {ForceCompleteAfterSeconds:F0}s cap — {DescribeWaitState( blockReason )}" );

		// Never force enter without terrain applied — that yields a broken first session.
		if ( ThornsTerrainBootstrap.Instance?.IsWorldApplied != true )
		{
			ThornsJoinFlowDebug.JoinWarn( "Enter wait timed out — terrain still not applied; keeping control locked." );
			return;
		}

		if ( TryResolveLocalPawn( out _, out _ ) )
			ForceCompleteEnter( "timeout" );
		else
			ThornsJoinFlowDebug.JoinWarn( $"Enter wait timed out — still no local pawn. {ThornsJoinFlowDebug.DescribePawnResolveFailure()}" );
	}

	public static void NotifyLocalPawnSpawned()
	{
		ThornsJoinFlowDebug.LogMilestone( "NotifyLocalPawnSpawned" );
		TryCompleteEnter( "pawn" );
	}

	public static void NotifyWorldBootComplete()
	{
		ThornsJoinFlowDebug.LogMilestone( "NotifyWorldBootComplete" );
		TryCompleteEnter( "boot" );
	}

	public static void TryCompleteEnter( string reason = "" )
	{
		if ( IsEnterComplete )
			return;

		RearmEnterWaitIfNeeded( reason );
		if ( !_awaitingEnter )
			return;

		if ( !CanEnterGameplay( out var blockReason ) )
		{
			ThornsJoinFlowDebug.LogEnterAttempt( $"{reason} blocked: {blockReason}", allowed: false );
			return;
		}

		CompleteEnter( reason );
	}

	public static void ForceCompleteEnter( string reason )
	{
		if ( IsEnterComplete )
			return;

		if ( !TryResolveLocalPawn( out var player, out var scene ) )
		{
			ThornsJoinFlowDebug.JoinWarn( $"Cannot force enter ({reason}) — {ThornsJoinFlowDebug.DescribePawnResolveFailure()}" );
			return;
		}

		ThornsJoinFlowDebug.JoinWarn(
			$"Forcing enter ({reason}) — player={player.Name} {ThornsJoinFlowDebug.DescribeEnterGates( compact: true )}" );

		ThornsLocalPlayerPresentation.EnsureLocalReady( scene, player );
		CompleteEnter( $"force:{reason}" );
	}

	/// <summary>Wait until enter completes — polling happens on the game thread via <see cref="OnUpdate"/>.</summary>
	public static async Task<bool> WaitUntilReadyAsync( int timeoutMs )
	{
		if ( IsEnterComplete )
			return true;

		BeginAwaitEnter();
		ThornsJoinFlowDebug.JoinInfo( $"WaitUntilReadyAsync start timeout={timeoutMs}ms" );

		var deadline = Time.Now + timeoutMs / 1000.0;
		while ( Time.Now < deadline )
		{
			Ensure();
			RearmEnterWaitIfNeeded( "WaitUntilReadyAsync" );

			if ( IsEnterComplete )
			{
				ThornsJoinFlowDebug.LogMilestone( "WaitUntilReadyAsync success" );
				return true;
			}

			await System.Threading.Tasks.Task.Delay( 50 );
		}

		ThornsJoinFlowDebug.JoinWarn( $"WaitUntilReadyAsync timed out after {timeoutMs}ms — {DescribeWaitState( "timeout" )}" );

		if ( ThornsTerrainBootstrap.Instance?.IsWorldApplied != true )
			return false;

		if ( TryResolveLocalPawn( out _, out _ ) )
		{
			ForceCompleteEnter( "wait-timeout" );
			return IsEnterComplete;
		}

		return false;
	}

	public static async Task<bool> WaitForEnterAsync( int timeoutMs ) => await WaitUntilReadyAsync( timeoutMs );

	static bool CanEnterGameplay( out string blockReason )
	{
		blockReason = "";

		if ( !TryResolveLocalPawn( out _, out _ ) )
		{
			blockReason = "no pawn";
			return false;
		}

		if ( ThornsTerrainBootstrap.Instance?.IsWorldApplied != true )
		{
			blockReason = "terrain not applied";
			return false;
		}

		if ( Networking.IsActive && !Networking.IsHost
		     && !ThornsWorldSession.IsAuthoritativeForJoin( ThornsTerrainBootstrap.Instance?.Config ) )
		{
			blockReason = "world seed not synced";
			return false;
		}

		if ( !ThornsWorldBootGate.IsLocalBootComplete )
		{
			blockReason = "boot gate open";
			return false;
		}

		if ( ThornsLocalPlayerPresentation.IsBootReady() )
			return true;

		if ( ThornsLocalPlayerPresentation.IsHudReady() )
			return true;

		if ( ThornsLocalPlayerPresentation.IsSnapshotReady() )
			return true;

		blockReason = "boot/hud/snapshot not ready";
		return false;
	}

	static void CompleteEnter( string reason )
	{
		if ( IsEnterComplete )
			return;

		if ( !TryResolveLocalPawn( out var player, out var scene ) )
		{
			ThornsJoinFlowDebug.JoinWarn( $"CompleteEnter aborted ({reason}) — pawn lost." );
			return;
		}

		ThornsJoinFlowDebug.LogMilestone( $"Enter complete ({reason})" );
		ThornsJoinFlowDebug.JoinInfo( $"Releasing overlay for '{player.Name}' — {ThornsJoinFlowDebug.DescribeHud()}" );

		ThornsLocalPlayerPresentation.EnsureLocalReady( scene, player );
		ThornsSessionBootstrap.CompleteJoinRemoteLobby();
		ThornsMenuJoinFlow.CompleteEnterWorld();
		ThornsGameplaySession.EnsureLocalPlayerControl();
		ThornsLocalHostSpawnCoordinator.CancelCosmeticsHold();

		IsEnterComplete = true;
		_awaitingEnter = false;
		Log.Info( $"[Thorns Session] Enter complete ({reason})." );
	}

	static bool TryResolveLocalPawn( out GameObject player, out Scene scene ) =>
		ThornsJoinLocalPlayer.TryResolve( out player, out scene );

	static string DescribeWaitState( string label ) =>
		$"{label}: elapsed={( _waitStartedRealtime > 0 ? Time.Now - _waitStartedRealtime : 0 ):F1}s {ThornsJoinFlowDebug.DescribeEnterGates( compact: false )}";

	static bool ShouldTrackEnterWait() =>
		ThornsMenuJoinFlow.IsProgressVisible || ThornsSessionBootstrap.IsJoiningRemoteLobby;

	static void RearmEnterWaitIfNeeded( string reason )
	{
		if ( _awaitingEnter || IsEnterComplete || !ShouldTrackEnterWait() )
			return;

		ThornsJoinFlowDebug.JoinWarn( $"Re-arming enter wait ({reason}) — await was cleared while join still active." );
		_awaitingEnter = true;
		if ( _waitStartedRealtime < 0 )
			_waitStartedRealtime = Time.Now;
	}
}
