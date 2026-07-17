namespace Terraingen.UI.Menu;

using System.Linq;
using Sandbox.UI;
using Terraingen;
using Terraingen.Multiplayer;
using Terraingen.UI;
using Terraingen.UI.Core;

/// <summary>Shared main-menu UI setup used by scene bootstrap and launcher fallback.</summary>
public static class ThornsMainMenuBootstrap
{
	/// <summary>
	/// BOOT FIX (2026-07): Prefer returning over throwing after partial setup.
	/// Thrown exceptions were caught by callers but left players on a blank "game won't boot" screen.
	/// Revert: restore throws if you need hard-fail during development.
	/// </summary>
	public static async System.Threading.Tasks.Task EnsureMenuUiAsync( GameObject hostObject, Scene scene )
	{
		if ( hostObject is null || !hostObject.IsValid )
		{
			Log.Error( "[Thorns Menu] BOOT: Main menu host GameObject is invalid." );
			return;
		}

		if ( scene is null || !scene.IsValid )
		{
			Log.Error( "[Thorns Menu] BOOT: Main menu scene is invalid." );
			return;
		}

		UiRevisionBus.ResetMenuListeners();
		if ( !ThornsMenuJoinFlow.IsProgressVisible && !ThornsSessionBootstrap.IsJoiningRemoteLobby )
			ThornsMenuJoinFlow.ResetForMainMenu();
		ThornsUiCursor.EnsureMainMenuVisible();
		ThornsMountedFiles.LogMountProbe( "main menu bootstrap" );
		ThornsPublishedAssetValidation.LogBootValidation( "main menu bootstrap" );
		if ( !ThornsMountedFiles.SamplePublishAssetsPresent )
			ThornsRequiredPublishAssets.LogMissingMounted( "main menu bootstrap" );
		EnsureMainCamera( scene );

		var screenPanel = hostObject.Components.Get<ScreenPanel>( FindMode.EnabledInSelf );
		if ( screenPanel is null || !screenPanel.IsValid )
			screenPanel = hostObject.Components.Create<ScreenPanel>();

		if ( screenPanel is null || !screenPanel.IsValid )
		{
			Log.Error( "[Thorns Menu] BOOT: Failed to create main menu ScreenPanel." );
			return;
		}

		ConfigureScreenPanel( screenPanel, scene );

		await System.Threading.Tasks.Task.Yield();
		await System.Threading.Tasks.Task.Yield();

		var host = hostObject.Components.Get<MainMenuHost>( FindMode.EnabledInSelf );
		if ( host is null || !host.IsValid )
			host = hostObject.Components.Create<MainMenuHost>();

		if ( host is null || !host.IsValid )
		{
			Log.Error( "[Thorns Menu] BOOT: Failed to create MainMenuHost." );
			TryInstallEmergencyMenuText( screenPanel, "Could not create MainMenuHost component." );
			return;
		}

		for ( var attempt = 0; attempt < 90; attempt++ )
		{
			if ( host.IsValid && host.IsPanelReady )
				break;

			await System.Threading.Tasks.Task.Yield();
		}

		if ( !host.IsValid || !host.IsPanelReady )
		{
			Log.Error( "[Thorns Menu] BOOT: MainMenuHost panel did not become ready — see emergency checklist." );
			TryInstallEmergencyMenuText( screenPanel, "Menu panel failed to bind. Check console / republish." );
			return;
		}

		ThornsLocalSettings.Load();
		ThornsMenuServerPrefs.Load();
		ThornsHostedServerCatalog.Load();
		_ = hostObject.Components.Get<ThornsMainMenuAtmosphere>() ?? hostObject.Components.Create<ThornsMainMenuAtmosphere>();

		host.EnsureBuilt();
		if ( !host.IsUiBuilt )
		{
			Log.Error( "[Thorns Menu] BOOT: MainMenuHost UI build failed — see emergency checklist." );
			TryInstallEmergencyMenuText( screenPanel, "Menu UI build failed. See console for details." );
			return;
		}

		host.ShowRootView();
		ThornsUiCursor.EnsureMainMenuVisible();
		Log.Info( "[Thorns Menu] BOOT: Main menu UI ready." );
	}

	/// <summary>Last-resort logging so "won't boot" is never silent — print recovery checklist.</summary>
	static void TryInstallEmergencyMenuText( ScreenPanel screenPanel, string message )
	{
		try
		{
			_ = screenPanel;
			Log.Error( $"[Thorns Menu] BOOT emergency: {message}" );
			Log.Error(
				"[Thorns Menu] BOOT emergency checklist: (1) terraingen.sbproj CsProjName=Code/thorns.csproj " +
				"(2) run Scripts/EnsureRequiredAssets.ps1 (3) Publish with Assets/*.png under Resources " +
				"(4) confirm scenes/thorns_main_menu.scene has ThornsMenuSceneBootstrap on MainMenuUi." );
		}
		catch ( System.Exception e )
		{
			Log.Error( e, "[Thorns Menu] BOOT: emergency UI install failed." );
		}
	}

	static void ConfigureScreenPanel( ScreenPanel screenPanel, Scene scene )
	{
		if ( screenPanel is null || !screenPanel.IsValid )
			return;

		screenPanel.Opacity = 1f;
		screenPanel.Scale = 1f;
		screenPanel.AutoScreenScale = true;
		screenPanel.ZIndex = 10000;

		if ( scene is null || !scene.IsValid )
			return;

		var cam = ResolveMenuCamera( scene );
		if ( cam is not null && cam.IsValid )
			screenPanel.TargetCamera = cam;
	}

	static void EnsureMainCamera( Scene scene )
	{
		var cam = ResolveMenuCamera( scene );
		if ( cam is null || !cam.IsValid )
		{
			Log.Warning( "[Thorns Menu] No menu camera found — UI may not render." );
			return;
		}

		cam.IsMainCamera = true;
		cam.Enabled = true;
		cam.BackgroundColor = new Color( 0.03f, 0.05f, 0.09f, 1f );
	}

	static CameraComponent ResolveMenuCamera( Scene scene )
	{
		if ( scene is null || !scene.IsValid )
			return null;

		var cam = scene.Camera;
		if ( cam is not null && cam.IsValid )
			return cam;

		var camGo = scene.GetAllObjects( true ).FirstOrDefault( o => o.Name == "MenuCamera" );
		if ( camGo is not null && camGo.IsValid )
			return camGo.Components.Get<CameraComponent>( FindMode.EverythingInSelf );

		return null;
	}
}
