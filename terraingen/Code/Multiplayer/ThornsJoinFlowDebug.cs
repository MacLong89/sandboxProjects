namespace Terraingen.Multiplayer;

using Sandbox.Network;
using Terraingen.Player;
using Terraingen.TerrainGen;
using Terraingen.UI.Core;
using Terraingen.UI.Menu;
using Terraingen.UI;

/// <summary>Verbose join/host enter diagnostics — filter console with [Thorns Join].</summary>
public static class ThornsJoinFlowDebug
{
	const string Tag = "[Thorns Join]";
	const double StatusIntervalSeconds = 2.0;

	static class Engine
	{
		public static void Info( string message ) => Log.Info( message );
		public static void Warning( string message ) => Log.Warning( message );
	}

	static double _lastStatusLogTime = -1;
	static string _lastStatusSignature = "";

	public static void JoinInfo( string message ) => Engine.Info( $"{Tag} {message}" );

	public static void JoinWarn( string message ) => Engine.Warning( $"{Tag} {message}" );

	public static void LogMilestone( string milestone ) => JoinInfo( $"Milestone: {milestone}" );

	public static void LogEnterAttempt( string reason, bool allowed )
	{
		if ( allowed )
		{
			JoinInfo( $"Enter attempt OK ({reason})." );
			return;
		}

		JoinInfo( $"Enter blocked ({reason}) — {DescribeEnterGates( compact: true )}" );
	}

	public static void LogPeriodicStatus( string context )
	{
		if ( _lastStatusLogTime > 0 && Time.Now - _lastStatusLogTime < StatusIntervalSeconds )
			return;

		var signature = DescribeEnterGates( compact: true );
		if ( signature == _lastStatusSignature && _lastStatusLogTime > 0 )
			return;

		_lastStatusLogTime = Time.Now;
		_lastStatusSignature = signature;
		JoinInfo( $"{context} — {DescribeEnterGates( compact: false )}" );
	}

	public static void ResetStatusThrottle()
	{
		_lastStatusLogTime = -1;
		_lastStatusSignature = "";
	}

	public static string DescribeEnterGates( bool compact )
	{
		var pawn = ThornsJoinLocalPlayer.TryResolve( out var player, out var scene );
		var terrain = ThornsTerrainBootstrap.Instance?.IsWorldApplied == true;
		var bootGate = ThornsWorldBootGate.IsLocalBootComplete;
		var bootReady = ThornsLocalPlayerPresentation.IsBootReady();
		var hud = ThornsLocalPlayerPresentation.IsHudReady();
		var snapshot = ThornsLocalPlayerPresentation.IsSnapshotReady();
		var deferred = ThornsLocalHostSpawnCoordinator.IsDeferredPending;
		var enterDone = ThornsSessionEnterController.IsEnterComplete;
		var awaiting = ThornsSessionEnterController.IsAwaitingEnter;
		var progress = ThornsMenuJoinFlow.IsProgressVisible;
		var stage = ThornsMenuJoinFlow.CurrentStage;
		var blocksPresentation = ThornsWorldBootGate.BlocksLocalOwnerPresentation;

		if ( compact )
		{
			return
				$"pawn={pawn} terrain={terrain} bootGate={bootGate} bootReady={bootReady} hud={hud} snap={snapshot} " +
				$"defer={deferred} enter={enterDone} await={awaiting} stage={stage} net={DescribeNetwork()}";
		}

		return
			$"pawn={pawn} player={( pawn && player.IsValid() ? player.Name : "none" )} scene={( pawn ? scene?.Name ?? "?" : "none" )} " +
			$"terrain={terrain} bootGate={bootGate} bootReady={bootReady} hud={hud} snapshot={snapshot} " +
			$"defer={deferred} enterComplete={enterDone} awaitingEnter={awaiting} progress={progress} stage={stage} " +
			$"blocksPresentation={blocksPresentation} ui={DescribeHud()} net={DescribeNetwork()} " +
			$"joinFlag={ThornsSessionBootstrap.IsJoiningRemoteLobby} hostSave={ThornsSessionBootstrap.IsHostingLocalSave}";
	}

	public static string DescribeHud()
	{
		var menuHost = ThornsMenuHost.Instance;
		if ( menuHost is null || !menuHost.IsValid() )
			return "menuHost=null";

		var panelOk = menuHost.Panel is { IsValid: true };
		var rootReady = panelOk && ThornsGameplayUiStyles.IsGameplayRootReady( menuHost.Panel );
		return $"menuHost built={menuHost.IsUiBuilt} panel={panelOk} root={rootReady} snapshot={ThornsUiClientState.HasSnapshot}";
	}

	public static string DescribeNetwork()
	{
		if ( !Networking.IsActive )
			return "offline";

		var role = Networking.IsHost ? "host" : "client";
		var conn = Connection.Local?.Id.ToString() ?? "null";
		return $"{role} conn={conn}";
	}

	public static string DescribePawnResolveFailure()
	{
		var localGameplay = ThornsPlayerGameplay.Local;
		if ( localGameplay is { IsValid: true } )
			return "Local gameplay set but TryResolve failed (scene mismatch?)";

		var localConn = Connection.Local;
		var scenes = new List<string>();
		if ( ThornsTerrainBootstrap.Instance?.Scene is { IsValid: true } s )
			scenes.Add( $"bootstrap:{s.Name}" );
		if ( Game.ActiveScene is { IsValid: true } a )
			scenes.Add( $"active:{a.Name}" );

		return $"no pawn — conn={localConn?.Id.ToString() ?? "null"} display={localConn?.DisplayName ?? "null"} scenes=[{string.Join( ", ", scenes )}]";
	}
}
