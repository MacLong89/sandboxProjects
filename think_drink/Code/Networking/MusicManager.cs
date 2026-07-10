namespace ThinkDrink;

/// <summary>Loops background music for lobby and in-match phases.</summary>
public sealed class MusicManager : Component
{
	const string MusicPath = "sounds/thinkdrink_music.mp3";

	MusicPlayer _player;
	MatchPhase _lastPhase = MatchPhase.Lobby;

	protected override void OnStart()
	{
		if ( Scene.IsEditor ) return;
		_player = TryStartMusic();
	}

	protected override void OnDestroy()
	{
		_player?.Dispose();
		_player = null;
	}

	protected override void OnUpdate()
	{
		if ( Scene.IsEditor ) return;

		var phase = MatchManager.Instance?.Phase ?? MatchPhase.Lobby;
		if ( phase != _lastPhase )
		{
			_lastPhase = phase;
			if ( ShouldPlayMusic( phase ) && _player is null )
				_player = TryStartMusic();
		}

		if ( _player is null ) return;

		var target = ShouldPlayMusic( _lastPhase )
			? GameSettings.Current.MusicVolume * GameSettings.Current.MasterVolume
			: 0f;

		_player.Volume = _player.Volume.Approach( target, Time.Delta * 1.5f );
		if ( _player.Volume <= 0.001f && target <= 0.001f )
		{
			_player.Dispose();
			_player = null;
		}
	}

	static bool ShouldPlayMusic( MatchPhase phase ) =>
		phase is not MatchPhase.PostMatch;

	static MusicPlayer TryStartMusic()
	{
		try
		{
			var player = MusicPlayer.Play( FileSystem.Mounted, MusicPath );
			player.Repeat = true;
			player.Volume = 0f;
			return player;
		}
		catch ( Exception e )
		{
			Log.Warning( $"Think & Drink: music failed to start — {e.Message}" );
			return null;
		}
	}
}
