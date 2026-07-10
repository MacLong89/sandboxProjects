namespace Sandbox;

/// <summary>Registered procedural building for interior loot/radio scatter (phase 9).</summary>
public readonly struct ThornsWorldGenProcBuildingInteriorLoot
{
	public readonly GameObject Root;
	public readonly int WidthCells;
	public readonly int DepthCells;
	public readonly int Stories;
	public readonly int MaterialTier;
	public readonly ThornsProcBuildingType BuildingType;
	public readonly string MaterialSlug;
	public readonly int DoorSide;
	public readonly int DoorIndex;

	public ThornsWorldGenProcBuildingInteriorLoot(
		GameObject root,
		int widthCells,
		int depthCells,
		int stories,
		int materialTier,
		ThornsProcBuildingType buildingType,
		string materialSlug,
		int doorSide,
		int doorIndex )
	{
		Root = root;
		WidthCells = widthCells;
		DepthCells = depthCells;
		Stories = stories;
		MaterialTier = materialTier;
		BuildingType = buildingType;
		MaterialSlug = materialSlug ?? ThornsProcBuildingMaterialPalette.AllSlugs[0];
		DoorSide = doorSide;
		DoorIndex = doorIndex;
	}
}
