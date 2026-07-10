namespace Terraingen.Combat;

using Sandbox;
using Terraingen;
using Terraingen.Foliage;
using Terraingen.Minerals;
using Terraingen.Multiplayer;
using Terraingen.Player;

/// <summary>Empty-hands punch salvage targeting for trees and stone nodes.</summary>
public static class ThornsGatherSalvage
{
	[ConVar( "gather_salvage_debug" )]
	public static bool Debug { get; set; }

	public enum SalvageTargetKind
	{
		None,
		Tree,
		Stone
	}

	public static bool PlayerHasEmptyHands( GameObject playerRoot )
	{
		if ( !playerRoot.IsValid() )
			return false;

		if ( ThornsAxeTools.PlayerHasAxeEquipped( playerRoot )
		     || ThornsPickaxeTools.PlayerHasPickaxeEquipped( playerRoot )
		     || ThornsPlayerWeaponCombat.IsRangedWeaponEquipped( playerRoot )
		     || ThornsPlayerBowCombat.IsBowEquipped( playerRoot ) )
			return false;

		return true;
	}

	public static bool TryResolveTarget(
		GameObject playerRoot,
		Vector3 aimOrigin,
		Vector3 aimDirection,
		out SalvageTargetKind kind,
		out int targetId )
	{
		kind = SalvageTargetKind.None;
		targetId = 0;

		if ( !playerRoot.IsValid() || !PlayerHasEmptyHands( playerRoot ) )
			return false;

		var gather = ThornsGatheringRange.Inches;

		var hasTree = ThornsTreeHitUtil.TryPickTreeAlongRay(
			playerRoot.Scene,
			aimOrigin,
			aimDirection,
			gather,
			playerRoot,
			out var treeId,
			out var treeHit );

		var hasStone = TryPickSalvageStone(
			playerRoot.Scene,
			aimOrigin,
			aimDirection,
			gather,
			playerRoot,
			out var stoneId,
			out var stoneHit );

		if ( !hasTree && !hasStone )
			return false;

		if ( hasTree && !hasStone )
		{
			kind = SalvageTargetKind.Tree;
			targetId = treeId;
			return true;
		}

		if ( !hasTree && hasStone )
		{
			kind = SalvageTargetKind.Stone;
			targetId = stoneId;
			return true;
		}

		var treeAlong = ThornsGatherTargeting.DistanceAlongAim( aimOrigin, aimDirection, treeHit );
		var stoneAlong = ThornsGatherTargeting.DistanceAlongAim( aimOrigin, aimDirection, stoneHit );
		if ( stoneAlong <= treeAlong )
		{
			kind = SalvageTargetKind.Stone;
			targetId = stoneId;
		}
		else
		{
			kind = SalvageTargetKind.Tree;
			targetId = treeId;
		}

		return true;
	}

	static bool TryPickSalvageStone(
		Scene scene,
		Vector3 aimOrigin,
		Vector3 aimDirection,
		float gatherRange,
		GameObject playerRoot,
		out int nodeId,
		out Vector3 hitPosition )
	{
		nodeId = 0;
		hitPosition = default;

		if ( !ThornsMineralHitUtil.TryPickNodeAlongRay(
			     scene,
			     aimOrigin,
			     aimDirection,
			     gatherRange,
			     playerRoot,
			     out nodeId,
			     out hitPosition ) )
			return false;

		var minerals = ThornsMineralWorldService.ResolveInstance();
		if ( minerals is null || !minerals.IsValid() )
			return false;

		return minerals.TryGetLiveNodeKind( nodeId, out var mineralKind )
		       && mineralKind == MineralKind.Stone;
	}

	public static bool HasSalvageTargetInFront( GameObject playerRoot )
	{
		if ( !TryResolveAimRay( playerRoot, out var origin, out var forward ) )
			return false;

		return TryResolveTarget( playerRoot, origin, forward, out var kind, out _ )
		       && kind != SalvageTargetKind.None;
	}

	public static bool TryGetPromptVerb( GameObject playerRoot, out string verbPhrase )
	{
		verbPhrase = "";
		if ( !TryResolveAimRay( playerRoot, out var origin, out var forward ) )
			return false;

		if ( !TryResolveTarget( playerRoot, origin, forward, out var kind, out _ ) )
			return false;

		verbPhrase = kind switch
		{
			SalvageTargetKind.Tree => "Punch for Wood",
			SalvageTargetKind.Stone => "Punch for Stone",
			_ => ""
		};

		return !string.IsNullOrWhiteSpace( verbPhrase );
	}

	public static bool HostTrySalvage(
		GameObject playerRoot,
		Vector3 origin,
		Vector3 direction,
		out SalvageTargetKind kind )
	{
		kind = SalvageTargetKind.None;

		if ( !ThornsMultiplayer.IsHostOrOffline )
			return false;

		if ( !ThornsCombatFireValidation.TryResolveAuthoritativeShot(
			     playerRoot,
			     origin,
			     direction,
			     ThornsGatheringRange.Inches,
			     out origin,
			     out direction ) )
			return false;

		if ( !TryResolveTarget( playerRoot, origin, direction, out kind, out var targetId ) )
		{
			if ( Debug )
				Log.Info( "[Thorns Salvage] HostTrySalvage: no fist target resolved." );

			return false;
		}

		var salvaged = kind switch
		{
			SalvageTargetKind.Tree =>
				ThornsTreeWorldService.ResolveInstance()?.HostTrySalvageWood( playerRoot, targetId, origin, direction ) == true,
			SalvageTargetKind.Stone =>
				ThornsMineralWorldService.ResolveInstance()?.HostTrySalvageStone( playerRoot, targetId, origin, direction ) == true,
			_ => false
		};

		if ( !salvaged )
		{
			if ( Debug )
				Log.Info( $"[Thorns Salvage] HostTrySalvage: {kind} #{targetId} apply failed (emptyHands={PlayerHasEmptyHands( playerRoot )})." );

			kind = SalvageTargetKind.None;
			return false;
		}

		return true;
	}

	public static void NotifySalvageStrikeSfx( GameObject playerRoot, SalvageTargetKind kind ) =>
		ThornsGameplaySfx.PlaySalvageStrikeSfx( playerRoot, kind );

	public static bool TryResolveAimRay( GameObject root, out Vector3 origin, out Vector3 forward )
	{
		if ( ThornsSceneObserver.TryResolveLocalAimRay( root, out origin, out forward, useScreenCenter: true ) )
			return true;

		var controller = root.Components.Get<PlayerController>( FindMode.EverythingInSelf );
		if ( !controller.IsValid() )
			return false;

		origin = root.WorldPosition + Vector3.Up * 64f;
		forward = controller.EyeAngles.ToRotation().Forward.Normal;
		return forward.Length >= 0.95f;
	}
}
