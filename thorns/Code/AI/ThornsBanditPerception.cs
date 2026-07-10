namespace Sandbox;

/// <summary>
/// Host-only perception helpers for bandits — LOS traces only run after radius filtering (THORNS §8 LOD / perf).
/// </summary>
public static class ThornsBanditPerception
{
	public static Vector3 ResolvePreferredAimWorldPoint( GameObject playerRoot )
	{
		if ( !playerRoot.IsValid() )
			return default;

		if ( ThornsCombatAuthority.TryGetAuthoritativeEye( playerRoot, out var eye, out _ ) )
			return eye;

		return playerRoot.WorldPosition + Vector3.Up * 56f;
	}

	public static bool HasClearLos( GameObject banditRoot, GameObject targetRoot, float maxDistance )
	{
		if ( !banditRoot.IsValid() || !targetRoot.IsValid() )
			return false;

		if ( !ThornsCombatAuthority.TryGetAuthoritativeEye( banditRoot, out var eye, out _ ) )
			eye = banditRoot.WorldPosition + Vector3.Up * 64f;

		var tgt = ResolvePreferredAimWorldPoint( targetRoot );
		var delta = tgt - eye;
		var len = delta.Length;
		if ( len < 12f || len > maxDistance )
			return false;

		var dir = delta.Normal;
		var traceLen = Math.Min( len - 6f, maxDistance );
		if ( traceLen < 8f )
			return true;

		var scene = banditRoot.Scene;
		if ( scene is null || !scene.IsValid() )
			return false;

		var tr = ThornsTraceUtility.RunRay( scene, new Ray( eye, dir ), traceLen, ThornsTraceProfile.AiLineOfSight, banditRoot );

		if ( !tr.Hit )
			return true;

		return TraceHitIsTargetRoot( tr.GameObject, targetRoot );
	}

	static bool TraceHitIsTargetRoot( GameObject hitGo, GameObject targetRoot )
	{
		if ( !hitGo.IsValid() || !targetRoot.IsValid() )
			return false;

		if ( hitGo == targetRoot )
			return true;

		for ( var p = hitGo; p.IsValid(); p = p.Parent )
		{
			if ( p == targetRoot )
				return true;
		}

		return false;
	}
}
