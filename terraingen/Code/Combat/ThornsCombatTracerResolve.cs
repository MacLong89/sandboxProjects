namespace Terraingen.Combat;

/// <summary>Resolves world-space tracer endpoints for hitscan shots.</summary>
public static class ThornsCombatTracerResolve
{
	const float MaxVisualTraceRange = 8192f;
	/// <summary>
	/// Visual tracer from <paramref name="segmentStart"/> (muzzle) along
	/// <paramref name="aimDirection"/> to the first world impact.
	/// </summary>
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
		_ = aimOrigin;

		if ( !TryResolveEnd( scene, segmentStart, aimDirection, maxRange, ignoreRoot, out segmentEnd ) )
			return false;

		if ( (segmentEnd - segmentStart).Length < 1f )
			return false;

		return true;
	}

	/// <summary>Tracer endpoint along the shot ray — world impact only, never target homing.</summary>
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
		var traceRange = Math.Min( maxRange, MaxVisualTraceRange );
		var traceEnd = origin + direction * traceRange;
		var trace = scene.Trace.Ray( origin, traceEnd ).UseHitboxes( true );
		if ( ignoreRoot.IsValid() )
			trace = trace.IgnoreGameObjectHierarchy( ignoreRoot );

		var result = trace.Run();
		return result.Hit ? result.HitPosition : traceEnd;
	}
}
