namespace FinalOutpost;

/// <summary>
/// Optional scene-authored boot hook. Prefer <see cref="OutpostBootstrap"/> —
/// game.scene no longer references this type (avoids "Generic Missing Component"
/// when Code fails to compile). Kept so you can drop it on a GO manually for tests.
/// </summary>
public sealed class SceneBoot : Component
{
	protected override void OnStart()
	{
		Log.Info( "[FinalOutpost] SceneBoot OnStart → GameBoot.Run" );
		GameBoot.Run( Scene );
	}

	protected override void OnAwake() =>
		Log.Info( "[FinalOutpost] SceneBoot OnAwake" );
}
