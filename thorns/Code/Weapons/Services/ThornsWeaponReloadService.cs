#nullable disable

using System;

namespace Sandbox;

public sealed class ThornsWeaponReloadService
{
	const float PumpReloadHudPulseSeconds = 0.05f;

	readonly IThornsWeaponReloadHost _host;
	readonly ThornsWeaponAmmoService _ammo;

	bool _hostReloadInProgress;
	/// <summary>Tube-style reload: one R press loads shells until full — keeps fire blocked even when <see cref="_hostReloadInProgress"/> is briefly false for FP reload edges.</summary>
	bool _hostShotgunPumpReloadSession;
	int _hostReloadHotbarSlot = -1;
	string _hostReloadWeaponInstanceId = "";

	public ThornsWeaponReloadService( IThornsWeaponReloadHost host, ThornsWeaponAmmoService ammo )
	{
		_host = host;
		_ammo = ammo;
	}

	public bool HostReloadInProgress
	{
		get => _hostReloadInProgress;
		set => _hostReloadInProgress = value;
	}

	public bool HostShotgunPumpReloadSession
	{
		get => _hostShotgunPumpReloadSession;
		set => _hostShotgunPumpReloadSession = value;
	}

	public int HostReloadHotbarSlot
	{
		get => _hostReloadHotbarSlot;
		set => _hostReloadHotbarSlot = value;
	}

	public string HostReloadWeaponInstanceId
	{
		get => _hostReloadWeaponInstanceId;
		set => _hostReloadWeaponInstanceId = value ?? "";
	}

	public bool IsReloadBlockingFire() => _hostReloadInProgress || _hostShotgunPumpReloadSession;

	public void CancelReloadState()
	{
		_hostReloadInProgress = false;
		_hostShotgunPumpReloadSession = false;
		_hostReloadHotbarSlot = -1;
		_hostReloadWeaponInstanceId = "";
	}

	public void HostOnSelectedNonWeapon()
	{
		if ( !Networking.IsHost )
			return;

		CancelReloadState();
		_host.Weapon.InvokeClientReceiveWeaponHudState( 0, 0, 0, 0, 0 );
	}

	public void HandleRequestReload()
	{
		if ( !Networking.IsHost )
			return;

		if ( !_host.ValidateRpcCallerOwnsPawn() )
		{
			return;
		}

		var hpReload = _host.GameObject.Components.Get<ThornsHealth>();
		if ( hpReload.IsValid() && ( hpReload.IsDeadState || !hpReload.IsAlive ) )
		{
			_host.ClientNotifyReloadFailed( "dead" );
			return;
		}

		var inv = _host.GameObject.Components.Get<ThornsInventory>();
		var equip = _host.GameObject.Components.Get<ThornsHotbarEquipment>();
		if ( !inv.IsValid() || !equip.IsValid() )
		{
			return;
		}

		var hotbar = equip.ServerGetSelectedHotbarIndex();
		if ( hotbar < 0 || !inv.TryGetHostSlot( hotbar, out var slot ) || slot.IsEmpty )
		{
			_host.ClientNotifyReloadFailed( "no_weapon" );
			return;
		}

		if ( !_host.TryResolveWeaponItemDefResilient( slot.ItemId, out var itemDef ) )
		{
			_host.ClientNotifyReloadFailed( "not_weapon" );
			return;
		}

		if ( itemDef.ItemType == ThornsItemType.Tool )
			return;

		var combatKey = string.IsNullOrEmpty( itemDef.CombatWeaponDefinitionId ) ? slot.ItemId : itemDef.CombatWeaponDefinitionId;
		combatKey = combatKey?.Trim() ?? "";
		var wdef = ThornsWeaponDefinitions.Get( combatKey );

		if ( ThornsWeaponDefinitions.IsMeleeWeapon( wdef ) || ThornsWeaponDefinitions.IsKnownMeleeCombatId( combatKey ) )
		{
			return;
		}

		if ( _host.IsWeaponBrokenInSlot( slot ) )
		{
			_host.ClientNotifyReloadFailed( "broken" );
			return;
		}

		if ( _hostReloadInProgress || _hostShotgunPumpReloadSession )
		{
			_host.ClientNotifyReloadFailed( "already_reloading" );
			return;
		}

		if ( slot.WeaponLoadedAmmo >= wdef.ClipSize )
		{
			_host.ClientNotifyReloadFailed( "clip_full" );
			return;
		}

		var reserve = _ammo.ServerCountAmmoMatchingType( inv, wdef.AmmoTypeId );
		if ( reserve <= 0 )
		{
			_host.ClientNotifyReloadFailed( "no_ammo" );
			return;
		}

		_hostReloadInProgress = true;
		_hostReloadHotbarSlot = hotbar;
		_hostReloadWeaponInstanceId = slot.WeaponInstanceId ?? "";

		var perShell = ThornsWeaponDefinitions.UsesPerShellReloadCycle( wdef, combatKey );
		if ( perShell )
			_hostShotgunPumpReloadSession = true;


		_ammo.PushWeaponHudToOwnerHost();

		_host.BeginHostReloadAsync( hotbar, _hostReloadWeaponInstanceId, combatKey );
	}

	public async Task RunHostReloadAsync( int hotbarSlot, string weaponInstanceAtStart, string combatKey )
	{
		var wdefInitial = ThornsWeaponDefinitions.Get( combatKey );
		var usesPerShellReload = ThornsWeaponDefinitions.UsesPerShellReloadCycle( wdefInitial, combatKey );

		try
		{
			if ( usesPerShellReload )
			{
				var firstShell = true;

				while ( Networking.IsHost && _host.GameObject.IsValid() && _hostShotgunPumpReloadSession )
				{
					var wdefLoop = ThornsWeaponDefinitions.Get( combatKey );
					if ( !ThornsWeaponDefinitions.UsesPerShellReloadCycle( wdefLoop, combatKey ) )
					{
						break;
					}

					if ( !firstShell )
					{
						_hostReloadInProgress = false;
						_ammo.PushWeaponHudToOwnerHost();
						await _host.AwaitRealtimeSeconds( PumpReloadHudPulseSeconds );
						if ( !Networking.IsHost || !_host.GameObject.IsValid() || !_hostShotgunPumpReloadSession )
							break;
						_hostReloadInProgress = true;
						_ammo.PushWeaponHudToOwnerHost();
					}

					var gate = ThornsWeaponDefinitions.ShellReloadGameplayGateSeconds( wdefLoop, combatKey );
					await _host.AwaitRealtimeSeconds( gate );

					if ( !Networking.IsHost || !_host.GameObject.IsValid() || !_hostShotgunPumpReloadSession )
						break;

					var inv = _host.GameObject.Components.Get<ThornsInventory>();
					var equip = _host.GameObject.Components.Get<ThornsHotbarEquipment>();
					if ( !inv.IsValid() || !equip.IsValid() )
					{
						break;
					}

					if ( equip.ServerGetSelectedHotbarIndex() != hotbarSlot )
					{
						break;
					}

					if ( !inv.TryGetHostSlot( hotbarSlot, out var slot ) || slot.IsEmpty )
					{
						break;
					}

					if ( (slot.WeaponInstanceId ?? "") != weaponInstanceAtStart )
					{
						break;
					}

					if ( _host.IsWeaponBrokenInSlot( slot ) )
					{
						break;
					}

					var space = wdefLoop.ClipSize - slot.WeaponLoadedAmmo;
					if ( space <= 0 )
					{
						break;
					}

					var reserve = _ammo.ServerCountAmmoMatchingType( inv, wdefLoop.AmmoTypeId );
					var toLoad = Math.Min( Math.Max( 1, wdefLoop.ReloadShellCountPerRpc ), Math.Min( space, reserve ) );
					if ( toLoad <= 0 )
					{
						break;
					}

					var removed = _ammo.ServerRemoveAmmoMatchingType( inv, wdefLoop.AmmoTypeId, toLoad );
					if ( removed <= 0 )
					{
						break;
					}

					if ( !inv.TryGetHostSlot( hotbarSlot, out slot ) )
						break;

					slot.WeaponLoadedAmmo += removed;
					if ( slot.WeaponLoadedAmmo > wdefLoop.ClipSize )
						slot.WeaponLoadedAmmo = wdefLoop.ClipSize;

					inv.ServerWriteSlot( hotbarSlot, slot );


					if ( string.Equals( combatKey, "shotgun", StringComparison.OrdinalIgnoreCase ) )
						_host.SendOwnerWeaponSound( ThornsWeapon.ShotgunReloadSoundResource );

					_ammo.PushWeaponHudToOwnerHost();

					var stillFullOrDry = slot.WeaponLoadedAmmo >= wdefLoop.ClipSize
					                     || _ammo.ServerCountAmmoMatchingType( inv, wdefLoop.AmmoTypeId ) <= 0;
					if ( stillFullOrDry )
						break;

					firstShell = false;
				}
			}
			else
			{
				if ( MagazineWeaponUsesM4StyleReloadSound( combatKey ) )
					_host.SendOwnerWeaponSound( ThornsWeapon.M4ReloadSoundResource );

				await _host.AwaitRealtimeSeconds( Math.Max( 0.01f, wdefInitial.ReloadTimeSeconds ) );

				if ( !Networking.IsHost || !_host.GameObject.IsValid() )
					return;

				var inv = _host.GameObject.Components.Get<ThornsInventory>();
				var equip = _host.GameObject.Components.Get<ThornsHotbarEquipment>();
				if ( !inv.IsValid() || !equip.IsValid() )
				{
					return;
				}

				if ( equip.ServerGetSelectedHotbarIndex() != hotbarSlot )
				{
					return;
				}

				if ( !inv.TryGetHostSlot( hotbarSlot, out var slot ) || slot.IsEmpty )
				{
					return;
				}

				var inst = slot.WeaponInstanceId ?? "";
				if ( inst != weaponInstanceAtStart )
				{
					return;
				}

				var wdef = ThornsWeaponDefinitions.Get( combatKey );

				if ( _host.IsWeaponBrokenInSlot( slot ) )
				{
					return;
				}

				var space = wdef.ClipSize - slot.WeaponLoadedAmmo;
				if ( space <= 0 )
				{
					return;
				}

				var reserve = _ammo.ServerCountAmmoMatchingType( inv, wdef.AmmoTypeId );
				var toLoad = Math.Min( space, reserve );
				if ( toLoad <= 0 )
				{
					return;
				}

				var removed = _ammo.ServerRemoveAmmoMatchingType( inv, wdef.AmmoTypeId, toLoad );
				if ( removed <= 0 )
				{
					return;
				}

				if ( !inv.TryGetHostSlot( hotbarSlot, out slot ) )
					return;

				slot.WeaponLoadedAmmo += removed;
				if ( slot.WeaponLoadedAmmo > wdef.ClipSize )
					slot.WeaponLoadedAmmo = wdef.ClipSize;

				inv.ServerWriteSlot( hotbarSlot, slot );

			}
		}
		finally
		{
			CancelReloadState();
			_ammo.PushWeaponHudToOwnerHost();
		}
	}

	static bool MagazineWeaponUsesM4StyleReloadSound( string combatKey ) =>
		MagazineWeaponUsesM4StyleFireSound( combatKey );

	static bool MagazineWeaponUsesM4StyleFireSound( string combatKey )
	{
		if ( string.IsNullOrWhiteSpace( combatKey ) )
			return false;

		return string.Equals( combatKey, "m4", StringComparison.OrdinalIgnoreCase )
		       || string.Equals( combatKey, "mp5", StringComparison.OrdinalIgnoreCase )
		       || string.Equals( combatKey, "sniper", StringComparison.OrdinalIgnoreCase );
	}
}
