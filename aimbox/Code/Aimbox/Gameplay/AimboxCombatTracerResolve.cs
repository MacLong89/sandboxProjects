namespace Sandbox;

/// <summary>Resolves world-space tracer endpoints for hitscan shots.</summary>
public static class AimboxCombatTracerResolve
{
	public static bool TryResolveSegmentTowardAim(
		Scene scene,
		Vector3 segmentStart,
		Vector3 aimOrigin,
		Vector3 aimDirection,
		float maxRange,
		GameObject ignoreRoot,
		out Vector3 segmentEnd )
	{
		segmentEnd = default;
		if ( !TryResolveEnd( scene, aimOrigin, aimDirection, maxRange, ignoreRoot, out segmentEnd ) )
			return false;

		return (segmentEnd - segmentStart).Length >= 1f;
	}

	public static bool TryResolveEnd(
		Scene scene,
		Vector3 origin,
		Vector3 direction,
		float maxRange,
		GameObject ignoreRoot,
		out Vector3 endPoint )
	{
		endPoint = default;
		if ( scene is null || !scene.IsValid() )
			return false;

		var dir = direction.Normal;
		if ( dir.Length < 0.95f || maxRange <= 0f )
			return false;

		endPoint = ResolveWorldImpact( scene, origin, dir, maxRange, ignoreRoot );
		return true;
	}

	static Vector3 ResolveWorldImpact( Scene scene, Vector3 origin, Vector3 direction, float maxRange, GameObject ignoreRoot )
	{
		var traceEnd = origin + direction * maxRange;
		var trace = scene.Trace.Ray( origin, traceEnd );
		if ( ignoreRoot.IsValid() )
			trace = trace.IgnoreGameObjectHierarchy( ignoreRoot );

		var result = trace.Run();
		return result.Hit ? result.HitPosition : traceEnd;
	}
}
