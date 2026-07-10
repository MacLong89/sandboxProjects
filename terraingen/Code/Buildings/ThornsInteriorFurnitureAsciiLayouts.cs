namespace Terraingen.Buildings;

/// <summary>Per-building-type furniture id lists mapped through canonical corner slots.</summary>
public static class ThornsInteriorFurnitureAsciiLayouts
{
	public const int GridCells = 3;

	public sealed class Variant
	{
		public string[] GroundFurniture { get; }
		public string[] UpperFurniture { get; }
		public string[] TopFurniture { get; }

		public Variant( string[] groundFurniture, string[] upperFurniture, string[] topFurniture = null )
		{
			GroundFurniture = groundFurniture ?? Array.Empty<string>();
			UpperFurniture = upperFurniture ?? Array.Empty<string>();
			TopFurniture = topFurniture ?? upperFurniture ?? Array.Empty<string>();
		}
	}

	static Variant V( string[] ground, string[] upper, string[] top = null ) => new( ground, upper, top );

	static Dictionary<ThornsProcBuildingType, Variant[]> _byType;

	static void EnsureCatalog() => _byType ??= BuildCatalog();

	static Dictionary<ThornsProcBuildingType, Variant[]> BuildCatalog() => new()
	{
		[ThornsProcBuildingType.House] = [V( ["couch", "kitchen_fridge", "chair"], ["bed", "cabinet"], ["desk", "bed", "cabinet"] )],
		[ThornsProcBuildingType.Ruin] = [V( ["couch", "fridge", "chair"], ["bed", "cabinet"], ["desk", "bed", "cabinet"] )],
		[ThornsProcBuildingType.Warehouse] = [V( ["pallets", "fridge", "chair"], ["bed", "cabinet"], ["desk", "bed", "cabinet"] )],
		[ThornsProcBuildingType.MilitaryComplex] = [V( ["military_supply", "chair", "conference"], ["bed", "cabinet"], ["military_supply", "bed", "cabinet"] )],
		[ThornsProcBuildingType.Cabin] = [V( ["kitchen_fridge", "couch", "chair"], ["bed", "cabinet"], ["bed", "desk", "cabinet"] )],
		[ThornsProcBuildingType.Store] = [V( ["retail", "kitchen_fridge", "chair"], ["bed", "cabinet"], ["desk", "bed", "cabinet"] )],
		[ThornsProcBuildingType.Apartment] = [V( ["couch", "kitchen_fridge", "chair"], ["bed", "cabinet"], ["desk", "bed", "cabinet"] )],
		[ThornsProcBuildingType.Factory] = [V( ["pallets", "fridge", "chair"], ["bed", "workbench"], ["bed", "workbench", "cabinet"] )],
		[ThornsProcBuildingType.Barn] = [V( ["pallets", "fridge", "chair"], ["bed", "cabinet"], ["cabinet", "bed", "desk"] )],
		[ThornsProcBuildingType.RadioOutpost] = [V( ["radio", "chair", "conference"], ["military_supply", "bed"], ["military_supply", "bed"] )],
		[ThornsProcBuildingType.ApartmentTower] = [V( ["desk", "kitchen_fridge", "chair"], ["bed", "cabinet"], ["desk", "bed", "cabinet"] )],
		[ThornsProcBuildingType.Skyscraper] = [V( ["conference", "desk", "chair"], ["bed", "cabinet"], ["desk", "bed", "cabinet"] )],
		[ThornsProcBuildingType.OfficeBuilding] = [V( ["conference", "chair", "desk"], ["bed", "cabinet"], ["desk", "bed", "cabinet"] )]
	};

	public static bool SupportsBuildingType( ThornsProcBuildingType type )
	{
		EnsureCatalog();
		return _byType.TryGetValue( type, out var variants ) && variants.Length > 0;
	}

	public static int VariantCount( ThornsProcBuildingType type )
	{
		EnsureCatalog();
		return SupportsBuildingType( type ) ? _byType[type].Length : 0;
	}

	public static bool TryCollectScriptedPlacements(
		ThornsProcBuildingType type,
		int variantIndex,
		int storyIndex,
		int widthCells,
		int depthCells,
		List<ThornsProcBuildingInterior.CellPlacement> into,
		int stories = 3,
		bool[,,] skipFloor = null )
	{
		if ( into is null )
			return false;

		EnsureCatalog();
		if ( !_byType.TryGetValue( type, out var variants ) || variants.Length == 0 )
			return false;

		var variant = variants[Math.Clamp( variantIndex, 0, variants.Length - 1 )];
		var furniture = GetFurnitureForStory( variant, storyIndex );
		if ( furniture is null || furniture.Length == 0 )
			return false;

		var added = ThornsInteriorFurnitureCanonicalSlots.CollectCornerPlacements(
			storyIndex,
			furniture,
			widthCells,
			depthCells,
			into,
			stories,
			skipFloor );

		if ( ThornsInteriorMegastructureFill.IsLargeFootprint( widthCells, depthCells ) )
		{
			ThornsInteriorMegastructureFill.TrimStoryPlacements(
				into,
				storyIndex,
				ThornsInteriorMegastructureFill.MaxPlacementsPerStory );

			var remaining = ThornsInteriorMegastructureFill.MaxPlacementsPerStory
			                - ThornsInteriorMegastructureFill.CountStoryPlacements( into, storyIndex );

			if ( remaining > 0 )
			{
				added = ThornsInteriorMegastructureFill.CountStoryPlacements( into, storyIndex );
				added += ThornsInteriorMegastructureFill.CollectFillPlacements(
					type,
					variantIndex,
					storyIndex,
					widthCells,
					depthCells,
					into,
					stories,
					remaining,
					skipFloor );
			}
			else
			{
				added = ThornsInteriorMegastructureFill.CountStoryPlacements( into, storyIndex );
			}
		}

		return added > 0;
	}

	static string[] GetFurnitureForStory( Variant variant, int storyIndex )
	{
		if ( storyIndex <= 0 )
			return variant.GroundFurniture;
		if ( storyIndex == 1 )
			return variant.UpperFurniture;
		return variant.TopFurniture;
	}

	public static string FormatVariant( ThornsProcBuildingType type, int variantIndex )
	{
		EnsureCatalog();
		if ( !_byType.TryGetValue( type, out var variants ) || variants.Length == 0 )
			return $"{type} (no layout)";

		var variant = variants[Math.Clamp( variantIndex, 0, variants.Length - 1 )];
		return $"{type} v{variantIndex}: ground=[{string.Join( ", ", variant.GroundFurniture )}] "
		       + $"upper=[{string.Join( ", ", variant.UpperFurniture )}] "
		       + $"top=[{string.Join( ", ", variant.TopFurniture )}]";
	}
}
