namespace Sandbox;

/// <summary>Loops main-menu music while the lobby / intermission hub is open.</summary>
public static class AimboxMenuMusic
{
	public const string TrackPath = "sounds/neon_dash.sound";
	public const float Volume = 0.5f;

	static MusicPlayer _player;
	static bool _loggedStartFailure;

	public static void Sync()
	{
		if ( ShouldPlay() )
			EnsurePlaying();
		else
			Stop();
	}

	static bool ShouldPlay()
	{
		if ( Application.IsDedicatedServer || Application.IsHeadless || !Game.IsPlaying )
			return false;

		var game = AimboxGame.Instance;
		if ( game is null || game.SkipMetaMenu || game.IsAttachmentLabScene )
			return false;

		return game.Phase == AimboxSessionPhase.Intermission && AimboxMetaNavigation.IsInIntermission;
	}

	static void EnsurePlaying()
	{
		if ( _player is not null )
		{
			_player.Volume = Volume * AimboxClientSettings.EffectiveMusicVolume;
			return;
		}

		_player = MusicPlayer.Play( FileSystem.Mounted, TrackPath );
		if ( _player is null )
		{
			if ( !_loggedStartFailure )
			{
				_loggedStartFailure = true;
				Log.Warning( $"[Aimbox] Main menu music failed to start: '{TrackPath}'." );
			}

			return;
		}

		_player.Volume = Volume * AimboxClientSettings.EffectiveMusicVolume;
		_player.Repeat = true;
		_loggedStartFailure = false;
	}

	static void Stop()
	{
		if ( _player is null )
			return;

		_player.Stop();
		_player.Dispose();
		_player = null;
	}
}
