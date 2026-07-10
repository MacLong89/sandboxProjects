namespace Sandbox;

/// <summary>Hand-authored footprints per <see cref="ThornsProcBuildingType"/>.</summary>
public static class ThornsProcBuildingArchetypes
{
	public static bool TryBuild(
		ThornsProcBuildingType type,
		Random rnd,
		out bool[] occupied,
		out int w,
		out int d,
		out int stories,
		out ThornsProcBuildingIdentityMeta identity )
	{
		occupied = null;
		w = d = stories = 0;
		identity = null;
		var def = ThornsProcBuildingIdentityRegistry.Get( type );

		if ( type == ThornsProcBuildingType.Ruin )
		{
			if ( !TryBuild( ThornsProcBuildingType.House, rnd, out occupied, out w, out d, out stories, out identity ) )
				return false;

			identity = new ThornsProcBuildingIdentityMeta
			{
				Type = ThornsProcBuildingType.Ruin,
				District = identity.District,
				IsRuinVariant = true,
				Facade = identity.Facade,
				LootThemeId = identity.LootThemeId
			};
			ApplyRuinDamage( occupied, w, d, stories, rnd, identity.Facade );
			return true;
		}

		identity = new ThornsProcBuildingIdentityMeta
		{
			Type = type,
			District = ThornsProcBuildingDistrict.Mixed,
			Facade = new ThornsProcBuildingFacadePlan { WindowChance = def.ExteriorWindowChance },
			LootThemeId = (int)type
		};

		return type switch
		{
			ThornsProcBuildingType.Cabin => BuildCabin( rnd, def, out occupied, out w, out d, out stories, identity ),
			ThornsProcBuildingType.House => BuildHouse( rnd, def, out occupied, out w, out d, out stories, identity ),
			ThornsProcBuildingType.Store => BuildStore( rnd, def, out occupied, out w, out d, out stories, identity ),
			ThornsProcBuildingType.Warehouse => BuildWarehouse( rnd, def, out occupied, out w, out d, out stories, identity ),
			ThornsProcBuildingType.Barn => BuildBarn( rnd, def, out occupied, out w, out d, out stories, identity ),
			ThornsProcBuildingType.Apartment => BuildApartment( rnd, def, out occupied, out w, out d, out stories, identity ),
			ThornsProcBuildingType.Factory => BuildFactory( rnd, def, out occupied, out w, out d, out stories, identity ),
			ThornsProcBuildingType.MilitaryComplex => BuildMilitary( rnd, def, out occupied, out w, out d, out stories, identity ),
			ThornsProcBuildingType.RadioOutpost => BuildRadio( rnd, def, out occupied, out w, out d, out stories, identity ),
			_ => false
		};
	}

	static bool BuildCabin( Random rnd, ThornsProcBuildingTypeDefinition def, out bool[] occ, out int w, out int d, out int s, ThornsProcBuildingIdentityMeta id )
	{
		w = rnd.Next( def.WidthMin, def.WidthMax + 1 );
		d = rnd.Next( def.DepthMin, def.DepthMax + 1 );
		s = 1;
		occ = ThornsProcBuildingGridBake.NewOccupancy( s, w, d );
		ThornsProcBuildingGridBake.FillRect( occ, s, w, d, 0, 0, w - 1, d - 1 );
		return true;
	}

	static bool BuildHouse( Random rnd, ThornsProcBuildingTypeDefinition def, out bool[] occ, out int w, out int d, out int s, ThornsProcBuildingIdentityMeta id )
	{
		w = 4;
		d = 4;
		s = 2;
		occ = ThornsProcBuildingGridBake.NewOccupancy( s, w, d );
		ThornsProcBuildingGridBake.FillRect( occ, s, w, d, 0, 0, w - 1, d - 1 );
		id.Facade.WindowChance = def.ExteriorWindowChance;
		return true;
	}

	static bool BuildStore( Random rnd, ThornsProcBuildingTypeDefinition def, out bool[] occ, out int w, out int d, out int s, ThornsProcBuildingIdentityMeta id )
	{
		w = 6;
		d = 3;
		s = 1;
		occ = ThornsProcBuildingGridBake.NewOccupancy( s, w, d );
		ThornsProcBuildingGridBake.FillRect( occ, s, w, d, 0, 0, w - 1, d - 1 );
		id.Facade.PreferFrontWindows = true;
		id.Facade.WindowChance = def.ExteriorWindowChance;
		for ( var y = 0; y < d; y++ )
			id.Facade.AddForceWindow( 0, 0, 1, y );
		return true;
	}

	static bool BuildWarehouse( Random rnd, ThornsProcBuildingTypeDefinition def, out bool[] occ, out int w, out int d, out int s, ThornsProcBuildingIdentityMeta id )
	{
		w = rnd.Next( 8, 10 );
		d = rnd.Next( 4, 6 );
		s = rnd.Next( def.StoriesMin, def.StoriesMax + 1 );
		occ = ThornsProcBuildingGridBake.NewOccupancy( s, w, d );
		ThornsProcBuildingGridBake.FillRect( occ, s, w, d, 0, 0, w - 1, d - 1 );
		id.Facade.WindowChance = def.ExteriorWindowChance;
		return true;
	}

	static bool BuildBarn( Random rnd, ThornsProcBuildingTypeDefinition def, out bool[] occ, out int w, out int d, out int s, ThornsProcBuildingIdentityMeta id )
	{
		w = rnd.Next( 5, 7 );
		d = rnd.Next( 4, 6 );
		s = 1;
		occ = ThornsProcBuildingGridBake.NewOccupancy( s, w, d );
		ThornsProcBuildingGridBake.FillRect( occ, s, w, d, 0, 0, w - 1, d - 1 );
		id.Facade.WindowChance = def.ExteriorWindowChance;
		var doorX = w / 2;
		id.Facade.AddDoor( 0, 0, doorX, 0 );
		if ( w >= 5 )
			id.Facade.AddDoor( 0, 0, doorX - 1, 0 );
		return true;
	}

	static bool BuildApartment( Random rnd, ThornsProcBuildingTypeDefinition def, out bool[] occ, out int w, out int d, out int s, ThornsProcBuildingIdentityMeta id )
	{
		w = 5;
		d = 6;
		s = 3;
		occ = ThornsProcBuildingGridBake.NewOccupancy( s, w, d );
		ThornsProcBuildingGridBake.FillRect( occ, s, w, d, 0, 0, w - 1, d - 1 );
		id.Facade.WindowChance = def.ExteriorWindowChance;
		return true;
	}

	static bool BuildFactory( Random rnd, ThornsProcBuildingTypeDefinition def, out bool[] occ, out int w, out int d, out int s, ThornsProcBuildingIdentityMeta id )
	{
		w = rnd.Next( 7, 9 );
		d = rnd.Next( 5, 7 );
		s = rnd.Next( def.StoriesMin, def.StoriesMax + 1 );
		occ = ThornsProcBuildingGridBake.NewOccupancy( s, w, d );
		ThornsProcBuildingGridBake.FillRect( occ, s, w, d, 0, 0, w - 1, d - 1 );
		if ( s >= 2 )
		{
			for ( var x = 2; x < w - 2; x++ )
			for ( var y = 2; y < d - 2; y++ )
				ThornsProcBuildingGridBake.Set( occ, s, w, d, 1, x, y, false );
		}

		id.Facade.WindowChance = def.ExteriorWindowChance;
		return true;
	}

	static bool BuildMilitary( Random rnd, ThornsProcBuildingTypeDefinition def, out bool[] occ, out int w, out int d, out int s, ThornsProcBuildingIdentityMeta id )
	{
		w = rnd.Next( 9, 12 );
		d = w;
		s = rnd.Next( 1, 3 );
		occ = ThornsProcBuildingGridBake.NewOccupancy( s, w, d );
		ThornsProcBuildingGridBake.FillRect( occ, s, w, d, 0, 0, w - 1, d - 1 );
		var pad = w >= 10 ? 3 : 2;
		for ( var st = 0; st < s; st++ )
		for ( var x = pad; x < w - pad; x++ )
		for ( var y = pad; y < d - pad; y++ )
			ThornsProcBuildingGridBake.Set( occ, s, w, d, st, x, y, false );

		// Inner barracks block.
		var bx0 = pad + 1;
		var by0 = pad + 1;
		var bx1 = w - pad - 2;
		var by1 = d - pad - 2;
		if ( bx1 > bx0 && by1 > by0 )
			ThornsProcBuildingGridBake.FillRect( occ, s, w, d, bx0, by0, bx1, by1 );

		id.Facade.WindowChance = def.ExteriorWindowChance;
		for ( var side = 0; side < 4; side++ )
		{
			var mid = side is 0 or 2 ? w / 2 : d / 2;
			id.Facade.AddDoor( 0, side, side is 0 or 2 ? mid : 0, side is 0 or 2 ? 0 : mid );
		}

		return true;
	}

	static bool BuildRadio( Random rnd, ThornsProcBuildingTypeDefinition def, out bool[] occ, out int w, out int d, out int s, ThornsProcBuildingIdentityMeta id )
	{
		w = 4;
		d = 4;
		s = 2;
		occ = ThornsProcBuildingGridBake.NewOccupancy( s, w, d );
		ThornsProcBuildingGridBake.FillRect( occ, s, w, d, 0, 0, w - 1, d - 1 );
		id.Facade.WindowChance = def.ExteriorWindowChance;
		return true;
	}

	static void ApplyRuinDamage( bool[] occ, int w, int d, int stories, Random rnd, ThornsProcBuildingFacadePlan facade )
	{
		var cuts = rnd.Next( 3, 8 );
		for ( var i = 0; i < cuts; i++ )
		{
			var st = rnd.Next( 0, stories );
			var x = rnd.Next( 0, w );
			var y = rnd.Next( 0, d );
			ThornsProcBuildingGridBake.Set( occ, stories, w, d, st, x, y, false );
		}

		var facadeHoles = rnd.Next( 4, 12 );
		for ( var e = 0; e < facadeHoles; e++ )
		{
			var st = rnd.Next( 0, stories );
			for ( var attempt = 0; attempt < 32; attempt++ )
			{
				var side = rnd.Next( 0, 4 );
				var x = rnd.Next( 0, w );
				var y = rnd.Next( 0, d );
				if ( !IsExteriorShellEdge( occ, stories, w, d, st, side, x, y ) )
					continue;

				facade.OmitPiece( st, side, x, y );
				break;
			}
		}

		facade.WindowChance *= 0.35f;
	}

	static bool IsExteriorShellEdge( bool[] occ, int stories, int w, int d, int story, int side, int x, int y )
	{
		if ( !ThornsProcBuildingGridBake.Get( occ, stories, w, d, story, x, y ) )
			return false;

		return side switch
		{
			0 => y == 0 || !ThornsProcBuildingGridBake.Get( occ, stories, w, d, story, x, y - 1 ),
			2 => y == d - 1 || !ThornsProcBuildingGridBake.Get( occ, stories, w, d, story, x, y + 1 ),
			3 => x == 0 || !ThornsProcBuildingGridBake.Get( occ, stories, w, d, story, x - 1, y ),
			1 => x == w - 1 || !ThornsProcBuildingGridBake.Get( occ, stories, w, d, story, x + 1, y ),
			_ => false
		};
	}

	/// <summary>Interior walls applied after draft layout exists (room carving).</summary>
	public static void ApplyInteriorLayout( ThornsProcBuildingLayout layout, ThornsProcBuildingType type, Random rnd )
	{
		var w = layout.WidthCells;
		var d = layout.DepthCells;
		var plan = ThornsProcBuildingWallPlan.Empty( layout.Stories, w, d );

		switch ( type )
		{
			case ThornsProcBuildingType.House:
			case ThornsProcBuildingType.Ruin:
				for ( var st = 0; st < layout.Stories; st++ )
				{
					ThornsProcBuildingGridBake.WallEast( plan, st, 1, 0 );
					ThornsProcBuildingGridBake.WallEast( plan, st, 1, 1 );
					ThornsProcBuildingGridBake.WallEast( plan, st, 1, 2 );
					ThornsProcBuildingGridBake.WallEast( plan, st, 1, 3 );
					ThornsProcBuildingGridBake.WallNorth( plan, st, 0, 1 );
					ThornsProcBuildingGridBake.WallNorth( plan, st, 1, 1 );
					ThornsProcBuildingGridBake.WallNorth( plan, st, 2, 1 );
					ThornsProcBuildingGridBake.WallNorth( plan, st, 3, 1 );
				}

				break;

			case ThornsProcBuildingType.Cabin:
				ThornsProcBuildingGridBake.WallEast( plan, 0, 1, 0 );
				ThornsProcBuildingGridBake.WallEast( plan, 0, 1, 1 );
				if ( d >= 4 )
					ThornsProcBuildingGridBake.WallEast( plan, 0, 1, 2 );
				break;

			case ThornsProcBuildingType.Store:
				for ( var y = 0; y < d; y++ )
					ThornsProcBuildingGridBake.WallEast( plan, 0, 2, y );
				break;

			case ThornsProcBuildingType.Apartment:
				for ( var st = 0; st < layout.Stories; st++ )
				for ( var y = 0; y < d; y++ )
				{
					ThornsProcBuildingGridBake.WallEast( plan, st, 0, y );
					if ( w >= 5 )
						ThornsProcBuildingGridBake.WallEast( plan, st, 3, y );
				}

				break;

			case ThornsProcBuildingType.RadioOutpost:
				ThornsProcBuildingGridBake.WallEast( plan, 0, 1, 1 );
				ThornsProcBuildingGridBake.WallEast( plan, 0, 1, 2 );
				ThornsProcBuildingGridBake.WallNorth( plan, 0, 1, 1 );
				ThornsProcBuildingGridBake.WallNorth( plan, 0, 2, 1 );
				if ( layout.Stories > 1 )
				{
					ThornsProcBuildingGridBake.WallEast( plan, 1, 1, 1 );
					ThornsProcBuildingGridBake.WallNorth( plan, 1, 1, 1 );
				}

				break;
		}

		if ( ThornsProcBuildingConnectivity.IsFullyReachableFromDoor( layout, plan, layout.DoorSide, layout.DoorIndex ) )
			layout.InteriorWalls = plan;
	}
}
