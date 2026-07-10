namespace Sandbox;

/// <summary>
/// TEMP — local keys to call armor equip RPCs for testing (inventory slot indices depend on starter grant order — print with <c>i</c>).
/// </summary>
[Title( "Thorns — Armor Dev Controls (TEMP)" )]
[Category( "Thorns" )]
[Icon( "shield_moon" )]
[Order( 501 )]
public sealed class ThornsArmorDevControls : Component
{
	[Property] public bool EnableHotkeys { get; set; } = true;

	[Property] public int InvSlotHelmet { get; set; } = 5;

	[Property] public int InvSlotChest { get; set; } = 6;

	[Property] public int InvSlotPants { get; set; } = 7;

	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying || !EnableHotkeys || !ThornsInventoryDev.EnableDevRpcs )
			return;

		if ( !ThornsPawn.IsLocalConnectionOwner( this ) )
			return;

		var armor = Components.Get<ThornsArmorEquipment>();
		if ( !armor.IsValid() )
			return;

		if ( Input.Keyboard.Pressed( "]" ) )
			armor.RequestEquipArmorFromInventory( InvSlotHelmet );

		if ( Input.Keyboard.Pressed( "\\" ) )
			armor.RequestEquipArmorFromInventory( InvSlotChest );

		if ( Input.Keyboard.Pressed( "=" ) )
			armor.RequestEquipArmorFromInventory( InvSlotPants );

		if ( Input.Keyboard.Pressed( "[" ) )
			armor.RequestUnequipArmor( 0 );

		if ( Input.Keyboard.Pressed( "'" ) )
			armor.RequestUnequipArmor( 1 );

		if ( Input.Keyboard.Pressed( ";" ) )
			armor.RequestUnequipArmor( 2 );
	}
}
