namespace FinalOutpost;

/// <summary>
/// Scene entry point that does not rely on a scene-authored component type.
/// Runs when the startup scene loads (editor play, standalone launch, or join).
/// </summary>
public sealed class OutpostBootstrap : GameObjectSystem<OutpostBootstrap>, ISceneStartup
{
	public OutpostBootstrap( Scene scene ) : base( scene )
	{
	}

	void ISceneStartup.OnHostPreInitialize( SceneFile scene )
	{
	}

	void ISceneStartup.OnHostInitialize()
	{
		Boot();
	}

	void ISceneStartup.OnClientInitialize()
	{
		Boot();
	}

	private static void Boot()
	{
		if ( GameCore.Instance is not null )
			return;

		var bootGo = new GameObject( true, "Boot" );
		bootGo.Components.Create<AmbiencePlayer>();
		bootGo.Components.Create<NightCombatMusicPlayer>();
		bootGo.Components.Create<DayNightLighting>();
		bootGo.Components.Create<WeaponModelLoader>();

		if ( !Networking.IsActive )
		{
			Networking.CreateLobby( new Sandbox.Network.LobbyConfig
			{
				MaxPlayers = 1,
				Name = "The Final Outpost",
				Privacy = Sandbox.Network.LobbyPrivacy.Public
			} );
		}

		var coreGo = new GameObject( true, "GameCore" );
		coreGo.Components.Create<GameCore>();
	}
}
