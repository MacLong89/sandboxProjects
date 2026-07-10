using Dynasty.Audio;
using Dynasty.LeagueNet;

namespace Dynasty.Bootstrap;

/// <summary>
/// Single scene entry point: creates lobby and ensures UI is on screen.
/// Add this + ScreenPanel + DynastyGameShell to the scene, or let this spawn UI at runtime.
/// </summary>
[Title( "Dynasty Game Entry" )]
[Category( "Dynasty" )]
[Icon( "sports_football" )]
public sealed class DynastyGameEntry : Component
{
	[Property] public bool CreateOfflineLobby { get; set; } = false;
	[Property] public bool SpawnUiIfMissing { get; set; } = true;

	protected override Task OnLoad()
	{
		if ( Scene.IsEditor )
			return Task.CompletedTask;

		Log.Info( "[Dynasty] Game entry loading…" );
		DynastyApp.Initialize();

		if ( CreateOfflineLobby && !GameNetworking.IsActive )
			GameNetworking.CreateLobby( new() );

		return Task.CompletedTask;
	}

	protected override void OnUpdate()
	{
		if ( !Scene.IsEditor )
			Mouse.Visibility = MouseVisibility.Visible;
	}

	protected override void OnStart()
	{
		if ( Scene.IsEditor )
			return;

		Mouse.Visibility = MouseVisibility.Visible;
		Log.Info( "[Dynasty] Ensuring league host and UI are present…" );
		DynastyApp.Initialize();

		if ( !GameObject.Components.Get<LeagueHostComponent>().IsValid() )
			GameObject.Components.Create<LeagueHostComponent>();

		if ( !SpawnUiIfMissing )
			return;

		var uiObject = Scene.GetAllObjects( true ).FirstOrDefault( go => go.Name == "UI" );
		if ( uiObject == null )
		{
			uiObject = Scene.CreateObject();
			uiObject.Name = "UI";
		}

		if ( !uiObject.Components.Get<ScreenPanel>().IsValid() )
		{
			var panel = uiObject.Components.Create<ScreenPanel>();
			panel.ZIndex = 100;
			panel.AutoScreenScale = true;
		}

		if ( !uiObject.Components.Get<Dynasty.UI.DynastyGameShell>().IsValid() )
			uiObject.Components.Create<Dynasty.UI.DynastyGameShell>();

		if ( !uiObject.Components.Get<DynastyAudioComponent>().IsValid() )
			uiObject.Components.Create<DynastyAudioComponent>();
	}
}
