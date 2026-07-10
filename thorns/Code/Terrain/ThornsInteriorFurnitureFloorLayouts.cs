namespace Sandbox;

/// <summary>
/// Per-<see cref="ThornsProcBuildingType"/> interior templates (3×3 ASCII when compact).
/// </summary>
public static class ThornsInteriorFurnitureFloorLayouts
{
	public readonly struct Variant
	{
		public int Index { get; init; }
	}

	public readonly struct BuildingLayouts
	{
		public ThornsProcBuildingType Type { get; init; }
		public int VariantCount { get; init; }
	}

	public static BuildingLayouts ForBuildingType( ThornsProcBuildingType type ) =>
		new()
		{
			Type = type,
			VariantCount = ThornsInteriorFurnitureAsciiLayouts.VariantCount( type )
		};

	public static int PickVariantIndex( Random rnd, in BuildingLayouts layouts ) =>
		ThornsInteriorFurnitureAsciiLayouts.PickVariantIndex( rnd, layouts.Type );

	public static bool TryGetAsciiFloorRows(
		ThornsProcBuildingType type,
		int variantIndex,
		int storyIndex,
		out string[] rows ) =>
		ThornsInteriorFurnitureAsciiLayouts.TryGetFloorRows( type, variantIndex, storyIndex, out rows );

	/// <summary>Furniture ids on one storey (from ASCII template).</summary>
	public static IReadOnlyList<string> GetFloorPieces(
		in BuildingLayouts layouts,
		int variantIndex,
		int storyIndex )
	{
		if ( !TryGetAsciiFloorRows( layouts.Type, variantIndex, storyIndex, out var rows ) )
			return Array.Empty<string>();

		return ThornsInteriorFurnitureAsciiLayouts.ListFurnitureIds( rows );
	}
}
