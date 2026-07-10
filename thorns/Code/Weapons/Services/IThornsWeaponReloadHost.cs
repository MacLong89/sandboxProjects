namespace Sandbox;

/// <summary>Reload authority surface for <see cref="ThornsWeaponReloadService"/>.</summary>
public interface IThornsWeaponReloadHost
{
	ThornsWeapon Weapon { get; }
	GameObject GameObject { get; }

	bool HostReloadInProgress { get; set; }
	bool HostShotgunPumpReloadSession { get; set; }
	int HostReloadHotbarSlot { get; set; }
	string HostReloadWeaponInstanceId { get; set; }

	bool ValidateRpcCallerOwnsPawn();
	void ClientNotifyReloadFailed( string reason );
	void PushWeaponHudToOwnerHost();
	void ClientReceiveWeaponHudState( int loadedAmmo, int reserveAmmo, int weaponBrokenInt, int reloadingInt, int shotgunPumpReloadSessionInt );
	void SendOwnerWeaponSound( string resourcePath );

	bool TryResolveWeaponItemDefResilient( string itemId, out ThornsItemRegistry.ThornsItemDefinition itemDef );
	bool IsWeaponBrokenInSlot( ThornsInventorySlot slot );

	/// <summary>s&amp;box <see cref="Task.DelayRealtimeSeconds"/> — only valid on component host.</summary>
	Task AwaitRealtimeSeconds( float seconds );

	void BeginHostReloadAsync( int hotbarSlot, string weaponInstanceAtStart, string combatKey );
}
