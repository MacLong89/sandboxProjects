namespace Terraingen.World;

using Terraingen.Player;

/// <summary>Shared rules for Use (E) on world interactables: view cone + optional LOS.</summary>
public static class ThornsWorldUseAim
{
	public const float MinLookDot = 0.52f;
	public const string InteriorRadioRootTag = "thorns_interior_radio";

	public static Vector3 GetInteractAimAnchorWorld( GameObject targetRoot )
	{
		if ( targetRoot is null || !targetRoot.IsValid() )
			return default;

		return targetRoot.WorldPosition + Vector3.Up * 40f;
	}

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

		if ( !ThornsLocalPlayer.TryGetAuthoritativeEye( pawnRoot, out var eye, out var eyeRot ) )
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

		var crossLen = Math.Min( maxHorizontalRange + 72f, 520f );
		var trCross = scene.Trace.Ray( new Ray( eye, fwd ), crossLen )
			.IgnoreGameObjectHierarchy( pawnRoot )
			.Run();

		if ( trCross.Hit && trCross.GameObject.IsValid() && TraceHitIsUnderRoot( trCross.GameObject, targetRoot ) )
			return true;

		var trAim = scene.Trace.Ray( new Ray( eye, dir ), dist + 14f )
			.IgnoreGameObjectHierarchy( pawnRoot )
			.Run();

		if ( !trAim.Hit || !trAim.GameObject.IsValid() )
			return true;

		if ( TraceHitIsUnderRoot( trAim.GameObject, targetRoot ) )
			return true;

		return trAim.Distance >= dist - 32f;
	}
}
