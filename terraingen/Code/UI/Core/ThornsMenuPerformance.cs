namespace Terraingen.UI.Core;

using Terraingen;
using Terraingen.Player;

/// <summary>Shared menu/overlay performance switches — keeps HUD and world work off hot paths.</summary>
public static class ThornsMenuPerformance
{
	static bool _tabMenuOpen;
	static bool _victoryIntroOpen;
	static bool _worldCameraPaused;
	static bool _cameraWasEnabled = true;

	public static bool IsTabMenuOpen => _tabMenuOpen;

	public static bool IsVictoryIntroOpen => _victoryIntroOpen;

	public static bool IsOverlayUiOpen =>
		_tabMenuOpen
		|| _victoryIntroOpen
		|| ThornsMenuHost.IsWorldContainerOpen
		|| ThornsPlayerGameplay.Local?.IsAwaitingWorldContainerUi == true
		|| ThornsMenuHost.IsRadioShopOpen
		|| ThornsMenuHost.IsResearchOpen
		|| ThornsMenuHost.IsCampfireOpen
		|| ThornsMenuHost.IsWorkbenchOpen
		|| ThornsUiManager.IsModalOpen
		|| ThornsUiManager.TopPriority >= ThornsUiPriority.FullscreenMenu;

	public static void SetTabMenuOpen( bool open )
	{
		if ( _tabMenuOpen == open )
			return;

		_tabMenuOpen = open;
		SyncWorldCameraPause();
	}

	public static void SetVictoryIntroOverlayOpen( bool open )
	{
		if ( _victoryIntroOpen == open )
			return;

		_victoryIntroOpen = open;
		SyncWorldCameraPause();
	}

	static void SyncWorldCameraPause()
	{
		if ( _tabMenuOpen || _victoryIntroOpen )
			PauseWorldCamera();
		else
			ResumeWorldCamera();
	}

	static void PauseWorldCamera()
	{
		if ( _worldCameraPaused )
			return;

		var scene = Game.ActiveScene;
		if ( scene is null || !scene.IsValid )
			return;

		var player = ThornsSceneObserver.FindLocalPlayerObject( scene );
		if ( !player.IsValid() )
			return;

		if ( !ThornsPlayerFirstPersonRig.TryResolveActivePlayerCamera( player, out var camera ) || !camera.IsValid() )
			return;

		_cameraWasEnabled = camera.Enabled;
		camera.Enabled = false;
		_worldCameraPaused = true;
	}

	static void ResumeWorldCamera()
	{
		if ( !_worldCameraPaused )
			return;

		_worldCameraPaused = false;

		var scene = Game.ActiveScene;
		if ( scene is null || !scene.IsValid )
			return;

		var player = ThornsSceneObserver.FindLocalPlayerObject( scene );
		if ( !player.IsValid() )
			return;

		if ( ThornsPlayerFirstPersonRig.TryResolveActivePlayerCamera( player, out var camera ) && camera.IsValid() )
			camera.Enabled = _cameraWasEnabled;

		ThornsSceneObserver.EnsureLocalPawnOwnsMainCamera( scene, player );
	}
}
