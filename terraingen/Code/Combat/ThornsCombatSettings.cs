namespace Terraingen.Combat;

/// <summary>Global combat toggles for shared survival multiplayer.</summary>
public static class ThornsCombatSettings
{
	/// <summary>When true, player weapon hitscan can damage other players just like NPCs and wildlife.</summary>
	public static bool EnablePvPDamage { get; set; } = true;

	/// <summary>When true, guild members cannot damage each other.</summary>
	public static bool BlockGuildFriendlyFire { get; set; }
}
