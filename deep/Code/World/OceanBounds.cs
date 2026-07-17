namespace Deep;

/// <summary>
/// Documents playable volume. Clamping is owned by <see cref="SeabedTerrain"/> + <see cref="DiverController"/>.
/// </summary>
public sealed class OceanBounds : Component
{
	public float LeftX => SeabedTerrain.Instance?.LeftX ?? -48f;
	public float RightX => SeabedTerrain.Instance?.RightX ?? 48f;
	public float MinZ => GameConstants.MinWorldZ;
	public float MaxZ => GameConstants.SurfaceSpawnZ + 0.5f;
}
