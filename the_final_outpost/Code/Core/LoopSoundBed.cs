namespace FinalOutpost;

/// <summary>Looping background bed via packaged <c>.sound</c> assets — same publish pipeline as SFX.</summary>
internal sealed class LoopSoundBed
{
	private readonly string _soundEvent;
	private readonly string _tag;

	private SoundHandle _handle;
	private bool _loggedMissing;

	public LoopSoundBed( string soundEvent, string tag )
	{
		_soundEvent = soundEvent;
		_tag = tag;
	}

	public void Update( float volume )
	{
		if ( volume <= 0.001f )
		{
			Stop();
			return;
		}

		if ( _handle is null || !_handle.IsValid || !_handle.IsPlaying )
			Start();

		if ( _handle is null || !_handle.IsValid )
			return;

		_handle.Volume = volume;
		if ( _handle.Paused )
			_handle.Paused = false;
	}

	public void Stop()
	{
		_handle?.Stop();
		_handle = null;
	}

	private void Start()
	{
		_handle?.Stop();

		try
		{
			_handle = Sound.Play( _soundEvent );
			if ( _handle is null )
			{
				LogMissing();
				return;
			}

			if ( Sfx.LogPlays )
				Log.Info( $"[Sfx] {_tag} loop — {_soundEvent} (SoundEvent)" );
		}
		catch ( Exception e )
		{
			LogMissing( e.Message );
		}
	}

	private void LogMissing( string detail = null )
	{
		if ( _loggedMissing )
			return;

		_loggedMissing = true;
		var suffix = string.IsNullOrWhiteSpace( detail ) ? "" : $": {detail}";
		Log.Warning( $"[FinalOutpost] Loop sound missing — {_soundEvent}{suffix}" );
	}
}
