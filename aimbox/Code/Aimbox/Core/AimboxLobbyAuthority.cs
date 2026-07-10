namespace Sandbox;

/// <summary>Host vs joiner permissions for intermission lobby flow.</summary>
public static class AimboxLobbyAuthority
{
	public static bool IsActiveSession => Networking.IsActive;

	public static bool IsHost => !Networking.IsActive || Networking.IsHost;

	public static bool IsJoiner => Networking.IsActive && !Networking.IsHost;

	public static bool CanControlLobbyFlow => IsHost;

	public static string HostAccountId
	{
		get
		{
			if ( !Networking.IsActive )
				return AimboxGame.Instance?.Players.FirstOrDefault( p => !p.IsProxy )?.AccountId ?? "offline";

			return Connection.Host?.Id.ToString() ?? "offline";
		}
	}
}
