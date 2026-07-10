namespace ThinkDrink;

/// <summary>Finalizes WorldPanel render settings after the Razor panel is attached.</summary>
[Order( 1000 )]
public sealed class StudioWorldPanelSetup : Component
{
	protected override void OnStart()
	{
		var wp = Components.Get<WorldPanel>( FindMode.EverythingInSelf );
		if ( !wp.IsValid() )
		{
			Log.Warning( "[ThinkDrink][WorldPanel] missing WorldPanel on scoreboard display." );
			return;
		}

		var cfg = Components.Get<StudioWorldPanelConfig>( FindMode.EverythingInSelf );
		if ( cfg.IsValid() )
		{
			wp.PanelSize = cfg.PanelSize;
			wp.RenderScale = cfg.RenderScale;
		}

		wp.LookAtCamera = false;
		wp.InteractionRange = 0f;
		wp.RenderOptions.Game = true;
		wp.RenderOptions.Overlay = false;
		wp.RenderOptions.Bloom = true;

		Log.Info(
			$"[ThinkDrink][WorldPanel] {GameObject.Name} size={wp.PanelSize} scale={wp.RenderScale} " +
			$"worldSize={wp.PanelSize * wp.RenderScale} pos={GameObject.WorldPosition} rot={GameObject.WorldRotation.Angles()}" );
	}
}
