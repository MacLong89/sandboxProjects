namespace Sandbox;

/// <summary>
/// Scene hook for the main menu: creates <see cref="ThornsMainMenuUI"/> after play starts so scene JSON does not
/// deserialize <see cref="PanelComponent"/> directly (avoids "Missing Component" / TypeLibrary timing when startup scene loads).
/// </summary>
[Title( "Thorns — Main Menu Launcher" )]
[Category( "Thorns/UI" )]
[Icon( "menu" )]
[Order( 3 )]
public sealed class ThornsMainMenuLauncher : Component
{
	double _ensureMenuUiAt;

	protected override void OnStart()
	{
		if ( Scene.IsEditor && !Game.IsPlaying )
			return;

		_ensureMenuUiAt = Time.Now + 0.05;
	}

	protected override void OnUpdate()
	{
		if ( _ensureMenuUiAt <= 0 || Time.Now < _ensureMenuUiAt )
			return;

		_ensureMenuUiAt = 0;
		EnsureMainMenuUi();
	}

	void EnsureMainMenuUi()
	{
		if ( !this.IsValid() || !GameObject.IsValid() )
			return;

		ThornsMainMenuBootstrap.EnsureMenuComponents( GameObject );
	}
}
