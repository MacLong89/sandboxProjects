namespace Terraingen.UI.Menu;

using Sandbox.Network;

public static class ThornsLobbyUtil
{
	public static bool IsValid( in LobbyInformation lobby ) => lobby.LobbyId != 0;
}
