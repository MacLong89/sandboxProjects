namespace Terraingen.UI.Menu;

using System.Threading;
using System.Threading.Tasks;
using Sandbox.Network;
using Terraingen.Multiplayer;
using Terraingen.TerrainGen;
using Terraingen.UI.Core;

/// <summary>Main menu → gameplay scene transition.</summary>
public static class ThornsMenuSceneLoader
{
	public const string GameplayScenePath = "scenes/thorns_terrain.scene";
	public const string MainMenuScenePath = "scenes/thorns_main_menu.scene";

	public static bool IsInGameplayScene( Scene scene = null ) =>
		TryGetGameplayScene( scene, out _ );

	/// <summary>
	/// Resolves the live gameplay scene. Joining clients can keep the menu as <see cref="Game.ActiveScene"/>
	/// while the host's terrain scene is already resident — use bootstrap/network markers globally.
	/// </summary>
	public static bool TryGetGameplayScene( Scene scene, out Scene gameplayScene )
	{
		gameplayScene = null;

		var bootstrap = ThornsTerrainBootstrap.Instance;
		if ( bootstrap.IsValid() && bootstrap.Scene is { IsValid: true } bootstrapScene )
		{
			gameplayScene = bootstrapScene;
			return true;
		}

		if ( scene is { IsValid: true } explicitScene && HasGameplayMarker( explicitScene ) )
		{
			gameplayScene = explicitScene;
			return true;
		}

		var active = Game.ActiveScene;
		if ( active is { IsValid: true } && HasGameplayMarker( active ) )
		{
			gameplayScene = active;
			return true;
		}

		return false;
	}

	static bool HasGameplayMarker( Scene scene ) =>
		scene.GetAllComponents<ThornsTerrainBootstrap>().Any()
		|| scene.GetAllComponents<ThornsNetworkGameManager>().Any();

	public static async Task LoadGameplayAsync()
	{
		if ( IsInGameplayScene() )
		{
			Log.Info( "[Thorns Menu] Already in gameplay scene — skipping ChangeScene." );
			DismissActiveMainMenuUi();
			return;
		}

		ThornsMenuAudioHandoff.ArmForGameplayTransition();
		ThornsMainMenuAtmosphere.BeginMusicFadeOut( 1.5f );
		ThornsMenuJoinFlow.SetStage( ThornsMenuJoinStage.LoadingWorld );
		ThornsLoadingScreenUtil.Show( ThornsMenuJoinFlow.StageLabel( ThornsMenuJoinStage.LoadingWorld ) );
		await Task.Delay( 80 );

		var opt = new SceneLoadOptions();
		if ( !opt.SetScene( GameplayScenePath ) )
		{
			Log.Error( $"[Thorns Menu] Could not load '{GameplayScenePath}'." );
			ThornsMenuAudioHandoff.Cancel();
			ThornsMenuJoinFlow.Reset();
			return;
		}

		if ( !Game.ChangeScene( opt ) )
		{
			if ( IsInGameplayScene() )
			{
				Log.Info( "[Thorns Menu] ChangeScene rejected — gameplay scene is already active." );
				DismissActiveMainMenuUi();
				return;
			}

			Log.Error( "[Thorns Menu] Game.ChangeScene rejected." );
			ThornsMenuAudioHandoff.Cancel();
			ThornsMenuJoinFlow.Reset();
			return;
		}

		for ( var attempt = 0; attempt < 120; attempt++ )
		{
			await Task.Delay( 50 );
			if ( IsInGameplayScene() )
			{
				if ( TryGetGameplayScene( null, out var gameplayScene ) )
				{
					ThornsSessionEnterController.Ensure( gameplayScene );
					ThornsWorldBootGate.EnsureDriver();
					var bootstrap = gameplayScene.GetAllComponents<ThornsTerrainBootstrap>().FirstOrDefault();
					if ( bootstrap.IsValid() )
						ThornsGameplayUiHost.EnsureOnBootstrap( bootstrap );
					else
						ThornsGameplayUiHost.EnsureOnHost( gameplayScene.GetAllComponents<ThornsNetworkGameManager>().FirstOrDefault()?.GameObject );
					ThornsJoinFlowDebug.LogMilestone(
						$"LoadGameplayAsync ready scene={gameplayScene.Name} controller={( ThornsSessionEnterController.Instance?.IsValid() == true )} uiHost={( ThornsGameplayUiHost.Instance?.IsValid() == true )}" );
				}

				return;
			}
		}

		Log.Warning( "[Thorns Menu] Gameplay scene did not become active after ChangeScene." );
	}

	/// <summary>Tears down menu ScreenPanels so stale overlays cannot block gameplay after a scene change.</summary>
	public static void DismissActiveMainMenuUi()
	{
		DestroyActiveMainMenuUiRoots();
	}

	public static void DestroyActiveMainMenuUiRoots()
	{
		if ( MainMenuHost.Instance is { IsValid: true } liveHost )
		{
			var root = liveHost.GameObject;
			if ( root.IsValid() )
				root.Destroy();
		}

		var scene = Game.ActiveScene;
		if ( scene is null || !scene.IsValid )
			return;

		foreach ( var host in scene.GetAllComponents<MainMenuHost>().ToArray() )
		{
			if ( !host.IsValid() )
				continue;

			var root = host.GameObject;
			if ( root.IsValid() )
				root.Destroy();
		}

		foreach ( var bootstrap in scene.GetAllComponents<ThornsMenuSceneBootstrap>().ToArray() )
		{
			if ( !bootstrap.IsValid() )
				continue;

			var root = bootstrap.GameObject;
			if ( root.IsValid() && !root.Components.Get<MainMenuHost>( FindMode.EnabledInSelf ).IsValid() )
				root.Destroy();
		}
	}

	public static async Task LoadMainMenuAsync()
	{
		if ( Networking.IsActive )
		{
			Networking.Disconnect();
			await Task.Delay( 300 );
		}

		ThornsNetworkSessionReset.ResetStaticState( "load-main-menu" );
		ThornsMenuAudioHandoff.Cancel();
		ThornsUiCursor.EnsureMainMenuVisible();
		await Task.Delay( 80 );

		if ( IsMainMenuScene( Game.ActiveScene ) )
			return;

		var opt = new SceneLoadOptions();
		if ( !opt.SetScene( MainMenuScenePath ) )
		{
			Log.Error( $"[Thorns Menu] Could not load '{MainMenuScenePath}'." );
			return;
		}

		if ( !Game.ChangeScene( opt ) )
		{
			Log.Error( "[Thorns Menu] Game.ChangeScene to main menu rejected." );
			return;
		}

		for ( var attempt = 0; attempt < 120; attempt++ )
		{
			await Task.Delay( 50 );
			if ( IsMainMenuScene( Game.ActiveScene ) )
				return;
		}

		Log.Warning( "[Thorns Menu] Thorns main menu did not become active after disconnect." );
	}

	static bool IsMainMenuScene( Scene scene )
	{
		if ( scene is null || !scene.IsValid )
			return false;

		if ( IsInGameplayScene( scene ) )
			return false;

		return scene.GetAllComponents<MainMenuHost>().Any()
		       || scene.GetAllComponents<ThornsMenuSceneBootstrap>().Any();
	}
}
