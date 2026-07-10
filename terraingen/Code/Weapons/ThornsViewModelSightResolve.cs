namespace Sandbox;

using Terraingen.Combat.Attachments;

/// <summary>
/// Resolves where the player's eye should sit relative to the animgraph camera bone during ADS.
/// sboxweapons ironsights anim only align iron sights — optics/scopes need a delta in code (official FP doc).
/// </summary>
public static class ThornsViewModelSightResolve
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
		ThornsAdsSightMode mode,
		SkinnedModelRenderer skin,
		ThornsViewModelAttachmentMount mount,
		out Vector3 skinLocal )
	{
		skinLocal = default;
		if ( !skin.IsValid() )
			return false;

		return mode switch
		{
			ThornsAdsSightMode.RedDot => TryGetRedDotAnchorSkinLocal( skin, mount, out skinLocal ),
			ThornsAdsSightMode.SniperScope => TryGetSniperScopeEyeSkinLocal( skin, mount, out skinLocal ),
			_ => false
		};
	}

	public static bool TryGetOpticLensSkinLocal(
		ThornsAdsSightMode mode,
		SkinnedModelRenderer skin,
		ThornsViewModelAttachmentMount mount,
		out Vector3 skinLocal )
	{
		skinLocal = default;
		if ( !skin.IsValid() )
			return false;

		return mode switch
		{
			ThornsAdsSightMode.RedDot => TryGetRedDotLensSkinLocal( skin, mount, out skinLocal ),
			ThornsAdsSightMode.SniperScope => TryGetSniperScopeLensSkinLocal( skin, mount, out skinLocal ),
			_ => false
		};
	}

	static bool TryGetRedDotAnchorSkinLocal(
		SkinnedModelRenderer skin,
		ThornsViewModelAttachmentMount mount,
		out Vector3 skinLocal ) =>
		TryGetRedDotLensSkinLocal( skin, mount, out skinLocal );

	static bool TryGetRedDotLensSkinLocal(
		SkinnedModelRenderer skin,
		ThornsViewModelAttachmentMount mount,
		out Vector3 skinLocal )
	{
		skinLocal = default;

		if ( mount is not null && mount.TryGetEquippedRedDotStyleAttachment( out var equippedSight )
		     && mount.TryGetAttachmentOriginSkinLocal( equippedSight, skin, out skinLocal ) )
			return true;

		return TryGetCatalogRedDotAnchorSkinLocal( skin, out skinLocal );
	}

	static bool TryGetCatalogRedDotAnchorSkinLocal( SkinnedModelRenderer skin, out Vector3 skinLocal )
	{
		skinLocal = default;
		if ( !ThornsSboxAttachmentCatalog.TryGetVisual( "m4", ThornsAttachmentId.RaisedRedDot, out var visual ) )
			return false;

		return TryGetWeaponRootOffsetSkinLocal(
			skin,
			visual.FallbackMountBone,
			visual.FallbackLocalPosition,
			out skinLocal );
	}

	static bool TryGetSniperScopeEyeSkinLocal( SkinnedModelRenderer skin, ThornsViewModelAttachmentMount mount, out Vector3 skinLocal )
	{
		skinLocal = default;

		if ( mount is not null
		     && mount.HasSpawnedAttachmentMesh( ThornsAttachmentId.RangedSight )
		     && mount.TryGetRangedSightLensSkinLocal( skin, out skinLocal ) )
			return true;

		if ( mount is not null
		     && mount.HasAttachment( ThornsAttachmentId.RangedSight )
		     && !mount.HasSpawnedAttachmentMesh( ThornsAttachmentId.RangedSight )
		     && TryGetCameraBoneSkinLocal( skin, out skinLocal, out _ ) )
			return true;

		if ( TryGetModelAnchorSkinLocal( skin, SniperScopeAnchorCandidates, out skinLocal ) )
			return true;

		if ( TryGetCameraBoneSkinLocal( skin, out skinLocal, out _ ) )
			return true;

		return TryGetWeaponRootOffsetSkinLocal(
			skin,
			"weapon_root",
			ThornsAdsSightTuning.M700ScopeEyeWeaponRootOffset,
			out skinLocal );
	}

	static bool TryGetSniperScopeLensSkinLocal( SkinnedModelRenderer skin, ThornsViewModelAttachmentMount mount, out Vector3 skinLocal )
	{
		skinLocal = default;

		if ( mount is not null
		     && mount.HasSpawnedAttachmentMesh( ThornsAttachmentId.RangedSight )
		     && mount.TryGetRangedSightLensSkinLocal( skin, out skinLocal ) )
			return true;

		if ( mount is not null
		     && mount.HasAttachment( ThornsAttachmentId.RangedSight )
		     && !mount.HasSpawnedAttachmentMesh( ThornsAttachmentId.RangedSight )
		     && TryGetCameraBoneSkinLocal( skin, out skinLocal, out _ ) )
			return true;

		if ( TryGetCameraBoneSkinLocal( skin, out skinLocal, out _ ) )
			return true;

		if ( !TryGetSniperScopeEyeSkinLocal( skin, mount, out var eyeSkinLocal ) )
			return false;

		var eyeWorld = skin.WorldTransform.PointToWorld( eyeSkinLocal );
		var lensWorld = eyeWorld + ResolveSniperScopeLensForward( skin, eyeSkinLocal, eyeWorld )
		                            * ThornsAdsSightTuning.M700ScopeLensForwardOffset;
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

		if ( ThornsViewModelAttachmentMount.TryResolveMountBone( skin, "weapon_root", out var weaponRoot )
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
		if ( !ThornsViewModelAttachmentMount.TryResolveMountBone( skin, mountBone, out var parent ) )
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
