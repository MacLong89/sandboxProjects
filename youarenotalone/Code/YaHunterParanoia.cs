namespace Sandbox;

/// <summary>Not Alone debuff when Alone uses Paranoia — vision overlay + quieter local weapon feedback.</summary>
public static class YaHunterParanoia
{
	/// <summary>2D sting when <see cref="YaGameStateSystem.ParanoiaDebuffSecondsRemaining"/> starts (local hunter).</summary>
	public const string ParanoiaDebuffSoundResource = "sounds/paranoia_sound.sound";

	/// <summary>Multiply owner weapon / FX loudness while debuffed (local Not Alone only).</summary>
	public static float LocalOwnedPawnSoundVolumeScale( GameObject pawnRoot )
	{
		if ( pawnRoot is null || !pawnRoot.IsValid() )
			return 1f;

		var gs = YaGameStateSystem.Instance;
		if ( gs is null || !gs.IsValid() || gs.ParanoiaDebuffSecondsRemaining <= 0.01f )
			return 1f;

		var role = pawnRoot.Components.Get<YaPlayerRoleComponent>( FindMode.EnabledInSelf );
		if ( !role.IsValid() || role.Role != YaPlayerRole.NotAlone )
			return 1f;

		return 0.38f;
	}
}
