namespace Sandbox;

/// <summary>
/// Owner-client intent: use consumable from selected hotbar slot (THORNS_EVERYTHING_DOCUMENT — server validates).
/// </summary>
[Title( "Thorns — Consumable Use Input" )]
[Category( "Thorns" )]
[Icon( "medication" )]
[Order( 80 )]
public sealed class ThornsConsumableUseInput : Component
{
	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying )
			return;

		if ( !ThornsPawn.IsLocalConnectionOwner( this ) )
			return;

		if ( !Input.Pressed( "use" ) && !Input.Pressed( "Use" ) )
			return;

		var harvest = Components.Get<ThornsHarvestInteractor>();
		if ( harvest.IsValid() && harvest.ShouldSuppressConsumableBecauseHarvestableInRange() )
			return;

		var hb = Components.Get<ThornsHotbarEquipment>();
		var inv = Components.Get<ThornsInventory>();
		if ( !hb.IsValid() || !inv.IsValid() )
			return;

		var sel = hb.ClientMirrorSelectedHotbar;
		if ( sel < 0 || sel >= ThornsInventory.HotbarSlotCount )
			return;

		if ( ThornsC4.IsEquippedPlacementItem( hb.ClientMirrorActiveItemId ) )
			return;

		Log.Info( $"[Thorns] Use key (hotbar slot {sel}) → RequestUseItemFromSlot" );
		inv.RequestUseItemFromSlot( sel );
	}
}
