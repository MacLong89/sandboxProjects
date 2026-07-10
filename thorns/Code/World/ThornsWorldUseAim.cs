using System;

namespace Sandbox;

/// <summary>
/// Shared rules for Use (E) on world interactables: client pick + host RPC validation must agree — view cone + anchor LOS
/// (prevents opening the nearest chest/crate while facing away).
/// </summary>
public static class ThornsWorldUseAim
{
	/// <summary>Minimum dot(camera forward, direction to interact anchor). ~0.52 ≈ 59° half-angle from bore.</summary>
	public const float MinLookDot = 0.52f;

	public const string InteriorRadioRootTag = "thorns_interior_radio";

	public static Vector3 GetInteractAimAnchorWorld( GameObject targetRoot )
	{
		if ( targetRoot is null || !targetRoot.IsValid() )
			return default;

		return targetRoot.WorldPosition + Vector3.Up * 40f;
	}

	/// <summary>True when <paramref name="targetRoot"/> or an ancestor carries <see cref="InteriorRadioRootTag"/>.</summary>
	public static bool HasInteriorRadioRootTag( GameObject targetRoot )
	{
		if ( targetRoot is null || !targetRoot.IsValid() )
			return false;

		for ( var p = targetRoot; p.IsValid(); p = p.Parent )
		{
			foreach ( var tag in p.Tags )
			{
				if ( tag == InteriorRadioRootTag )
					return true;
			}
		}

		return false;
	}

	static bool TraceHitIsUnderRoot( GameObject hitGo, GameObject targetRoot )
	{
		if ( !hitGo.IsValid() || !targetRoot.IsValid() )
			return false;

		for ( var p = hitGo; p.IsValid(); p = p.Parent )
		{
			if ( p == targetRoot )
				return true;
		}

		return false;
	}

	/// <summary>
	/// True when the pawn is within horizontal <paramref name="maxHorizontalRange"/> of <paramref name="targetRoot"/>,
	/// the view cone includes the interact anchor, and a trace toward that anchor is not blocked by unrelated geometry.
	/// </summary>
	/// <param name="coneOnlyWhenInteriorRadio">When true and the target is an interior proc-building radio, skip LOS traces
	/// (proc wall colliders often block the forward ray even while fairly aimed at the set).</param>
	public static bool PawnLooksAtInteractableRoot(
		GameObject pawnRoot,
		GameObject targetRoot,
		float maxHorizontalRange,
		bool coneOnlyWhenInteriorRadio = false )
	{
		if ( pawnRoot is null || !pawnRoot.IsValid() || targetRoot is null || !targetRoot.IsValid() )
			return false;

		var flatD = (pawnRoot.WorldPosition - targetRoot.WorldPosition).Length;
		if ( flatD > maxHorizontalRange )
			return false;

		if ( !ThornsCombatAuthority.TryGetAuthoritativeEye( pawnRoot, out var eye, out var eyeRot ) )
			return false;

		var fwd = eyeRot.Forward.Normal;
		var aim = GetInteractAimAnchorWorld( targetRoot );
		var to = aim - eye;
		var dist = to.Length;
		if ( dist < 2.5f )
			return true;

		if ( dist > maxHorizontalRange + 140f )
			return false;

		var dir = to / dist;
		if ( Vector3.Dot( fwd, dir ) < MinLookDot )
			return false;

		if ( coneOnlyWhenInteriorRadio && HasInteriorRadioRootTag( targetRoot ) )
			return true;

		var scene = pawnRoot.Scene;
		if ( scene is null || !scene.IsValid() )
			return false;

		// Crosshair ray hits this interactable first (common case).
		var crossLen = Math.Min( maxHorizontalRange + 72f, 520f );
		var trCross = ThornsTraceUtility.RunRay( scene, new Ray( eye, fwd ), crossLen, ThornsTraceProfile.InteractionUse, pawnRoot );

		if ( trCross.Hit && trCross.GameObject.IsValid() && TraceHitIsUnderRoot( trCross.GameObject, targetRoot ) )
			return true;

		// Fallback: anchor in cone + LOS toward anchor (handles slight aim offset when forward ray misses collider).
		var trAim = ThornsTraceUtility.RunRay( scene, new Ray( eye, dir ), dist + 14f, ThornsTraceProfile.InteractionUse, pawnRoot );

		if ( !trAim.Hit || !trAim.GameObject.IsValid() )
			return true;

		if ( TraceHitIsUnderRoot( trAim.GameObject, targetRoot ) )
			return true;

		return trAim.Distance >= dist - 32f;
	}
}
