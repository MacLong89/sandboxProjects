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
	public static async System.Threading.Tasks.Task EnsureMenuUiAsync( GameObject hostObject, Scene scene )
	{
		if ( hostObject is null || !hostObject.IsValid )
			throw new System.InvalidOperationException( "Main menu host GameObject is invalid." );

		if ( scene is null || !scene.IsValid )
			throw new System.InvalidOperationException( "Main menu scene is invalid." );

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
			throw new System.InvalidOperationException( "Failed to create main menu ScreenPanel." );

		ConfigureScreenPanel( screenPanel, scene );

		await System.Threading.Tasks.Task.Yield();
		await System.Threading.Tasks.Task.Yield();

		var host = hostObject.Components.Get<MainMenuHost>( FindMode.EnabledInSelf );
		if ( host is null || !host.IsValid )
			host = hostObject.Components.Create<MainMenuHost>();

		if ( host is null || !host.IsValid )
			throw new System.InvalidOperationException( "Failed to create MainMenuHost." );

		for ( var attempt = 0; attempt < 60; attempt++ )
		{
			if ( host.IsValid && host.IsPanelReady )
				break;

			await System.Threading.Tasks.Task.Yield();
		}

		if ( !host.IsValid || !host.IsPanelReady )
			throw new System.InvalidOperationException( "MainMenuHost panel did not become ready." );

		ThornsLocalSettings.Load();
		ThornsMenuServerPrefs.Load();
		ThornsHostedServerCatalog.Load();
		_ = hostObject.Components.Get<ThornsMainMenuAtmosphere>() ?? hostObject.Components.Create<ThornsMainMenuAtmosphere>();

		host.EnsureBuilt();
		if ( !host.IsUiBuilt )
			throw new System.InvalidOperationException( "MainMenuHost UI build failed." );

		host.ShowRootView();
		ThornsUiCursor.EnsureMainMenuVisible();
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
