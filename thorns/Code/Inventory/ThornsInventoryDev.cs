namespace Sandbox;

/// <summary>
/// TEMPORARY — toggle dev inventory RPCs / starter grants. Disable for shipping builds.
/// </summary>
public static class ThornsInventoryDev
{
	public static bool EnableDevRpcs { get; set; } = false;

	/// <summary>If true, reserved for future dev auto-grant hooks; default join/respawn inventory is empty.</summary>
	public static bool GrantStarterOnSpawn { get; set; } = false;
}
