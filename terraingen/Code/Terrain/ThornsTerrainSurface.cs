namespace Terraingen.TerrainGen;

/// <summary>Ground snapping for world spawns and placement probes.</summary>
public static class ThornsTerrainSurface
{
	public const float InchesPerMeter = 39.3700787402f;

	public static float GetSeaLevelWorldZ( Terrain terrain, ThornsTerrainConfig config )
	{
		if ( !terrain.IsValid() )
			return 0f;

		var seaNorm = config?.SeaLevelNormalized ?? 0.06f;
		return terrain.GameObject.WorldPosition.z + seaNorm * terrain.TerrainHeight;
	}
	/// <summary>Intersect the terrain heightfield along a world-space view ray (crosshair placement).</summary>
	public static bool TryIntersectAlongRay( Terrain terrain, Vector3 origin, Vector3 direction, float maxDistance, out Vector3 worldPosition )
	{
		worldPosition = default;

		if ( !terrain.IsValid() || maxDistance <= 0f )
			return false;

		var ray = new Ray( origin, direction.Normal );
		if ( !terrain.RayIntersects( ray, maxDistance, out var localHit ) )
			return false;

		worldPosition = terrain.GameObject.WorldTransform.PointToWorld( localHit );
		return true;
	}

	public static bool TryRaycastGround( Terrain terrain, float worldX, float worldY, out Vector3 worldPosition )
	{
		worldPosition = default;

		if ( !terrain.IsValid() )
			return false;

		var originZ = terrain.GameObject.WorldPosition.z;
		var maxHeight = terrain.TerrainHeight;
		var rayStart = new Vector3( worldX, worldY, originZ + maxHeight * 2.5f );
		var ray = new Ray( rayStart, Vector3.Down );

		if ( !terrain.RayIntersects( ray, maxHeight * 5f, out var localHit ) )
			return false;

		worldPosition = terrain.GameObject.WorldTransform.PointToWorld( localHit );
		return true;
	}

	public static bool TrySnapToTerrain( Terrain terrain, Vector3 worldPosition, out Vector3 snapped )
	{
		snapped = worldPosition;

		if ( !TryRaycastGround( terrain, worldPosition.x, worldPosition.y, out snapped ) )
			return false;

		snapped.z += 2f;
		return true;
	}

	public static bool TrySampleSpawn(
		Terrain terrain,
		float worldX,
		float worldY,
		out Vector3 worldPosition )
	{
		worldPosition = default;
		if ( !terrain.IsValid() )
			return false;

		var probe = new Vector3( worldX, worldY, 0f );
		if ( !TrySnapToTerrain( terrain, probe, out worldPosition ) )
			return false;

		return worldPosition.z > terrain.GameObject.WorldPosition.z + 8f;
	}

	/// <summary>Clamp a world axis to terrain planar bounds without throwing when margin exceeds terrain width.</summary>
	public static float ClampPlanarAxis( float value, float terrainMin, float terrainMax, float margin = 32f )
	{
		var lower = terrainMin + margin;
		var upper = terrainMax - margin;
		if ( lower > upper )
			return (terrainMin + terrainMax) * 0.5f;

		return Math.Clamp( value, lower, upper );
	}

	public static Vector3 ClampToTerrainBounds( Terrain terrain, Vector3 position, float margin = 32f )
	{
		if ( !terrain.IsValid() )
			return position;

		var min = terrain.GameObject.WorldPosition;
		var max = min + new Vector3( terrain.TerrainSize, terrain.TerrainSize, 0f );
		return new Vector3(
			ClampPlanarAxis( position.x, min.x, max.x, margin ),
			ClampPlanarAxis( position.y, min.y, max.y, margin ),
			position.z );
	}
}
