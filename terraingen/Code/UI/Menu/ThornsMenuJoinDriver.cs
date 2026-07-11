namespace Terraingen.UI.Menu;

using Sandbox.Network;
using Terraingen.Multiplayer;
using Terraingen.Player;
using Terraingen.UI.Core;

/// <summary>Join/host readiness waits — poll on the game thread via DelayRealtimeSeconds (not component OnUpdate).</summary>
public static class ThornsMenuJoinDriver
{
	const float PollIntervalSeconds = 0.05f;

	public static void Ensure()
	{
		ThornsLocalHostSpawnDriver.Ensure();
	}

	public static async System.Threading.Tasks.Task<bool> WaitForPawnReadyAsync( int timeoutMs )
	{
		if ( ThornsJoinLocalPlayer.IsReadyForJoinHandoff() )
			return true;

		Ensure();
		var deadline = Time.Now + timeoutMs / 1000.0;
		while ( Time.Now < deadline )
		{
			if ( ThornsJoinLocalPlayer.IsReadyForJoinHandoff() )
				return true;

			if ( ThornsJoinLocalPlayer.TryResolve( out _, out _ ) )
				return true;

			ThornsMenuJoinHandoff.TryComplete();
			if ( !ThornsMenuJoinFlow.IsProgressVisible )
				return true;

			await System.Threading.Tasks.Task.Delay( 50 );
		}

		var resolved = ThornsJoinLocalPlayer.TryResolve( out var player, out _ );
		if ( !resolved )
		{
			Log.Warning(
				$"[Thorns Menu] Join pawn wait timed out — resolved=false networking={Networking.IsActive} " +
				$"localConn={Connection.Local?.Id.ToString() ?? "null"} activeScene={Game.ActiveScene?.Name ?? "null"}." );
		}

		return resolved || ThornsJoinLocalPlayer.IsReadyForJoinHandoff();
	}

	public static async System.Threading.Tasks.Task<bool> WaitForMinimallyPlayableAsync( int timeoutMs )
	{
		ThornsJoinFlowDebug.JoinInfo( $"WaitForMinimallyPlayableAsync start timeout={timeoutMs}ms" );
		if ( CanReleaseJoinOverlay() )
			return true;

		Ensure();
		var deadline = Time.Now + timeoutMs / 1000.0;
		while ( Time.Now < deadline )
		{
			ThornsLocalPlayerPresentation.TickAssetBootstrap();

			if ( ThornsJoinLocalPlayer.TryResolve( out var player, out var scene ) )
				ThornsLocalPlayerPresentation.EnsureLocalReady( scene, player );

			if ( CanReleaseJoinOverlay() )
				return true;

			ThornsMenuJoinHandoff.TryComplete();
			if ( !ThornsMenuJoinFlow.IsProgressVisible )
				return true;

			await System.Threading.Tasks.Task.Delay( 50 );
		}

		if ( !CanReleaseJoinOverlay() )
		{
			ThornsJoinFlowDebug.JoinWarn(
				$"WaitForMinimallyPlayableAsync timed out — boot={ThornsLocalPlayerPresentation.IsBootReady()} " +
				$"hud={ThornsLocalPlayerPresentation.IsHudReady()} snapshot={ThornsLocalPlayerPresentation.IsSnapshotReady()} " +
				$"pawn={ThornsJoinLocalPlayer.TryResolve( out _, out _ )} {ThornsJoinFlowDebug.DescribeHud()}" );
			Log.Warning(
				$"[Thorns Menu] Join HUD wait timed out — boot={ThornsLocalPlayerPresentation.IsBootReady()} " +
				$"hud={ThornsLocalPlayerPresentation.IsHudReady()} snapshot={ThornsLocalPlayerPresentation.IsSnapshotReady()} " +
				$"pawn={ThornsJoinLocalPlayer.TryResolve( out _, out _ )}." );
		}

		return CanReleaseJoinOverlay() || ThornsJoinLocalPlayer.TryResolve( out _, out _ );
	}

	public static void NotifyLocalPawnSpawned()
	{
		if ( !ThornsJoinLocalPlayer.TryResolve( out var player, out var scene ) )
		{
			ThornsJoinFlowDebug.JoinWarn( $"NotifyLocalPawnSpawned — pawn not resolved. {ThornsJoinFlowDebug.DescribePawnResolveFailure()}" );
			return;
		}

		ThornsJoinFlowDebug.LogMilestone( $"NotifyLocalPawnSpawned player={player.Name}" );
		ThornsLocalPlayerPresentation.EnsureLocalReady( scene, player );
		ThornsMenuJoinHandoff.TryComplete();
	}

	static bool CanReleaseJoinOverlay()
	{
		if ( !ThornsJoinLocalPlayer.TryResolve( out _, out _ ) )
			return false;

		if ( !ThornsLocalPlayerPresentation.IsBootReady() )
			return false;

		if ( ThornsLocalPlayerPresentation.IsMinimallyPlayable() )
			return true;

		// Boot + pawn is enough once snapshot or HUD has started — don't block on full chrome.
		return ThornsLocalPlayerPresentation.IsSnapshotReady()
		       || ThornsLocalPlayerPresentation.IsHudReady();
	}
}
