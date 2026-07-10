using System.Collections.Generic;
using System.Text;

namespace Sandbox;

/// <summary>
/// 3×3 ASCII interior templates per <see cref="ThornsProcBuildingType"/> (north at top).
/// Compact settlements use <see cref="ThornsInteriorFurnitureFloorplanAscii.SettlementAsciiStories"/> (3) with switchback ramps;
/// furniture ids map to corners via <see cref="ThornsInteriorFurnitureCanonicalSlots"/> (shaft/opening cells excluded).
/// </summary>
public static class ThornsInteriorFurnitureAsciiLayouts
{
	public const int GridCells = 3;

	/// <summary>Bump when catalog or canonical slots change — forces <see cref="EnsureCatalog"/> rebuild (hotload-safe).</summary>
	public const int LayoutCatalogRevision = 16;

	public static readonly IReadOnlyDictionary<char, string> FurnitureCharToId = new Dictionary<char, string>
	{
		['C'] = "couch",
		['h'] = "chair",
		['k'] = "kitchen_fridge",
		['F'] = "fridge",
		['B'] = "bed",
		['w'] = "workbench",
		['d'] = "desk",
		['T'] = "dining_table",
		['c'] = "conference",
		['b'] = "bunk",
		['M'] = "military_supply",
		['P'] = "pallets",
		['r'] = "retail",
		['A'] = "radio",
		['K'] = "cabinet"
	};

	public sealed class Variant
	{
		public string[] GroundFurniture { get; }
		/// <summary>Second storey (index 1) — usually two corners (ramp shaft takes the third).</summary>
		public string[] UpperFurniture { get; }
		/// <summary>Third storey (index 2) and above in compact 3×3 settlements.</summary>
		public string[] TopFurniture { get; }

		public Variant( string[] groundFurniture, string[] upperFurniture, string[] topFurniture = null )
		{
			GroundFurniture = groundFurniture ?? Array.Empty<string>();
			UpperFurniture = upperFurniture ?? Array.Empty<string>();
			TopFurniture = topFurniture ?? upperFurniture ?? Array.Empty<string>();
		}
	}

	static Variant V( string[] ground, string[] upper, string[] top = null ) => new( ground, upper, top );

	static int _catalogBuiltRevision = -1;
	static Dictionary<ThornsProcBuildingType, Variant[]> _byType;

	static void EnsureCatalog()
	{
		if ( _byType is not null && _catalogBuiltRevision == LayoutCatalogRevision )
			return;

		_catalogBuiltRevision = LayoutCatalogRevision;
		_byType = BuildCatalog();
	}

	static readonly Variant HouseA = V(
		["couch", "kitchen_fridge", "chair"],
		["bed", "cabinet"],
		["desk", "bed", "cabinet"] );

	static readonly Variant HouseB = V(
		["couch", "dining_table", "chair"],
		["bed", "cabinet"],
		["bed", "workbench", "cabinet"] );

	static readonly Variant CabinA = V(
		["kitchen_fridge", "couch", "chair"],
		["bed", "cabinet"],
		["bed", "desk", "cabinet"] );

	static readonly Variant CabinB = V(
		["fridge", "chair", "desk"],
		["bed", "workbench"],
		["bed", "workbench", "cabinet"] );

	static readonly Variant ApartmentA = HouseA;

	static readonly Variant ApartmentB = V(
		["couch", "kitchen_fridge", "chair"],
		["bed", "cabinet"],
		["desk", "bed", "cabinet"] );

	static readonly Variant ApartmentTowerA = V(
		["desk", "kitchen_fridge", "chair"],
		["bed", "cabinet"],
		["desk", "bed", "cabinet"] );

	static readonly Variant ApartmentTowerB = V(
		["desk", "kitchen_fridge", "chair"],
		["bed", "workbench"],
		["bed", "workbench", "cabinet"] );

	static readonly Variant RuinA = V(
		["couch", "fridge", "chair"],
		["bed", "cabinet"],
		["desk", "bed", "cabinet"] );

	static readonly Variant RuinB = V(
		["chair", "bed", "cabinet"],
		["bed", "cabinet"],
		["desk", "bed", "cabinet"] );

	static readonly Variant OfficeA = V(
		["conference", "chair", "desk"],
		["bed", "cabinet"],
		["desk", "bed", "cabinet"] );

	static readonly Variant OfficeB = V(
		["conference", "desk", "chair"],
		["cabinet", "bed"],
		["cabinet", "bed", "desk"] );

	static readonly Variant SkyscraperA = V(
		["conference", "desk", "chair"],
		["bed", "cabinet"],
		["desk", "bed", "cabinet"] );

	static readonly Variant SkyscraperB = V(
		["conference", "desk", "chair"],
		["bed", "workbench"],
		["bed", "workbench", "cabinet"] );

	static readonly Variant StoreA = V(
		["retail", "kitchen_fridge", "chair"],
		["bed", "cabinet"],
		["desk", "bed", "cabinet"] );

	static readonly Variant StoreB = V(
		["retail", "kitchen_fridge", "chair"],
		["bed", "cabinet"],
		["desk", "bed", "cabinet"] );

	static readonly Variant WarehouseA = V(
		["pallets", "fridge", "chair"],
		["bed", "cabinet"],
		["desk", "bed", "cabinet"] );

	static readonly Variant WarehouseB = V(
		["workbench", "pallets", "chair"],
		["bed", "desk"],
		["cabinet", "bed", "desk"] );

	static readonly Variant FactoryA = V(
		["pallets", "fridge", "chair"],
		["bed", "workbench"],
		["bed", "workbench", "cabinet"] );

	static readonly Variant FactoryB = V(
		["pallets", "fridge", "chair"],
		["bed", "cabinet"],
		["chair", "bed", "cabinet"] );

	static readonly Variant BarnA = V(
		["pallets", "fridge", "chair"],
		["bed", "cabinet"],
		["cabinet", "bed", "desk"] );

	static readonly Variant BarnB = V(
		["pallets", "fridge", "chair"],
		["bed", "cabinet"],
		["chair", "bed", "cabinet"] );

	static readonly Variant MilitaryA = V(
		["military_supply", "chair", "conference"],
		["bed", "cabinet"],
		["military_supply", "bed", "cabinet"] );

	static readonly Variant MilitaryB = V(
		["military_supply", "chair", "bunk"],
		["bed", "cabinet"],
		["desk", "bed", "cabinet"] );

	static readonly Variant RadioOutpostA = V(
		["radio", "chair", "conference"],
		["bunk", "bed"],
		["bunk", "bed", "military_supply"] );

	static readonly Variant RadioOutpostB = V(
		["radio", "chair", "desk"],
		["bunk", "bed"],
		["bunk", "bed", "cabinet"] );

	static Dictionary<ThornsProcBuildingType, Variant[]> BuildCatalog() => new()
	{
		[ThornsProcBuildingType.House] = [HouseA],
		[ThornsProcBuildingType.Ruin] = [RuinA],
		[ThornsProcBuildingType.Warehouse] = [WarehouseA],
		[ThornsProcBuildingType.MilitaryComplex] = [MilitaryA],
		[ThornsProcBuildingType.Cabin] = [CabinA],
		[ThornsProcBuildingType.Store] = [StoreA],
		[ThornsProcBuildingType.Apartment] = [ApartmentA],
		[ThornsProcBuildingType.Factory] = [FactoryA],
		[ThornsProcBuildingType.Barn] = [BarnA],
		[ThornsProcBuildingType.RadioOutpost] = [RadioOutpostA],
		[ThornsProcBuildingType.ApartmentTower] = [ApartmentTowerA],
		[ThornsProcBuildingType.Skyscraper] = [SkyscraperA],
		[ThornsProcBuildingType.OfficeBuilding] = [OfficeA]
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

	public static int PickVariantIndex( Random rnd, ThornsProcBuildingType type )
	{
		var count = VariantCount( type );
		return count <= 0 ? 0 : rnd.Next( count );
	}

	public static string DescribeFloorRowsFailure(
		ThornsProcBuildingType type,
		int variantIndex,
		int storyIndex )
	{
		EnsureCatalog();
		if ( !_byType.TryGetValue( type, out var variants ) || variants.Length == 0 )
			return "no catalog entry";

		var variant = variants[Math.Clamp( variantIndex, 0, variants.Length - 1 )];
		if ( variant is null )
			return "variant is null";

		var furniture = GetFurnitureForStory( variant, storyIndex );
		if ( furniture is null || furniture.Length == 0 )
			return "no furniture ids";

		if ( ThornsInteriorFurnitureCanonicalSlots.TryBuildFloorRows( storyIndex, furniture, out var built ) )
			return $"unexpected ok rows [{string.Join( " | ", built )}]";

		return $"build failed for [{string.Join( ", ", furniture )}] (layoutRev={LayoutCatalogRevision})";
	}

	public static IReadOnlyList<string> ListFurnitureIds( string[] rows )
	{
		var placements = new List<ThornsInteriorFurnitureFloorplanAscii.CellPlacement>( 8 );
		CollectFurniture( rows, story: 0, GridCells, GridCells, placements );
		var ids = new List<string>( placements.Count );
		for ( var i = 0; i < placements.Count; i++ )
			ids.Add( placements[i].StructureDefId );

		return ids;
	}

	public static void ParseFurniturePlacements(
		string[] rows,
		int story,
		int widthCells,
		int depthCells,
		List<ThornsInteriorFurnitureFloorplanAscii.CellPlacement> into )
	{
		if ( rows is null || into is null )
			return;

		CollectFurniture( rows, story, widthCells, depthCells, into );
	}

	static void CollectFurniture(
		string[] rows,
		int story,
		int w,
		int d,
		List<ThornsInteriorFurnitureFloorplanAscii.CellPlacement> into )
	{
		for ( var row = 0; row < rows.Length && row < d; row++ )
		{
			var gy = d - 1 - row;
			var line = rows[row];
			for ( var gx = 0; gx < w; gx++ )
			{
				var ch = gx < line.Length ? line[gx] : '.';
				if ( !FurnitureCharToId.TryGetValue( ch, out var id ) )
					continue;

				into.Add( new ThornsInteriorFurnitureFloorplanAscii.CellPlacement
				{
					Story = story,
					GridX = gx,
					GridY = gy,
					StructureDefId = id
				} );
			}
		}
	}

	public static bool ValidateRows( string[] rows, int widthCells = GridCells, int depthCells = GridCells )
	{
		if ( rows is null || rows.Length != depthCells )
			return false;

		for ( var i = 0; i < rows.Length; i++ )
		{
			if ( rows[i] is null || rows[i].Length != widthCells )
				return false;
		}

		return true;
	}

	/// <summary>Scripted corner placements for one storey (matches proc layout ramp/shaft).</summary>
	public static bool TryCollectScriptedPlacements(
		ThornsProcBuildingType type,
		int variantIndex,
		int storyIndex,
		int widthCells,
		int depthCells,
		ThornsProcBuildingLayout layout,
		List<ThornsInteriorFurnitureFloorplanAscii.CellPlacement> into )
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

		return ThornsInteriorFurnitureCanonicalSlots.CollectCornerPlacements(
			storyIndex,
			furniture,
			widthCells,
			depthCells,
			layout,
			into ) > 0;
	}

	static bool TryGetVariantFurniture(
		ThornsProcBuildingType type,
		int variantIndex,
		int storyIndex,
		out string[] furniture )
	{
		furniture = null;
		EnsureCatalog();
		if ( !_byType.TryGetValue( type, out var variants ) || variants.Length == 0 )
			return false;

		var variant = variants[Math.Clamp( variantIndex, 0, variants.Length - 1 )];
		furniture = GetFurnitureForStory( variant, storyIndex );
		return furniture is { Length: > 0 };
	}

	static string[] GetFurnitureForStory( Variant variant, int storyIndex )
	{
		if ( storyIndex <= 0 )
			return variant.GroundFurniture;
		if ( storyIndex == 1 )
			return variant.UpperFurniture;
		return variant.TopFurniture;
	}

	public static int SettlementAsciiStories => ThornsInteriorFurnitureFloorplanAscii.SettlementAsciiStories;

	public static bool TryGetFloorRows(
		ThornsProcBuildingType type,
		int variantIndex,
		int storyIndex,
		out string[] rows,
		ThornsProcBuildingLayout layout = null,
		int widthCells = GridCells,
		int depthCells = GridCells )
	{
		rows = null;
		EnsureCatalog();
		if ( !_byType.TryGetValue( type, out var variants ) || variants.Length == 0 )
			return false;

		var variant = variants[Math.Clamp( variantIndex, 0, variants.Length - 1 )];
		var furniture = GetFurnitureForStory( variant, storyIndex );
		if ( furniture is null || furniture.Length == 0 )
			return false;

		return ThornsInteriorFurnitureCanonicalSlots.TryBuildFloorRows(
			storyIndex,
			furniture,
			out rows,
			layout: null,
			widthCells,
			depthCells );
	}

	public static string FormatVariant( ThornsProcBuildingType type, int variantIndex )
	{
		if ( !SupportsBuildingType( type ) )
			return $"{type}: (none)";

		var sb = new StringBuilder();
		sb.Append( type );
		sb.Append( " v" );
		sb.Append( variantIndex );
		sb.AppendLine();

		if ( TryGetFloorRows( type, variantIndex, 0, out var g ) )
		{
			sb.AppendLine( "  F0:" );
			AppendRows( sb, g, "    " );
		}

		if ( TryGetFloorRows( type, variantIndex, 1, out var u ) )
		{
			sb.AppendLine( "  F1:" );
			AppendRows( sb, u, "    " );
		}

		if ( TryGetFloorRows( type, variantIndex, 2, out var t ) )
		{
			sb.AppendLine( "  F2:" );
			AppendRows( sb, t, "    " );
		}

		return sb.ToString();
	}

	static void AppendRows( StringBuilder sb, string[] rows, string indent )
	{
		foreach ( var row in rows )
			sb.AppendLine( indent + row );
	}
}
