namespace FinalOutpost;

/// <summary>Account-level data shared across game modes (settings, mode flags).</summary>
public sealed class PlayerProfile
{
	public int Version { get; set; } = 1;

	public bool HasEverStartedSurvival { get; set; }
	public bool HasEverStartedCure { get; set; }
	public GameModeId LastMode { get; set; } = GameModeId.Survival;

	public float AudioMaster { get; set; } = GameConstants.DefaultAudioVolume;
	public float AudioSfx { get; set; } = GameConstants.DefaultAudioVolume;
	public float AudioAmbience { get; set; } = GameConstants.DefaultAudioVolume;
	public float AudioMusic { get; set; } = GameConstants.DefaultAudioVolume;

	public void ApplyAudioTo( SaveData save )
	{
		if ( save is null ) return;
		save.AudioMaster = AudioMaster;
		save.AudioSfx = AudioSfx;
		save.AudioAmbience = AudioAmbience;
		save.AudioMusic = AudioMusic;
	}

	public void PullAudioFrom( SaveData save )
	{
		if ( save is null ) return;
		AudioMaster = save.AudioMaster;
		AudioSfx = save.AudioSfx;
		AudioAmbience = save.AudioAmbience;
		AudioMusic = save.AudioMusic;
	}
}
