namespace Terraingen.Combat;

using Terraingen.AI;
using Terraingen.GameData;
using Terraingen.Player;

/// <summary>World-space muzzle origins for combat tracers (FP viewmodel, TP weapon mesh, or hand fallback).</summary>
public static class ThornsCombatMuzzleResolve
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

	public static Vector3 ResolveTracerOrigin(
		GameObject attackerRoot,
		Vector3 aimDirection,
		ThornsCombatTracerSource source,
		string combatWeaponDefinitionId = "",
		bool preferFirstPersonViewmodel = false )
	{
		if ( !attackerRoot.IsValid() )
			return default;

		return source switch
		{
			ThornsCombatTracerSource.Npc => ResolveNpcTracerOrigin( attackerRoot, aimDirection, combatWeaponDefinitionId ),
			_ => ResolvePlayerTracerOrigin( attackerRoot, aimDirection, combatWeaponDefinitionId, preferFirstPersonViewmodel )
		};
	}

	public static Vector3 ResolvePlayerTracerOrigin(
		GameObject pawnRoot,
		Vector3 aimDirection,
		string combatWeaponDefinitionId,
		bool preferFirstPersonViewmodel )
	{
		if ( !pawnRoot.IsValid() )
			return default;

		var combatId = combatWeaponDefinitionId?.Trim() ?? "";
		if ( ThornsWeaponDefinitions.IsBowWeapon( ThornsWeaponDefinitions.Get( combatId ), combatId ) )
		{
			if ( preferFirstPersonViewmodel
			     && ThornsViewModelController.TryResolveOwnerBowMuzzleWorld( pawnRoot, out var bowFpMuzzle ) )
				return bowFpMuzzle;

			if ( TryResolveBowPresentationMuzzle( pawnRoot, aimDirection, out var bowMuzzle ) )
				return bowMuzzle;
		}

		if ( preferFirstPersonViewmodel
		     && ThornsViewModelController.TryResolveOwnerMuzzleWorld( pawnRoot, combatWeaponDefinitionId, out var fpMuzzle ) )
			return fpMuzzle;

		if ( TryResolveWorldWeaponMuzzle( pawnRoot, aimDirection, combatWeaponDefinitionId, out var worldMuzzle ) )
			return worldMuzzle;

		if ( TryResolveCitizenHandMuzzle( pawnRoot, aimDirection, out var handMuzzle ) )
			return handMuzzle;

		return ResolveViewAimFallback( pawnRoot, aimDirection );
	}

	public static Vector3 ResolveNpcTracerOrigin(
		GameObject banditRoot,
		Vector3 aimDirection,
		string combatWeaponDefinitionId )
	{
		if ( !banditRoot.IsValid() )
			return default;

		if ( TryResolveWorldWeaponMuzzle( banditRoot, aimDirection, combatWeaponDefinitionId, out var weaponMuzzle ) )
			return weaponMuzzle;

		if ( TryResolveCitizenHandMuzzle( banditRoot, aimDirection, out var handMuzzle ) )
			return handMuzzle;

		return ResolveViewAimFallback( banditRoot, aimDirection );
	}

	public static string InferCombatIdFromViewModelPath( string modelPath )
	{
		if ( string.IsNullOrWhiteSpace( modelPath ) )
			return "";

		var path = modelPath.Trim().Replace( '\\', '/' );
		if ( path.Contains( "m4a1", StringComparison.OrdinalIgnoreCase )
		     || path.Contains( "assault_m4", StringComparison.OrdinalIgnoreCase ) )
			return "m4";

		if ( path.Contains( "mp5", StringComparison.OrdinalIgnoreCase ) )
			return "mp5";

		if ( path.Contains( "spaghellim4", StringComparison.OrdinalIgnoreCase )
		     || path.Contains( "shotgun", StringComparison.OrdinalIgnoreCase ) )
			return "shotgun";

		if ( path.Contains( "m700", StringComparison.OrdinalIgnoreCase )
		     || path.Contains( "sniper", StringComparison.OrdinalIgnoreCase ) )
			return "sniper";

		if ( path.Contains( "m9bayonet", StringComparison.OrdinalIgnoreCase )
		     || path.Contains( "bayonet", StringComparison.OrdinalIgnoreCase ) )
			return "m9_bayonet";

		if ( path.Contains( "bow", StringComparison.OrdinalIgnoreCase ) )
			return "bow";

		return "";
	}

	static bool TryResolveBowPresentationMuzzle( GameObject pawnRoot, Vector3 aimDirection, out Vector3 muzzleWorld )
	{
		muzzleWorld = default;
		var view = ThornsPlayerFirstPersonRig.ResolvePresentationCameraObject( pawnRoot );
		if ( !view.IsValid() )
			return false;

		var gripWorld = view.WorldTransform.PointToWorld( ThornsFpItemHelpers.FpBowViewmodelRootOffset );
		var dir = aimDirection.Normal;
		if ( dir.Length < 0.95f )
			dir = view.WorldRotation.Forward.Normal;

		muzzleWorld = gripWorld + dir * 14f;
		return true;
	}

	public static bool TryResolveSkinnedMuzzleWorld(
		SkinnedModelRenderer smr,
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
			var attachment = smr.GetAttachment( attachmentName, worldSpace: true );
			if ( attachment.HasValue && AcceptMuzzleCandidate( attachment.Value.Position, proximityReference, out muzzleWorld ) )
				return true;

			if ( smr.TryGetBoneTransform( attachmentName, out var boneTx )
			     && AcceptMuzzleCandidate( boneTx.Position, proximityReference, out muzzleWorld ) )
				return true;

			var boneObj = smr.GetBoneObject( attachmentName );
			if ( boneObj.IsValid()
			     && AcceptMuzzleCandidate( boneObj.WorldPosition, proximityReference, out muzzleWorld ) )
				return true;
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
		if ( AcceptMuzzleCandidate( barrelCandidate, proximityReference, out muzzleWorld ) )
			return true;

		return false;
	}

	static bool TryResolveWorldWeaponMuzzle(
		GameObject pawnRoot,
		Vector3 aimDirection,
		string combatWeaponDefinitionId,
		out Vector3 muzzleWorld )
	{
		muzzleWorld = default;
		if ( !pawnRoot.IsValid() )
			return false;

		var weaponGo = ThornsBanditUtil.FindDescendantNamed( pawnRoot, ThornsBanditUtil.WorldWeaponChildName );
		if ( !weaponGo.IsValid() )
			return false;

		var smr = weaponGo.Components.Get<SkinnedModelRenderer>( FindMode.EverythingInSelf );
		if ( !smr.IsValid() )
			return false;

		var forward = aimDirection.Normal;
		if ( forward.Length < 0.95f )
			forward = weaponGo.WorldRotation.Forward.Normal;

		return TryResolveSkinnedMuzzleWorld(
			smr,
			combatWeaponDefinitionId,
			forward,
			proximityReference: null,
			useViewmodelLocalOffsets: false,
			out muzzleWorld );
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

		// Viewmodel-local units before the ~10x FP mesh scale (+X = barrel forward on sbox weapons).
		localOffset = id switch
		{
			"m4" => new Vector3( 3.4f, 0.14f, 0.06f ),
			"mp5" => new Vector3( 2.4f, 0.11f, 0.05f ),
			"shotgun" => new Vector3( 2.2f, 0.12f, 0.04f ),
			"sniper" => new Vector3( 4.6f, 0.1f, 0.05f ),
			"m9_bayonet" => new Vector3( 1.2f, 0.08f, 0.02f ),
			"bow" => ThornsFpItemHelpers.FpBowTracerLocalOffset,
			_ => default
		};

		return localOffset != default;
	}

	static bool TryResolveCitizenHandMuzzle( GameObject pawnRoot, Vector3 aimDirection, out Vector3 muzzleWorld )
	{
		muzzleWorld = default;
		if ( !pawnRoot.IsValid() )
			return false;

		var body = ThornsBanditUtil.FindDescendantNamed( pawnRoot, "Body" );
		if ( !body.IsValid() )
			return false;

		var skin = body.Components.Get<SkinnedModelRenderer>();
		if ( !skin.IsValid() )
			return false;

		foreach ( var boneName in ThornsBanditUtil.CitizenTpWeaponRightHandBoneCandidates )
		{
			if ( !skin.TryGetBoneTransform( boneName, out var handTx ) )
				continue;

			var dir = aimDirection.Normal;
			if ( dir.Length < 0.95f )
				dir = handTx.Rotation.Forward.Normal;

			muzzleWorld = handTx.Position + dir * HandMuzzleForwardInches + Vector3.Up * HandMuzzleUpInches;
			return true;
		}

		return false;
	}

	static Vector3 ResolveViewAimFallback( GameObject pawnRoot, Vector3 aimDirection )
	{
		var view = ThornsPlayerFirstPersonRig.ResolvePresentationCameraObject( pawnRoot );
		if ( view.IsValid() )
		{
			var dir = aimDirection.Normal;
			if ( dir.Length < 0.95f )
				dir = view.WorldRotation.Forward.Normal;

			return view.WorldPosition + dir * 28f + view.WorldRotation.Right * 6f + Vector3.Down * 4f;
		}

		if ( Terraingen.Player.ThornsLocalPlayer.TryGetAuthoritativeEye( pawnRoot, out var eye, out var eyeRot ) )
		{
			var dir = aimDirection.Normal;
			if ( dir.Length < 0.95f )
				dir = eyeRot.Forward.Normal;

			return eye + dir * 28f + eyeRot.Right * 6f + Vector3.Down * 4f;
		}

		return pawnRoot.WorldPosition + Vector3.Up * 48f;
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
			"shotgun" => 22f,
			"mp5" => 20f,
			"sniper" => 40f,
			"m9_bayonet" => 12f,
			_ => 28f
		};
	}
}
