namespace FinalOutpost;

/// <summary>
/// Daytime rival seed-plot visuals matching the player's starting walls + command post.
/// Buildings/recruits come from <see cref="RivalGarrison"/>.
/// </summary>
public static class RivalBaseVisual
{
	/// <summary>Full rival outpost: command post, perimeter walls, buildings, optional guard props.</summary>
	public static void BuildSeed(
		GameObject parent,
		Vector3 center,
		RivalGarrisonLayout layout,
		bool includeGuards,
		bool includeDefenseBuildings = true )
	{
		BuildCommandPost( parent, center );
		BuildWalls( parent, center );

		foreach ( var slot in layout.Buildings )
		{
			if ( !BuildableCatalog.TryGet( slot.Id, out var def ) )
				continue;
			if ( !includeDefenseBuildings && def.Role == BuildingRole.Defense )
				continue;
			BuildBuilding( parent, center, slot );
		}

		if ( !includeGuards ) return;

		var tint = new Color( 0.78f, 0.3f, 0.32f );
		foreach ( var slot in layout.Recruits )
		{
			var rp = center + slot.LocalOffset;
			rp.z = OutpostTerrain.SampleHeight( rp.x, rp.y );
			var go = new GameObject( parent, true, "RivalGuardVisual" );
			go.WorldPosition = rp;
			go.WorldRotation = Rotation.FromYaw(
				MathF.Atan2( -slot.LocalOffset.y, -slot.LocalOffset.x ) * (180f / MathF.PI) );
			var character = go.Components.Create<CharacterModel>();
			var def = RecruitWeapons.Get( slot.Weapon );
			character.Setup( tint * 0.9f, def.WorldModel, def.Hold, def.WeaponScale * 0.95f );
			character.Tick( Vector3.Zero, go.WorldRotation );
		}
	}

	static void BuildCommandPost( GameObject parent, Vector3 center )
	{
		var root = new GameObject( parent, true, "RivalCommandPost" );
		root.WorldPosition = center.WithZ( OutpostTerrain.SampleHeight( center.x, center.y ) );

		CommandPostVisual.Build( root, null );
	}

	static void BuildWalls( GameObject parent, Vector3 center )
	{
		var cell = GameConstants.CellSize;
		var h = GameConstants.WallHeight;
		var t = GameConstants.WallThickness;
		var groundZ = OutpostTerrain.SampleHeight( center.x, center.y );
		var min = -GameConstants.SegmentsPerSide / 2;
		var max = GameConstants.SegmentsPerSide / 2 - 1;

		void SpawnSeg( int cellX, int cellY, Vector3 size )
		{
			var local = new Vector3(
				(cellX + 0.5f) * cell,
				(cellY + 0.5f) * cell,
				h * 0.5f );
			var world = center + local;
			world.z = groundZ + local.z;
			var go = new GameObject( parent, true, "RivalWall" );
			go.WorldPosition = world;
			WallScaffoldVisual.Build( go, size, null, world );
		}

		for ( var i = min; i <= max; i++ )
		{
			SpawnSeg( i, max, new Vector3( cell, t, h ) );
			SpawnSeg( i, min, new Vector3( cell, t, h ) );
		}

		for ( var i = min + 1; i <= max - 1; i++ )
		{
			SpawnSeg( max, i, new Vector3( t, cell, h ) );
			SpawnSeg( min, i, new Vector3( t, cell, h ) );
		}

		void Corner( float x, float y, float ox, float oy )
		{
			var world = center + new Vector3( x, y, h * 0.5f );
			world.z = groundZ + h * 0.5f;
			var go = new GameObject( parent, true, "RivalWallCorner" );
			go.WorldPosition = world;
			WallScaffoldVisual.BuildCorner( go, t, h, ox, oy, null );
		}

		var rc = GameConstants.WallRingCenter;
		Corner( rc, rc, 1f, 1f );
		Corner( -rc, rc, -1f, 1f );
		Corner( rc, -rc, 1f, -1f );
		Corner( -rc, -rc, -1f, -1f );
	}

	static void BuildBuilding( GameObject parent, Vector3 center, RivalBuildingSlot slot )
	{
		var pos = center + slot.LocalOffset;
		pos.z = OutpostTerrain.SampleHeight( pos.x, pos.y );
		var go = new GameObject( parent, true, $"Rival_{slot.Id}" );
		go.WorldPosition = pos;
		BuildingVisual.Build( go, slot.Id, pos, includeRubble: false );
	}
}
