namespace Sandbox;

/// <summary>
/// TEMP — local hotkeys to trigger inventory dev RPCs. Remove or disable for shipping.
/// </summary>
[Title( "Thorns — Inventory Dev Controls (TEMP)" )]
[Category( "Thorns" )]
[Icon( "bug_report" )]
[Order( 500 )]
public sealed class ThornsInventoryDevControls : Component
{
	[Property] public bool EnableHotkeys { get; set; } = true;

	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying || !EnableHotkeys || !ThornsInventoryDev.EnableDevRpcs )
			return;

		if ( !ThornsPawn.IsLocalConnectionOwner( this ) )
			return;

		var inv = Components.Get<ThornsInventory>();
		if ( !inv.IsValid() )
			return;

		if ( Input.Keyboard.Pressed( "g" ) )
			inv.RequestDevGrantStarter();

		if ( Input.Keyboard.Pressed( "i" ) )
			inv.RequestDevPrintInventory();

		if ( Input.Keyboard.Pressed( "k" ) )
			inv.RequestDevClearInventory();

		if ( Input.Keyboard.Pressed( "f9" ) )
			inv.RequestDevMoveTest( 0, 9, 5 );

		if ( Input.Keyboard.Pressed( "f10" ) )
			inv.RequestDevSwapTest( 0, 1 );
	}
}
