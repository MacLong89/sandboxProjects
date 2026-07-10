namespace Fauna2;

/// <summary>
/// Host authority checks for reading and writing local save files.
/// </summary>
public static class SaveHost
{
	/// <summary>True when this machine may mutate and persist zoo saves.</summary>
	public static bool CanPersist =>
		Networking.IsActive && Networking.IsHost;

	/// <summary>True when the main menu may start or continue a session.</summary>
	public static bool CanStartSession => CanPersist;
}
