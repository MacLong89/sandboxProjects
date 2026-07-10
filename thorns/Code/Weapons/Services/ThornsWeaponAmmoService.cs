#nullable disable

using System;
using System.Threading.Tasks;

namespace Sandbox;

public sealed class ThornsWeaponAmmoService
{
	readonly ThornsWeapon _weapon;

	int _clientLoadedAmmo;
	int _clientReserveAmmo;
	bool _clientWeaponBroken;
	bool _clientReloading;

	/// <summary>Mirrored from host shotgun pump RPC — latch <see cref="ThornsViewModelFpAnimator"/> <c>b_reloading</c>; independent from brief <see cref="_clientReloading"/> HUD pulses.</summary>
	bool _clientShotgunPumpHud;

	public ThornsWeaponAmmoService( ThornsWeapon weapon ) => _weapon = weapon;

	public int ClientMirrorLoadedAmmo => _clientLoadedAmmo;
	public int ClientMirrorReserveAmmo => _clientReserveAmmo;
	public bool ClientMirrorWeaponBroken => _clientWeaponBroken;
	public bool ClientMirrorReloading => _clientReloading;
	public bool ClientShotgunPumpHud => _clientShotgunPumpHud;

	public int ServerCountAmmoMatchingType( ThornsInventory inv, string ammoTypeId ) =>
		inv.ServerCountAmmoMatchingType( ammoTypeId );

	public int ServerRemoveAmmoMatchingType( ThornsInventory inv, string ammoTypeId, int count ) =>
		inv.ServerRemoveAmmoMatchingType( ammoTypeId, count );

	public void HostPushWeaponHudFromInventory()
	{
		if ( !Networking.IsHost )
			return;

		PushWeaponHudToOwnerHost();
	}

	public void PushWeaponHudToOwnerHost()
	{
		if ( !Networking.IsHost )
			return;

		var inv = _weapon.GameObject.Components.Get<ThornsInventory>();
		var equip = _weapon.GameObject.Components.Get<ThornsHotbarEquipment>();
		if ( !inv.IsValid() || !equip.IsValid() )
		{
			_weapon.InvokeClientReceiveWeaponHudState( 0, 0, 0, 0, 0 );
			return;
		}

		var idx = equip.ServerGetSelectedHotbarIndex();
		if ( idx < 0 || idx >= ThornsInventory.HotbarSlotCount || !inv.TryGetHostSlot( idx, out var slot ) || slot.IsEmpty )
		{
			_weapon.InvokeClientReceiveWeaponHudState( 0, 0, 0, 0, 0 );
			return;
		}

		if ( !_weapon.TryResolveWeaponItemDefResilient( slot.ItemId, out var idef ) )
		{
			_weapon.InvokeClientReceiveWeaponHudState( 0, 0, 0, 0, 0 );
			return;
		}

		string combatId;
		if ( idef.ItemType == ThornsItemType.Tool )
			combatId = ThornsToolMeleeCombat.GetCombatDefinitionIdForToolItemId( slot.ItemId )?.Trim() ?? "";
		else
			combatId = ( string.IsNullOrEmpty( idef.CombatWeaponDefinitionId ) ? slot.ItemId : idef.CombatWeaponDefinitionId )
				?.Trim() ?? "";

		var wdef = ThornsWeaponDefinitions.Get( combatId );
		var reserve = ServerCountAmmoMatchingType( inv, wdef.AmmoTypeId );
		var broken = ThornsWeapon.IsWeaponBrokenInSlot( slot );
		var reloading = ((IThornsWeaponReloadHost)_weapon).HostReloadInProgress;
		var pump = ((IThornsWeaponReloadHost)_weapon).HostShotgunPumpReloadSession ? 1 : 0;

		if ( ThornsWeaponDefinitions.IsMeleeWeapon( wdef ) || ThornsWeaponDefinitions.IsKnownMeleeCombatId( combatId ) )
		{
			_weapon.InvokeClientReceiveWeaponHudState( -1, -1, broken ? 1 : 0, reloading ? 1 : 0, pump );
			return;
		}

		_weapon.InvokeClientReceiveWeaponHudState( slot.WeaponLoadedAmmo, reserve, broken ? 1 : 0, reloading ? 1 : 0, pump );
	}

	public void ClientReceiveWeaponHudState( int loadedAmmo, int reserveAmmo, int weaponBrokenInt, int reloadingInt, int shotgunPumpReloadSessionInt )
	{
		_clientLoadedAmmo = loadedAmmo;
		_clientReserveAmmo = reserveAmmo;
		_clientWeaponBroken = weaponBrokenInt != 0;
		_clientReloading = reloadingInt != 0;
		_clientShotgunPumpHud = shotgunPumpReloadSessionInt != 0;
	}

	public bool TryConsumeRangedShotAmmo(
		int hotbar,
		ThornsInventory inv,
		ref ThornsInventorySlot slot,
		ThornsWeaponDefinitions.WeaponDefinition def,
		double now,
		out bool brokenNow )
	{
		brokenNow = false;

		if ( slot.WeaponLoadedAmmo <= 0 )
			return false;

		var upsLuck = _weapon.GameObject.Components.Get<ThornsPlayerUpgrades>();
		var luckySkipAmmo = upsLuck.IsValid()
		                    && Random.Shared.NextDouble() < upsLuck.GetLuckyChamberProcChance();
		if ( !luckySkipAmmo )
			slot.WeaponLoadedAmmo--;

		slot.HasDurability = true;
		var durLoss = def.DurabilityLossPerShot;
		if ( upsLuck.IsValid() && upsLuck.ReinforcedRank > 0 )
			durLoss *= upsLuck.GetReinforcedDurabilityLossMultiplier();
		slot.Durability -= durLoss;
		brokenNow = slot.Durability <= 0f;
		if ( brokenNow )
			slot.Durability = 0f;

		inv.ServerWriteSlot( hotbar, slot );

		((IThornsWeaponCombatHost)_weapon).NextFireAllowedHostTime = now + def.FireIntervalSeconds;

		PushWeaponHudToOwnerHost();

		if ( brokenNow )
			_weapon.InvokeClientNotifyWeaponBroken();

		return true;
	}
}
