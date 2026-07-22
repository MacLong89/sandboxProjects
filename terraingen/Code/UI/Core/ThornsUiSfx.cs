namespace Terraingen.UI;

using Sandbox;

/// <summary>Non-spatial UI feedback sounds.</summary>
public static class ThornsUiSfx
{
	public const string Button = "sounds/button.sound";
	public const string Economy = "sounds/economy.sound";

	public static void PlayButtonClick() => PlayUi( Button, "sounds/button.mp3" );

	public static void PlayEconomyTransaction() => PlayUi( Economy, "sounds/economy.mp3" );

	static void PlayUi( string soundEventPath, string rawAudioFallback )
	{
		if ( Application.IsDedicatedServer || Application.IsHeadless )
			return;

		if ( TryPlayUi( soundEventPath ) )
			return;

		TryPlayUi( rawAudioFallback );
	}

	static bool TryPlayUi( string path )
	{
		if ( string.IsNullOrWhiteSpace( path ) )
			return false;

		var handle = Sound.Play( path.Trim(), Vector3.Zero );
		if ( !handle.IsValid() )
			return false;

		handle.SpacialBlend = 0f;
		handle.Volume = ThornsAudioSettings.EffectiveSfxVolume;
		return true;
	}
}
