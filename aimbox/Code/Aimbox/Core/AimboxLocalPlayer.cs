namespace Sandbox;

/// <summary>Resolves the human player owned by this machine (never a network proxy).</summary>
public static class AimboxLocalPlayer
{
	public static AimboxPlayerController Controller =>
		AimboxGame.Instance?.Players.FirstOrDefault( p => !p.IsProxy )
		?? Game.ActiveScene?.GetAllComponents<AimboxPlayerController>().FirstOrDefault( p => !p.IsProxy );

	public static AimboxPlayerData Data => Controller?.Data;

	public static string AccountId => Controller?.AccountId ?? "offline";

	public static bool IsAvailable => Controller is { IsProxy: false, Data: not null };
}
