namespace Sandbox;

/// <summary>Combat authority surface for <see cref="ThornsWeaponHostCombatService"/>.</summary>
public interface IThornsWeaponCombatHost
{
	ThornsWeapon Weapon { get; }
	GameObject GameObject { get; }

	float AimDotMin { get; }

	double NextFireAllowedHostTime { get; set; }
	double NextMeleeHeavyAllowedHostTime { get; set; }
	double HostRecoilLastShotTime { get; set; }
	int HostRecoilPatternIndex { get; set; }
	int HostRecoilSprayOrdinal { get; set; }

	void HostApplyPrimaryMeleeCooldownSeconds( float seconds );
	bool IsReloadBlockingFire();

	bool HostTryResolveHitscanDamageTarget(
		Vector3 start,
		Vector3 dir,
		float range,
		float meleeMaxAbsVerticalSeparationFeet,
		out SceneTraceResult tr,
		out GameObject hitGo,
		out ThornsPawn victimPawn,
		out ThornsHealth victimHealth,
		out bool usedAnalyticFallback,
		out Vector3 analyticHitPosition );

	Vector3 HostFeedbackEndpointWorldTrace( Vector3 rayStart, Vector3 dirN, float range, out ThornsWeaponImpactSurfaceKind surfaceKind );
	bool IsOriginPlausible( Vector3 eyeWorld );

	void SendRpcFireOutcome(
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
		bool feedbackTargetKilled );

	void PushWeaponHudToOwnerHost();
	void ClientNotifyWeaponBroken();

	bool TryConsumeRangedShotAmmo(
		int hotbar,
		ThornsInventory inv,
		ref ThornsInventorySlot slot,
		ThornsWeaponDefinitions.WeaponDefinition def,
		double now,
		out bool brokenNow );
}
