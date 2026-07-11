namespace Terraingen.UI.Core;

using Sandbox.UI;
using Terraingen;
using Terraingen.Buildings;
using Terraingen.Multiplayer;
using Terraingen.NpcGuild;
using Terraingen.Player;
using Terraingen.TerrainGen;
using Terraingen.UI.Menu;
using Terraingen.Victory;

/// <summary>Bootstraps gameplay HUD and menu on a dedicated local ScreenPanel (not shared with debug HUD).</summary>
[Title( "Thorns Gameplay UI Host" )]
[Category( "Thorns/UI" )]
[Icon( "dashboard" )]
public sealed class ThornsGameplayUiHost : Component
{
	public static ThornsGameplayUiHost Instance { get; private set; }

	GameObject _screenUiRoot;
	bool _bootstrapped;
	bool _loggedBootstrapUi;
	System.Threading.CancellationTokenSource _bootstrapCts;

	protected override void OnAwake()
	{
		ThornsGameplaySession.PrepareScene();
	}

	protected override void OnStart()
	{
		if ( Scene.IsEditor && !Game.IsPlaying )
			return;

		Instance = this;
		_bootstrapCts?.Cancel();
		_bootstrapCts?.Dispose();
		_bootstrapCts = new System.Threading.CancellationTokenSource();
		_ = BootstrapAsync( _bootstrapCts.Token );
	}

	async System.Threading.Tasks.Task BootstrapAsync( System.Threading.CancellationToken cancel )
	{
		try
		{
			ThornsGameplaySession.PrepareScene();
			ThornsLocalSettings.Load();
			await WaitForMountedAssetsAsync( cancel );
			ThornsMountedFiles.LogMountProbe( "gameplay bootstrap" );
			if ( !ThornsMountedFiles.SamplePublishAssetsPresent )
				ThornsRequiredPublishAssets.LogMissingMounted( "gameplay bootstrap" );

			await System.Threading.Tasks.Task.Yield();
			await System.Threading.Tasks.Task.Yield();

			EnsureScreenUiComponents();
			LogScreenUiState( "after-bootstrap" );
			_ = ThornsGuildWorldService.EnsureInstance();
			_ = ThornsNpcGuildWorldService.EnsureInstance();

			_ = Scene.GetAllComponents<ThornsMapWorldService>().FirstOrDefault()
			    ?? Scene.GetAllComponents<ThornsNetworkGameManager>().FirstOrDefault()?.Components.Create<ThornsMapWorldService>();

			_ = ThornsVictoryManager.EnsureInstance();

			_bootstrapped = true;
			LogJoinableServerState();

			if ( ThornsMenuJoinFlow.CurrentStage != ThornsMenuJoinStage.Idle )
				ThornsMenuJoinFlow.SetStage( ThornsMenuJoinStage.EnteringWorld );
		}
		catch ( Exception e )
		{
			Log.Error( e, "[Thorns UI] Gameplay UI bootstrap failed." );
		}
	}

	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying )
			return;

		try
		{
			OnUpdateGameplayUi();
		}
		catch ( Exception e )
		{
			if ( _lastUpdateErrorLog > 5f )
			{
				_lastUpdateErrorLog = 0f;
				Log.Error( e, "[Thorns UI] GameplayUiHost Update failed — restart the play session if this persists after hot reload." );
			}
		}
	}

	static TimeSince _lastUpdateErrorLog;

	static async System.Threading.Tasks.Task WaitForMountedAssetsAsync( System.Threading.CancellationToken cancel )
	{
		if ( ThornsMountedFiles.SamplePublishAssetsPresent )
			return;

		ThornsMenuJoinFlow.SetProgressMessage( "Loading game files..." );
		for ( var attempt = 0; attempt < 120; attempt++ )
		{
			cancel.ThrowIfCancellationRequested();
			if ( ThornsMountedFiles.SamplePublishAssetsPresent )
				return;

			await System.Threading.Tasks.Task.Delay( 100, cancel );
		}

		ThornsRequiredPublishAssets.LogMissingMounted( "gameplay bootstrap timeout" );
	}

	void OnUpdateGameplayUi()
	{
		if ( !_bootstrapped )
			EnsureScreenUiComponents();

		var menuHost = ResolveMenuHost();

		ThornsPlayerBuildingController.Local?.TickBuildInput();
		ThornsPlayerHotbarInput.Tick();

		if ( !ThornsMenuHost.IsOpen )
			ThornsUiManager.SetContext( ThornsUiManager.UiContext.Gameplay );

		if ( menuHost.IsValid() )
		{
			menuHost.EnsureUiReady();
			menuHost.HandleGameplayInput();
		}

		ThornsUiCursor.SyncForActiveContext();

		ThornsGameplayUiDiagnostics.Heartbeat( this, menuHost, _screenUiRoot );

		if ( ThornsLocalHostSpawnCoordinator.IsDeferredPending )
			ThornsLocalHostSpawnCoordinator.TickDeferred();
	}

	ThornsMenuHost ResolveMenuHost()
	{
		if ( ThornsMenuHost.Instance.IsValid() )
			return ThornsMenuHost.Instance;

		if ( _screenUiRoot.IsValid() )
			return _screenUiRoot.Components.Get<ThornsMenuHost>( FindMode.EnabledInSelf );

		return default;
	}

	public void EnsureScreenUiForDeferredHud()
	{
		ThornsJoinFlowDebug.JoinInfo( "EnsureScreenUiForDeferredHud begin" );
		EnsureScreenUiComponents();
		var menuHost = ResolveMenuHost();
		if ( menuHost.IsValid() )
		{
			ThornsJoinFlowDebug.JoinInfo( $"EnsureScreenUiForDeferredHud menuHost ok — {ThornsJoinFlowDebug.DescribeHud()}" );
			menuHost.EnsureUiReady();
		}
		else
		{
			ThornsJoinFlowDebug.JoinWarn(
				$"EnsureScreenUiForDeferredHud menuHost still missing — screenRoot={_screenUiRoot.IsValid()} scene={Scene?.Name ?? "null"}" );
		}
	}

	/// <summary>Rebind gameplay ScreenPanel to the active scene camera after local pawn spawn.</summary>
	public static void RefreshScreenPanelCamera( Scene scene )
	{
		if ( scene is null || !scene.IsValid )
			return;

		var cam = scene.Camera;
		if ( !cam.IsValid() )
			return;

		foreach ( var screenPanel in scene.GetAllComponents<ScreenPanel>() )
		{
			if ( !screenPanel.IsValid() )
				continue;

			var go = screenPanel.GameObject;
			if ( !go.IsValid() || go.Name != "Thorns Screen UI" )
				continue;

			screenPanel.TargetCamera = cam;
			return;
		}

		if ( Instance is not null && Instance.IsValid() )
			Instance.EnsureScreenUiComponents();
	}

	void EnsureScreenUiComponents()
	{
		RemoveCompetingDebugPanel();

		if ( !_screenUiRoot.IsValid() )
		{
			_screenUiRoot = new GameObject( true, "Thorns Screen UI" );
			_screenUiRoot.SetParent( null );
		}

		var screenPanel = _screenUiRoot.Components.Get<ScreenPanel>( FindMode.EnabledInSelf );
		if ( !screenPanel.IsValid() )
			screenPanel = _screenUiRoot.Components.Create<ScreenPanel>();

		ConfigureScreenPanel( screenPanel, Scene );

		var menuHost = _screenUiRoot.Components.Get<ThornsMenuHost>( FindMode.EnabledInSelf );
		if ( !menuHost.IsValid() )
			menuHost = _screenUiRoot.Components.Create<ThornsMenuHost>();

		if ( menuHost.IsValid() )
			menuHost.EnsureUiReady();

		ThornsLocalHostSpawnDriver.Ensure();

		if ( !_loggedBootstrapUi )
		{
			_loggedBootstrapUi = true;
			ThornsGameplayUiDiagnostics.Event(
				$"EnsureScreenUi root='{_screenUiRoot.Name}' parent={_screenUiRoot.Parent?.Name ?? "scene"} " +
				$"screenPanel={screenPanel.IsValid()} menuHost={menuHost.IsValid()}" );
		}
	}

	static void ConfigureScreenPanel( ScreenPanel screenPanel, Scene scene )
	{
		if ( !screenPanel.IsValid() )
			return;

		screenPanel.Opacity = 1f;
		screenPanel.Scale = 1f;
		screenPanel.AutoScreenScale = true;
		screenPanel.ZIndex = 10000;

		if ( scene is null || !scene.IsValid )
			return;

		var cam = scene.Camera;
		if ( cam.IsValid() )
			screenPanel.TargetCamera = cam;
	}

	static void RemoveCompetingDebugPanel()
	{
		var scene = Game.ActiveScene;
		if ( scene is null || !scene.IsValid )
			return;

		foreach ( var debugHud in scene.GetAllComponents<ThornsDebugHud>() )
		{
			if ( !debugHud.IsValid() )
				continue;

			ThornsGameplayUiDiagnostics.Warn(
				$"Destroying competing ThornsDebugHud on '{debugHud.GameObject.Name}' (only one PanelComponent per ScreenPanel)." );
			debugHud.Destroy();
		}

		foreach ( var screenPanel in scene.GetAllComponents<ScreenPanel>() )
		{
			if ( !screenPanel.IsValid() )
				continue;

			var go = screenPanel.GameObject;
			if ( !go.IsValid() || go.Name == "Thorns Screen UI" )
				continue;

			if ( go.Components.Get<ThornsMenuHost>( FindMode.EnabledInSelf ) is { IsValid: true } )
				continue;

			if ( go.Components.Get<ThornsDebugHudHost>( FindMode.EnabledInSelf ) is not { IsValid: true } )
				continue;

			ThornsGameplayUiDiagnostics.Warn(
				$"Destroying competing ScreenPanel on '{go.Name}' — gameplay menu host owns screen UI." );
			screenPanel.Destroy();
		}
	}

	static void LogJoinableServerState()
	{
		if ( !Networking.IsActive )
		{
			ThornsGameplayUiDiagnostics.Warn( "Networking not active — server is not joinable until a lobby is created." );
			return;
		}

		ThornsGameplayUiDiagnostics.Event(
			$"Server joinable: active={Networking.IsActive} host={Networking.IsHost} " +
			"(public lobby via ThornsNetworkGameManager.CreateLobbyOnLoad — visible in server browser)" );
	}

	void LogScreenUiState( string phase )
	{
		var menuHost = ResolveMenuHost();
		var panelOk = menuHost.IsValid() && menuHost.Panel.IsValid;
		ThornsGameplayUiDiagnostics.Event(
			$"{phase}: screenPanels={Scene.GetAllComponents<ScreenPanel>().Count()} " +
			$"menuHost={menuHost.IsValid()} panelOk={panelOk} uiBuilt={menuHost.IsValid() && menuHost.IsUiBuilt}" );

		if ( panelOk )
			ThornsGameplayUiDiagnostics.Event( ThornsGameplayUiDiagnostics.DescribePanel( menuHost.Panel, "menuRoot" ) );
	}

	protected override void OnDestroy()
	{
		_bootstrapCts?.Cancel();
		_bootstrapCts?.Dispose();
		_bootstrapCts = null;

		try
		{
			ThornsMenuHost.ForceGameplayState();
		}
		catch ( Exception e )
		{
			Log.Warning( e, "[Thorns UI] ForceGameplayState during GameplayUiHost destroy." );
		}

		if ( _screenUiRoot.IsValid() )
			_screenUiRoot.Destroy();

		if ( Instance == this )
			Instance = null;

		ThornsUiCursor.SyncForActiveContext();
	}

	public static void EnsureOnBootstrap( ThornsTerrainBootstrap bootstrap ) => EnsureOnHost( bootstrap?.GameObject );

	public static void EnsureOnHost( GameObject host )
	{
		if ( !host.IsValid() )
			return;

		_ = host.Components.Get<ThornsGameplayUiHost>() ?? host.Components.Create<ThornsGameplayUiHost>();
	}
}
