using OffshoreFishing.Core;

namespace Sandbox;

/// <summary>
/// Optional world SpriteRenderer layer. Disabled at boot — the playable view is
/// drawn by <see cref="FishingHudRoot"/> in UI space. Creating many Texture.Create
/// sprites during OnAwake was crashing the editor on Play.
/// </summary>
[Title( "World Presenter" )]
public sealed class WorldPresenter : Component
{
	FishingGameController _game;
	bool _ready;

	public void Bind( FishingGameController game )
	{
		_game = game;
		_ready = true;
		Log.Info( "[Offshore] WorldPresenter bound (UI world path — no GPU sprites)" );
	}

	protected override void OnAwake()
	{
		_ready = true;
	}

	protected override void OnStart()
	{
		_ready = true;
		Log.Info( "[Offshore] WorldPresenter sprites ready" );
	}

	public void Sync( GameSession session )
	{
		// World is rendered by FishingHudRoot; keep this as a no-op bridge.
		_ = session;
		_ = _game;
		_ = _ready;
	}

	public void OnEvent( IDomainEvent e )
	{
	}
}
