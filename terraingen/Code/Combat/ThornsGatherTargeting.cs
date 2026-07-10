namespace Terraingen.Combat;

using Terraingen.Foliage;
using Terraingen.Minerals;
using Terraingen.Player;

/// <summary>Shared melee gather targeting — crosshair ray vs tagged hitbox, then reach check.</summary>
public static class ThornsGatherTargeting
{
	public static SceneTraceResult TraceGatherTarget(
		Scene scene,
		Vector3 origin,
		Vector3 direction,
		float maxRange,
		string tag,
		GameObject ignoreRoot,
		float sweepRadius = 0f )
	{
		if ( scene is null || !scene.IsValid() || maxRange <= 0f || string.IsNullOrWhiteSpace( tag ) )
			return default;

		var dir = direction.Normal;
		if ( dir.Length < 0.95f )
			return default;

		var start = origin;
		var end = origin + dir * maxRange;

		// sweepRadius > 0 widens the aim window (fat spherecast) for a specific gather
		// type without touching the underlying collider. Callers that leave it at 0 keep
		// the original thin-ray behaviour (minerals, combat presentation, etc.).
		var trace = sweepRadius > 0f
			? scene.Trace.Sphere( sweepRadius, start, end )
			: scene.Trace.Ray( start, end );

		return trace
			.WithTag( tag )
			.IgnoreGameObjectHierarchy( ignoreRoot )
			.Run();
	}

	public static float DistanceAlongAim( Vector3 origin, Vector3 direction, Vector3 hit )
	{
		var dir = direction.Normal;
		if ( dir.Length < 0.95f || hit == default )
			return float.MaxValue;

		return Vector3.Dot( hit - origin, dir );
	}

	public static bool IsWithinGatherReach( Vector3 playerPos, Vector3 hitPos, float gatherRangeInches = 0f )
	{
		if ( hitPos == default )
			return false;

		return (hitPos - playerPos).Length <= ThornsGatheringRange.MeleeForgivenessInches( gatherRangeInches );
	}

	/// <summary>When <c>gather_target_debug 1</c> — draw pick volumes for axe/pick.</summary>
	public static void DrawActiveGatherDebug( GameObject playerRoot )
	{
		if ( !ThornsGatherTargetDebug.OverlayEnabled || !playerRoot.IsValid() )
			return;

		if ( !TryResolveAimRay( playerRoot, out var origin, out var forward ) )
			return;

		var gather = ThornsGatheringRange.Inches;
		var forgiveness = ThornsGatheringRange.MeleeForgivenessInches( gather );

		if ( ThornsAxeTools.PlayerHasAxeEquipped( playerRoot ) )
		{
			var trees = ThornsTreeWorldService.ResolveInstance();
			if ( trees is not null && trees.IsValid() )
			{
				ThornsTreeHitUtil.TryPickTreeAlongRay(
					playerRoot.Scene,
					origin,
					forward,
					gather,
					playerRoot,
					out var treeId,
					out _ );
				trees.DrawGatherDebug( playerRoot.WorldPosition, origin, forward, gather, forgiveness, treeId );
			}
		}

		if ( ThornsPickaxeTools.PlayerHasPickaxeEquipped( playerRoot ) )
		{
			var minerals = ThornsMineralWorldService.Instance;
			if ( minerals is not null && minerals.IsValid() )
			{
				ThornsMineralHitUtil.TryPickNodeAlongRay(
					playerRoot.Scene,
					origin,
					forward,
					gather,
					playerRoot,
					out var nodeId,
					out _ );
				minerals.DrawGatherDebug( playerRoot.WorldPosition, origin, forward, gather, forgiveness, nodeId );
			}
		}
	}

	static bool TryResolveAimRay( GameObject root, out Vector3 origin, out Vector3 forward )
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
