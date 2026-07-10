namespace Sandbox;

/// <summary>Server-side match events (subscribe from gameplay systems; invoked only on host).</summary>
public static class YaGameEvents
{
	/// <summary>Fired on host after roles + loadouts are applied and players respawned for a new round.</summary>
	public static event Action HostRoundStarted;

	/// <summary>Fired on host when a round ends, before intermission countdown.</summary>
	public static event Action<YaRoundEndReason> HostRoundEnded;

	internal static void InvokeHostRoundStarted() => HostRoundStarted?.Invoke();

	internal static void InvokeHostRoundEnded( YaRoundEndReason reason ) => HostRoundEnded?.Invoke( reason );
}
