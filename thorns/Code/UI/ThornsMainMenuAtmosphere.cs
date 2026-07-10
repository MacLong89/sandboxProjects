namespace Sandbox;

/// <summary>
/// Boot menu audio bed + hero slide timing. Created at runtime by <see cref="ThornsMainMenuBootstrap"/> on the MainMenuUi object.
/// </summary>
[Title( "Thorns — Main Menu Atmosphere" )]
[Category( "Thorns/UI" )]
[Icon( "music_note" )]
[Order( 4 )]
public sealed class ThornsMainMenuAtmosphere : Component
{
	static ThornsMainMenuAtmosphere _active;

	[Property, Group( "Audio" )] public List<string> MenuMusicSoundPaths { get; set; } = new( ThornsMainMenuPresentation.MenuMusicSoundPaths );

	[Property, Group( "Audio" )] public string AmbienceSoundPath { get; set; } = ThornsMainMenuPresentation.AmbienceSoundPath;

	[Property, Group( "Audio" )] public string BootStingSoundPath { get; set; } = ThornsMainMenuPresentation.BootStingSoundPath;

	[Property, Group( "Audio" )] public float MusicVolume { get; set; } = 0.42f;

	[Property, Group( "Audio" )] public float AmbienceVolume { get; set; } = 0.22f;

	[Property, Group( "Audio" )] public float StingVolume { get; set; } = 0.55f;

	/// <summary>Music + menu ambience fade when committing to host/join/load gameplay.</summary>
	[Property, Group( "Audio" )] public float EnterGameFadeOutSeconds { get; set; } = 10f;

	[Property, Group( "Audio" )] public float MusicFadeOutSeconds
	{
		get => EnterGameFadeOutSeconds;
		set => EnterGameFadeOutSeconds = value;
	}

	[Property, Group( "Hero art" )] public float HeroSlideIntervalSeconds { get; set; } = 9f;

	[Property, Group( "Hero art" )] public List<string> HeroTexturePaths { get; set; } = new( ThornsMainMenuPresentation.DefaultHeroTexturePaths );

	SoundHandle _music;
	SoundHandle _ambience;
	string _pickedMusicPath;
	bool _playedSting;
	bool _warnedMissingMusic;
	bool _musicFadeOutRequested;
	double _musicFadeEndTime;
	float _musicFadeDuration;
	float _musicFadeStartVolume;
	bool _ambienceFadeOutRequested;
	double _ambienceFadeEndTime;
	float _ambienceFadeDuration;
	float _ambienceFadeStartVolume;

	/// <summary>Fades menu music + ambience when the player commits to host or join (call from main menu UI).</summary>
	public static void BeginMusicFadeOut( float? fadeSeconds = null )
	{
		if ( _active is null || !_active.IsValid() )
			return;

		_active.StartEnterGameAudioFadeOut( fadeSeconds ?? _active.EnterGameFadeOutSeconds );
	}

	/// <summary>Active menu atmosphere in the boot scene (if any).</summary>
	public static bool TryGetActive( out ThornsMainMenuAtmosphere atmosphere )
	{
		atmosphere = _active is not null && _active.IsValid() ? _active : null;
		return atmosphere is not null;
	}

	void StartEnterGameAudioFadeOut( float fadeSeconds )
	{
		StartMusicFadeOut( fadeSeconds );
		StartAmbienceFadeOut( fadeSeconds );
	}

	/// <summary>Waits until the enter-game fade completes (call before unloading the menu scene).</summary>
	public async Task WaitForEnterGameFadeAsync()
	{
		if ( !_musicFadeOutRequested && !_ambienceFadeOutRequested )
			StartEnterGameAudioFadeOut( EnterGameFadeOutSeconds );

		var endTime = Math.Max( _musicFadeEndTime, _ambienceFadeEndTime );
		if ( endTime <= 0 )
		{
			await Task.DelayRealtimeSeconds( EnterGameFadeOutSeconds );
			return;
		}

		var remaining = (float)(endTime - Time.Now);
		if ( remaining > 0.01f )
			await Task.DelayRealtimeSeconds( remaining );
	}

	protected override void OnStart()
	{
		if ( Scene.IsEditor && !Game.IsPlaying )
			return;

		_active = this;
		_pickedMusicPath = PickRandomMenuMusicPath();
		ThornsMainMenuHeroArt.Reset();
		ThornsMainMenuHeroArt.EnsureLoaded( HeroTexturePaths );
		TryPlayBootSting();
		TryStartMenuMusic();
	}

	protected override void OnDestroy()
	{
		if ( _active == this )
			_active = null;

		if ( ThornsMenuAudioHandoff.TryTakeFromMenuAtmosphere( ref _music, ref _ambience, MusicVolume, AmbienceVolume ) )
			return;

		StopBed( ref _music );
		StopBed( ref _ambience );
	}

	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying )
		{
			StopBed( ref _music );
			StopBed( ref _ambience );
			return;
		}

		ThornsMainMenuHeroArt.EnsureLoaded( HeroTexturePaths );
		ThornsMainMenuHeroArt.TickSlides( HeroSlideIntervalSeconds );

		TickMusic();
		TickAmbience();
	}

	void StartAmbienceFadeOut( float fadeSeconds )
	{
		if ( _ambienceFadeOutRequested )
			return;

		_ambienceFadeOutRequested = true;
		fadeSeconds = MathF.Max( 0.05f, fadeSeconds );

		if ( _ambience is not { IsValid: true } )
		{
			_ambienceFadeEndTime = 0;
			return;
		}

		_ambienceFadeStartVolume = _ambience.Volume;
		_ambienceFadeDuration = fadeSeconds;
		_ambienceFadeEndTime = Time.Now + fadeSeconds;
	}

	void TickAmbience()
	{
		if ( _ambienceFadeOutRequested )
		{
			if ( TickAmbienceFadeOut() )
				return;
		}

		TickLoopingBed( ref _ambience, AmbienceSoundPath, AmbienceVolume );
	}

	bool TickAmbienceFadeOut()
	{
		if ( _ambience is not { IsValid: true } )
		{
			_ambience = default;
			_ambienceFadeEndTime = 0;
			return true;
		}

		if ( _ambienceFadeEndTime <= 0 )
			return true;

		var remaining = (float)(_ambienceFadeEndTime - Time.Now);
		var duration = MathF.Max( 0.05f, _ambienceFadeDuration );
		var t = Math.Clamp( remaining / duration, 0f, 1f );
		_ambience.Volume = MathF.Max( 0f, _ambienceFadeStartVolume * t );

		if ( t > 0f )
			return true;

		StopBed( ref _ambience );
		_ambienceFadeEndTime = 0;
		return true;
	}

	void StartMusicFadeOut( float fadeSeconds )
	{
		if ( _musicFadeOutRequested )
			return;

		_musicFadeOutRequested = true;
		fadeSeconds = MathF.Max( 0.05f, fadeSeconds );

		if ( _music is not { IsValid: true } )
		{
			_musicFadeEndTime = 0;
			return;
		}

		_musicFadeStartVolume = _music.Volume;
		_musicFadeDuration = fadeSeconds;
		_musicFadeEndTime = Time.Now + fadeSeconds;
	}

	void TickMusic()
	{
		if ( _musicFadeOutRequested )
		{
			if ( TickMusicFadeOut() )
				return;
		}

		if ( _music is { IsValid: true, IsPlaying: true } )
			return;

		TryStartMenuMusic();
	}

	bool TickMusicFadeOut()
	{
		if ( _music is not { IsValid: true } )
		{
			_music = default;
			_musicFadeEndTime = 0;
			return true;
		}

		if ( _musicFadeEndTime <= 0 )
			return true;

		var remaining = (float)(_musicFadeEndTime - Time.Now);
		var duration = MathF.Max( 0.05f, _musicFadeDuration );
		var t = Math.Clamp( remaining / duration, 0f, 1f );
		_music.Volume = MathF.Max( 0f, _musicFadeStartVolume * t );

		if ( t > 0f )
			return true;

		StopBed( ref _music );
		_musicFadeEndTime = 0;
		return true;
	}

	void TryStartMenuMusic()
	{
		if ( _musicFadeOutRequested || _ambienceFadeOutRequested )
			return;

		foreach ( var path in EnumerateMusicPathsToTry() )
		{
			if ( TryPlayMusicPath( path, out var handle ) )
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
			Log.Warning(
				"[Thorns] Main menu: could not start menu music — check sounds/thorns_music_*.sound + ThornsMusic_*.mp3 under Assets/sounds/." );
		}
	}

	IEnumerable<string> EnumerateMusicPathsToTry()
	{
		if ( !string.IsNullOrWhiteSpace( _pickedMusicPath ) )
			yield return _pickedMusicPath.Trim();

		var shuffled = new List<string>();
		foreach ( var path in MenuMusicSoundPaths )
		{
			if ( string.IsNullOrWhiteSpace( path ) )
				continue;

			var trimmed = path.Trim();
			if ( shuffled.Contains( trimmed, StringComparer.OrdinalIgnoreCase ) )
				continue;

			shuffled.Add( trimmed );
		}

		for ( var n = shuffled.Count; n > 1; n-- )
		{
			var j = Random.Shared.Next( n );
			(shuffled[n - 1], shuffled[j]) = (shuffled[j], shuffled[n - 1]);
		}

		foreach ( var path in shuffled )
			yield return path;
	}

	static bool TryPlayMusicPath( string path, out SoundHandle handle )
	{
		handle = default;
		if ( string.IsNullOrWhiteSpace( path ) )
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
		var candidates = new List<string>();
		foreach ( var path in MenuMusicSoundPaths )
		{
			if ( string.IsNullOrWhiteSpace( path ) )
				continue;

			var trimmed = path.Trim();
			if ( !candidates.Contains( trimmed, StringComparer.OrdinalIgnoreCase ) )
				candidates.Add( trimmed );
		}

		if ( candidates.Count == 0 )
			return "";

		return candidates[Random.Shared.Next( candidates.Count )];
	}

	void TryPlayBootSting()
	{
		if ( _playedSting || string.IsNullOrWhiteSpace( BootStingSoundPath ) )
			return;

		_playedSting = true;
		if ( !TryPlayMusicPath( BootStingSoundPath.Trim(), out var h ) )
			return;

		h.Volume = StingVolume;
	}

	static void TickLoopingBed( ref SoundHandle handle, string path, float volume )
	{
		if ( string.IsNullOrWhiteSpace( path ) )
		{
			StopBed( ref handle );
			return;
		}

		if ( handle is { IsValid: true, IsPlaying: true } )
			return;

		if ( !TryPlayMusicPath( path.Trim(), out var h ) )
			return;

		h.Volume = MathF.Max( 0f, volume );
		handle = h;
	}

	static void StopBed( ref SoundHandle handle )
	{
		var h = handle;
		handle = default;
		if ( h is { IsValid: true } )
			h.Stop( 0.15f );
	}
}
