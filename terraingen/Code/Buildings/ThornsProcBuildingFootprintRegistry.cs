namespace Terraingen.Buildings;

using Terraingen.World;

/// <summary>World XY exclusion zones for proc town buildings (foliage/mineral/boulder scatter).</summary>
public static class ThornsProcBuildingFootprintRegistry
{
	public static int Count => ThornsWorldScatterFootprintRegistry.Count;

	public static void Clear() => ThornsWorldScatterFootprintRegistry.Clear();

	public static void Register( Vector3 worldPosition, Rotation worldRotation ) =>
		Register( worldPosition, worldRotation, ThornsProcBuildingInterior.GridCells, ThornsProcBuildingInterior.GridCells );

	public static void Register( Vector3 worldPosition, Rotation worldRotation, int widthCells, int depthCells ) =>
		ThornsWorldScatterFootprintRegistry.RegisterBuilding( worldPosition, worldRotation, widthCells, depthCells );

	public static bool ContainsWorldPoint( float worldX, float worldY, float extraMarginInches = 0f ) =>
		ThornsWorldScatterFootprintRegistry.ContainsWorldPoint( worldX, worldY, extraMarginInches );
}
