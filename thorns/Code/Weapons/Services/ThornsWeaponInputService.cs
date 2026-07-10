#nullable disable

using System;

namespace Sandbox;

public sealed class ThornsWeaponInputService
{
	readonly ThornsWeapon _weapon;
	readonly ThornsWeaponClientFxService _clientFx;

	double _clientNextAutoFireIntentTime;

	public ThornsWeaponInputService( ThornsWeapon weapon, ThornsWeaponClientFxService clientFx )
	{
		_weapon = weapon;
		_clientFx = clientFx;
	}

	public void TickLocalOwnerInput()
	{
		if ( (Input.Pressed( "reload" ) || Input.Pressed( "Reload" )) && Connection.Local is not null )
		{
			_weapon.Components.Get<ThornsHotTipDirector>()?.NotifyReloadIntent();
			_weapon.RequestReload();
		}

		var attack1PressedForConsumable =
			Input.Pressed( "Attack1" ) || Input.Pressed( "attack1" );
		if ( attack1PressedForConsumable && TryRequestUseEquippedConsumableFromAttack() )
			return;

		var attack2PressedForConsumable =
			Input.Pressed( "Attack2" ) || Input.Pressed( "attack2" );
		if ( attack2PressedForConsumable && TryRequestUseEquippedConsumableFromAttack() )
			return;

		var cidUx = ThornsToolMeleeCombat.ResolveClientCombatDefinitionIdForInput( _weapon );
		var combatDef = ThornsWeaponDefinitions.Get( cidUx );

		if ( ThornsWeaponDefinitions.TreatsAsMeleeWeapon( combatDef, cidUx ) )
		{
			if ( ThornsWeaponDefinitions.HasSecondaryMeleeResolved( combatDef, cidUx )
			     && (Input.Pressed( "Attack2" ) || Input.Pressed( "attack2" )) )
			{
				TryLocalMeleeHeavyIntent();
				return;
			}

			if ( !(Input.Pressed( "Attack1" ) || Input.Pressed( "attack1" )) )
				return;

			var harvestUx = _weapon.Components.Get<ThornsHarvestInteractor>();
			if ( harvestUx.IsValid() && harvestUx.ClientTryHandlePrimaryHarvestSwing() )
				return;

			TryLocalFireIntent();
			return;
		}

		var autoFire = string.Equals( combatDef.FireMode, "auto", StringComparison.OrdinalIgnoreCase );
		var attackDown = Input.Down( "Attack1" ) || Input.Down( "attack1" );
		var attackPressed = Input.Pressed( "Attack1" ) || Input.Pressed( "attack1" );

		if ( autoFire )
		{
			if ( !attackDown )
				return;

			if ( Time.Now < _clientNextAutoFireIntentTime )
				return;

			_clientNextAutoFireIntentTime = Time.Now + combatDef.FireIntervalSeconds * 0.92;
		}
		else if ( !attackPressed )
		{
			return;
		}

		TryLocalFireIntent();
	}

	public void ResetAutoFireIntentTime() => _clientNextAutoFireIntentTime = 0;

	/// <summary>
	/// Selected hotbar is a usable consumable — same path as <see cref="ThornsConsumableUseInput"/>; <c>Attack1</c>/<c>Attack2</c> both request use.
	/// </summary>
	bool TryRequestUseEquippedConsumableFromAttack()
	{
		var harvest = _weapon.Components.Get<ThornsHarvestInteractor>();
		if ( harvest.IsValid() && harvest.ShouldSuppressConsumableBecauseHarvestableInRange() )
			return false;

		var hb = _weapon.Components.Get<ThornsHotbarEquipment>();
		var inv = _weapon.Components.Get<ThornsInventory>();
		if ( !hb.IsValid() || !inv.IsValid() )
			return false;

		var sel = hb.ClientMirrorSelectedHotbar;
		if ( sel < 0 || sel >= ThornsInventory.HotbarSlotCount )
			return false;

		if ( !inv.TryGetClientMirrorSlot( sel, out var net )
		     || net.Quantity <= 0 || string.IsNullOrWhiteSpace( net.ItemId ) )
			return false;

		if ( ThornsC4.IsEquippedPlacementItem( net.ItemId ) )
			return false;

		if ( !ThornsItemRegistry.TryGet( net.ItemId, out var def )
		     || !ThornsItemRegistry.IsUsableConsumable( def ) )
			return false;

		inv.RequestUseItemFromSlot( sel );
		return true;
	}

	void TryLocalFireIntent()
	{
		var hp = _weapon.Components.Get<ThornsHealth>();
		if ( hp.IsValid() && ( !hp.IsAlive || hp.IsDeadState ) )
			return;

		if ( !_weapon.ClientMirrorMayFireIntent() )
			return;

		if ( !ThornsCombatAuthority.TryGetAuthoritativeEye( _weapon.GameObject, out var eyePos, out var eyeRot ) )
			return;

		var dir = eyeRot.Forward.Normal;

		var cidLight = (_weapon.ClientMirrorCombatDefinitionId ?? "").Trim();
		if ( ThornsToolMeleeCombat.IsToolMeleeCombatId( cidLight ) )
		{
			if ( !ThornsToolMeleeCombat.ClientTryConsumePrimaryStrikeCadence() )
				return;

			// Predicted local melee swing is visual-only; authoritative RpcFireOutcome chooses hit/miss audio.
			ThornsToolMeleeCombat.ClientPlayPrimaryToolStrikePresentation( _weapon.GameObject, playSound: false );
			ThornsToolMeleeCombat.ClientMarkPrimaryStrikePresentationPlayed();
		}

		_clientFx.PlayLocalFireFeedback();

		var defForAds = ThornsWeaponDefinitions.Get( _weapon.ClientMirrorCombatDefinitionId );
		if ( !ThornsWeaponDefinitions.IsMeleeWeapon( defForAds ) )
			_weapon.Components.Get<ThornsHotTipDirector>()?.NotifyFirstGunshot();

		var adsHeld = !ThornsWeaponDefinitions.IsMeleeWeapon( defForAds )
		              && (Input.Down( "Attack2" ) || Input.Down( "attack2" ));
		_weapon.RequestFire( dir, attackVariant: 0, aimDownSights: adsHeld );
	}

	void TryLocalMeleeHeavyIntent()
	{
		var hp = _weapon.Components.Get<ThornsHealth>();
		if ( hp.IsValid() && ( !hp.IsAlive || hp.IsDeadState ) )
			return;

		if ( !_weapon.ClientMirrorMayFireIntent() )
			return;

		if ( !ThornsCombatAuthority.TryGetAuthoritativeEye( _weapon.GameObject, out _, out var eyeRot ) )
			return;

		var dir = eyeRot.Forward.Normal;

		_weapon.RequestFire( dir, attackVariant: 1, aimDownSights: false );
	}
}
