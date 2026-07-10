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

		ResetStaticState();
	}

	public static void ResetStaticState()
	{
		ThornsWorldSession.Reset();
		ThornsSceneObserver.ClearCachedLocalPlayer();
		ThornsWorldBootGate.ResetBootState();
		ThornsLocalHostSpawnCoordinator.ResetState();
		ThornsMenuJoinFlow.ResetForMainMenu();
	}
}
