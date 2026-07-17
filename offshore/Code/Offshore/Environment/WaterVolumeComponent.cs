namespace Offshore;

/// <summary>Axis-aligned water volume for cast landing validation (XZ play plane).</summary>
public sealed class WaterVolumeComponent : Component
{
	[Property] public float MinX { get; set; } = OffshoreConstants.WaterMinX;
	[Property] public float MaxX { get; set; } = OffshoreConstants.WaterMaxX;
	[Property] public float MinZ { get; set; } = OffshoreConstants.WaterMinZ;
	[Property] public float MaxZ { get; set; } = OffshoreConstants.WaterMaxZ;
	[Property] public float SurfaceZ { get; set; } = OffshoreConstants.WaterSurfaceZ;

	public bool ContainsPoint( Vector3 point ) =>
		point.x >= MinX && point.x <= MaxX &&
		point.z >= MinZ && point.z <= MaxZ;

	public bool IsAboveWater( Vector3 point ) => point.z > SurfaceZ;

	public float ClampX( float x ) => Math.Clamp( x, MinX, MaxX );
}
