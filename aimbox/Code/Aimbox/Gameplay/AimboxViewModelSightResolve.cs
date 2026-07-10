namespace Sandbox;

/// <summary>
/// Resolves where the player's eye should sit relative to the animgraph camera bone during ADS.
/// sboxweapons ironsights anim only align iron sights — optics/scopes need a delta in code (official FP doc).
/// </summary>
public static class AimboxViewModelSightResolve
{
	static readonly string[] SniperScopeAnchorCandidates =
	[
		"scope",
		"scope_eye",
		"optic",
		"sight",
		"lens",
		"sniper",
		"telescopic"
	];

	public static bool TryGetOpticAnchorSkinLocal(
		AimboxAdsSightMode mode,
		SkinnedModelRenderer skin,
		AimboxViewModelAttachmentMount mount,
		out Vector3 skinLocal )
	{
		skinLocal = default;
		if ( !skin.IsValid() )
			return false;

		return mode switch
		{
			AimboxAdsSightMode.RedDot => TryGetRedDotAnchorSkinLocal( skin, mount, out skinLocal ),
			AimboxAdsSightMode.SniperScope => TryGetSniperScopeEyeSkinLocal( skin, mount, out skinLocal ),
			_ => false
		};
	}

	/// <summary>Lens plane in skin space — used to center the physical optic mesh and PiP on screen.</summary>
	public static bool TryGetOpticLensSkinLocal(
		AimboxAdsSightMode mode,
		SkinnedModelRenderer skin,
		AimboxViewModelAttachmentMount mount,
		out Vector3 skinLocal )
	{
		skinLocal = default;
		if ( !skin.IsValid() )
			return false;

		return mode switch
		{
			AimboxAdsSightMode.RedDot => TryGetRedDotLensSkinLocal( skin, mount, out skinLocal ),
			AimboxAdsSightMode.SniperScope => TryGetSniperScopeLensSkinLocal( skin, mount, out skinLocal ),
			_ => false
		};
	}

	static bool TryGetRedDotAnchorSkinLocal(
		SkinnedModelRenderer skin,
		AimboxViewModelAttachmentMount mount,
		out Vector3 skinLocal ) =>
		TryGetRedDotLensSkinLocal( skin, mount, out skinLocal );

	static bool TryGetRedDotLensSkinLocal(
		SkinnedModelRenderer skin,
		AimboxViewModelAttachmentMount mount,
		out Vector3 skinLocal )
	{
		skinLocal = default;

		if ( mount is not null && mount.TryGetEquippedRedDotStyleAttachment( out var equippedSight )
		     && mount.TryGetAttachmentOriginSkinLocal( equippedSight, skin, out skinLocal ) )
			return true;

		if ( AimboxViewModelAttachmentTuner.Instance is { OverrideGameplayMounts: true } tuner )
		{
			foreach ( var attachment in AimboxAttachmentCatalog.RedDotStyleAttachments )
			{
				if ( tuner.TryGetTunedAttachmentOriginSkinLocal( attachment, skin, out skinLocal ) )
					return true;
			}
		}

		return TryGetCatalogRedDotAnchorSkinLocal( skin, out skinLocal );
	}

	static bool TryGetCatalogRedDotAnchorSkinLocal( SkinnedModelRenderer skin, out Vector3 skinLocal )
	{
		skinLocal = default;
		if ( !AimboxSboxAttachmentCatalog.TryGetVisual( AimboxWeaponId.M4A1, AimboxAttachmentId.RaisedRedDot, out var visual ) )
			return false;

		return TryGetWeaponRootOffsetSkinLocal(
			skin,
			visual.FallbackMountBone,
			visual.FallbackLocalPosition,
			out skinLocal );
	}

	static bool TryGetSniperScopeEyeSkinLocal( SkinnedModelRenderer skin, AimboxViewModelAttachmentMount mount, out Vector3 skinLocal )
	{
		skinLocal = default;

		if ( mount is not null
		     && mount.HasSpawnedAttachmentMesh( AimboxAttachmentId.RangedSight )
		     && mount.TryGetRangedSightLensSkinLocal( skin, out skinLocal ) )
			return true;

		if ( mount is not null
		     && mount.HasAttachment( AimboxAttachmentId.RangedSight )
		     && !mount.HasSpawnedAttachmentMesh( AimboxAttachmentId.RangedSight )
		     && TryGetCameraBoneSkinLocal( skin, out skinLocal, out _ ) )
			return true;

		if ( TryGetModelAnchorSkinLocal( skin, SniperScopeAnchorCandidates, out skinLocal ) )
			return true;

		if ( AimboxViewModelAttachmentTuner.Instance is { UseTunedM700ScopeEye: true } tuner
		     && tuner.TryGetTunedM700ScopeEyeSkinLocal( skin, out skinLocal ) )
			return true;

		if ( TryGetCameraBoneSkinLocal( skin, out skinLocal, out _ ) )
			return true;

		return TryGetWeaponRootOffsetSkinLocal(
			skin,
			"weapon_root",
			AimboxAdsSightTuning.M700ScopeEyeWeaponRootOffset,
			out skinLocal );
	}

	static bool TryGetSniperScopeLensSkinLocal( SkinnedModelRenderer skin, AimboxViewModelAttachmentMount mount, out Vector3 skinLocal )
	{
		skinLocal = default;

		if ( mount is not null
		     && mount.HasSpawnedAttachmentMesh( AimboxAttachmentId.RangedSight )
		     && mount.TryGetRangedSightLensSkinLocal( skin, out skinLocal ) )
			return true;

		if ( mount is not null
		     && mount.HasAttachment( AimboxAttachmentId.RangedSight )
		     && !mount.HasSpawnedAttachmentMesh( AimboxAttachmentId.RangedSight )
		     && TryGetCameraBoneSkinLocal( skin, out skinLocal, out _ ) )
			return true;

		if ( TryGetCameraBoneSkinLocal( skin, out skinLocal, out _ ) )
			return true;

		if ( !TryGetSniperScopeEyeSkinLocal( skin, mount, out var eyeSkinLocal ) )
			return false;

		var eyeWorld = skin.WorldTransform.PointToWorld( eyeSkinLocal );
		var lensWorld = eyeWorld + ResolveSniperScopeLensForward( skin, eyeSkinLocal, eyeWorld )
		                            * AimboxAdsSightTuning.M700ScopeLensForwardOffset;
		skinLocal = skin.WorldTransform.PointToLocal( lensWorld );
		return true;
	}

	static Vector3 ResolveSniperScopeLensForward( SkinnedModelRenderer skin, Vector3 eyeSkinLocal, Vector3 eyeWorld )
	{
		if ( TryGetCameraBoneSkinLocal( skin, out var boneSkinLocal, out _ ) )
		{
			var boneWorld = skin.WorldTransform.PointToWorld( boneSkinLocal );
			var viewAxis = (eyeWorld - boneWorld).Normal;
			if ( viewAxis.Length > 0.01f )
				return viewAxis;
		}

		if ( AimboxViewModelAttachmentMount.TryResolveMountBone( skin, "weapon_root", out var weaponRoot )
		     && weaponRoot.WorldRotation.Forward.Length > 0.01f )
			return weaponRoot.WorldRotation.Forward;

		return Vector3.Forward;
	}

	static bool TryGetCameraBoneSkinLocal( SkinnedModelRenderer skin, out Vector3 skinLocal, out Rotation skinLocalRotation )
	{
		skinLocal = default;
		skinLocalRotation = Rotation.Identity;

		if ( !skin.IsValid() )
			return false;

		var boneObject = skin.GetBoneObject( "camera" );
		if ( !boneObject.IsValid() && skin.Model.IsValid() && skin.Model.Bones.HasBone( "camera" ) )
			boneObject = skin.GetBoneObject( skin.Model.Bones.GetBone( "camera" ) );

		if ( !boneObject.IsValid() )
			return false;

		skinLocal = skin.WorldTransform.PointToLocal( boneObject.WorldPosition );
		skinLocalRotation = boneObject.LocalRotation;
		return true;
	}

	public static bool TryGetWeaponRootOffsetSkinLocal(
		SkinnedModelRenderer skin,
		string mountBone,
		Vector3 mountLocalOffset,
		out Vector3 skinLocal )
	{
		skinLocal = default;
		if ( !AimboxViewModelAttachmentMount.TryResolveMountBone( skin, mountBone, out var parent ) )
			return false;

		var world = parent.WorldTransform.PointToWorld( mountLocalOffset );
		skinLocal = skin.WorldTransform.PointToLocal( world );
		return true;
	}

	static bool TryGetModelAnchorSkinLocal( SkinnedModelRenderer skin, string[] keywords, out Vector3 skinLocal )
	{
		skinLocal = default;
		if ( !skin.Model.IsValid() || skin.Model.IsError )
			return false;

		foreach ( var attachment in skin.Model.Attachments.All )
		{
			if ( !NameMatchesKeywords( attachment.Name, keywords ) )
				continue;

			if ( TryGetAttachmentSkinLocal( skin, attachment.Name, out skinLocal ) )
				return true;
		}

		foreach ( var bone in skin.Model.Bones.AllBones )
		{
			if ( !NameMatchesKeywords( bone.Name, keywords ) )
				continue;

			var boneObject = skin.GetBoneObject( bone );
			if ( !boneObject.IsValid() )
				continue;

			skinLocal = skin.WorldTransform.PointToLocal( boneObject.WorldPosition );
			return true;
		}

		if ( skin.Model.Attachments.All.Count() == 2 )
		{
			Vector3? highest = null;
			foreach ( var attachment in skin.Model.Attachments.All )
			{
				if ( string.Equals( attachment.Name, "muzzle", StringComparison.OrdinalIgnoreCase ) )
					continue;

				if ( !TryGetAttachmentSkinLocal( skin, attachment.Name, out var candidate ) )
					continue;

				if ( highest is null || candidate.z > highest.Value.z )
					highest = candidate;
			}

			if ( highest.HasValue )
			{
				skinLocal = highest.Value;
				return true;
			}
		}

		return false;
	}

	static bool TryGetAttachmentSkinLocal( SkinnedModelRenderer skin, string attachmentName, out Vector3 skinLocal )
	{
		skinLocal = default;

		var localAttachment = skin.GetAttachment( attachmentName, worldSpace: false );
		if ( localAttachment.HasValue )
		{
			skinLocal = localAttachment.Value.Position;
			return true;
		}

		var worldAttachment = skin.GetAttachment( attachmentName, worldSpace: true );
		if ( !worldAttachment.HasValue )
			return false;

		skinLocal = skin.WorldTransform.PointToLocal( worldAttachment.Value.Position );
		return true;
	}

	static bool NameMatchesKeywords( string name, string[] keywords )
	{
		if ( string.IsNullOrWhiteSpace( name ) || keywords is null || keywords.Length <= 0 )
			return false;

		foreach ( var keyword in keywords )
		{
			if ( name.Contains( keyword, StringComparison.OrdinalIgnoreCase ) )
				return true;
		}

		return false;
	}
}