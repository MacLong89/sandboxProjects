namespace Terraingen.GameData;

/// <summary>Campfire ore → ingot smelting (not available via hand/workbench crafting).</summary>
public static class ThornsCampfireSmelt
{
	public const string WoodItemId = "wood";
	public const string OreItemId = "metal_ore";
	public const string IngotItemId = "smelt_metal";
	public const int WoodPerBatch = 1;
	public const int OrePerIngot = 2;
	public const int IngotPerBatch = 1;
	public const float SecondsPerBatch = 6f;
	public const int MaxBatchCount = 20;

	public static int MaxAffordableBatches( int oreCount, int woodCount )
	{
		if ( oreCount < OrePerIngot || woodCount < WoodPerBatch )
			return 0;

		var byOre = oreCount / OrePerIngot;
		var byWood = woodCount / WoodPerBatch;
		return Math.Max( 0, Math.Min( byOre, byWood ) );
	}

	public static int MaxAffordableBatches( int oreCount )
		=> MaxAffordableBatches( oreCount, int.MaxValue );

	public static int ClampBatchCount( int requested, int oreAvailable, int woodAvailable ) =>
		Math.Clamp(
			requested,
			1,
			Math.Min( MaxBatchCount, Math.Max( 1, MaxAffordableBatches( oreAvailable, woodAvailable ) ) ) );

	public static int ClampBatchCount( int requested, int oreAvailable )
		=> ClampBatchCount( requested, oreAvailable, int.MaxValue );
}
