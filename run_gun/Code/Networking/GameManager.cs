namespace RunGun;

/// <summary>
/// Scene entry point placed in game.scene. Creates a solo lobby and spawns the GameCore,
/// which builds the whole game at runtime (no scene authoring required).
/// </summary>
public sealed class GameManager : Component
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
				Name = "Run & Gun",
				Privacy = Sandbox.Network.LobbyPrivacy.Public
			} );
		}

		if ( GameCore.Instance is null )
		{
			var go = new GameObject( true, "GameCore" );
			go.Components.Create<GameCore>();
		}
	}
}
