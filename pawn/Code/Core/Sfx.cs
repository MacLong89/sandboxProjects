namespace PawnShop;

/// <summary>
/// Central sound hooks. Every constant points at a .sound asset under Assets/sounds/.
/// Missing assets warn once then go quiet, so audio can never break gameplay.
/// </summary>
public static class Sfx
{
	public const string UiClick = "sounds/button.sound";
	public const string UiError = "sounds/placement_error.sound";
	public const string CashRegister = "sounds/economy.sound";
	public const string DealAccepted = "sounds/buzzer_correct.sound";
	public const string DealRejected = "sounds/buzzer_incorrect.sound";
	public const string DoorBell = "sounds/build_menu_or_place.sound";
	public const string ItemPlaced = "sounds/armor_equip.sound";
	public const string Cleaning = "sounds/brush.sound";
	public const string Repairing = "sounds/pickaxe.sound";
	public const string ResearchDone = "sounds/skill_upgrade.sound";
	public const string DayStart = "sounds/round_start.sound";
	public const string DayEnd = "sounds/open_build.sound";
	public const string RepUp = "sounds/level_up.sound";
	public const string Reward = "sounds/tame.sound";
	public const string Alarm = "sounds/melee_miss.sound";
	public const string Scrap = "sounds/demolish.sound";
	public const string Footstep = "sounds/footsteps.sound";
	public const string BigFind = "sounds/airdrop.sound";
	public const string Splash = "sounds/water.sound";

	private static readonly HashSet<string> _missing = new();

	public static void Play( string path, float volume = 1f )
	{
		var handle = PlayHandle( path );
		if ( handle is { IsValid: true } )
			handle.Volume = volume;
	}

	public static SoundHandle PlayHandle( string path )
	{
		if ( string.IsNullOrEmpty( path ) || _missing.Contains( path ) )
			return null;

		try
		{
			var handle = Sound.Play( path );
			if ( handle is null )
				_missing.Add( path );
			return handle;
		}
		catch
		{
			_missing.Add( path );
			return null;
		}
	}

	public static void PlayAt( string path, Vector3 position )
	{
		if ( string.IsNullOrEmpty( path ) || _missing.Contains( path ) )
			return;

		try
		{
			var handle = Sound.Play( path, position );
			if ( handle is null )
				_missing.Add( path );
		}
		catch
		{
			_missing.Add( path );
		}
	}
}
