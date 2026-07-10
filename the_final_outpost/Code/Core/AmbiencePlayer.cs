namespace FinalOutpost;

/// <summary>
/// Looping day ambience plus optional background music. The ambience bed only plays during
/// <see cref="GamePhase.Day"/> and fades in/out with phase changes. Volumes follow
/// <see cref="AudioSettings"/>.
/// </summary>
public sealed class AmbiencePlayer : Component
{
	public const string AmbienceSound = "sounds/fo_ambience.sound";

	private const float DayFadeSeconds = 1.75f;
	private const float DayAmbienceMix = 0.9f;

	public static AmbiencePlayer Instance { get; private set; }

	private LoopSoundBed _ambience;
	private float _dayFade;

	protected override void OnAwake() => Instance = this;

	protected override void OnDestroy()
	{
		if ( Instance == this ) Instance = null;
		_ambience?.Stop();
		_ambience = null;
	}

	protected override void OnUpdate()
	{
		var wantDay = GameCore.Instance?.Phase == GamePhase.Day;
		var target = wantDay ? 1f : 0f;

		if ( MathF.Abs( _dayFade - target ) >= 0.001f )
		{
			var step = Time.Delta / DayFadeSeconds;
			_dayFade = wantDay
				? MathF.Min( 1f, _dayFade + step )
				: MathF.Max( 0f, _dayFade - step );
		}
		else
		{
			_dayFade = target;
		}

		UpdateAmbiencePlayback();
	}

	public void RefreshVolumes() => UpdateAmbiencePlayback();

	private void UpdateAmbiencePlayback()
	{
		var volume = Math.Min( 1f, AudioSettings.EffectiveAmbience * DayAmbienceMix * _dayFade * GameConstants.LoopMusicGain );
		_ambience ??= new LoopSoundBed( AmbienceSound, "ambience" );
		_ambience.Update( volume );
	}
}
