namespace Sandbox;

/// <summary>
/// After the loading screen clears and the local pawn exists, fades boot-menu audio out and
/// fades world ambience in over <see cref="FadeSeconds"/> (default 10s).
/// </summary>
[Title( "Thorns — Gameplay Enter Audio Fade" )]
[Category( "Thorns/Audio" )]
[Icon( "music_note" )]
public sealed class ThornsGameplayEnterAudioFade : Component
{
	public const float DefaultFadeSeconds = 10f;

	[Property] public float FadeSeconds { get; set; } = DefaultFadeSeconds;

	[Property] public float WorldAmbienceTargetMultiplier { get; set; } = 1f;

	bool _fadeActive;
	double _fadeEndTime;
	float _fadeDuration;
	float _menuMusicStartVolume;
	float _menuAmbienceStartVolume;
	ThornsWorldAmbience _worldAmbience;

	protected override void OnStart()
	{
		_worldAmbience = Components.Get<ThornsWorldAmbience>();
	}

	protected override void OnDestroy()
	{
		ThornsMenuAudioHandoff.Clear();
	}

	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying )
			return;

		if ( _fadeActive )
		{
			TickFade();
			return;
		}

		if ( !IsLocalPlayerInWorldAfterLoading() )
			return;

		BeginEnterFade();
	}

	static bool IsLocalPlayerInWorldAfterLoading()
	{
		if ( ThornsPawn.Local is null || !ThornsPawn.Local.IsValid() )
			return false;

		if ( !string.IsNullOrWhiteSpace( LoadingScreen.Title ) )
			return false;

		return true;
	}

	void BeginEnterFade()
	{
		_fadeActive = true;
		_fadeDuration = MathF.Max( 0.05f, FadeSeconds );
		_fadeEndTime = Time.Now + _fadeDuration;

		_menuMusicStartVolume = ThornsMenuAudioHandoff.Music is { IsValid: true }
			? ThornsMenuAudioHandoff.Music.Volume
			: 0f;
		_menuAmbienceStartVolume = ThornsMenuAudioHandoff.Ambience is { IsValid: true }
			? ThornsMenuAudioHandoff.Ambience.Volume
			: 0f;

		if ( _worldAmbience.IsValid() )
			_worldAmbience.RuntimeVolumeMultiplier = 0f;
	}

	void TickFade()
	{
		var remaining = (float)(_fadeEndTime - Time.Now);
		var t = Math.Clamp( remaining / _fadeDuration, 0f, 1f );

		if ( ThornsMenuAudioHandoff.Music is { IsValid: true } )
			ThornsMenuAudioHandoff.Music.Volume = MathF.Max( 0f, _menuMusicStartVolume * t );

		if ( ThornsMenuAudioHandoff.Ambience is { IsValid: true } )
			ThornsMenuAudioHandoff.Ambience.Volume = MathF.Max( 0f, _menuAmbienceStartVolume * t );

		if ( _worldAmbience.IsValid() )
		{
			var target = MathF.Max( 0f, WorldAmbienceTargetMultiplier );
			_worldAmbience.RuntimeVolumeMultiplier = target * (1f - t);
		}

		if ( t > 0f )
			return;

		ThornsMenuAudioHandoff.Clear();
		_fadeActive = false;

		if ( _worldAmbience.IsValid() )
			_worldAmbience.RuntimeVolumeMultiplier = MathF.Max( 0f, WorldAmbienceTargetMultiplier );
	}
}
