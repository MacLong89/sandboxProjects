namespace Sandbox;

/// <summary>Horizontal slab (chunk-local XY) with half extents and yaw for overlap checks and resource scatter exclusion.</summary>
public readonly struct ThornsWorldGenProcBuildingFootprint
{
	public readonly float CenterX, CenterY, HalfW, HalfD, YawRad;
	/// <summary>Chunk-local Z of the bottom of the lowest floor slab (terrain should match this).</summary>
	public readonly float FloorSurfaceZ;

	public ThornsWorldGenProcBuildingFootprint( float cx, float cy, float hw, float hd, float yawRad, float floorSurfaceZ = float.NaN )
	{
		CenterX = cx;
		CenterY = cy;
		HalfW = hw;
		HalfD = hd;
		YawRad = yawRad;
		FloorSurfaceZ = floorSurfaceZ;
	}
}
