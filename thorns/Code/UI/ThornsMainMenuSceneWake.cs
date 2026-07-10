namespace Sandbox;

/// <summary>
/// Lives on a stock scene object (e.g. menu camera). Spawns menu UI/atmosphere at play — no custom types on MainMenuUi in scene JSON.
/// </summary>
[Title( "Thorns — Main Menu Scene Wake" )]
[Category( "Thorns/UI" )]
[Icon( "wake_up" )]
[Order( 2 )]
public sealed class ThornsMainMenuSceneWake : Component
{
	double _ensureAt;

	protected override void OnStart()
	{
		if ( Scene.IsEditor && !Game.IsPlaying )
			return;

		_ensureAt = Time.Now + 0.05;
	}

	protected override void OnUpdate()
	{
		if ( _ensureAt <= 0 || Time.Now < _ensureAt )
			return;

		_ensureAt = 0;
		ThornsMainMenuBootstrap.EnsureOnScene( Scene );
	}
}
