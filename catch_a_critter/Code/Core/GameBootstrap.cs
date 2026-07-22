namespace CatchACritter;

/// <summary>Scene bootstrap — creates every system, the HUD, and the camera if missing.</summary>
public sealed class GameBootstrap : Component
{
	protected override void OnAwake()
	{
		if ( !Components.Get<PlayerProgress>().IsValid() )
			Components.Create<PlayerProgress>();

		if ( !Components.Get<CritterGame>().IsValid() )
			Components.Create<CritterGame>();

		if ( !Components.Get<CritterSpawner>().IsValid() )
			Components.Create<CritterSpawner>();

		EnsureHud();
		EnsureCamera();
	}

	void EnsureHud()
	{
		var existing = Scene.GetAllComponents<ScreenPanel>().FirstOrDefault();
		GameObject hudRoot;
		if ( existing.IsValid() )
		{
			hudRoot = existing.GameObject;
		}
		else
		{
			hudRoot = new GameObject( true, "HUD" );
			hudRoot.SetParent( GameObject );
			hudRoot.Components.Create<ScreenPanel>();
		}

		if ( !hudRoot.Components.Get<Hud>().IsValid() )
			hudRoot.Components.Create<Hud>();
		if ( !hudRoot.Components.Get<MenuPanel>().IsValid() )
			hudRoot.Components.Create<MenuPanel>();
	}

	void EnsureCamera()
	{
		if ( Scene.GetAllComponents<PlayerCamera>().Any() ) return;
		var cam = new GameObject( true, "Camera" );
		cam.SetParent( GameObject );
		cam.Components.Create<PlayerCamera>();
	}
}
