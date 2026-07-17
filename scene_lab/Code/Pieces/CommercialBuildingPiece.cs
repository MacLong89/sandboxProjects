namespace SceneLab;

/// <summary>
/// Legend-colored commercial buildings sized off <see cref="CityScale"/> house module.
/// All interiors walkable; multi-story via stairs.
/// Local +X = front (street), +Y = along frontage.
/// </summary>
public static class CommercialBuildingPiece
{
	public static GameObject Build( GameObject parent, Vector3 localPos, float yaw, BuildingKind kind )
	{
		var entry = BuildingLegend.Of( kind );
		var root = new GameObject( parent, true, PieceId( kind ) );
		root.LocalPosition = localPos.WithZ( MathF.Max( localPos.z, Depth.Sit ) );
		root.LocalRotation = Rotation.FromYaw( yaw );

		var body = KitBox.Solid( entry.Color );
		var trim = KitBox.Solid( entry.Color, 0.75f );
		var dark = Palette.MetalDark;
		var glass = Palette.CarGlass;
		var floorC = KitBox.Solid( entry.Color, 0.55f );

		return kind switch
		{
			BuildingKind.Skyscraper => BuildSkyscraper( root, body, trim, glass, dark, floorC ),
			BuildingKind.Office => BuildOffice( root, body, trim, glass, dark, floorC ),
			BuildingKind.Apartment => BuildApartment( root, body, trim, glass, dark, floorC ),
			BuildingKind.Factory => BuildFactory( root, body, trim, dark, floorC ),
			_ => BuildWarehouse( root, body, trim, dark, floorC ),
		};
	}

	private static string PieceId( BuildingKind kind ) => kind switch
	{
		BuildingKind.Skyscraper => PieceIds.Skyscraper,
		BuildingKind.Office => PieceIds.Office,
		BuildingKind.Apartment => PieceIds.Apartment,
		BuildingKind.Factory => PieceIds.Factory,
		_ => PieceIds.Warehouse,
	};

	private static GameObject BuildSkyscraper( GameObject root, Color body, Color trim, Color glass, Color dark, Color floorC )
	{
		var w = CityScale.SkyscraperFront;
		var d = CityScale.SkyscraperDepth;
		var found = CityScale.HouseStory * 0.09f;
		const int floors = 8;
		var story = CityScale.SkyscraperStory;
		HouseShell.Foundation( root, d, w, found );

		var top = HouseShell.AccessibleStack( root, 0f, 0f, d, w, found, story, floors, body, floorC,
			groundFrontDoor: true, doorW: CityScale.DoorW, doorH: CityScale.DoorH,
			windowCols: 4, windowHFrac: 0.38f, windowTrim: trim );

		var front = d * 0.5f;
		HouseShell.OpenDoorTrim( root, front, 0f, found, CityScale.DoorW, MathF.Min( CityScale.DoorH, story * 0.7f ), trim, glass );

		for ( var f = 0; f < floors; f++ )
		{
			var z = found + f * story + story * 0.55f;
			KitBox.Box( root, "Band",
				new Vector3( front + Depth.Step, 0f, z ),
				new Vector3( 4f, w * 0.9f, 5f ),
				trim );
		}

		KitBox.Box( root, "Crown",
			new Vector3( 0f, 0f, top + CityScale.HouseStory * 0.1f ),
			new Vector3( d * 0.72f, w * 0.72f, CityScale.HouseStory * 0.2f ),
			trim );
		KitBox.Box( root, "Antenna",
			new Vector3( 0f, 0f, top + CityScale.HouseStory * 0.4f ),
			new Vector3( 8f, 8f, CityScale.HouseStory * 0.4f ),
			dark );
		return root;
	}

	private static GameObject BuildOffice( GameObject root, Color body, Color trim, Color glass, Color dark, Color floorC )
	{
		var w = CityScale.OfficeFront;
		var d = CityScale.OfficeDepth;
		var found = CityScale.HouseStory * 0.08f;
		const int floors = 4;
		var story = CityScale.OfficeStory;
		HouseShell.Foundation( root, d, w, found );

		var top = HouseShell.AccessibleStack( root, 0f, 0f, d, w, found, story, floors, body, floorC,
			groundFrontDoor: true, doorW: CityScale.DoorW, doorH: CityScale.DoorH,
			windowCols: 5, windowHFrac: 0.40f, windowTrim: trim );

		var front = d * 0.5f;
		HouseShell.OpenDoorTrim( root, front, 0f, found, CityScale.DoorW, MathF.Min( CityScale.DoorH, story * 0.7f ), trim, glass );

		KitBox.Box( root, "Parapet",
			new Vector3( 0f, 0f, top + 8f ),
			new Vector3( d + 12f, w + 12f, 12f ),
			trim );
		KitBox.Box( root, "EntryCanopy",
			new Vector3( front + CityScale.HouseDepth * 0.07f, 0f, found + story * 0.55f ),
			new Vector3( CityScale.HouseDepth * 0.14f, w * 0.32f, 10f ),
			dark );
		return root;
	}

	private static GameObject BuildApartment( GameObject root, Color body, Color trim, Color glass, Color dark, Color floorC )
	{
		var w = CityScale.ApartmentFront;
		var d = CityScale.ApartmentDepth;
		var found = CityScale.HouseStory * 0.08f;
		const int floors = 4;
		var story = CityScale.ApartmentStory;
		HouseShell.Foundation( root, d, w, found );

		var top = HouseShell.AccessibleStack( root, 0f, 0f, d, w, found, story, floors, body, floorC,
			groundFrontDoor: true, doorW: CityScale.DoorW, doorH: CityScale.DoorH,
			windowCols: 5, windowHFrac: 0.35f, windowTrim: trim );

		var front = d * 0.5f;
		HouseShell.OpenDoorTrim( root, front, 0f, found, CityScale.DoorW, MathF.Min( CityScale.DoorH, story * 0.7f ), dark, dark );

		for ( var floor = 0; floor < floors; floor++ )
		{
			var z = found + floor * story + story * 0.42f;
			KitBox.Box( root, "Balcony",
				new Vector3( front + 14f, 0f, z ),
				new Vector3( CityScale.HouseDepth * 0.06f, w * 0.86f, 6f ),
				trim );
			for ( var col = -2; col <= 2; col++ )
			{
				KitBox.Box( root, "Rail",
					new Vector3( front + 22f, col * (w * 0.15f), z + 12f ),
					new Vector3( 4f, w * 0.09f, 16f ),
					dark );
			}
		}

		KitBox.Box( root, "Roof",
			new Vector3( 0f, 0f, top + 8f ),
			new Vector3( d + 10f, w + 10f, 12f ),
			trim );
		return root;
	}

	private static GameObject BuildFactory( GameObject root, Color body, Color trim, Color dark, Color floorC )
	{
		var w = CityScale.FactoryFront;
		var d = CityScale.FactoryDepth;
		var found = CityScale.HouseStory * 0.07f;
		var story = CityScale.FactoryStory;
		HouseShell.Foundation( root, d, w, found );

		var top = HouseShell.AccessibleStack( root, 0f, 0f, d, w, found, story, floors: 1, body, floorC,
			groundFrontDoor: false, groundOpenFront: true, doorW: CityScale.DoorW, doorH: CityScale.DoorH );

		for ( var i = -1; i <= 1; i++ )
		{
			KitBox.Box( root, "Monitor",
				new Vector3( i * (d * 0.22f), 0f, top + 16f ),
				new Vector3( d * 0.2f, w * 0.85f, CityScale.HouseStory * 0.15f ),
				trim );
		}

		foreach ( var sy in new[] { -0.28f, 0.28f } )
		{
			KitBox.CollidingBox( root, "Stack",
				new Vector3( -d * 0.2f, sy * w, top + CityScale.HouseStory * 0.35f ),
				new Vector3( CityScale.HouseFront * 0.05f, CityScale.HouseFront * 0.05f, CityScale.HouseStory * 0.65f ),
				dark );
		}

		var front = d * 0.5f;
		for ( var i = -1; i <= 1; i++ )
		{
			KitBox.Box( root, "BayTrim",
				new Vector3( front + Depth.Step, i * (w * 0.28f), found + story * 0.82f ),
				new Vector3( 5f, w * 0.18f, 10f ),
				dark );
		}

		return root;
	}

	private static GameObject BuildWarehouse( GameObject root, Color body, Color trim, Color dark, Color floorC )
	{
		var w = CityScale.WarehouseFront;
		var d = CityScale.WarehouseDepth;
		var found = CityScale.HouseStory * 0.07f;
		var story = CityScale.WarehouseStory;
		var officeW = CityScale.HouseFront * 0.18f;
		var officeD = CityScale.HouseDepth * 0.25f;
		var officeH = story * 0.75f;
		var hallCy = 0f;
		var officeCy = HouseShell.AbutY( hallCy, w, officeW, -1f );
		var officeX = -d * 0.05f;
		var passX = officeX;
		var buried = found + Depth.Step;

		KitBox.CollidingBox( root, "Foundation",
			new Vector3( 0f, -officeW * 0.5f, buried * 0.5f - Depth.Step ),
			new Vector3( d + 20f, w + officeW + 20f, buried ),
			Palette.HouseFoundation );

		HouseShell.HollowRoom( root, "Hall", 0f, hallCy, d, w, found, story, body, floorC,
			openFront: true,
			openSideSign: -1f, openPassW: CityScale.DoorW, openPassX: passX );

		HouseShell.HollowRoom( root, "Office", officeX, officeCy, officeD, officeW, found, officeH,
			KitBox.Solid( body, 0.85f ), floorC,
			frontDoor: true,
			doorW: CityScale.DoorW, doorH: MathF.Min( CityScale.DoorH, officeH * 0.7f ),
			omitSideSign: 1f );

		var top = found + story;
		KitBox.Box( root, "Roof",
			new Vector3( 0f, hallCy, top + 10f ),
			new Vector3( d + 18f, w + 18f, 14f ),
			trim );
		KitBox.Box( root, "OfficeRoof",
			new Vector3( officeX, officeCy, found + officeH + 8f ),
			new Vector3( officeD + 12f, officeW + 12f, 10f ),
			trim );

		var front = d * 0.5f;
		for ( var i = -1; i <= 1; i++ )
		{
			KitBox.Box( root, "RollTrim",
				new Vector3( front + Depth.Step, hallCy + i * (w * 0.26f), found + story * 0.85f ),
				new Vector3( 5f, w * 0.22f, 10f ),
				dark );
			KitBox.CollidingBox( root, "Dock",
				new Vector3( front + CityScale.HouseDepth * 0.055f, hallCy + i * (w * 0.26f), found * 0.5f + 4f ),
				new Vector3( CityScale.HouseDepth * 0.09f, w * 0.23f, found + 6f ),
				Palette.HouseFoundation );
		}

		var officeFront = officeX + officeD * 0.5f;
		HouseShell.OpenDoorTrim( root, officeFront, officeCy, found, CityScale.DoorW * 0.9f,
			MathF.Min( CityScale.DoorH, officeH * 0.7f ), trim, dark );
		return root;
	}
}
