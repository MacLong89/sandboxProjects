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

	public static bool IsInGameplayScene( Scene scene = null )
	{
		scene ??= Game.ActiveScene;
		if ( scene is null || !scene.IsValid )
			return false;

		return scene.GetAllComponents<ThornsTerrainBootstrap>().Any()
		       || scene.GetAllComponents<ThornsNetworkGameManager>().Any();
	}

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
				return;
		}

		Log.Warning( "[Thorns Menu] Gameplay scene did not become active after ChangeScene." );
	}

	/// <summary>Tears down menu ScreenPanels so stale overlays cannot block gameplay after a scene change.</summary>
	public static void DismissActiveMainMenuUi()
	{
		DestroyActiveMainMenuUiRoots();
		ThornsMenuJoinFlow.CompleteEnterWorld();
	}

	public static void DestroyActiveMainMenuUiRoots()
	{
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
		await ThornsNetworkSessionReset.DisconnectAndResetAsync();
		ThornsMenuAudioHandoff.Cancel();
		ThornsUiCursor.EnsureMainMenuVisible();
		await Task.Delay( 50 );

		var opt = new SceneLoadOptions();
		if ( !opt.SetScene( MainMenuScenePath ) )
		{
			Log.Error( $"[Thorns Menu] Could not load '{MainMenuScenePath}'." );
			return;
		}

		if ( !Game.ChangeScene( opt ) )
			Log.Error( "[Thorns Menu] Game.ChangeScene to main menu rejected." );
	}
}
