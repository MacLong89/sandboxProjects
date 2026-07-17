namespace Sandbox;

/// <summary>Ensures required GameObjects exist when playing. Never creates GPU textures here.</summary>
[Title( "Game Bootstrap" )]
public sealed class GameBootstrap : Component
{
	protected override void OnStart()
	{
		if ( Scene.IsEditor )
			return;

		Log.Info( "[Offshore] GameBootstrap OnStart" );

		if ( Scene.GetAllComponents<FishingGameController>().FirstOrDefault() == null )
		{
			var game = new GameObject( true, "Game" );
			game.Components.Create<FishingGameController>();
			game.Components.Create<WorldPresenter>();
			game.Components.Create<AudioDirector>();
		}

		if ( Scene.GetAllComponents<PixelCamera>().FirstOrDefault() == null )
		{
			var camGo = new GameObject( true, "PixelCamera" );
			camGo.Components.Create<CameraComponent>();
			var pixel = camGo.Components.Create<PixelCamera>();
			pixel.OrthoHeight = 360f;
			pixel.ClearColor = new Color( 0.12f, 0.28f, 0.36f );
		}

		if ( Scene.GetAllComponents<FishingHudRoot>().FirstOrDefault() == null )
		{
			var ui = new GameObject( true, "ScreenUI" );
			ui.Components.Create<ScreenPanel>();
			ui.Components.Create<FishingHudRoot>();
		}

		if ( Scene.GetAllComponents<DirectionalLight>().FirstOrDefault() == null )
		{
			var lightGo = new GameObject( true, "FillLight" );
			var light = lightGo.Components.Create<DirectionalLight>();
			light.LightColor = new Color( 1.0f, 0.98f, 0.94f );
			light.SkyColor = new Color( 0.35f, 0.45f, 0.55f );
			lightGo.WorldRotation = Rotation.FromPitch( 50f );
		}

		Log.Info( "[Offshore] GameBootstrap complete" );
	}
}
