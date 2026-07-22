namespace Offshore;

public sealed class AudioService
{
	public float Master = 1f;
	public float Music = 0.7f;
	public float Effects = 1f;
	public float Ambient = 0.8f;

	string _ambientKey;
	SoundHandle _ambient;
	SoundHandle _engine;
	bool _engineOn;

	public void Load( SaveData save )
	{
		Master = save.MasterVolume;
		Music = save.MusicVolume;
		Effects = save.EffectsVolume;
		Ambient = save.AmbientVolume;
	}

	public void SyncTo( SaveData save )
	{
		save.MasterVolume = Master;
		save.MusicVolume = Music;
		save.EffectsVolume = Effects;
		save.AmbientVolume = Ambient;
	}

	public void PlayUi( string name ) => PlayEffect( name );
	public void PlayEffect( string name )
	{
		try
		{
			var file = SoundFile.Load( $"sounds/{name}.wav" );
			if ( file is null ) return;
			var h = Sound.PlayFile( file, Master * Effects );
		}
		catch { /* missing sound is non-fatal */ }
	}

	public void SetAmbient( string key )
	{
		if ( _ambientKey == key )
			return;
		_ambientKey = key;
		try { _ambient?.Stop(); } catch { }
		_ambient = null;
		if ( string.IsNullOrEmpty( key ) )
			return;
		try
		{
			var file = SoundFile.Load( $"sounds/{key}.wav" );
			if ( file is null ) return;
			_ambient = Sound.PlayFile( file, Master * Ambient );
		}
		catch { }
	}

	public void SetEngine( bool on, float pitch = 1f )
	{
		if ( on )
		{
			if ( !_engineOn )
			{
				try
				{
					var file = SoundFile.Load( "sounds/engine_loop.wav" );
					if ( file is not null )
						_engine = Sound.PlayFile( file, Master * Effects * Math.Clamp( pitch, 0.2f, 1f ) );
				}
				catch { }
				_engineOn = true;
			}
		}
		else if ( _engineOn )
		{
			try { _engine?.Stop(); } catch { }
			_engine = null;
			_engineOn = false;
		}
	}

	public void StopAll()
	{
		try { _ambient?.Stop(); } catch { }
		try { _engine?.Stop(); } catch { }
		_ambient = null;
		_engine = null;
		_ambientKey = null;
		_engineOn = false;
	}
}
