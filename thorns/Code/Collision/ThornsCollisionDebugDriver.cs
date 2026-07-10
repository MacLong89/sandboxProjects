namespace Sandbox;

/// <summary>
/// Local pawn driver for collision debug (P). Runs on a plain <see cref="Component"/> so wireframes do not depend on HUD <see cref="PanelComponent"/> init.
/// </summary>
[Title( "Thorns — Collision Debug" )]
[Category( "Thorns" )]
[Icon( "grid_on" )]
[Order( 24 )]
public sealed class ThornsCollisionDebugDriver : Component
{
	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying || !ThornsPawn.IsLocalConnectionOwner( this ) )
			return;

		if ( Input.Keyboard.Pressed( "p" ) || Input.Keyboard.Pressed( "P" ) )
			ThornsCollisionDebug.ToggleAndLog();

		if ( ThornsCollisionDebug.ShowNearbySolidColliders )
			ThornsCollisionDebug.TickDraw( GameObject );
	}
}
