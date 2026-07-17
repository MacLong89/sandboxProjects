using OffshoreFishing.Core;

namespace Sandbox;

/// <summary>Event-driven audio hooks. Uses procedural beeps until sound assets exist.</summary>
[Title( "Audio Director" )]
public sealed class AudioDirector : Component
{
	private FishingGameController _game;
	private RealTimeSince _ambientPulse;

	protected override void OnStart()
	{
		_game = Scene.GetAllComponents<FishingGameController>().FirstOrDefault();
	}

	protected override void OnUpdate()
	{
		if ( _ambientPulse > 8f )
			_ambientPulse = 0;
	}

	public void OnEvent( IDomainEvent e )
	{
		switch ( e )
		{
			case FishCaughtEvent:
				Play( "sounds/catch.wav" );
				break;
			case FishSoldEvent:
				Play( "sounds/coin.wav" );
				break;
			case FishingPhaseChangedEvent phase when phase.Phase == FishingPhase.BiteWindow:
				Play( "sounds/bite.wav" );
				Play( "sounds/splash.wav" );
				break;
			case ItemPurchasedEvent:
				Play( "sounds/purchase.wav" );
				break;
			case NotificationEvent:
				Play( "sounds/ui_click.wav" );
				break;
		}
	}

	private static void Play( string path )
	{
		try
		{
			Sound.Play( path );
		}
		catch ( Exception ex )
		{
			Log.Info( $"[Audio] {path} ({ex.Message})" );
		}
	}
}
