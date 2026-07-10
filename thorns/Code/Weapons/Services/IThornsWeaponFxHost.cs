namespace Sandbox;

/// <summary>Owner-client FX surface for <see cref="ThornsWeaponClientFxService"/>.</summary>
public interface IThornsWeaponFxHost
{
	ThornsWeapon Weapon { get; }
	GameObject GameObject { get; }

	string OwnerMirrorCombatWeaponDefinitionId { get; set; }
	string ClientMirrorCombatDefinitionId { get; }

	string HitMarkerBodySound { get; }
	string HitMarkerHeadshotSound { get; }
	float HitMarkerBodyVolume { get; }
	float HitMarkerHeadshotVolume { get; }

	ThornsViewModelFpAnimator ResolveLocalFpAnimator();
	void PlayOwnerWeaponSoundAtEar( string resourcePath, float volumeMultiplier = 1f );
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
		bool feedbackTargetKilled );

	void HostMaybeBroadcastObserverGunshot( Guid shooterOwnerConnectionId, string resourcePath );
	bool HostTryResolveMirrorGunFireSoundPath( out string path );
}
