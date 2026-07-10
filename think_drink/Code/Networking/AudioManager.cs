namespace ThinkDrink;

/// <summary>Centralized audio routing for game-show SFX with fallback chains.</summary>
public sealed class AudioManager : Component
{
	public static AudioManager Instance { get; private set; }

	protected override void OnAwake()
	{
		Instance = this;
		GameEvents.AudioRequested += OnAudioRequested;
		GameEvents.AchievementUnlocked += OnAchievementUnlocked;
	}

	protected override void OnDestroy()
	{
		GameEvents.AudioRequested -= OnAudioRequested;
		GameEvents.AchievementUnlocked -= OnAchievementUnlocked;
		if ( Instance == this ) Instance = null;
	}

	private void OnAchievementUnlocked( AchievementDefinition def ) =>
		OnAudioRequested( AudioEventId.AchievementUnlock );

	private void OnAudioRequested( AudioEventId id )
	{
		var volume = GameSettings.Current.SfxVolume * GameSettings.Current.MasterVolume;
		if ( volume <= 0.01f ) return;

		foreach ( var path in GetCandidates( id ) )
		{
			if ( TryPlay( path, volume ) )
				return;
		}
	}

	static IEnumerable<string> GetCandidates( AudioEventId id ) => id switch
	{
		AudioEventId.Buzz or AudioEventId.BuzzerPress => Custom( "click_buzzer" ),
		AudioEventId.Correct => Custom( "buzzer_correct" ),
		AudioEventId.Incorrect => Custom( "buzzer_incorrect" ),
		AudioEventId.RoundStart or AudioEventId.CategoryReveal => Custom( "round_start" ),
		AudioEventId.UiClick => Custom( "button" ),
		AudioEventId.RankUp or AudioEventId.AchievementUnlock or AudioEventId.Win => Custom( "milestoneprogressionlevelup" ),
		AudioEventId.Countdown or AudioEventId.Steal or AudioEventId.ScoreboardReveal
			or AudioEventId.RandomEventStinger or AudioEventId.StreakBonus => Custom( "economy" ),
		_ => Array.Empty<string>()
	};

	static IEnumerable<string> Custom( string name )
	{
		yield return $"sounds/{name}.sound";
		yield return name;
	}

	static bool TryPlay( string path, float volume )
	{
		if ( string.IsNullOrEmpty( path ) ) return false;

		try
		{
			var handle = Sound.Play( path );
			if ( handle is null ) return false;

			handle.Volume = volume;
			return true;
		}
		catch
		{
			return false;
		}
	}
}
