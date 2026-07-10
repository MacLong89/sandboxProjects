namespace Sandbox;

using Terraingen.UI.Menu;

[Title( "Thorns Gameplay Enter Audio Fade" )]
[Category( "Thorns/Audio" )]
[Icon( "music_note" )]
public sealed class ThornsGameplayEnterAudioFade : Component
{
	[Property] public float FadeSeconds { get; set; } = 10f;
	[Property] public float WorldAmbienceTargetMultiplier { get; set; } = 1f;

	double _fadeStartTime;
	float _menuMusicStartVolume;
	float _menuAmbienceStartVolume;
	bool _active;
	ThornsWorldAmbience _worldAmbience;

	protected override void OnStart()
	{
		_worldAmbience = Components.Get<ThornsWorldAmbience>();
		if ( !ThornsMenuAudioHandoff.HasAny )
		{
			ThornsMenuAudioHandoff.Clear();
			return;
		}

		_active = true;
		_fadeStartTime = Time.Now;
		_menuMusicStartVolume = ThornsMenuAudioHandoff.Music is { IsValid: true } ? ThornsMenuAudioHandoff.Music.Volume : 0f;
		_menuAmbienceStartVolume = ThornsMenuAudioHandoff.Ambience is { IsValid: true } ? ThornsMenuAudioHandoff.Ambience.Volume : 0f;

		if ( _worldAmbience.IsValid() )
			_worldAmbience.RuntimeVolumeMultiplier = 0f;
	}

	protected override void OnUpdate()
	{
		if ( !_active )
			return;

		var duration = MathF.Max( 0.05f, FadeSeconds );
		var elapsed = (float)(Time.Now - _fadeStartTime);
		var t = Math.Clamp( elapsed / duration, 0f, 1f );
		var menuMul = 1f - t;

		if ( ThornsMenuAudioHandoff.Music is { IsValid: true } )
			ThornsMenuAudioHandoff.Music.Volume = MathF.Max( 0f, _menuMusicStartVolume * menuMul );
		if ( ThornsMenuAudioHandoff.Ambience is { IsValid: true } )
			ThornsMenuAudioHandoff.Ambience.Volume = MathF.Max( 0f, _menuAmbienceStartVolume * menuMul );

		if ( _worldAmbience.IsValid() )
			_worldAmbience.RuntimeVolumeMultiplier = MathF.Max( 0f, WorldAmbienceTargetMultiplier ) * t;

		if ( t < 1f )
			return;

		if ( ThornsMenuAudioHandoff.Music is { IsValid: true } )
			ThornsMenuAudioHandoff.Music.Stop( 0f );
		if ( ThornsMenuAudioHandoff.Ambience is { IsValid: true } )
			ThornsMenuAudioHandoff.Ambience.Stop( 0f );

		ThornsMenuAudioHandoff.Clear();
		if ( _worldAmbience.IsValid() )
			_worldAmbience.RuntimeVolumeMultiplier = MathF.Max( 0f, WorldAmbienceTargetMultiplier );

		_active = false;
	}
}
