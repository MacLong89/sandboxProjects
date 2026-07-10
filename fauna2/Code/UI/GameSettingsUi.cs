namespace Fauna2.UI;

/// <summary>Shared helpers for main-menu and in-game settings panels.</summary>
public static class GameSettingsUi
{
	public static int VolumePercent( GameSettingsData settings ) =>
		(int)(settings.MasterVolume * 100f).Clamp( 0f, 100f );

	public static void SetVolume( GameSettingsData settings, float volume )
	{
		settings.MasterVolume = volume.Clamp( 0f, 1f );
		Commit( settings );
	}

	public static void AdjustVolume( GameSettingsData settings, float delta ) =>
		SetVolume( settings, settings.MasterVolume + delta );

	public static void SetRevenueMultiplier( GameSettingsData settings, float multiplier )
	{
		settings.GuestRevenueMultiplier = multiplier.Clamp( 0.75f, 2.5f );
		Commit( settings );
	}

	public static void AdjustRevenueMultiplier( GameSettingsData settings, float delta )
	{
		settings.GuestRevenueMultiplier = (settings.GuestRevenueMultiplier + delta).Clamp( 0.75f, 2.5f );
		Commit( settings );
	}

	public static string RevenuePresetClass( GameSettingsData settings, float multiplier ) =>
		MathF.Abs( settings.GuestRevenueMultiplier - multiplier ) < 0.01f ? "active" : "";

	public static string VolumeSegmentClass( GameSettingsData settings, int index )
	{
		var threshold = index / 10f;
		return settings.MasterVolume >= threshold - 0.001f ? "filled" : "";
	}

	public static void Commit( GameSettingsData settings )
	{
		GameSettings.Current = settings;
		GameSettings.Save();
	}
}
