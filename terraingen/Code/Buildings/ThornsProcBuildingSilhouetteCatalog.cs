namespace Terraingen.Buildings;

/// <summary>Per-archetype roof silhouette and exterior wall panel rules (ramps/door stay canonical).</summary>
public static class ThornsProcBuildingSilhouetteCatalog
{
	public const float ExteriorFootprintInches = 318f;

	/// <summary>Roof surface extends past the wall line on every side (eave overhang).</summary>
	public const float RoofEaveOverhangInches = 16f;

	public static float RoofSurfaceFootprintInches =>
		ExteriorFootprintInches + RoofEaveOverhangInches * 2f;

	public const int DoorSide = ThornsProcBuildingInterior.DoorSide;
	public const int DoorCell = ThornsProcBuildingInterior.DoorIndex;

	/// <summary>North wall exterior plane (+Y), matching proc wall spawn geometry.</summary>
	public static float NorthWallExteriorY =>
		150f + ThornsBuildingModule.WallThickness * 0.5f;

	public static float GroundDoorTopLocalZ =>
		ThornsBuildingModule.FloorThickness * 0.5f + ThornsBuildingModule.DoorHeight;

	public static float StorefrontAwningWidthInches => ExteriorFootprintInches - 10f;
	public const float StorefrontAwningDepthInches = 48f;

	public enum RoofStyle
	{
		FlatSlab,
		FlatParapet,
		Gable,
		Shed,
		Penthouse,
		Sawtooth,
		RuinPartial
	}

	public enum WallPanelKind
	{
		Solid,
		Window,
		Door,
		Omitted
	}

	public readonly record struct RoofSpec(
		RoofStyle Style,
		float GableRiseInches = 48f,
		float ParapetHeightInches = 14f,
		float PenthouseHeightInches = 55f,
		float PenthouseScale = 0.42f,
		float SawtoothRiseInches = 28f,
		int SawtoothSegments = 3 )
	{
		public static RoofSpec For( ThornsProcBuildingType type ) => type switch
		{
			ThornsProcBuildingType.House => new RoofSpec( RoofStyle.Gable, GableRiseInches: 44f ),
			ThornsProcBuildingType.Cabin => new RoofSpec( RoofStyle.Shed, GableRiseInches: 38f ),
			ThornsProcBuildingType.Barn => new RoofSpec( RoofStyle.Gable, GableRiseInches: 62f ),
			ThornsProcBuildingType.Apartment => new RoofSpec( RoofStyle.Gable, GableRiseInches: 36f ),
			ThornsProcBuildingType.Store => new RoofSpec( RoofStyle.FlatSlab ),
			ThornsProcBuildingType.Warehouse => new RoofSpec( RoofStyle.FlatParapet, ParapetHeightInches: 12f ),
			ThornsProcBuildingType.Factory => new RoofSpec( RoofStyle.Sawtooth, SawtoothRiseInches: 32f, SawtoothSegments: 3 ),
			ThornsProcBuildingType.MilitaryComplex => new RoofSpec( RoofStyle.FlatParapet, ParapetHeightInches: 18f ),
			ThornsProcBuildingType.RadioOutpost => new RoofSpec( RoofStyle.FlatParapet, ParapetHeightInches: 10f ),
			ThornsProcBuildingType.Skyscraper => new RoofSpec(
				RoofStyle.Penthouse,
				PenthouseHeightInches: 72f,
				PenthouseScale: 0.38f ),
			ThornsProcBuildingType.ApartmentTower => new RoofSpec(
				RoofStyle.Penthouse,
				PenthouseHeightInches: 58f,
				PenthouseScale: 0.4f ),
			ThornsProcBuildingType.OfficeBuilding => new RoofSpec(
				RoofStyle.Penthouse,
				PenthouseHeightInches: 48f,
				PenthouseScale: 0.44f ),
			ThornsProcBuildingType.Ruin => new RoofSpec( RoofStyle.RuinPartial, GableRiseInches: 28f ),
			_ => new RoofSpec( RoofStyle.FlatSlab )
		};
	}

	public static WallPanelKind ResolveWallPanel(
		ThornsProcBuildingType type,
		int side,
		int cell,
		int story,
		int stories,
		int buildingIndex,
		int cellsAlongSide )
	{
		var doorCell = side is 0 or 2
			? ThornsProcBuildingInterior.DoorCellForWidth( cellsAlongSide )
			: ThornsProcBuildingInterior.DoorCellForWidth( cellsAlongSide );

		if ( story == 0 && side == DoorSide && cell == doorCell )
			return WallPanelKind.Door;

		return type switch
		{
			ThornsProcBuildingType.Warehouse => ResolveIndustrialWall( side, cell, story, stories ),
			ThornsProcBuildingType.Factory => ResolveFactoryWall( side, cell, story, stories ),
			ThornsProcBuildingType.MilitaryComplex => ResolveMilitaryWall( side, cell, story ),
			ThornsProcBuildingType.RadioOutpost => ResolveRadioWall( side, cell, story ),
			ThornsProcBuildingType.Barn => ResolveBarnWall( side, cell, story ),
			ThornsProcBuildingType.Store => ResolveStoreWall( side, cell, story ),
			ThornsProcBuildingType.Skyscraper or ThornsProcBuildingType.ApartmentTower or ThornsProcBuildingType.OfficeBuilding
				=> WallPanelKind.Window,
			ThornsProcBuildingType.Ruin => ResolveRuinWall( side, cell, story, buildingIndex ),
			ThornsProcBuildingType.Cabin => story == 0 && side != DoorSide && cell == 1
				? WallPanelKind.Window
				: ResolveResidentialWindow( side, cell, story ),
			_ => ResolveResidentialWindow( side, cell, story )
		};
	}

	static WallPanelKind ResolveIndustrialWall( int side, int cell, int story, int stories )
	{
		if ( story == 0 && side == DoorSide )
			return cell == DoorCell ? WallPanelKind.Door : WallPanelKind.Solid;

		if ( story >= stories - 1 && side != DoorSide && cell == 1 )
			return WallPanelKind.Window;

		return WallPanelKind.Solid;
	}

	static WallPanelKind ResolveFactoryWall( int side, int cell, int story, int stories )
	{
		if ( story == 0 && side == DoorSide )
			return cell == DoorCell ? WallPanelKind.Door : WallPanelKind.Solid;

		if ( story > 0 && cell == 1 && (side == 0 || side == 2) )
			return WallPanelKind.Window;

		return story == 0 ? WallPanelKind.Solid : WallPanelKind.Solid;
	}

	static WallPanelKind ResolveMilitaryWall( int side, int cell, int story )
	{
		if ( story == 0 && side == DoorSide )
			return cell == DoorCell ? WallPanelKind.Door : WallPanelKind.Solid;

		return story == 0 && cell == 1 && side is 0 or 1
			? WallPanelKind.Window
			: WallPanelKind.Solid;
	}

	static WallPanelKind ResolveRadioWall( int side, int cell, int story )
	{
		if ( story == 0 && side == DoorSide && cell == DoorCell )
			return WallPanelKind.Door;

		return story == 0 && cell == 1 ? WallPanelKind.Window : WallPanelKind.Solid;
	}

	static WallPanelKind ResolveBarnWall( int side, int cell, int story )
	{
		if ( story == 0 && side == DoorSide && cell == DoorCell )
			return WallPanelKind.Door;

		if ( story == 0 && side == 0 && cell == 1 )
			return WallPanelKind.Door;

		return story == 0 ? WallPanelKind.Solid : WallPanelKind.Solid;
	}

	static WallPanelKind ResolveStoreWall( int side, int cell, int story )
	{
		if ( story == 0 && side == DoorSide )
		{
			return cell switch
			{
				0 or 2 => WallPanelKind.Window,
				_ => WallPanelKind.Door
			};
		}

		return ResolveResidentialWindow( side, cell, story );
	}

	static WallPanelKind ResolveResidentialWindow( int side, int cell, int story )
	{
		if ( side == DoorSide && cell == DoorCell )
			return WallPanelKind.Solid;

		return (side + cell + story) % 2 == 0 || side != DoorSide
			? WallPanelKind.Window
			: WallPanelKind.Solid;
	}

	static WallPanelKind ResolveRuinWall( int side, int cell, int story, int buildingIndex )
	{
		var hash = HashCode.Combine( side, cell, story, buildingIndex, 0x7710001 );
		if ( (hash & 15) == 0 )
			return WallPanelKind.Omitted;

		return (hash & 3) == 0 ? WallPanelKind.Solid : WallPanelKind.Window;
	}

	public readonly record struct ExteriorPropSpec(
		Vector3 LocalOffset,
		Vector3 Size,
		string MaterialSlug,
		bool Solid = false );

	public static void CollectExteriorProps(
		ThornsProcBuildingType type,
		int stories,
		int buildingIndex,
		float roofBaseLocalZ,
		List<ExteriorPropSpec> into )
	{
		if ( into is null )
			return;

		switch ( type )
		{
			case ThornsProcBuildingType.RadioOutpost:
				into.Add( new ExteriorPropSpec(
					new Vector3( 0f, 0f, roofBaseLocalZ + 70f ),
					new Vector3( 12f, 12f, 140f ),
					"metal" ) );
				into.Add( new ExteriorPropSpec(
					new Vector3( 18f, 0f, roofBaseLocalZ + 120f ),
					new Vector3( 36f, 36f, 8f ),
					"sheet_metal" ) );
				break;

			case ThornsProcBuildingType.Factory:
				into.Add( new ExteriorPropSpec(
					new Vector3( -110f, 90f, roofBaseLocalZ + 55f ),
					new Vector3( 22f, 22f, 110f ),
					"stone_brick" ) );
				break;

			case ThornsProcBuildingType.Skyscraper:
			case ThornsProcBuildingType.ApartmentTower:
			case ThornsProcBuildingType.OfficeBuilding:
				AddRoofMechanicals( buildingIndex, roofBaseLocalZ, into, count: 3 );
				break;
		}
	}

	static void AddRoofMechanicals( int buildingIndex, float roofBaseLocalZ, List<ExteriorPropSpec> into, int count )
	{
		var offsets = new[]
		{
			new Vector3( -70f, -60f, roofBaseLocalZ + 8f ),
			new Vector3( 80f, 50f, roofBaseLocalZ + 8f ),
			new Vector3( -40f, 90f, roofBaseLocalZ + 8f ),
			new Vector3( 60f, -80f, roofBaseLocalZ + 8f )
		};

		for ( var i = 0; i < count && i < offsets.Length; i++ )
		{
			var slot = (buildingIndex + i) % offsets.Length;
			into.Add( new ExteriorPropSpec(
				offsets[slot],
				new Vector3( 28f, 20f, 14f ),
				"metal",
				Solid: false ) );
		}
	}
}
