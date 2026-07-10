namespace Sandbox;

public static class AimboxBotSpawner
{
	public static void EnsureBots( Scene scene, int count )
	{
		if ( scene is null || !scene.IsValid() || count <= 0 )
			return;

		var existing = scene.GetAllComponents<AimboxBotController>().Count();
		for ( var i = existing; i < count; i++ )
			SpawnBot( scene, i + 1 );
	}

	public static void SetBotCount( Scene scene, int count )
	{
		if ( scene is null || !scene.IsValid() )
			return;

		count = Math.Max( 0, count );
		var bots = scene.GetAllComponents<AimboxBotController>().ToList();
		for ( var i = bots.Count - 1; i >= count; i-- )
			bots[i].GameObject.Destroy();

		EnsureBots( scene, count );
	}

	public static AimboxBotController SpawnBot( Scene scene, int index )
	{
		var gamertag = AimboxBotGamertags.ForSlot( index );
		var go = new GameObject( true, gamertag );
		go.NetworkMode = NetworkMode.Object;
		AimboxHitboxes.ConfigureCitizenCapsule( go.Components.Create<CapsuleCollider>() );
		var bot = go.Components.Create<AimboxBotController>();
		bot.BotId = $"bot_{index:D3}";
		bot.Gamertag = gamertag;
		return bot;
	}
}
