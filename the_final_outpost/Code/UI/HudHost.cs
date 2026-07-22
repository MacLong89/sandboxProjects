namespace FinalOutpost;

/// <summary>Creates and wires <see cref="ScreenPanel"/> + <see cref="UI.Hud"/> to the active camera.</summary>
public sealed class HudHost : Component
{
	/// <summary>When false, screen HUD and world labels are hidden (U / ToggleUi).</summary>
	public static bool UiVisible { get; private set; } = true;

	protected override void OnStart()
	{
		UiVisible = true;

		var screen = Components.GetOrCreate<ScreenPanel>();
		screen.AutoScreenScale = true;
		screen.ZIndex = 100;

		if ( Components.Get<UI.Hud>( FindMode.EverythingInSelf ) is null )
			Components.Create<UI.Hud>();

		BindCamera( screen );
		ApplyVisibility();
		Log.Info( $"[FinalOutpost] HudHost OnStart hud={(Components.Get<UI.Hud>() is not null)} targetCam={screen.TargetCamera.IsValid()}" );
	}

	protected override void OnUpdate()
	{
		if ( Input.Pressed( "ToggleUi" ) )
		{
			UiVisible = !UiVisible;
			ApplyVisibility();
			Log.Info( $"[FinalOutpost] UI {(UiVisible ? "enabled" : "hidden")} (ToggleUi)" );
		}
		else if ( !UiVisible )
		{
			// Keep newly spawned world labels hidden after they appear mid-combat.
			ApplyVisibility();
		}

		var screen = Components.Get<ScreenPanel>();
		if ( screen is null )
			return;

		var mainCam = Scene.GetAllComponents<CameraComponent>().FirstOrDefault( c => c.IsMainCamera );
		if ( mainCam.IsValid() && screen.TargetCamera != mainCam )
			screen.TargetCamera = mainCam;

		// After ScreenPanel/camera bind — same slot aimbox uses for SyncAfterUi.
		TakeoverCursor.SyncAfterUi();
	}

	protected override void OnPreRender()
	{
		TakeoverCursor.Sync();
	}

	private void BindCamera( ScreenPanel screen )
	{
		var mainCam = Scene.GetAllComponents<CameraComponent>().FirstOrDefault( c => c.IsMainCamera );
		if ( mainCam.IsValid() )
		{
			screen.TargetCamera = mainCam;
			Log.Info( $"[FinalOutpost] HudHost bound ScreenPanel to camera '{mainCam.GameObject.Name}'" );
		}
		else
		{
			Log.Warning( "[FinalOutpost] HudHost could not find main camera" );
		}
	}

	void ApplyVisibility()
	{
		foreach ( var screen in Scene.GetAllComponents<ScreenPanel>() )
		{
			if ( screen.IsValid() )
				screen.Enabled = UiVisible;
		}

		foreach ( var world in Scene.GetAllComponents<Sandbox.WorldPanel>() )
		{
			if ( world.IsValid() )
				world.Enabled = UiVisible;
		}
	}
}
