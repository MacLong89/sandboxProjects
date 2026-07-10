namespace Terraingen.Progression;

using Terraingen.Multiplayer;
using Terraingen.Player;

/// <summary>First-session registration — wildlife spawn grace only (no scripted encounters).</summary>
public static class ThornsFirstSessionRetention
{
	public static void HostOnNewPlayerReady( ThornsPlayerGameplay gameplay )
	{
		if ( gameplay is null || !ThornsMultiplayer.IsHostOrOffline || !gameplay.GameObject.IsValid() )
			return;

		ThornsNewPlayerWildlifeGrace.HostRegisterPlayerReady( gameplay );
	}

	public static void HostTick( Scene scene )
	{
		_ = scene;
		ThornsNewPlayerWildlifeGrace.HostPruneExpired();
	}
}
