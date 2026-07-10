namespace Terraingen.Minerals;

/// <summary>Aim picking for mineral node harvesting.</summary>
public static class ThornsMineralHitUtil
{
	public static bool TryPickNodeAlongRay(
		Scene scene,
		Vector3 origin,
		Vector3 direction,
		float maxRange,
		GameObject ignoreRoot,
		out int nodeId )
		=> TryPickNodeAlongRay( scene, origin, direction, maxRange, ignoreRoot, out nodeId, out _ );

	public static bool TryPickNodeAlongRay(
		Scene scene,
		Vector3 origin,
		Vector3 direction,
		float maxRange,
		GameObject ignoreRoot,
		out int nodeId,
		out Vector3 hitPosition )
	{
		nodeId = 0;
		hitPosition = default;

		var service = ThornsMineralWorldService.ResolveInstance();
		if ( service is null || !service.IsValid() )
			return false;

		return service.TryPickNodeAlongRay( origin, direction, maxRange, ignoreRoot, out nodeId, out hitPosition );
	}
}
