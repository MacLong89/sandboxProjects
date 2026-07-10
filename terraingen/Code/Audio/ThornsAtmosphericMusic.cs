namespace Sandbox;

/// <summary>Lightweight exploration music bed using the Thorns music assets.</summary>
[Title( "Thorns Atmospheric Music" )]
[Category( "Thorns/Audio" )]
[Icon( "music_note" )]
public sealed class ThornsAtmosphericMusic : Component
{
	[Property] public bool EnabledInGame { get; set; } = true;
	[Property] public float Volume { get; set; } = 0.32f;
	[Property] public float MinSilenceSeconds { get; set; } = 75f;
	[Property] public float MaxSilenceSeconds { get; set; } = 180f;
	[Property] public float WorldAmbienceDuckWhenMusic { get; set; } = 0.62f;

	static readonly string[] Tracks =
	{
		"sounds/thorns_music_blood_compass.sound",
		"sounds/thorns_music_dusty_banjo.sound"
	};

	SoundHandle _music;
	double _nextAllowedPlayTime;
	ThornsWorldAmbience _ambience;

	protected override void OnStart()
	{
		ScheduleNext();
	}

	protected override void OnDestroy()
	{
		StopMusic();
		RestoreAmbience();
	}

	protected override void OnUpdate()
	{
		if ( !EnabledInGame || !Game.IsPlaying || Application.IsDedicatedServer || Application.IsHeadless )
		{
			StopMusic();
			RestoreAmbience();
			return;
		}

		_ambience ??= Components.Get<ThornsWorldAmbience>();

		if ( _music is { IsValid: true, IsPlaying: true } )
		{
			_music.Volume = Volume;
			if ( _ambience.IsValid() )
				_ambience.RuntimeVolumeMultiplier = WorldAmbienceDuckWhenMusic;
			return;
		}

		if ( _music is { IsValid: true, IsPlaying: false } )
		{
			StopMusic();
			RestoreAmbience();
			ScheduleNext();
		}

		if ( Time.Now < _nextAllowedPlayTime )
			return;

		var path = Tracks[Game.Random.Int( 0, Tracks.Length - 1 )];
		var h = Sound.Play( path, Vector3.Zero );
		if ( !h.IsValid() )
		{
			ScheduleNext();
			return;
		}

		h.SpacialBlend = 0f;
		h.Volume = Volume;
		_music = h;
	}

	void ScheduleNext()
	{
		_nextAllowedPlayTime = Time.Now + Game.Random.Float( MathF.Max( 0f, MinSilenceSeconds ), MathF.Max( MinSilenceSeconds, MaxSilenceSeconds ) );
	}

	void StopMusic()
	{
		var h = _music;
		_music = default;
		if ( h is { IsValid: true } )
			h.Stop( 0.5f );
	}

	void RestoreAmbience()
	{
		if ( _ambience.IsValid() )
			_ambience.RuntimeVolumeMultiplier = 1f;
	}
}
