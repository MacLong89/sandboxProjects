namespace Sandbox;

/// <summary>Cached local pawn world position for distance culling / LOD (avoids per-frame scene scans).</summary>
public static class ThornsLocalViewer
{
	public static bool TryGetWorldPosition( out Vector3 worldPosition )
	{
		worldPosition = default;
		var local = ThornsPawn.Local;
		if ( !local.IsValid() || !local.GameObject.IsValid() )
			return false;

		worldPosition = local.GameObject.WorldPosition;
		return true;
	}
}
