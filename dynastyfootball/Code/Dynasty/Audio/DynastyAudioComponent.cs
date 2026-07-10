using Dynasty.Bootstrap;

namespace Dynasty.Audio;

/// <summary>
/// Drives menu music and reacts to league events with SFX.
/// </summary>
[Title( "Dynasty Audio" )]
[Category( "Dynasty" )]
[Icon( "volume_up" )]
public sealed class DynastyAudioComponent : Component
{
	GameScreen _lastScreen;

	protected override void OnStart()
	{
		if ( Scene.IsEditor )
			return;

		DynastyApp.Initialize();
		DynastyAudio.RegisterEventHandlers();
		_lastScreen = DynastyApp.Session.Screen;
		SyncMenuMusic();
	}

	protected override void OnUpdate()
	{
		if ( Scene.IsEditor )
			return;

		var screen = DynastyApp.Session.Screen;
		if ( screen == _lastScreen )
			return;

		_lastScreen = screen;
		SyncMenuMusic();
	}

	protected override void OnDestroy()
	{
		DynastyAudio.StopMenuMusic();
	}

	void SyncMenuMusic()
	{
		if ( DynastyApp.Session.Screen == GameScreen.MainMenu )
			DynastyAudio.StartMenuMusic();
		else
			DynastyAudio.StopMenuMusic();
	}
}
