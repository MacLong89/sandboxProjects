namespace DeepDive;

/// <summary>
/// Scene entry bootstrap. Spawns <see cref="DeepDiveGame"/> — not a gameplay manager.
/// Lobby creation is kept for sbox hosting (solo MaxPlayers = 1).
/// </summary>
public sealed class DeepDiveBootstrap : Component
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
				Name = "DEEP DIVE",
				Privacy = Sandbox.Network.LobbyPrivacy.Public
			} );
		}

		if ( DeepDiveGame.Instance is null )
		{
			var go = new GameObject( true, "DeepDiveGame" );
			go.Components.Create<DeepDiveGame>();
		}
	}
}
