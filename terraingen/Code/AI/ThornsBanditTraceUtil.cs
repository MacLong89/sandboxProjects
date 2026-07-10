namespace Terraingen.AI;

/// <summary>Trace helpers for bandit LOS and footstep ground checks.</summary>
public static class ThornsBanditTraceUtil
{
	public static readonly string LosProfile = "bandit_los";
	public static readonly string GroundProfile = "bandit_ground";

	public static SceneTraceResult RunRay( Scene scene, Ray ray, float maxDistance, string profile, GameObject ignoreRoot )
	{
		if ( scene is null || !scene.IsValid )
			return default;

		var trace = scene.Trace.Ray( ray.Position, ray.Position + ray.Forward * maxDistance );
		if ( ignoreRoot.IsValid() )
			trace = trace.IgnoreGameObjectHierarchy( ignoreRoot );

		return trace.Run();
	}
}
