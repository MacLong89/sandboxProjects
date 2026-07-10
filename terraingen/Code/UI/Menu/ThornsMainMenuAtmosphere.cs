namespace Terraingen.UI.Menu;

[Title( "Thorns Main Menu Atmosphere" )]
[Category( "Thorns/UI" )]
[Icon( "music_note" )]
public sealed class ThornsMainMenuAtmosphere : Component
{
	static ThornsMainMenuAtmosphere _active;

	[Property] public List<string> MenuMusicSoundPaths { get; set; } = new()
	{
		"sounds/thorns_music_blood_compass.sound",
		"sounds/thorns_music_dusty_banjo.sound"
	};

	[Property] public string AmbienceSoundPath { get; set; } = "";
	[Property] public float MusicVolume { get; set; } = 0.42f;
	[Property] public float AmbienceVolume { get; set; } = 0.22f;
	[Property] public float MenuCommitFadeSeconds { get; set; } = 1.5f;

	SoundHandle _music;
	SoundHandle _ambience;
	string _pickedMusicPath;
	bool _warnedMissingMusic;
	bool _fadeOutRequested;
	double _fadeEndTime;
	float _fadeDuration;
	float _musicFadeStartVolume;
	float _ambienceFadeStartVolume;

	public static void BeginMusicFadeOut( float? fadeSeconds = null )
	{
		if ( _active is not null && _active.IsValid() )
			_active.StartFadeOut( fadeSeconds ?? _active.MenuCommitFadeSeconds );
	}

	protected override void OnStart()
	{
		_active = this;
		_pickedMusicPath = PickRandomMenuMusicPath();
		TryStartMenuMusic();
		TickAmbience();
	}

	protected override void OnDestroy()
	{
		if ( _active == this )
			_active = null;

		if ( ThornsMenuAudioHandoff.TryTakeFromMenuAtmosphere( ref _music, ref _ambience, MusicVolume, AmbienceVolume ) )
			return;

		StopBed( ref _music, 0.25f );
		StopBed( ref _ambience, 0.25f );
	}

	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying || Application.IsDedicatedServer || Application.IsHeadless )
		{
			StopBed( ref _music, 0f );
			StopBed( ref _ambience, 0f );
			return;
		}

		if ( _fadeOutRequested )
		{
			TickFadeOut();
			return;
		}

		TickMusic();
		TickAmbience();
	}

	void StartFadeOut( float fadeSeconds )
	{
		if ( _fadeOutRequested )
			return;

		_fadeOutRequested = true;
		_fadeDuration = MathF.Max( 0.05f, fadeSeconds );
		_fadeEndTime = Time.Now + _fadeDuration;
		_musicFadeStartVolume = _music is { IsValid: true } ? _music.Volume : 0f;
		_ambienceFadeStartVolume = _ambience is { IsValid: true } ? _ambience.Volume : 0f;
	}

	void TickFadeOut()
	{
		var remaining = (float)(_fadeEndTime - Time.Now);
		var t = Math.Clamp( remaining / MathF.Max( 0.05f, _fadeDuration ), 0f, 1f );

		if ( _music is { IsValid: true } )
			_music.Volume = MathF.Max( 0f, _musicFadeStartVolume * t );
		if ( _ambience is { IsValid: true } )
			_ambience.Volume = MathF.Max( 0f, _ambienceFadeStartVolume * t );

		if ( remaining > 0f )
			return;

		StopBed( ref _music, 0f );
		StopBed( ref _ambience, 0f );
	}

	void TickMusic()
	{
		if ( _music is { IsValid: true, IsPlaying: true } )
		{
			_music.Volume = MusicVolume;
			return;
		}

		TryStartMenuMusic();
	}

	void TickAmbience()
	{
		if ( string.IsNullOrWhiteSpace( AmbienceSoundPath ) )
			return;

		if ( _ambience is { IsValid: true, IsPlaying: true } )
		{
			_ambience.Volume = AmbienceVolume;
			return;
		}

		if ( TryPlayNonSpatial( AmbienceSoundPath.Trim(), out var h ) )
		{
			h.Volume = AmbienceVolume;
			_ambience = h;
		}
	}

	void TryStartMenuMusic()
	{
		foreach ( var path in EnumerateMusicPathsToTry() )
		{
			if ( TryPlayNonSpatial( path, out var handle ) )
			{
				_pickedMusicPath = path;
				handle.Volume = MusicVolume;
				_music = handle;
				return;
			}
		}

		if ( !_warnedMissingMusic )
		{
			_warnedMissingMusic = true;
			Log.Warning( "[Thorns Menu] Could not start menu music. Check sounds/thorns_music_*.sound under Assets/sounds." );
		}
	}

	IEnumerable<string> EnumerateMusicPathsToTry()
	{
		if ( !string.IsNullOrWhiteSpace( _pickedMusicPath ) )
			yield return _pickedMusicPath.Trim();

		foreach ( var path in MenuMusicSoundPaths )
		{
			if ( string.IsNullOrWhiteSpace( path ) )
				continue;

			var trimmed = path.Trim();
			if ( string.Equals( trimmed, _pickedMusicPath, StringComparison.OrdinalIgnoreCase ) )
				continue;

			yield return trimmed;
		}
	}

	static bool TryPlayNonSpatial( string path, out SoundHandle handle )
	{
		handle = default;
		if ( string.IsNullOrWhiteSpace( path ) || Application.IsDedicatedServer || Application.IsHeadless )
			return false;

		var h = Sound.Play( path.Trim(), Vector3.Zero );
		if ( !h.IsValid() )
			return false;

		h.SpacialBlend = 0f;
		handle = h;
		return true;
	}

	string PickRandomMenuMusicPath()
	{
		var valid = MenuMusicSoundPaths
			.Where( x => !string.IsNullOrWhiteSpace( x ) )
			.Select( x => x.Trim() )
			.ToArray();

		return valid.Length == 0 ? "" : valid[Game.Random.Int( 0, valid.Length - 1 )];
	}

	static void StopBed( ref SoundHandle handle, float fadeSeconds )
	{
		var h = handle;
		handle = default;
		if ( h is { IsValid: true } )
			h.Stop( MathF.Max( 0f, fadeSeconds ) );
	}
}
