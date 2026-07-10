using System.Text;

namespace Sandbox;

/// <summary>
/// ASCII floorplans for <see cref="ThornsInteriorFurnitureFloorplanTest"/> (3×3, 3 storeys).
/// Rows are generated from <see cref="ThornsInteriorFurnitureCanonicalSlots"/> (corner furniture + ramp).
/// Row 0 in the strings is the north edge; grid Y increases south (engine convention).
/// </summary>
public static class ThornsInteriorFurnitureFloorplanAscii
{
	/// <summary>Bump when floor strings change — play log must match or the assembly is stale.</summary>
	public const int FloorplanRevision = 16;

	public const int TestWidthCells = 3;
	public const int TestDepthCells = 3;
	/// <summary>Max storeys for compact settlement ASCII shells (world-gen rolls 1–<see cref="MaxSettlementAsciiStories"/>).</summary>
	public const int MaxSettlementAsciiStories = 3;

	/// <summary>Legacy alias — floorplan test gallery uses full height unless <paramref name="storyCount"/> is set.</summary>
	public const int SettlementAsciiStories = MaxSettlementAsciiStories;

	public const int TestStories = MaxSettlementAsciiStories;

	/// <summary>World settlement: random 1–3 walkable storeys (same furniture rules as the floorplan test scene).</summary>
	public static int RollSettlementStoryCount( Random rnd ) =>
		(rnd ?? Random.Shared).Next( 1, MaxSettlementAsciiStories + 1 );

	static string[] GetFloorRows( int story ) =>
		ThornsInteriorFurnitureAsciiLayouts.TryGetFloorRows( ThornsProcBuildingType.House, 0, story, out var rows )
			? rows
			: story switch
			{
				0 => ["...", "...", "..R"],
				1 => ["...", "...", "..."],
				_ => ["...", "...", "..."]
			};


	static readonly Dictionary<char, string> FurnitureCharToId = new()
	{
		['C'] = "couch",
		['h'] = "chair",
		['k'] = "kitchen_fridge",
		['B'] = "bed",
		['w'] = "workbench",
		['d'] = "desk",
		['T'] = "dining_table",
		['c'] = "conference",
		['b'] = "bunk",
		['M'] = "military_supply",
		['P'] = "pallets",
		['r'] = "retail",
		['A'] = "radio"
	};

	public readonly struct CellPlacement
	{
		public int Story { get; init; }
		public int GridX { get; init; }
		public int GridY { get; init; }
		public string StructureDefId { get; init; }
	}

	/// <summary>House variant A — dev shorthand.</summary>
	public static ThornsProcBuildingLayout CreateTestHouseLayout( out IReadOnlyList<CellPlacement> furniture ) =>
		CreateBuildingTypeLayout( ThornsProcBuildingType.House, variantIndex: 0, out furniture );

	/// <summary>World-gen: 3×3 ASCII shell (1–3 storeys) when the type is catalogued.</summary>
	public static bool TryCreateSettlementLayout(
		ThornsProcBuildingType buildingType,
		ThornsProcBuildingDistrict district,
		Random rnd,
		bool isRuinVariant,
		out ThornsProcBuildingLayout layout,
		out int variantIndex )
	{
		layout = null;
		variantIndex = -1;
		if ( !ThornsInteriorFurnitureAsciiLayouts.SupportsBuildingType( buildingType ) )
			return false;

		variantIndex = ThornsInteriorFurnitureAsciiLayouts.PickVariantIndex( rnd, buildingType );
		var stories = RollSettlementStoryCount( rnd );
		try
		{
			layout = CreateBuildingTypeLayout(
				buildingType,
				variantIndex,
				out _,
				district,
				isRuinVariant,
				storyCount: stories );
			return layout is not null;
		}
		catch ( Exception ex )
		{
			Log.Warning(
				$"[Thorns ProcBuilding] ASCII floorplan layout failed for {buildingType} v{variantIndex}: {ex.Message}" );
			layout = null;
			variantIndex = -1;
			return false;
		}
	}

	/// <summary>3×3 shell + furniture from <see cref="ThornsInteriorFurnitureAsciiLayouts"/> (switchback ramps when multi-storey).</summary>
	public static ThornsProcBuildingLayout CreateBuildingTypeLayout(
		ThornsProcBuildingType buildingType,
		int variantIndex,
		out IReadOnlyList<CellPlacement> furniture,
		ThornsProcBuildingDistrict district = ThornsProcBuildingDistrict.Residential,
		bool isRuinVariant = false,
		int storyCount = -1 )
	{
		var w = TestWidthCells;
		var d = TestDepthCells;
		var stories = storyCount > 0
			? ThornsProcBuildingPoc.EffectiveStories( storyCount )
			: MaxSettlementAsciiStories;
		var occupied = new bool[stories * w * d];
		var opening = new bool[stories * w * d];
		for ( var s = 0; s < stories; s++ )
		for ( var x = 0; x < w; x++ )
		for ( var y = 0; y < d; y++ )
			occupied[s * w * d + y * w + x] = true;

		var rampsByStory = ThornsProcBuildingRampPlanner.BuildSettlementRampsByStory( stories, w, d );
		var flatRamps = ThornsProcBuildingRampPlanner.BuildFlatRampList( rampsByStory );
		ThornsProcTileRampHeadroom.ApplyRequiredOpenings( opening, w, d, flatRamps );
		EnsureRampAnchorsWalkable( w, d, occupied, opening, flatRamps );

		var layout = ThornsProcBuildingLayout.CreateFromBlueprint( w, d, stories, occupied, opening, flatRamps );
		if ( layout is null )
			throw new InvalidOperationException(
				$"[Thorns FloorplanTest] Layout blueprint rejected for {buildingType} v{variantIndex} "
				+ $"(ramps={flatRamps.Count}, openings={CountOpeningsInArrays( w, d, stories, opening )})." );

		const int doorSide = 2;
		const int doorIndex = 1;
		layout.SetGroundDoor( doorSide, doorIndex );
		layout.InteriorWalls = ThornsProcBuildingWallPlan.Empty( stories, w, d );

		if ( !ThornsProcBuildingStrictValidation.TryValidate( layout, null, out _, out var failure ) )
		{
			throw new InvalidOperationException(
				$"[Thorns FloorplanTest] Layout validation failed for {buildingType} v{variantIndex}: "
				+ $"{failure.Rule} {failure.Reason} story={failure.Story}." );
		}
		layout.Identity = CreateFloorplanIdentity(
			buildingType,
			w,
			d,
			stories,
			doorSide,
			doorIndex,
			district,
			isRuinVariant,
			variantIndex );

		var placements = new List<CellPlacement>();
		for ( var story = 0; story < stories; story++ )
		{
			ThornsInteriorFurnitureAsciiLayouts.TryCollectScriptedPlacements(
				buildingType,
				variantIndex,
				story,
				w,
				d,
				layout,
				placements );
		}

		furniture = placements;
		return layout;
	}

	static ThornsProcBuildingIdentityMeta CreateFloorplanIdentity(
		ThornsProcBuildingType buildingType,
		int w,
		int d,
		int stories,
		int doorSide,
		int doorIndex,
		ThornsProcBuildingDistrict district,
		bool isRuinVariant,
		int variantIndex )
	{
		var facade = new ThornsProcBuildingFacadePlan { WindowChance = 0f };
		ApplyPerimeterWindows( facade, w, d, stories, doorSide, doorIndex );
		return new ThornsProcBuildingIdentityMeta
		{
			Type = buildingType,
			District = district,
			IsRuinVariant = isRuinVariant,
			Facade = facade,
			InteriorAsciiVariantIndex = variantIndex
		};
	}

	/// <summary>Door on one side; windows on the other three (and flanking the door on the entry side).</summary>
	static void ApplyPerimeterWindows(
		ThornsProcBuildingFacadePlan facade,
		int w,
		int d,
		int stories,
		int doorSide,
		int doorIndex )
	{
		for ( var s = 0; s < stories; s++ )
		{
			for ( var x = 0; x < w; x++ )
			{
				if ( doorSide != 2 || s != 0 || x != doorIndex )
					facade.AddForceWindow( s, 2, x, d - 1 );

				facade.AddForceWindow( s, 0, x, 0 );
			}

			for ( var y = 0; y < d; y++ )
			{
				facade.AddForceWindow( s, 3, 0, y );
				facade.AddForceWindow( s, 1, w - 1, y );
			}
		}
	}

	static void ValidateFloorRows( string[] rows, string label )
	{
		if ( rows.Length != TestDepthCells )
		{
			throw new InvalidOperationException(
				$"[Thorns FloorplanTest] rev={FloorplanRevision} {label} row count {rows.Length} != {TestDepthCells}." );
		}

		for ( var i = 0; i < rows.Length; i++ )
		{
			if ( rows[i].Length != TestWidthCells )
			{
				throw new InvalidOperationException(
					$"[Thorns FloorplanTest] rev={FloorplanRevision} {label} row[{i}] '{rows[i]}' length {rows[i].Length} != {TestWidthCells}." );
			}
		}
	}

	/// <summary>Ramp anchor must stay occupied even if a planner fallback lands on a marked opening cell.</summary>
	static void EnsureRampAnchorsWalkable(
		int w,
		int d,
		bool[] occupied,
		bool[] opening,
		IReadOnlyList<ThornsProcRampSpec> ramps )
	{
		if ( ramps is null )
			return;

		foreach ( var ramp in ramps )
		{
			var i = ramp.Story * w * d + ramp.Y * w + ramp.X;
			if ( i < 0 || i >= occupied.Length )
				continue;

			occupied[i] = true;
			opening[i] = false;
		}
	}

	static int CountOpeningsInArrays( int w, int d, int stories, bool[] opening )
	{
		if ( opening is null )
			return 0;

		var n = 0;
		for ( var s = 0; s < stories; s++ )
		for ( var y = 0; y < d; y++ )
		for ( var x = 0; x < w; x++ )
		{
			if ( opening[s * w * d + y * w + x] )
				n++;
		}

		return n;
	}

	public static int CountOccupiedCells( ThornsProcBuildingLayout layout )
	{
		if ( layout is null )
			return 0;

		var n = 0;
		for ( var s = 0; s < layout.Stories; s++ )
		for ( var y = 0; y < layout.DepthCells; y++ )
		for ( var x = 0; x < layout.WidthCells; x++ )
		{
			if ( layout.IsCellOccupied( s, x, y ) )
				n++;
		}

		return n;
	}

	public static int CountOpenings( ThornsProcBuildingLayout layout )
	{
		if ( layout is null )
			return 0;

		var n = 0;
		for ( var s = 0; s < layout.Stories; s++ )
		for ( var y = 0; y < layout.DepthCells; y++ )
		for ( var x = 0; x < layout.WidthCells; x++ )
		{
			if ( layout.IsOpeningCell( s, x, y ) )
				n++;
		}

		return n;
	}

	static void ParseStorey(
		string[] rows,
		int story,
		int w,
		int d,
		bool[] occupied,
		bool[] opening,
		List<CellPlacement> placements,
		ref int rampGx,
		ref int rampGy )
	{
		for ( var row = 0; row < rows.Length && row < d; row++ )
		{
			var gy = d - 1 - row;
			var line = rows[row];
			for ( var gx = 0; gx < w; gx++ )
			{
				var ch = gx < line.Length ? line[gx] : '.';
				var i = story * w * d + gy * w + gx;

				switch ( ch )
				{
					case '*':
						occupied[i] = false;
						opening[i] = true;
						break;
					case '.':
					case 'R':
						occupied[i] = true;
						if ( ch == 'R' && story == 0 )
						{
							rampGx = gx;
							rampGy = gy;
						}

						break;
					default:
						if ( ThornsInteriorFurnitureAsciiLayouts.FurnitureCharToId.TryGetValue( ch, out var id )
						     || FurnitureCharToId.TryGetValue( ch, out id ) )
						{
							occupied[i] = true;
							placements.Add( new CellPlacement
							{
								Story = story,
								GridX = gx,
								GridY = gy,
								StructureDefId = id
							} );
						}
						else
						{
							occupied[i] = false;
							Log.Warning(
								$"[Thorns FloorplanTest] rev={FloorplanRevision} unknown symbol '{ch}' story={story} ({gx},{gy})" );
						}

						break;
				}
			}
		}
	}

	public static string FormatLegend() =>
		$"rev={FloorplanRevision} | .=floor R=ramp *=open shaft | "
		+ "C=couch h=chair k=kitchen_fridge B=bed w=workbench d=desk | "
		+ "north door + windows elsewhere; shaft cells keep walls/roof";

	public static string FormatStoreyAscii( int story )
	{
		var rows = GetFloorRows( story );
		var sb = new StringBuilder();
		sb.Append( story switch { 0 => "Floor1", 1 => "Floor2", _ => "Floor3" } );
		sb.Append( " [" );
		for ( var i = 0; i < rows.Length; i++ )
		{
			if ( i > 0 )
				sb.Append( " | " );

			sb.Append( '\'' );
			sb.Append( rows[i] );
			sb.Append( '\'' );
		}

		sb.Append( ']' );
		return sb.ToString();
	}
}
