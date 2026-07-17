namespace Deep;

/// <summary>
/// Scene entry bootstrap. Spawns <see cref="DeepGame"/> — not a gameplay manager.
/// Lobby creation is kept for sbox hosting (solo MaxPlayers = 1).
/// </summary>
public sealed class DeepBootstrap : Component
{
	protected override void OnStart()
	{
		if ( Scene.IsEditor )
			return;

		if ( !Networking.IsActive )
		{
			Networking.CreateLobby( new Sandbox.Network.LobbyConfig
			{
				MaxPlayers = 1,
				Name = "DEEP",
				Privacy = Sandbox.Network.LobbyPrivacy.Public
			} );
		}

		if ( DeepGame.Instance is null )
		{
			var go = new GameObject( true, "DeepGame" );
			go.Components.Create<DeepGame>();
		}
	}
}
