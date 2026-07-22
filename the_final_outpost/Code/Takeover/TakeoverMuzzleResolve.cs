namespace FinalOutpost;

/// <summary>Aimbox <c>AimboxCombatMuzzleResolve</c> — FP muzzle for tracers.</summary>
public static class TakeoverMuzzleResolve
{
	public const float MinMuzzleSeparationFromCameraInches = 12f;

	static readonly string[] MuzzleAttachmentCandidates =
	{
		"muzzle",
		"Muzzle",
		"muzzle_attach",
		"muzzle_silenced",
		"attach_muzzle"
	};

	public static Vector3 Resolve(
		TakeoverPawn pawn,
		Vector3 aimDirection,
		SkinnedModelRenderer viewModelRenderer,
		GameObject viewModelRoot,
		CameraComponent camera )
	{
		if ( pawn is null ) return default;

		if ( TryResolveViewmodelMuzzleWorld(
			     viewModelRenderer,
			     viewModelRoot,
			     CombatIdFrom( pawn.Weapon?.Definition.RecruitType ?? RecruitWeaponType.AssaultRifle ),
			     camera,
			     out var fpMuzzle ) )
			return fpMuzzle;

		return ResolveEyeFallback( pawn, aimDirection );
	}

	static string CombatIdFrom( RecruitWeaponType type ) => type switch
	{
		RecruitWeaponType.AssaultRifle => "m4",
		RecruitWeaponType.Smg => "mp5",
		RecruitWeaponType.Shotgun => "shotgun",
		RecruitWeaponType.Sniper => "sniper",
		RecruitWeaponType.Pistol => "usp",
		_ => "m4"
	};

	static bool TryResolveViewmodelMuzzleWorld(
		SkinnedModelRenderer smr,
		GameObject viewModelRoot,
		string combatId,
		CameraComponent camera,
		out Vector3 muzzleWorld )
	{
		muzzleWorld = default;
		if ( smr is null || !smr.IsValid() || !smr.Enabled || !smr.Model.IsValid() || smr.Model.IsError )
			return false;

		var proximity = camera.IsValid() ? camera.GameObject : null;
		var preferredForward = viewModelRoot.IsValid()
			? viewModelRoot.WorldRotation.Forward
			: smr.GameObject.WorldRotation.Forward;

		foreach ( var attachmentName in MuzzleAttachmentCandidates )
		{
			var attachment = smr.GetAttachment( attachmentName, worldSpace: true );
			if ( attachment.HasValue && AcceptMuzzleCandidate( attachment.Value.Position, proximity, out muzzleWorld ) )
				return true;

			if ( smr.TryGetBoneTransform( attachmentName, out var boneTx )
			     && AcceptMuzzleCandidate( boneTx.Position, proximity, out muzzleWorld ) )
				return true;

			var boneObj = smr.GetBoneObject( attachmentName );
			if ( boneObj.IsValid() && AcceptMuzzleCandidate( boneObj.WorldPosition, proximity, out muzzleWorld ) )
				return true;
		}

		if ( TryResolveViewmodelLocalMuzzle( viewModelRoot.IsValid() ? viewModelRoot : smr.GameObject, combatId, proximity, out muzzleWorld ) )
			return true;

		var forward = preferredForward.Normal;
		if ( forward.Length < 0.95f )
			forward = smr.GameObject.WorldRotation.Forward.Normal;

		var barrelLength = ResolveBarrelForwardInches( combatId );
		var barrelCandidate = smr.GameObject.WorldPosition + forward * barrelLength;
		return AcceptMuzzleCandidate( barrelCandidate, proximity, out muzzleWorld );
	}

	static bool TryResolveViewmodelLocalMuzzle(
		GameObject viewmodelRoot,
		string combatId,
		GameObject proximityReference,
		out Vector3 muzzleWorld )
	{
		muzzleWorld = default;
		if ( !viewmodelRoot.IsValid() ) return false;
		if ( !TryResolveViewmodelLocalMuzzleOffset( combatId, out var localOffset ) ) return false;
		var candidate = viewmodelRoot.WorldTransform.PointToWorld( localOffset );
		return AcceptMuzzleCandidate( candidate, proximityReference, out muzzleWorld );
	}

	static bool TryResolveViewmodelLocalMuzzleOffset( string combatId, out Vector3 localOffset )
	{
		localOffset = combatId switch
		{
			"usp" => new Vector3( 1.8f, 0.1f, 0.04f ),
			"m4" => new Vector3( 3.4f, 0.14f, 0.06f ),
			"mp5" => new Vector3( 2.4f, 0.11f, 0.05f ),
			"shotgun" => new Vector3( 2.2f, 0.12f, 0.04f ),
			"sniper" => new Vector3( 4.6f, 0.1f, 0.05f ),
			_ => default
		};
		return localOffset != default;
	}

	static Vector3 ResolveEyeFallback( TakeoverPawn pawn, Vector3 aimDirection )
	{
		var dir = aimDirection.Normal;
		if ( dir.Length < 0.95f )
			dir = pawn.EyeRotation.Forward;
		return pawn.EyePosition + dir * 28f + pawn.EyeRotation.Right * 6f + Vector3.Down * 4f;
	}

	static bool AcceptMuzzleCandidate( Vector3 candidate, GameObject proximityReference, out Vector3 accepted )
	{
		accepted = candidate;
		if ( !proximityReference.IsValid() )
			return true;
		return Vector3.DistanceBetween( candidate, proximityReference.WorldPosition ) >= MinMuzzleSeparationFromCameraInches;
	}

	static float ResolveBarrelForwardInches( string combatId ) => combatId switch
	{
		"shotgun" => 22f,
		"mp5" => 20f,
		"sniper" => 40f,
		"usp" => 16f,
		_ => 28f
	};
}
