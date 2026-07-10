namespace Sandbox;

/// <summary>Weapon service orchestration — binds input, reload, ammo HUD, host combat, client FX, observer sync.</summary>
public sealed class ThornsWeaponCoordinator
{
	public ThornsWeaponInputService Input { get; private set; }
	public ThornsWeaponReloadService Reload { get; private set; }
	public ThornsWeaponAmmoService Ammo { get; private set; }
	public ThornsWeaponHostCombatService Combat { get; private set; }
	public ThornsWeaponClientFxService ClientFx { get; private set; }
	public ThornsWeaponObserverSyncService ObserverSync { get; private set; }

	public bool IsBound => Reload is not null;

	public void Bind( ThornsWeapon weapon )
	{
		if ( IsBound )
			return;

		Ammo = new ThornsWeaponAmmoService( weapon );
		ObserverSync = new ThornsWeaponObserverSyncService( weapon );
		ClientFx = new ThornsWeaponClientFxService( weapon, ObserverSync );
		Reload = new ThornsWeaponReloadService( weapon, Ammo );
		Combat = new ThornsWeaponHostCombatService( weapon );
		Input = new ThornsWeaponInputService( weapon, ClientFx );
	}
}
