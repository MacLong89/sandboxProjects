#nullable disable

namespace Sandbox;

public sealed partial class ThornsWeapon : IThornsWeaponCombatHost, IThornsWeaponReloadHost, IThornsWeaponFxHost
{
	readonly ThornsWeaponCoordinator _weaponServices = new();

	ThornsWeapon IThornsWeaponCombatHost.Weapon => this;
	GameObject IThornsWeaponCombatHost.GameObject => GameObject;
	float IThornsWeaponCombatHost.AimDotMin => AimDotMin;

	double IThornsWeaponCombatHost.NextFireAllowedHostTime
	{
		get => _nextFireAllowedHostTime;
		set => _nextFireAllowedHostTime = value;
	}

	double IThornsWeaponCombatHost.NextMeleeHeavyAllowedHostTime
	{
		get => _nextMeleeHeavyAllowedHostTime;
		set => _nextMeleeHeavyAllowedHostTime = value;
	}

	double IThornsWeaponCombatHost.HostRecoilLastShotTime
	{
		get => _hostRecoilLastShotTime;
		set => _hostRecoilLastShotTime = value;
	}

	int IThornsWeaponCombatHost.HostRecoilPatternIndex
	{
		get => _hostRecoilPatternIndex;
		set => _hostRecoilPatternIndex = value;
	}

	int IThornsWeaponCombatHost.HostRecoilSprayOrdinal
	{
		get => _hostRecoilSprayOrdinal;
		set => _hostRecoilSprayOrdinal = value;
	}

	bool IThornsWeaponCombatHost.IsReloadBlockingFire() => _weaponServices.Reload.IsReloadBlockingFire();

	bool IThornsWeaponCombatHost.HostTryResolveHitscanDamageTarget(
		Vector3 start,
		Vector3 dir,
		float range,
		float meleeMaxAbsVerticalSeparationFeet,
		out SceneTraceResult tr,
		out GameObject hitGo,
		out ThornsPawn victimPawn,
		out ThornsHealth victimHealth,
		out bool usedAnalyticFallback,
		out Vector3 analyticHitPosition ) =>
		HostTryResolveHitscanDamageTarget(
			start,
			dir,
			range,
			meleeMaxAbsVerticalSeparationFeet,
			out tr,
			out hitGo,
			out victimPawn,
			out victimHealth,
			out usedAnalyticFallback,
			out analyticHitPosition );

	Vector3 IThornsWeaponCombatHost.HostFeedbackEndpointWorldTrace( Vector3 rayStart, Vector3 dirN, float range, out ThornsWeaponImpactSurfaceKind surfaceKind ) =>
		HostFeedbackEndpointWorldTrace( rayStart, dirN, range, out surfaceKind );

	bool IThornsWeaponCombatHost.IsOriginPlausible( Vector3 eyeWorld ) => IsOriginPlausible( eyeWorld );

	void IThornsWeaponCombatHost.SendRpcFireOutcome(
		bool ammunitionExpended,
		bool damageAppliedToTarget,
		float damageDealt,
		bool hitMarkerHighlight,
		int fpAttackPresentationKind,
		float clientKickPitch,
		float clientKickYaw,
		bool feedbackHasEndpoint,
		Vector3? feedbackHitWorld,
		ThornsWeaponImpactSurfaceKind feedbackSurface,
		bool feedbackTargetKilled ) =>
		_weaponServices.ClientFx.SendRpcFireOutcome(
			ammunitionExpended,
			damageAppliedToTarget,
			damageDealt,
			hitMarkerHighlight,
			fpAttackPresentationKind,
			clientKickPitch,
			clientKickYaw,
			feedbackHasEndpoint,
			feedbackHitWorld,
			feedbackSurface,
			feedbackTargetKilled );

	void IThornsWeaponCombatHost.PushWeaponHudToOwnerHost() => _weaponServices.Ammo.PushWeaponHudToOwnerHost();

	void IThornsWeaponCombatHost.ClientNotifyWeaponBroken() => ClientNotifyWeaponBroken();

	bool IThornsWeaponCombatHost.TryConsumeRangedShotAmmo(
		int hotbar,
		ThornsInventory inv,
		ref ThornsInventorySlot slot,
		ThornsWeaponDefinitions.WeaponDefinition def,
		double now,
		out bool brokenNow ) =>
		_weaponServices.Ammo.TryConsumeRangedShotAmmo( hotbar, inv, ref slot, def, now, out brokenNow );

	ThornsWeapon IThornsWeaponReloadHost.Weapon => this;
	GameObject IThornsWeaponReloadHost.GameObject => GameObject;

	bool IThornsWeaponReloadHost.HostReloadInProgress
	{
		get => _weaponServices.Reload.HostReloadInProgress;
		set => _weaponServices.Reload.HostReloadInProgress = value;
	}

	bool IThornsWeaponReloadHost.HostShotgunPumpReloadSession
	{
		get => _weaponServices.Reload.HostShotgunPumpReloadSession;
		set => _weaponServices.Reload.HostShotgunPumpReloadSession = value;
	}

	int IThornsWeaponReloadHost.HostReloadHotbarSlot
	{
		get => _weaponServices.Reload.HostReloadHotbarSlot;
		set => _weaponServices.Reload.HostReloadHotbarSlot = value;
	}

	string IThornsWeaponReloadHost.HostReloadWeaponInstanceId
	{
		get => _weaponServices.Reload.HostReloadWeaponInstanceId;
		set => _weaponServices.Reload.HostReloadWeaponInstanceId = value;
	}

	bool IThornsWeaponReloadHost.ValidateRpcCallerOwnsPawn() => ValidateRpcCallerOwnsPawn();
	void IThornsWeaponReloadHost.ClientNotifyReloadFailed( string reason ) => ClientNotifyReloadFailed( reason );
	void IThornsWeaponReloadHost.PushWeaponHudToOwnerHost() => _weaponServices.Ammo.PushWeaponHudToOwnerHost();
	void IThornsWeaponReloadHost.ClientReceiveWeaponHudState( int loadedAmmo, int reserveAmmo, int weaponBrokenInt, int reloadingInt, int shotgunPumpReloadSessionInt ) =>
		ClientReceiveWeaponHudState( loadedAmmo, reserveAmmo, weaponBrokenInt, reloadingInt, shotgunPumpReloadSessionInt );
	void IThornsWeaponReloadHost.SendOwnerWeaponSound( string resourcePath ) => _weaponServices.ObserverSync.SendOwnerWeaponSound( resourcePath );
	bool IThornsWeaponReloadHost.TryResolveWeaponItemDefResilient( string itemId, out ThornsItemRegistry.ThornsItemDefinition itemDef ) =>
		TryResolveWeaponItemDefResilient( itemId, out itemDef );
	bool IThornsWeaponReloadHost.IsWeaponBrokenInSlot( ThornsInventorySlot slot ) => IsWeaponBrokenInSlot( slot );

	async Task IThornsWeaponReloadHost.AwaitRealtimeSeconds( float seconds ) =>
		await Task.DelayRealtimeSeconds( seconds );

	void IThornsWeaponReloadHost.BeginHostReloadAsync( int hotbarSlot, string weaponInstanceAtStart, string combatKey ) =>
		_ = _weaponServices.Reload.RunHostReloadAsync( hotbarSlot, weaponInstanceAtStart, combatKey );

	ThornsWeapon IThornsWeaponFxHost.Weapon => this;
	GameObject IThornsWeaponFxHost.GameObject => GameObject;

	string IThornsWeaponFxHost.OwnerMirrorCombatWeaponDefinitionId
	{
		get => _ownerMirrorCombatWeaponDefinitionId;
		set => _ownerMirrorCombatWeaponDefinitionId = value;
	}

	string IThornsWeaponFxHost.ClientMirrorCombatDefinitionId => ClientMirrorCombatDefinitionId;
	string IThornsWeaponFxHost.HitMarkerBodySound => HitMarkerBodySound;
	string IThornsWeaponFxHost.HitMarkerHeadshotSound => HitMarkerHeadshotSound;
	float IThornsWeaponFxHost.HitMarkerBodyVolume => HitMarkerBodyVolume;
	float IThornsWeaponFxHost.HitMarkerHeadshotVolume => HitMarkerHeadshotVolume;

	ThornsViewModelFpAnimator IThornsWeaponFxHost.ResolveLocalFpAnimator() => ResolveLocalFpAnimator();

	void IThornsWeaponFxHost.PlayOwnerWeaponSoundAtEar( string resourcePath, float volumeMultiplier ) =>
		_weaponServices.ObserverSync.PlayOwnerWeaponSoundAtEar( resourcePath, volumeMultiplier );

	void IThornsWeaponFxHost.RpcFireOutcome(
		bool ammunitionExpended,
		bool damageAppliedToTarget,
		float damageDealt,
		bool hitMarkerHighlight,
		int fpAttackPresentationKind,
		int clientKickPitchMilliDegrees,
		int clientKickYawMilliDegrees,
		bool feedbackHasEndpoint,
		int feedbackHitXMm,
		int feedbackHitYMm,
		int feedbackHitZMm,
		int feedbackSurfaceKind,
		bool feedbackTargetKilled ) =>
		RpcFireOutcome(
			ammunitionExpended,
			damageAppliedToTarget,
			damageDealt,
			hitMarkerHighlight,
			fpAttackPresentationKind,
			clientKickPitchMilliDegrees,
			clientKickYawMilliDegrees,
			feedbackHasEndpoint,
			feedbackHitXMm,
			feedbackHitYMm,
			feedbackHitZMm,
			feedbackSurfaceKind,
			feedbackTargetKilled );

	void IThornsWeaponFxHost.HostMaybeBroadcastObserverGunshot( Guid shooterOwnerConnectionId, string resourcePath ) =>
		_weaponServices.ObserverSync.HostMaybeBroadcastObserverGunshot( shooterOwnerConnectionId, resourcePath );

	bool IThornsWeaponFxHost.HostTryResolveMirrorGunFireSoundPath( out string path ) => HostTryResolveMirrorGunFireSoundPath( out path );

	void BindWeaponServices() => _weaponServices.Bind( this );

	/// <summary>Spawn / inventory may clear equipment before <see cref="OnStart"/> — bind services early.</summary>
	void EnsureWeaponServicesBound() => BindWeaponServices();

	/// <summary>Host: refresh owner HUD mirror (loaded / reserve / broken / reloading). Other players never receive this.</summary>
	public void HostPushWeaponHudFromInventory()
	{
		EnsureWeaponServicesBound();
		_weaponServices.Ammo.HostPushWeaponHudFromInventory();
	}

	/// <summary>Host: non-weapon hotbar selection — cancel reload and clear weapon HUD numbers.</summary>
	public void HostOnSelectedNonWeapon()
	{
		EnsureWeaponServicesBound();
		_weaponServices.Reload.HostOnSelectedNonWeapon();
	}

	internal bool TryResolveWeaponItemDefResilient( string itemId, out ThornsItemRegistry.ThornsItemDefinition itemDef ) =>
		TryResolveWeaponItemDefResilientImpl( itemId, out itemDef );

	internal void InvokeClientReceiveWeaponHudState( int loadedAmmo, int reserveAmmo, int weaponBrokenInt, int reloadingInt, int shotgunPumpReloadSessionInt ) =>
		ClientReceiveWeaponHudState( loadedAmmo, reserveAmmo, weaponBrokenInt, reloadingInt, shotgunPumpReloadSessionInt );

	internal void InvokeClientNotifyWeaponBroken() => ClientNotifyWeaponBroken();

	[Rpc.Host]
	internal void RequestReload()
	{
		EnsureWeaponServicesBound();
		_weaponServices.Reload.HandleRequestReload();
	}

	[Rpc.Host]
	internal void RequestFire( Vector3 directionWorld, int attackVariant, bool aimDownSights )
	{
		EnsureWeaponServicesBound();
		_weaponServices.Combat.HandleRequestFire( directionWorld, attackVariant, aimDownSights );
	}

	[Rpc.Owner]
	internal void RpcPlayOwnerWeaponSound( string resourcePath ) => _weaponServices.ObserverSync.RpcPlayOwnerWeaponSound( resourcePath );

	[Rpc.Broadcast]
	internal void RpcObserversPlayerGunWorldShot( Guid shooterOwnerConnectionId, string resourcePath ) =>
		_weaponServices.ObserverSync.RpcObserversPlayerGunWorldShot( shooterOwnerConnectionId, resourcePath );

	[Rpc.Owner]
	void ClientReceiveWeaponHudState( int loadedAmmo, int reserveAmmo, int weaponBrokenInt, int reloadingInt, int shotgunPumpReloadSessionInt ) =>
		_weaponServices.Ammo.ClientReceiveWeaponHudState( loadedAmmo, reserveAmmo, weaponBrokenInt, reloadingInt, shotgunPumpReloadSessionInt );

	[Rpc.Owner]
	void RpcFireOutcome(
		bool ammunitionExpended,
		bool damageAppliedToTarget,
		float damageDealt,
		bool hitMarkerHighlight,
		int fpAttackPresentationKind,
		int clientKickPitchMilliDegrees,
		int clientKickYawMilliDegrees,
		bool feedbackHasEndpoint,
		int feedbackHitXMm,
		int feedbackHitYMm,
		int feedbackHitZMm,
		int feedbackSurfaceKind,
		bool feedbackTargetKilled ) =>
		_weaponServices.ClientFx.RpcFireOutcome(
			ammunitionExpended,
			damageAppliedToTarget,
			damageDealt,
			hitMarkerHighlight,
			fpAttackPresentationKind,
			clientKickPitchMilliDegrees,
			clientKickYawMilliDegrees,
			feedbackHasEndpoint,
			feedbackHitXMm,
			feedbackHitYMm,
			feedbackHitZMm,
			feedbackSurfaceKind,
			feedbackTargetKilled );
}
