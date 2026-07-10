namespace Terraingen.Foliage;

/// <summary>Aim picking for tree harvesting (delegates to <see cref="ThornsTreeWorldService"/>).</summary>
public static class ThornsTreeHitUtil
{
	public static bool TryPickTreeAlongRay(
		Scene scene,
		Vector3 origin,
		Vector3 direction,
		float maxRange,
		GameObject ignoreRoot,
		out int treeId )
		=> TryPickTreeAlongRay( scene, origin, direction, maxRange, ignoreRoot, out treeId, out _ );

	public static bool TryPickTreeAlongRay(
		Scene scene,
		Vector3 origin,
		Vector3 direction,
		float maxRange,
		GameObject ignoreRoot,
		out int treeId,
		out Vector3 hitPosition )
	{
		treeId = 0;
		hitPosition = default;
		_ = scene;

		var service = ThornsTreeWorldService.ResolveInstance();
		if ( service is null || !service.IsValid() )
			return false;

		return service.TryPickTreeAlongRay( origin, direction, maxRange, ignoreRoot, out treeId, out hitPosition );
	}
}
