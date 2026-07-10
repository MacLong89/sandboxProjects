namespace Terraingen.Multiplayer;

using Terraingen;

/// <summary>Stable per-player keys for host-local server saves.</summary>
public static class ThornsPersistenceIdentity
{
	public static string GetStableAccountKey( Connection connection )
	{
		if ( connection is null )
			return "";

		var steam = connection.SteamId;
		if ( steam.Value != 0 )
			return $"steam:{steam.Value}";

		return $"conn:{connection.Id:D}";
	}

	public static string GetStableAccountKey( GameObject playerRoot )
	{
		if ( playerRoot is null || !playerRoot.IsValid() )
			return "";

		var session = playerRoot.Components.Get<ThornsPlayerSession>( FindMode.EverythingInSelf );
		if ( session.IsValid() && !string.IsNullOrEmpty( session.HostPersistenceAccountKey ) )
			return session.HostPersistenceAccountKey;

		var owner = playerRoot.Network?.Owner;
		if ( owner is not null )
			return GetStableAccountKey( owner );

		return "";
	}
}
