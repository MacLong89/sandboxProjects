namespace Terraingen.Combat;

/// <summary>Helpers for building authoritative combat trace results when physics did not produce one.</summary>
public static class ThornsCombatTraceUtil
{
	public static SceneTraceResult SyntheticHit( GameObject gameObject, Vector3 hitPosition )
	{
		var trace = default( SceneTraceResult );
		trace.Hit = true;
		trace.GameObject = gameObject;
		trace.HitPosition = hitPosition;
		return trace;
	}

	public static float ResolveDistance( Vector3 origin, SceneTraceResult trace )
	{
		if ( !trace.Hit )
			return 0f;

		if ( trace.Distance > 0f )
			return trace.Distance;

		return Vector3.DistanceBetween( origin, trace.HitPosition );
	}

	/// <summary>True when the trace came from a skeleton/model hitbox (not a synthetic capsule hit).</summary>
	public static bool HasModelHitboxData( SceneTraceResult trace )
	{
		if ( !trace.Hit )
			return false;

		try
		{
			var hitbox = trace.Hitbox;
			if ( hitbox is null )
				return false;

			return hitbox.GameObject.IsValid();
		}
		catch ( NullReferenceException )
		{
			return false;
		}
	}

	public static bool HasHeadHitboxTag( SceneTraceResult trace )
	{
		if ( !HasModelHitboxData( trace ) )
			return false;

		try
		{
			return trace.Hitbox.Tags.Has( "head" );
		}
		catch ( NullReferenceException )
		{
			return false;
		}
	}
}
