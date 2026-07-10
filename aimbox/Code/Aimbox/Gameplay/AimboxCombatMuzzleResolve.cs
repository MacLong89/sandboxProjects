namespace Sandbox;

/// <summary>World-space muzzle origins for combat tracers (FP viewmodel or eye fallback).</summary>
public static class AimboxCombatMuzzleResolve
{
	public const float HandMuzzleForwardInches = 34f;
	public const float HandMuzzleUpInches = 2f;
	public const float MinMuzzleSeparationFromCameraInches = 12f;

	static readonly string[] MuzzleAttachmentCandidates =
	{
		"muzzle",
		"Muzzle",
		"muzzle_attach",
		"muzzle_silenced",
		"attach_muzzle"
	};

	public static Vector3 ResolvePlayerTracerOrigin(
		AimboxPlayerController player,
		Vector3 aimDirection,
		AimboxWeaponId weaponId,
		SkinnedModelRenderer viewModelRenderer,
		GameObject viewModelRoot,
		CameraComponent camera,
		ModelRenderer suppressorRenderer = null )
	{
		if ( player is null || !player.GameObject.IsValid() )
			return default;

		if ( player.IsProxy
		     && TryResolveThirdPersonMuzzleWorld( player, weaponId, aimDirection, out var tpMuzzle ) )
			return tpMuzzle;

		if ( TryResolveViewmodelMuzzleWorld( viewModelRenderer, viewModelRoot, weaponId, camera, suppressorRenderer, out var fpMuzzle ) )
			return fpMuzzle;

		return ResolveEyeFallback( player, aimDirection );
	}

	public static bool TryResolveThirdPersonMuzzleWorld(
		IAimboxCombatActor actor,
		AimboxWeaponId weaponId,
		Vector3 aimDirection,
		out Vector3 muzzleWorld )
	{
		muzzleWorld = default;
		if ( actor is null || !actor.GameObject.IsValid() )
			return false;

		if ( !TryGetThirdPersonWeaponRenderer( actor.GameObject, out var weaponRenderer ) )
			return false;

		var combatId = CombatIdFromWeapon( weaponId );
		var forward = aimDirection.Normal;
		if ( forward.Length < 0.95f )
			forward = actor.AimForward.Normal;

		if ( TryResolveSkinnedMuzzleWorld(
			     weaponRenderer,
			     combatId,
			     forward,
			     null,
			     useViewmodelLocalOffsets: false,
			     out muzzleWorld ) )
			return true;

		return TryResolveThirdPersonHandMuzzle( actor, forward, out muzzleWorld );
	}

	public static Vector3 ResolveThirdPersonTracerFallback( IAimboxCombatActor actor, Vector3 aimDirection )
	{
		if ( TryResolveThirdPersonHandMuzzle( actor, aimDirection, out var handMuzzle ) )
			return handMuzzle;

		var forward = aimDirection.Normal;
		if ( forward.Length < 0.95f )
			forward = actor.AimForward.Normal;

		var chestHeight = actor.IsCrouching ? 40f : 48f;
		return actor.WorldPosition + Vector3.Up * chestHeight + forward * HandMuzzleForwardInches;
	}

	static bool TryGetThirdPersonWeaponRenderer( GameObject pawnRoot, out SkinnedModelRenderer weaponRenderer )
	{
		weaponRenderer = default;
		if ( !pawnRoot.IsValid() )
			return false;

		var weaponGo = AimboxCitizenPresentation.FindDescendantNamed( pawnRoot, AimboxCitizenPresentation.WorldWeaponChildName );
		if ( !weaponGo.IsValid() )
			return false;

		weaponRenderer = weaponGo.Components.Get<SkinnedModelRenderer>();
		return weaponRenderer.IsValid() && weaponRenderer.Enabled && weaponRenderer.Model.IsValid() && !weaponRenderer.Model.IsError;
	}

	static bool TryResolveThirdPersonHandMuzzle( IAimboxCombatActor actor, Vector3 aimDirection, out Vector3 muzzleWorld )
	{
		muzzleWorld = default;
		if ( actor is null || !actor.GameObject.IsValid() )
			return false;

		if ( !AimboxCitizenPresentation.TryGetCitizenBodySkin( actor.GameObject, out var bodySkin ) )
			return false;

		bodySkin.CreateBoneObjects = true;
		foreach ( var boneName in ThirdPersonHandBoneCandidates )
		{
			if ( !TryGetBoneWorldPosition( bodySkin, boneName, out var handWorld ) )
				continue;

			muzzleWorld = handWorld + aimDirection.Normal * (HandMuzzleForwardInches * 0.85f);
			return true;
		}

		return false;
	}

	static readonly string[] ThirdPersonHandBoneCandidates =
	[
		"hand_R",
		"Hold_R",
		"wrist_R",
		"weapon_hand_R",
		"hand_R_IK_target"
	];

	static bool TryGetBoneWorldPosition( SkinnedModelRenderer skin, string boneName, out Vector3 worldPosition )
	{
		worldPosition = default;
		if ( !skin.IsValid() || string.IsNullOrWhiteSpace( boneName ) )
			return false;

		if ( skin.TryGetBoneTransform( boneName, out var boneTx ) )
		{
			worldPosition = boneTx.Position;
			return true;
		}

		if ( skin.Model.IsValid() && skin.Model.Bones.HasBone( boneName ) )
		{
			var boneObject = skin.GetBoneObject( skin.Model.Bones.GetBone( boneName ) );
			if ( boneObject.IsValid() )
			{
				worldPosition = boneObject.WorldPosition;
				return true;
			}
		}

		var namedBone = skin.GetBoneObject( boneName );
		if ( !namedBone.IsValid() )
			return false;

		worldPosition = namedBone.WorldPosition;
		return true;
	}

	public static string CombatIdFromWeapon( AimboxWeaponId weaponId ) =>
		weaponId switch
		{
			AimboxWeaponId.M4A1 => "m4",
			AimboxWeaponId.Mp5 => "mp5",
			AimboxWeaponId.SpaghelliM4 => "shotgun",
			AimboxWeaponId.M700 => "sniper",
			AimboxWeaponId.M9Bayonet => "m9_bayonet",
			_ => ""
		};

	public static bool TryResolveViewmodelMuzzleWorld(
		SkinnedModelRenderer smr,
		GameObject viewModelRoot,
		AimboxWeaponId weaponId,
		CameraComponent camera,
		ModelRenderer suppressorRenderer,
		out Vector3 muzzleWorld )
	{
		muzzleWorld = default;
		if ( suppressorRenderer.IsValid()
		     && TryResolveSkinnedMuzzleWorld(
			     suppressorRenderer,
			     CombatIdFromWeapon( weaponId ),
			     viewModelRoot.IsValid() ? viewModelRoot.WorldRotation.Forward : suppressorRenderer.GameObject.WorldRotation.Forward,
			     camera.IsValid() ? camera.GameObject : null,
			     useViewmodelLocalOffsets: false,
			     out muzzleWorld ) )
			return true;

		if ( smr is null || !smr.IsValid() || !smr.Enabled || !smr.Model.IsValid() || smr.Model.IsError )
			return false;

		var combatId = CombatIdFromWeapon( weaponId );
		var proximityReference = camera.IsValid() ? camera.GameObject : null;
		return TryResolveSkinnedMuzzleWorld(
			smr,
			combatId,
			viewModelRoot.IsValid() ? viewModelRoot.WorldRotation.Forward : smr.GameObject.WorldRotation.Forward,
			proximityReference,
			useViewmodelLocalOffsets: true,
			out muzzleWorld );
	}

	public static bool TryResolveSkinnedMuzzleWorld(
		ModelRenderer smr,
		string combatWeaponDefinitionId,
		Vector3 preferredForward,
		GameObject proximityReference,
		bool useViewmodelLocalOffsets,
		out Vector3 muzzleWorld )
	{
		muzzleWorld = default;
		if ( !smr.IsValid() || !smr.Enabled || !smr.Model.IsValid() || smr.Model.IsError )
			return false;

		foreach ( var attachmentName in MuzzleAttachmentCandidates )
		{
			if ( smr is SkinnedModelRenderer skinned )
			{
				var attachment = skinned.GetAttachment( attachmentName, worldSpace: true );
				if ( attachment.HasValue && AcceptMuzzleCandidate( attachment.Value.Position, proximityReference, out muzzleWorld ) )
					return true;

				if ( skinned.TryGetBoneTransform( attachmentName, out var boneTx )
				     && AcceptMuzzleCandidate( boneTx.Position, proximityReference, out muzzleWorld ) )
					return true;
			}

			if ( smr is SkinnedModelRenderer skinnedBone )
			{
				var boneObj = skinnedBone.GetBoneObject( attachmentName );
				if ( boneObj.IsValid()
				     && AcceptMuzzleCandidate( boneObj.WorldPosition, proximityReference, out muzzleWorld ) )
					return true;
			}
			else if ( smr.Model.IsValid() && smr.Model.Bones.HasBone( attachmentName ) )
			{
				var boneObj = smr.GetBoneObject( smr.Model.Bones.GetBone( attachmentName ) );
				if ( boneObj.IsValid()
				     && AcceptMuzzleCandidate( boneObj.WorldPosition, proximityReference, out muzzleWorld ) )
					return true;
			}
		}

		if ( smr.SceneObject is SceneModel sceneModel )
		{
			foreach ( var attachmentName in MuzzleAttachmentCandidates )
			{
				var attachment = sceneModel.GetAttachment( attachmentName, worldspace: true );
				if ( attachment.HasValue && AcceptMuzzleCandidate( attachment.Value.Position, proximityReference, out muzzleWorld ) )
					return true;
			}
		}

		if ( useViewmodelLocalOffsets
		     && TryResolveViewmodelLocalMuzzle( smr.GameObject, combatWeaponDefinitionId, proximityReference, out muzzleWorld ) )
			return true;

		var forward = preferredForward.Normal;
		if ( forward.Length < 0.95f )
			forward = smr.GameObject.WorldRotation.Forward.Normal;

		var barrelLength = ResolveBarrelForwardInches( combatWeaponDefinitionId );
		var barrelCandidate = smr.GameObject.WorldPosition + forward * barrelLength;
		return AcceptMuzzleCandidate( barrelCandidate, proximityReference, out muzzleWorld );
	}

	static bool TryResolveViewmodelLocalMuzzle(
		GameObject viewmodelRoot,
		string combatWeaponDefinitionId,
		GameObject proximityReference,
		out Vector3 muzzleWorld )
	{
		muzzleWorld = default;
		if ( !viewmodelRoot.IsValid() )
			return false;

		if ( !TryResolveViewmodelLocalMuzzleOffset( combatWeaponDefinitionId, out var localOffset ) )
			return false;

		var candidate = viewmodelRoot.WorldTransform.PointToWorld( localOffset );
		return AcceptMuzzleCandidate( candidate, proximityReference, out muzzleWorld );
	}

	static bool TryResolveViewmodelLocalMuzzleOffset( string combatWeaponDefinitionId, out Vector3 localOffset )
	{
		localOffset = default;
		var id = combatWeaponDefinitionId?.Trim() ?? "";
		if ( string.IsNullOrWhiteSpace( id ) )
			return false;

		localOffset = id switch
		{
			"bow" => new Vector3( 2.8f, 0.1f, 0.04f ),
			"m4" => new Vector3( 3.4f, 0.14f, 0.06f ),
			"mp5" => new Vector3( 2.4f, 0.11f, 0.05f ),
			"shotgun" => new Vector3( 2.2f, 0.12f, 0.04f ),
			"sniper" => new Vector3( 4.6f, 0.1f, 0.05f ),
			"m9_bayonet" => new Vector3( 1.2f, 0.08f, 0.02f ),
			_ => default
		};

		return localOffset != default;
	}

	static Vector3 ResolveEyeFallback( AimboxPlayerController player, Vector3 aimDirection )
	{
		var eye = player.EyePosition;
		var dir = aimDirection.Normal;
		if ( dir.Length < 0.95f )
			dir = player.AimForward;

		return eye + dir * 28f + player.EyeRotation.Right * 6f + Vector3.Down * 4f;
	}

	static bool AcceptMuzzleCandidate( Vector3 candidate, GameObject proximityReference, out Vector3 accepted )
	{
		accepted = candidate;
		if ( !proximityReference.IsValid() )
			return true;

		return Vector3.DistanceBetween( candidate, proximityReference.WorldPosition ) >= MinMuzzleSeparationFromCameraInches;
	}

	static float ResolveBarrelForwardInches( string combatWeaponDefinitionId )
	{
		var id = combatWeaponDefinitionId?.Trim() ?? "";
		return id switch
		{
			"bow" => 24f,
			"shotgun" => 22f,
			"mp5" => 20f,
			"sniper" => 40f,
			"m9_bayonet" => 12f,
			_ => 28f
		};
	}
}
