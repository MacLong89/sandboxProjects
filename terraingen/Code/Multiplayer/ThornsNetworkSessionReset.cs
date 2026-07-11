namespace Terraingen.Multiplayer;

using Terraingen;
using Terraingen.Player;
using Terraingen.UI.Menu;

/// <summary>Clears static multiplayer/session state when leaving gameplay or disconnecting.</summary>
public static class ThornsNetworkSessionReset
{
	public static async System.Threading.Tasks.Task DisconnectAndResetAsync()
	{
		ThornsSessionBootstrap.CancelJoinRemoteLobbyRequest();
		ThornsSessionBootstrap.CancelHostFromLocalSaveRequest();

		if ( ThornsMultiplayer.IsHostOrOffline )
			ThornsWorldPersistence.FlushBeforeExit();

		if ( Networking.IsActive )
		{
			Networking.Disconnect();
			await System.Threading.Tasks.Task.Delay( 120 );
		}

		ResetStaticState( "disconnect" );
	}

	public static void ResetStaticState( string reason = "unspecified" )
	{
		if ( ThornsMenuJoinFlow.IsProgressVisible || ThornsSessionBootstrap.IsJoiningRemoteLobby )
		{
			ThornsJoinFlowDebug.JoinWarn(
				$"ResetStaticState ({reason}) during active join — progress={ThornsMenuJoinFlow.IsProgressVisible} join={ThornsSessionBootstrap.IsJoiningRemoteLobby}" );
		}

		ThornsWorldSession.Reset();
		ThornsSceneObserver.ClearCachedLocalPlayer();
		ThornsWorldBootGate.ResetBootState();
		ThornsLocalHostSpawnCoordinator.ResetState();
		ThornsSessionEnterController.ResetForSession( reason );
		ThornsMenuJoinFlow.ResetForMainMenu();
	}
}
