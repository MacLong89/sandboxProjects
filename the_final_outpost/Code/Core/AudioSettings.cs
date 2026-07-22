namespace FinalOutpost;

/// <summary>Persisted volume mix — master scales SFX, ambience, and music together.</summary>
public static class AudioSettings
{
	private static SaveData _save;

	public static event Action Changed;

	public static void Bind( SaveData save ) => _save = save;

	public static float Master
	{
		get => _save?.AudioMaster ?? GameConstants.DefaultAudioVolume;
		set
		{
			if ( _save is null ) return;
			var clamped = Math.Clamp( value, 0f, 1f );
			if ( MathF.Abs( _save.AudioMaster - clamped ) < 0.001f ) return;
			_save.AudioMaster = clamped;
			NotifyChanged();
		}
	}

	public static float Sfx
	{
		get => _save?.AudioSfx ?? GameConstants.DefaultAudioVolume;
		set
		{
			if ( _save is null ) return;
			var clamped = Math.Clamp( value, 0f, 1f );
			if ( MathF.Abs( _save.AudioSfx - clamped ) < 0.001f ) return;
			_save.AudioSfx = clamped;
			NotifyChanged();
		}
	}

	public static float Ambience
	{
		get => _save?.AudioAmbience ?? GameConstants.DefaultAudioVolume;
		set
		{
			if ( _save is null ) return;
			var clamped = Math.Clamp( value, 0f, 1f );
			if ( MathF.Abs( _save.AudioAmbience - clamped ) < 0.001f ) return;
			_save.AudioAmbience = clamped;
			NotifyChanged();
		}
	}

	public static float Music
	{
		get => _save?.AudioMusic ?? GameConstants.DefaultAudioVolume;
		set
		{
			if ( _save is null ) return;
			var clamped = Math.Clamp( value, 0f, 1f );
			if ( MathF.Abs( _save.AudioMusic - clamped ) < 0.001f ) return;
			_save.AudioMusic = clamped;
			NotifyChanged();
		}
	}

	public static float EffectiveSfx => Master * Sfx;
	public static float EffectiveAmbience => Master * Ambience;
	public static float EffectiveMusic => Master * Music;

	public static float CameraSensitivity
	{
		get => _save?.CameraSensitivity > 0f
			? Math.Clamp( _save.CameraSensitivity, GameConstants.MinCameraSensitivity, GameConstants.MaxCameraSensitivity )
			: GameConstants.DefaultCameraSensitivity;
		set
		{
			if ( _save is null ) return;
			var clamped = Math.Clamp( value, GameConstants.MinCameraSensitivity, GameConstants.MaxCameraSensitivity );
			if ( MathF.Abs( _save.CameraSensitivity - clamped ) < 0.001f ) return;
			_save.CameraSensitivity = clamped;
			NotifyChanged();
		}
	}

	private static void NotifyChanged()
	{
		Changed?.Invoke();
		GameCore.Instance?.SaveManagerTouch();
		AmbiencePlayer.Instance?.RefreshVolumes();
		NightCombatMusicPlayer.Instance?.RefreshVolumes();
	}
}
