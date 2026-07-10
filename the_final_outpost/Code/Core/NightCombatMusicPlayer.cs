namespace FinalOutpost;

/// <summary>
/// Loops combat.mp3 and combat2.mp3 during <see cref="GamePhase.Night"/> when
/// <see cref="GameConstants.UseNightCombatMusicLoop"/> is enabled. All other sounds are muted at night via <see cref="Sfx"/>.
/// </summary>
public sealed class NightCombatMusicPlayer : Component
{
	public static NightCombatMusicPlayer Instance { get; private set; }

	private static readonly (string Sound, string Tag)[] Tracks =
	{
		( "sounds/fo_combat.sound", "combat" ),
		( "sounds/fo_combat2.sound", "combat2" ),
	};

	private const float FadeSeconds = 1.25f;

	private readonly LoopSoundBed[] _loops = new LoopSoundBed[Tracks.Length];
	private float _nightFade;

	protected override void OnAwake() => Instance = this;

	protected override void OnDestroy()
	{
		if ( Instance == this ) Instance = null;
		StopAll();
	}

	protected override void OnUpdate()
	{
		if ( !GameConstants.UseNightCombatMusicLoop )
		{
			if ( _nightFade > 0f )
			{
				_nightFade = 0f;
				StopAll();
			}

			return;
		}

		var wantNight = GameCore.Instance?.Phase == GamePhase.Night;
		var target = wantNight ? 1f : 0f;

		if ( MathF.Abs( _nightFade - target ) >= 0.001f )
		{
			var step = Time.Delta / FadeSeconds;
			_nightFade = wantNight
				? MathF.Min( 1f, _nightFade + step )
				: MathF.Max( 0f, _nightFade - step );
		}
		else
		{
			_nightFade = target;
		}

		UpdatePlayback();
	}

	public void RefreshVolumes() => UpdatePlayback();

	private void UpdatePlayback()
	{
		var volume = Math.Min( 1f, AudioSettings.EffectiveMusic * GameConstants.NightCombatTrackVolume * _nightFade * GameConstants.LoopMusicGain );
		if ( volume <= 0.001f )
		{
			StopAll();
			return;
		}

		for ( var i = 0; i < Tracks.Length; i++ )
		{
			_loops[i] ??= new LoopSoundBed( Tracks[i].Sound, Tracks[i].Tag );
			_loops[i].Update( volume );
		}
	}

	private void StopAll()
	{
		for ( var i = 0; i < _loops.Length; i++ )
		{
			_loops[i]?.Stop();
			_loops[i] = null;
		}
	}
}
