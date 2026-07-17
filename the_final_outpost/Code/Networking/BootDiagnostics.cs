namespace FinalOutpost;

/// <summary>Console + on-screen boot diagnostics.</summary>
public static class BootDiagnostics
{
	public static int Frame { get; internal set; }
	public static string LastSummary { get; internal set; } = "waiting for boot";

	public static void LogSceneState( string tag, Scene scene )
	{
		if ( scene is null )
		{
			Log.Warning( $"[FinalOutpost][{tag}] scene is null" );
			return;
		}

		var cameras = scene.GetAllComponents<CameraComponent>().ToList();
		var mainCameras = cameras.Where( c => c.IsMainCamera ).ToList();
		var hudCount = scene.GetAllComponents<UI.Hud>().Count();
		var screenCount = scene.GetAllComponents<ScreenPanel>().Count();
		var hudHostCount = scene.GetAllComponents<HudHost>().Count();
		var terrainCount = scene.GetAllComponents<OutpostTerrain>().Count();
		var core = GameCore.Instance;

		LastSummary = $"cams={cameras.Count} main={mainCameras.Count} hud={hudCount} screens={screenCount} terrain={terrainCount} core={(core is not null)}";

		Log.Info( $"[FinalOutpost][{tag}] {LastSummary} phase={core?.Phase} bootErr={GameCore.BootError ?? "none"} welcome={core?.WelcomePending} daily={core?.DailyPending}" );

		foreach ( var cam in mainCameras )
		{
			Log.Info( $"[FinalOutpost][{tag}] mainCam name='{cam.GameObject.Name}' pos={cam.WorldPosition} rot={cam.WorldRotation}" );
		}

		if ( mainCameras.Count == 0 )
			Log.Warning( $"[FinalOutpost][{tag}] NO MAIN CAMERA" );

		foreach ( var screen in scene.GetAllComponents<ScreenPanel>() )
		{
			var targetName = screen.TargetCamera.IsValid() ? screen.TargetCamera.GameObject.Name : "none";
			var targetMain = screen.TargetCamera.IsValid() && screen.TargetCamera.IsMainCamera;
			Log.Info( $"[FinalOutpost][{tag}] ScreenPanel '{screen.GameObject.Name}' target='{targetName}' targetMain={targetMain} z={screen.ZIndex}" );
		}

		if ( hudCount == 0 )
			Log.Warning( $"[FinalOutpost][{tag}] NO HUD COMPONENT" );

		if ( terrainCount == 0 )
			Log.Warning( $"[FinalOutpost][{tag}] NO TERRAIN" );
	}
}

/// <summary>Logs scene state for the first few seconds after boot.</summary>
public sealed class BootDiagnosticsRunner : Component
{
	int _ticks;

	protected override void OnStart()
	{
		Log.Info( "[FinalOutpost] BootDiagnosticsRunner OnStart" );
		BootDiagnostics.LogSceneState( "DiagRunnerStart", Scene );
	}

	protected override void OnUpdate()
	{
		BootDiagnostics.Frame++;

		_ticks++;
		if ( _ticks is 1 or 30 or 120 or 300 )
			BootDiagnostics.LogSceneState( $"DiagTick{_ticks}", Scene );
	}
}
