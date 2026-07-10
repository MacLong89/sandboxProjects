namespace Dynasty.UI;

using Dynasty.Audio;
using Sandbox.UI;

/// <summary>
/// Spawns the main menu and in-game HUD shell when the scene loads.
/// Ensures a ScreenPanel exists for consistent scaling and z-order.
/// </summary>
[Title( "Dynasty UI" )]
[Category( "Dynasty" )]
[Icon( "dashboard" )]
public sealed class DynastyHudComponent : Component
{
	protected override void OnStart()
	{
		if ( Scene.IsEditor )
			return;

		DynastyApp.Initialize();

		if ( !GameObject.Components.Get<ScreenPanel>().IsValid() )
		{
			var panel = GameObject.Components.Create<ScreenPanel>();
			panel.ZIndex = 100;
			panel.AutoScreenScale = true;
		}

		if ( !GameObject.Components.Get<DynastyGameShell>().IsValid() )
			GameObject.Components.Create<DynastyGameShell>();

		if ( !GameObject.Components.Get<DynastyAudioComponent>().IsValid() )
			GameObject.Components.Create<DynastyAudioComponent>();
	}
}
