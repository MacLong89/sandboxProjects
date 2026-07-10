namespace Sandbox;

/// <summary>
/// Placeable furniture catalog. <see cref="GetWorldSizeInches"/> is the final world size (inches) — gallery, proc interiors, and player kits match 1:1.
/// </summary>
public static class ThornsPlaceableFurnitureCatalog
{
	/// <summary>Proc-interior cabinet mesh scale (building-local inches, all axes).</summary>
	static readonly Vector3 CabinetInteriorLocalScale = new( 97.836f, 74.805f, 89.366f );

	public enum InteriorPlacementStyle
	{
		WallBacked,
		CenterTile
	}

	/// <summary>How <see cref="Entry.InteriorPlacementLocalOffsetInches"/> is applied after the anchor point is chosen.</summary>
	public enum InteriorPlacementOffsetSpace
	{
		/// <summary>+X / +Y follow the proc building root (same axes as <see cref="ThornsProcBuildingLayout.GridAxisLocalX"/>).</summary>
		BuildingLocal,
		/// <summary>Offset rotates with the prop yaw (+Y = into the room for wall-backed yaw).</summary>
		FurnitureLocal
	}

	/// <param name="WorldSizeInches">Target size in world inches: X width, Y depth, Z height.</param>
	/// <param name="InteriorPlacementLocalOffsetInches">Nudge from anchor (cell center or wall slot), inches — see <see cref="GetInteriorPlacementLocalOffsetInches"/>.</param>
	public readonly record struct Entry(
		string StructureDefId,
		string KitItemId,
		string ModelPath,
		InteriorPlacementStyle PlacementStyle,
		float PlanarHalfAlongWall,
		float PlanarHalfFromWall,
		float PlanarHalfCenterTile,
		Vector3 WorldSizeInches,
		bool ExcludeFromProcInteriorScatter = false,
		bool AllowPlayerKitPlacement = true,
		ThornsCraftingRecipes.ThornsCraftIngredient[] CraftIngredients = null,
		float InteriorDecorYawOffsetDegrees = 0f,
		Vector3 InteriorPlacementLocalOffsetInches = default,
		InteriorPlacementOffsetSpace InteriorPlacementOffsetSpace = InteriorPlacementOffsetSpace.BuildingLocal );

	/// <summary>Default target size until tuned per prop (width X, depth Y, height Z).</summary>
	public static readonly Vector3 DefaultWorldSizeInches = new( 100f, 100f, 100f );

	/// <summary>Shorthand for catalog world sizes (inches).</summary>
	public static Vector3 Inches( float width, float depth, float height ) => new( width, depth, height );

	/// <summary>Authoritative building-local pose for a tuned corner cell (floorplan export).</summary>
	public readonly record struct InteriorPlacementTune(
		float BuildingLocalYawDegrees,
		Vector3 OffsetFromCellCenterBuildingLocal,
		Vector3? LocalScaleOverride = null );

	public static readonly Entry[] All =
	[
		Craftable(
			"chair",
			"chair_kit",
			"models/placeables/chair.vmdl",
			InteriorPlacementStyle.WallBacked,
			22f, 20f, 24f,
			[ new( "wood", 18 ), new( "cloth", 4 ) ] ),
		Craftable(
			"couch",
			"couch_kit",
			"models/placeables/couch.vmdl",
			InteriorPlacementStyle.WallBacked,
			42f, 26f, 44f,
			[ new( "wood", 35 ), new( "cloth", 12 ) ],
			interiorDecorYawOffsetDegrees: -90f ),
		Craftable(
			"cabinet",
			"cabinet_kit",
			"models/placeables/cabinet.vmdl",
			InteriorPlacementStyle.WallBacked,
			28f, 22f, 30f,
			[ new( "wood", 24 ), new( "metal", 6 ) ] ),
		Craftable(
			"fridge",
			"fridge_kit",
			"models/placeables/fridge.vmdl",
			InteriorPlacementStyle.WallBacked,
			26f, 26f, 30f,
			[ new( "metal", 28 ), new( "stone", 8 ) ],
			interiorDecorYawOffsetDegrees: 0f ),
		Craftable(
			"kitchen_fridge",
			"kitchen_fridge_kit",
			"models/placeables/kitchen_fridge.vmdl",
			InteriorPlacementStyle.WallBacked,
			46f, 26f, 48f,
			[ new( "wood", 30 ), new( "metal", 14 ), new( "stone", 10 ) ],
			interiorDecorYawOffsetDegrees: 90f ),
		Craftable(
			"bed",
			"bed_kit",
			"models/placeables/bed.vmdl",
			InteriorPlacementStyle.WallBacked,
			44f, 28f, 46f,
			[ new( "wood", 40 ), new( "cloth", 10 ) ] ),
		Craftable(
			"workbench",
			"workbench_kit",
			"models/placeables/workbench.vmdl",
			InteriorPlacementStyle.WallBacked,
			34f, 26f, 36f,
			[ new( "metal", 22 ), new( "wood", 35 ), new( "cloth", 10 ) ] ),

		ProcOnly(
			"desk",
			"models/placeables/desk.vmdl",
			InteriorPlacementStyle.WallBacked,
			36f, 23f, 37f ),
		ProcOnly(
			"dining_table",
			"models/placeables/dining_table.vmdl",
			InteriorPlacementStyle.CenterTile,
			41f, 41f, 52f ),
		ProcOnly(
			"conference",
			"models/placeables/conference.vmdl",
			InteriorPlacementStyle.CenterTile,
			54f, 54f, 58f ),
		ProcOnly(
			"bunk",
			"models/placeables/bunk.vmdl",
			InteriorPlacementStyle.WallBacked,
			48f, 31f, 50f,
			interiorDecorYawOffsetDegrees: 180f ),
		ProcOnly(
			"military_supply",
			"models/placeables/military_supply.vmdl",
			InteriorPlacementStyle.WallBacked,
			46f, 32f, 48f ),
		ProcOnly(
			"pallets",
			"models/placeables/pallets.vmdl",
			InteriorPlacementStyle.CenterTile,
			46f, 46f, 56f ),
		ProcOnly(
			"retail",
			"models/placeables/retail.vmdl",
			InteriorPlacementStyle.WallBacked,
			54f, 30f, 54f,
			interiorDecorYawOffsetDegrees: 90f ),
		ProcOnly(
			"radio",
			"models/placeables/radio.vmdl",
			InteriorPlacementStyle.WallBacked,
			52f, 30f, 54f ),

		PlayerOnly(
			"storage_chest",
			"storage_chest_kit",
			"models/placeables/chest.vmdl",
			InteriorPlacementStyle.WallBacked,
			24f, 22f, 26f,
			[ new( "wood", 55 ) ] ),
		PlayerOnly(
			"campfire",
			"campfire_kit",
			"models/placeables/campfire.vmdl",
			InteriorPlacementStyle.CenterTile,
			18f, 18f, 20f,
			[ new( "wood", 28 ), new( "stone", 8 ) ] )
	];

	/// <summary>
	/// Authoritative world size (inches) for gallery, player-placed kits, build ghost, and proc interior furniture.
	/// Edit sizes here only — every path reads this method.
	/// </summary>
	public static Vector3 GetWorldSizeInches( string structureDefId )
	{
		var id = NormalizeStructureId( structureDefId );
		if ( string.IsNullOrEmpty( id ) )
			return DefaultWorldSizeInches;

		// Explicit switch so hotload picks up size edits (static dictionary init does not always refresh).
		return id switch
		{
			"chair" => Inches( 50f, 50f, 50f ),
			"couch" => Inches( 140f, 140f, 60f ),
			"cabinet" => Inches( 120f, 40f, 50f ),
			"fridge" => Inches( 60f, 60f, 85f ),
			"kitchen_fridge" => Inches( 175f, 50f, 100f ),
			"bed" => Inches( 80f, 100f, 50f ),
			"workbench" => Inches( 100f, 70f, 80f ),
			"desk" => Inches( 100f, 70f, 70f ),
			"dining_table" => Inches( 100f, 100f, 50f ),
			"conference" => Inches( 100f, 100f, 50f ),
			"bunk" => Inches( 100f, 70f, 100f ),
			"pallets" => Inches( 120f, 120f, 100f ),
			"retail" => Inches( 125f, 24f, 100f ),
			"radio" => Inches( 100f, 70f, 80f ),
			"military_supply" => Inches( 100f, 100f, 100f ),
			_ => DefaultWorldSizeInches
		};
	}

	/// <summary>
	/// Proc-interior nudge from the placement anchor (grid cell center or wall slot), inches — XY only (Z comes from the floor plane).
	/// Edit here — applied on every scatter path via <see cref="ApplyInteriorPlacementOffset"/>.
	/// Building-local +X/+Y match the building root; use <see cref="GetInteriorPlacementOffsetSpace"/> for wall-flush props.
	/// </summary>
	public static Vector3 GetInteriorPlacementLocalOffsetInches( string structureDefId )
	{
		var id = NormalizeStructureId( structureDefId );
		if ( string.IsNullOrEmpty( id ) )
			return Vector3.Zero;

		return id switch
		{
			"couch" => PlanarPlacementOffset( 25f, 25f ),
			"pallets" => PlanarPlacementOffset( 25f, 25f ),
			"conference" => PlanarPlacementOffset( 25f, 25f ),
			"fridge" => Vector3.Zero,
			"kitchen_fridge" => PlanarPlacementOffset( 20f, -50f ),
			"bed" => PlanarPlacementOffset( 0f, -5f ),
			"bunk" => PlanarPlacementOffset( 80f, 110f ),
			"military_supply" => PlanarPlacementOffset( 14f, 14f ),
			"dining_table" => PlanarPlacementOffset( 8f, 8f ),
			"desk" => PlanarPlacementOffset( 8f, 8f ),
			"cabinet" => PlanarPlacementOffset( -20f, 20f ),
			"retail" => Vector3.Zero,
			_ => Vector3.Zero
		};
	}

	static Vector3 PlanarPlacementOffset( float xInches, float yInches ) => new( xInches, yInches, 0f );

	static Vector3 PlanarPlacementOffset( Vector3 offsetInches ) =>
		new( offsetInches.x, offsetInches.y, 0f );

	public static InteriorPlacementOffsetSpace GetInteriorPlacementOffsetSpace( string structureDefId )
	{
		var id = NormalizeStructureId( structureDefId );
		if ( string.IsNullOrEmpty( id ) )
			return InteriorPlacementOffsetSpace.BuildingLocal;

		return id switch
		{
			"kitchen_fridge" => InteriorPlacementOffsetSpace.BuildingLocal,
			_ => InteriorPlacementOffsetSpace.BuildingLocal
		};
	}

	/// <summary>Fixed building-root local pose for tuned corner cells (3×3 floorplan exports).</summary>
	public static bool TryGetInteriorPlacementTune(
		string structureDefId,
		int storyIndex,
		int gridX,
		int gridY,
		int widthCells,
		int depthCells,
		out InteriorPlacementTune tune )
	{
		tune = default;
		var id = NormalizeStructureId( structureDefId );
		if ( string.IsNullOrEmpty( id ) || widthCells < 1 || depthCells < 1 )
			return false;

		var maxX = widthCells - 1;
		var maxY = depthCells - 1;

		// Level 3 cabinet — SE corner; building-local pos (80, -120) on 3×3 @ cell=100.
		if ( storyIndex >= 2
		     && gridX == maxX
		     && gridY == 0
		     && string.Equals( id, "cabinet", StringComparison.OrdinalIgnoreCase ) )
		{
			tune = new InteriorPlacementTune(
				180f,
				PlanarPlacementOffset( -20f, -20f ),
				CabinetInteriorLocalScale );
			return true;
		}

		if ( !ThornsInteriorFurnitureCanonicalSlots.TryResolveCornerSlot( id, storyIndex, out var slot )
		     || slot.GridX != gridX
		     || slot.GridY != gridY )
			return false;

		if ( string.Equals( id, "retail", StringComparison.OrdinalIgnoreCase )
		     && TryBuildRetailInteriorPlacementTune(
			     gridX,
			     gridY,
			     widthCells,
			     depthCells,
			     out tune ) )
			return true;

		if ( storyIndex == 0 && gridX == maxX && gridY == maxY
		     && string.Equals( id, "kitchen_fridge", StringComparison.OrdinalIgnoreCase ) )
		{
			tune = new InteriorPlacementTune( 90f, PlanarPlacementOffset( 20f, -50f ) );
			return true;
		}

		if ( storyIndex == 0 && gridX == 0 && gridY == 0
		     && string.Equals( id, "pallets", StringComparison.OrdinalIgnoreCase ) )
		{
			tune = new InteriorPlacementTune( -90f, PlanarPlacementOffset( 25f, 25f ) );
			return true;
		}

		if ( storyIndex == 0 && gridX == 0 && gridY == 0
		     && string.Equals( id, "conference", StringComparison.OrdinalIgnoreCase ) )
		{
			tune = new InteriorPlacementTune( -90f, PlanarPlacementOffset( 25f, 25f ) );
			return true;
		}

		if ( storyIndex == 0 && gridX == 0 && gridY == 0
		     && string.Equals( id, "couch", StringComparison.OrdinalIgnoreCase ) )
		{
			tune = new InteriorPlacementTune( -90f, PlanarPlacementOffset( 25f, 25f ) );
			return true;
		}

		if ( storyIndex == 0 && gridX == 0 && gridY == maxY
		     && string.Equals( id, "chair", StringComparison.OrdinalIgnoreCase ) )
		{
			tune = new InteriorPlacementTune( 180f, PlanarPlacementOffset( 8f, 8f ) );
			return true;
		}

		if ( gridX == 0 && gridY == 0
		     && storyIndex >= 1
		     && string.Equals( id, "desk", StringComparison.OrdinalIgnoreCase ) )
		{
			tune = new InteriorPlacementTune(
				0f,
				PlanarPlacementOffset( 8f, 8f ),
				new Vector3( 80f, 100f, 75f ) );
			return true;
		}

		if ( gridX == 0 && gridY == maxY
		     && storyIndex >= 1
		     && string.Equals( id, "bed", StringComparison.OrdinalIgnoreCase ) )
		{
			tune = new InteriorPlacementTune(
				-180f,
				PlanarPlacementOffset( 0f, -5f ),
				new Vector3( 112.33f, 102.024f, 108.163f ) );
			return true;
		}

		if ( storyIndex == 1 && gridX == maxX && gridY == maxY
		     && string.Equals( id, "cabinet", StringComparison.OrdinalIgnoreCase ) )
		{
			tune = new InteriorPlacementTune(
				-180f,
				PlanarPlacementOffset( -20f, 20f ),
				CabinetInteriorLocalScale );
			return true;
		}

		return false;
	}

	/// <summary>Retail NW corner — fixed building-local (-110, 70), yaw −90°.</summary>
	public static bool TryBuildRetailInteriorPlacementTune(
		int gridX,
		int gridY,
		int widthCells,
		int depthCells,
		out InteriorPlacementTune tune )
	{
		tune = default;
		if ( widthCells < 1 || depthCells < 1 )
			return false;

		if ( !ThornsProcBuildingInteriorSample.TryGridCellCenterLocalPublic(
			     gridX,
			     gridY,
			     widthCells,
			     depthCells,
			     ThornsBuildingModule.Cell,
			     out var cellCenterX,
			     out var cellCenterY ) )
			return false;

		tune = new InteriorPlacementTune(
			RetailBuildingLocalYawDegrees,
			PlanarPlacementOffset(
				RetailBuildingLocalX - cellCenterX,
				RetailBuildingLocalY - cellCenterY ) );
		return true;
	}

	/// <summary>Tuned corner cells keep authored building-local pose (no floor Z flatten before mesh align).</summary>
	public static bool UsesInteriorPlacementTunePose(
		string structureDefId,
		int storyIndex,
		int gridX,
		int gridY,
		int widthCells,
		int depthCells ) =>
		TryGetInteriorPlacementTune(
			structureDefId,
			storyIndex,
			gridX,
			gridY,
			widthCells,
			depthCells,
			out _ );

	/// <summary>Fixed building-root local yaw when no grid tune exists.</summary>
	public static bool TryGetInteriorPlacementBuildingLocalYaw( string structureDefId, out float yawDegrees )
	{
		yawDegrees = 0f;
		if ( TryGetInteriorPlacementTune( structureDefId, 0, 2, 2, 3, 3, out var tune )
		     && string.Equals( NormalizeStructureId( structureDefId ), "kitchen_fridge", StringComparison.OrdinalIgnoreCase ) )
		{
			yawDegrees = tune.BuildingLocalYawDegrees;
			return true;
		}

		return false;
	}

	public static bool TryGetInteriorPlacementLocalScale( string structureDefId, out Vector3 localScale )
	{
		localScale = Vector3.One;
		var id = NormalizeStructureId( structureDefId );
		if ( string.IsNullOrEmpty( id ) )
			return false;

		switch ( id )
		{
			case "desk":
				localScale = new Vector3( 80f, 100f, 75f );
				return true;
			case "bed":
				localScale = new Vector3( 112.33f, 102.024f, 108.163f );
				return true;
			case "bunk":
				localScale = new Vector3( 100f, 100f, 120f );
				return true;
			case "cabinet":
				localScale = CabinetInteriorLocalScale;
				return true;
			case "military_supply":
				localScale = new Vector3( 80f, 80f, 110f );
				return true;
			default:
				return false;
		}
	}

	/// <summary>Authoritative building-local XY for proc retail (3×3 @ cell=100).</summary>
	public const float RetailBuildingLocalX = -110f;

	public const float RetailBuildingLocalY = 70f;

	public const float RetailBuildingLocalYawDegrees = -90f;

	public const float RetailDoorAwayInches = 20f;
	/// <summary>Rendered retail depth exceeds catalog Y — use for corner wall clearance.</summary>
	public const float RetailPlacementDepthInches = 90f;
	/// <summary>Extra inches beyond rotated half-extent when seating on a corner wall.</summary>
	public const float RetailWallStandoffInches = 12f;
	/// <summary>Additional inset from each non-door side wall (user-tuned).</summary>
	public const float RetailTowardCenterFromSideWallExtraInches = 20f;
	public const float RetailTowardWallInches = 40f;

	/// <summary>
	/// Building-local furniture pivot: grid cell center XY + offset; Z on walkable floor top (same storey plane as interior loot crates).
	/// </summary>
	public static Vector3 BuildInteriorFurnitureLocalPosition(
		int storyIndex,
		float cellCenterLocalX,
		float cellCenterLocalY,
		Vector3 offsetFromCellCenterBuildingLocal )
	{
		var planar = PlanarPlacementOffset( offsetFromCellCenterBuildingLocal );
		return new Vector3(
			cellCenterLocalX + planar.x,
			cellCenterLocalY + planar.y,
			ThornsProcBuildingInteriorSample.InteriorFloorWalkLocalZForStory( storyIndex ) );
	}

	/// <summary>Legacy name — no global Z inset (offsets are applied in building-local space only).</summary>
	public static Vector3 OffsetFromCellCenterWithGlobalInset( Vector3 offsetFromCellCenterBuildingLocal ) =>
		offsetFromCellCenterBuildingLocal;

	/// <summary>World position after catalog anchor nudge (building-local or furniture-local).</summary>
	public static Vector3 ApplyInteriorPlacementOffset(
		GameObject buildingRoot,
		Vector3 worldPos,
		Rotation worldRotation,
		in Entry profile,
		in ThornsProcBuildingInteriorSample.InteriorPlacementHints hints = default,
		int widthCells = 0,
		int depthCells = 0 )
	{
		var pos = worldPos;
		var off = PlanarPlacementOffset( profile.InteriorPlacementLocalOffsetInches );
		if ( off.LengthSquared >= 1e-6f )
		{
			Vector3 delta;
			if ( profile.InteriorPlacementOffsetSpace == InteriorPlacementOffsetSpace.FurnitureLocal )
				delta = worldRotation * off;
			else if ( buildingRoot is not null && buildingRoot.IsValid() )
				delta = buildingRoot.WorldRotation * off;
			else
				delta = off;

			pos += delta;
		}

		if ( IsRetailStructure( profile.StructureDefId )
		     && buildingRoot is not null
		     && buildingRoot.IsValid()
		     && widthCells >= 1
		     && depthCells >= 1
		     && !( hints.PlacementGridX >= 0
		           && hints.PlacementGridY >= 0
		           && UsesInteriorPlacementTunePose(
			           profile.StructureDefId,
			           0,
			           hints.PlacementGridX,
			           hints.PlacementGridY,
			           widthCells,
			           depthCells ) ) )
		{
			pos += buildingRoot.WorldRotation * PlanarPlacementOffset(
				GetRetailContextualOffsetBuildingLocal(
					buildingRoot,
					pos,
					worldRotation,
					in profile,
					in hints,
					widthCells,
					depthCells ) );
		}

		return pos;
	}

	static bool IsRetailStructure( string structureDefId ) =>
		string.Equals( NormalizeStructureId( structureDefId ), "retail", StringComparison.OrdinalIgnoreCase );

	/// <summary>Corner wall clearance from rotated mesh footprint + door inset when on the door wall.</summary>
	static Vector3 GetRetailContextualOffsetBuildingLocal(
		GameObject buildingRoot,
		Vector3 worldPos,
		Rotation worldRotation,
		in Entry profile,
		in ThornsProcBuildingInteriorSample.InteriorPlacementHints hints,
		int widthCells,
		int depthCells )
	{
		if ( hints.PlacementGridX >= 0 && hints.PlacementGridY >= 0 )
		{
			var offset = GetRetailCornerWallClearanceOffsetBuildingLocal(
				hints.PlacementGridX,
				hints.PlacementGridY,
				widthCells,
				depthCells,
				buildingRoot,
				worldRotation,
				in profile );

			if ( hints.DoorSide is >= 0 and <= 3
			     && RetailCornerTouchesDoorWall(
				     hints.PlacementGridX,
				     hints.PlacementGridY,
				     widthCells,
				     depthCells,
				     hints.DoorSide ) )
			{
				offset += GetDoorAwayOffsetBuildingLocal( hints.DoorSide, RetailDoorAwayInches );
			}

			return offset;
		}

		var local = buildingRoot.WorldRotation.Inverse * (worldPos - buildingRoot.WorldPosition);
		var fallback = Vector3.Zero;
		if ( hints.DoorSide is >= 0 and <= 3
		     && TryGetClosestExteriorWallSide(
			     local.x,
			     local.y,
			     widthCells,
			     depthCells,
			     hints.DoorSide,
			     out var sideWall ) )
		{
			var inset = RetailWallStandoffInches
			            + RetailTowardCenterFromSideWallExtraInches
			            + GetRetailPlacementDepthInches( in profile ) * 0.5f;
			fallback += GetInwardFromExteriorWallOffsetBuildingLocal( sideWall, inset );
		}

		return fallback;
	}

	static Vector3 GetRetailCornerWallClearanceOffsetBuildingLocal(
		int gridX,
		int gridY,
		int widthCells,
		int depthCells,
		GameObject buildingRoot,
		Rotation worldRotation,
		in Entry profile )
	{
		var maxX = widthCells - 1;
		var maxY = depthCells - 1;
		var yawDeg = worldRotation.Angles().yaw - buildingRoot.WorldRotation.Angles().yaw;
		var footprint = GetRetailPlacementFootprintSize( in profile );
		var yawRad = yawDeg * ( MathF.PI / 180f );
		var hx = footprint.x * 0.5f;
		var hy = footprint.y * 0.5f;
		var halfExtentX = MathF.Abs( MathF.Cos( yawRad ) ) * hx + MathF.Abs( MathF.Sin( yawRad ) ) * hy;
		var halfExtentY = MathF.Abs( MathF.Sin( yawRad ) ) * hx + MathF.Abs( MathF.Cos( yawRad ) ) * hy;
		var margin = RetailWallStandoffInches + RetailTowardCenterFromSideWallExtraInches;
		var offset = Vector3.Zero;

		if ( gridX <= 0 )
			offset.x += halfExtentX + margin;
		else if ( gridX >= maxX )
			offset.x -= halfExtentX + margin;

		if ( gridY <= 0 )
			offset.y += halfExtentY + margin;
		else if ( gridY >= maxY )
			offset.y -= halfExtentY + margin;

		return offset;
	}

	static bool RetailCornerTouchesDoorWall(
		int gridX,
		int gridY,
		int widthCells,
		int depthCells,
		int doorSide )
	{
		var maxX = widthCells - 1;
		var maxY = depthCells - 1;
		return doorSide switch
		{
			0 => gridY <= 0,
			2 => gridY >= maxY,
			3 => gridX <= 0,
			1 => gridX >= maxX,
			_ => false
		};
	}

	static Vector3 GetRetailPlacementFootprintSize( in Entry profile )
	{
		var catalog = ThornsPlaceableFurnitureScale.EffectiveWorldSizeInches( in profile );
		return new Vector3(
			catalog.x,
			MathF.Max( catalog.y, GetRetailPlacementDepthInches( in profile ) ),
			catalog.z );
	}

	static float GetRetailPlacementDepthInches( in Entry profile ) =>
		MathF.Max( RetailPlacementDepthInches, profile.WorldSizeInches.y );

	static Vector3 GetDoorAwayOffsetBuildingLocal( int doorSide, float distanceInches ) =>
		doorSide switch
		{
			0 => new Vector3( 0f, -distanceInches, 0f ),
			2 => new Vector3( 0f, distanceInches, 0f ),
			3 => new Vector3( -distanceInches, 0f, 0f ),
			1 => new Vector3( distanceInches, 0f, 0f ),
			_ => Vector3.Zero
		};

	static bool TryGetClosestExteriorWallSide(
		float localX,
		float localY,
		int widthCells,
		int depthCells,
		int excludeSide,
		out int side )
	{
		var closestSide = -1;
		var cell = ThornsBuildingModule.Cell;
		var southY = ThornsProcBuildingInteriorSample.GridAxisLocalPublic( 0, depthCells, cell );
		var northY = ThornsProcBuildingInteriorSample.GridAxisLocalPublic( depthCells - 1, depthCells, cell );
		var westX = ThornsProcBuildingInteriorSample.GridAxisLocalPublic( 0, widthCells, cell );
		var eastX = ThornsProcBuildingInteriorSample.GridAxisLocalPublic( widthCells - 1, widthCells, cell );

		var best = float.MaxValue;
		Consider( 0, MathF.Abs( localY - southY ) );
		Consider( 2, MathF.Abs( northY - localY ) );
		Consider( 3, MathF.Abs( localX - westX ) );
		Consider( 1, MathF.Abs( eastX - localX ) );
		side = closestSide;
		return closestSide >= 0;

		void Consider( int candidateSide, float distance )
		{
			if ( excludeSide >= 0 && candidateSide == excludeSide )
				return;

			if ( distance >= best )
				return;

			best = distance;
			closestSide = candidateSide;
		}
	}

	static Vector3 GetInwardFromExteriorWallOffsetBuildingLocal( int side, float distanceInches ) =>
		side switch
		{
			0 => new Vector3( 0f, distanceInches, 0f ),
			2 => new Vector3( 0f, -distanceInches, 0f ),
			3 => new Vector3( distanceInches, 0f, 0f ),
			1 => new Vector3( -distanceInches, 0f, 0f ),
			_ => Vector3.Zero
		};

	public static Vector3 ResolveWorldSizeInches( Vector3 worldSizeInches ) =>
		worldSizeInches.LengthSquared < 1f ? DefaultWorldSizeInches : worldSizeInches;

	/// <summary>Applies <see cref="GetWorldSizeInches"/> (use instead of raw <see cref="All"/> rows).</summary>
	public static Entry ResolveEntry( in Entry entry )
	{
		var id = entry.StructureDefId;
		return entry with
		{
			WorldSizeInches = GetWorldSizeInches( id ),
			InteriorPlacementLocalOffsetInches = GetInteriorPlacementLocalOffsetInches( id ),
			InteriorPlacementOffsetSpace = GetInteriorPlacementOffsetSpace( id )
		};
	}

	/// <summary>Catalog row with authoritative <see cref="GetWorldSizeInches"/> — use for gallery, scatter, kits, and ghosts.</summary>
	public static bool TryCreateSizedEntry( string structureDefId, out Entry entry )
	{
		if ( !TryGet( structureDefId, out entry ) )
			return false;

		entry = ResolveEntry( in entry );
		return true;
	}

	static Entry Craftable(
		string id,
		string kit,
		string model,
		InteriorPlacementStyle style,
		float along,
		float from,
		float center,
		ThornsCraftingRecipes.ThornsCraftIngredient[] ingredients,
		float interiorDecorYawOffsetDegrees = 0f ) =>
		new(
			id,
			kit,
			model,
			style,
			along,
			from,
			center,
			GetWorldSizeInches( id ),
			ExcludeFromProcInteriorScatter: false,
			AllowPlayerKitPlacement: true,
			CraftIngredients: ingredients,
			InteriorDecorYawOffsetDegrees: interiorDecorYawOffsetDegrees );

	static Entry ProcOnly(
		string id,
		string model,
		InteriorPlacementStyle style,
		float along,
		float from,
		float center,
		Vector3 worldSizeInches = default,
		float interiorDecorYawOffsetDegrees = 0f ) =>
		new(
			id,
			KitItemId: "",
			model,
			style,
			along,
			from,
			center,
			ResolveWorldSizeInches( worldSizeInches ),
			ExcludeFromProcInteriorScatter: false,
			AllowPlayerKitPlacement: false,
			CraftIngredients: null,
			InteriorDecorYawOffsetDegrees: interiorDecorYawOffsetDegrees );

	static Entry PlayerOnly(
		string id,
		string kit,
		string model,
		InteriorPlacementStyle style,
		float along,
		float from,
		float center,
		ThornsCraftingRecipes.ThornsCraftIngredient[] ingredients,
		float interiorDecorYawOffsetDegrees = 0f ) =>
		new(
			id,
			kit,
			model,
			style,
			along,
			from,
			center,
			GetWorldSizeInches( id ),
			ExcludeFromProcInteriorScatter: true,
			AllowPlayerKitPlacement: true,
			CraftIngredients: ingredients,
			InteriorDecorYawOffsetDegrees: interiorDecorYawOffsetDegrees );

	public static IReadOnlyList<Entry> InteriorScatterEntries
	{
		get
		{
			var list = new List<Entry>( All.Length );
			foreach ( var e in All )
			{
				if ( !e.ExcludeFromProcInteriorScatter && e.StructureDefId != "radio" )
					list.Add( ResolveEntry( in e ) );
			}

			return list;
		}
	}

	public static bool RequiresFlushAgainstWall( string structureDefId ) =>
		string.Equals( structureDefId, "kitchen_fridge", StringComparison.OrdinalIgnoreCase );

	/// <summary>Player-facing label from a catalog structure id (e.g. <c>kitchen_fridge</c> → Kitchen Fridge).</summary>
	public static string FormatDisplayName( string structureDefId )
	{
		if ( string.IsNullOrWhiteSpace( structureDefId ) )
			return "";

		var normalized = structureDefId.Trim().Replace( '_', ' ' );
		var parts = normalized.Split( ' ', StringSplitOptions.RemoveEmptyEntries );
		if ( parts.Length == 0 )
			return normalized;

		for ( var i = 0; i < parts.Length; i++ )
			parts[i] = TitleCaseToken( parts[i] );

		return string.Join( ' ', parts );
	}

	static string TitleCaseToken( string token )
	{
		if ( string.IsNullOrEmpty( token ) )
			return token;

		if ( token.Length == 1 )
			return token.ToUpperInvariant();

		return char.ToUpperInvariant( token[0] ) + token[1..].ToLowerInvariant();
	}

	public static bool TryGet( string structureDefId, out Entry entry )
	{
		var needle = NormalizeStructureId( structureDefId );
		if ( string.IsNullOrEmpty( needle ) )
		{
			entry = default;
			return false;
		}

		foreach ( var e in All )
		{
			if ( string.Equals( NormalizeStructureId( e.StructureDefId ), needle, StringComparison.OrdinalIgnoreCase ) )
			{
				entry = ResolveEntry( in e );
				return true;
			}
		}

		entry = default;
		return false;
	}

	public static bool TryGetKit( string kitItemId, out Entry entry )
	{
		if ( string.IsNullOrEmpty( kitItemId ) )
		{
			entry = default;
			return false;
		}

		foreach ( var e in All )
		{
			if ( string.Equals( e.KitItemId, kitItemId, StringComparison.OrdinalIgnoreCase ) )
			{
				entry = ResolveEntry( in e );
				return true;
			}
		}

		entry = default;
		return false;
	}

	public static bool IsPortableKitStructureId( string structureDefId )
	{
		var needle = NormalizeStructureId( structureDefId );
		if ( string.IsNullOrEmpty( needle ) )
			return false;

		foreach ( var e in All )
		{
			if ( !e.AllowPlayerKitPlacement )
				continue;

			if ( string.Equals( NormalizeStructureId( e.StructureDefId ), needle, StringComparison.OrdinalIgnoreCase ) )
				return true;
		}

		return false;
	}

	public static bool IsPortableKitItemId( string itemId )
	{
		foreach ( var e in All )
		{
			if ( !e.AllowPlayerKitPlacement || string.IsNullOrEmpty( e.KitItemId ) )
				continue;

			if ( string.Equals( e.KitItemId, itemId, StringComparison.OrdinalIgnoreCase ) )
				return true;
		}

		return false;
	}

	static string NormalizeStructureId( string id )
	{
		if ( string.IsNullOrWhiteSpace( id ) )
			return "";

		var buffer = new char[id.Length];
		var n = 0;
		for ( var i = 0; i < id.Length; i++ )
		{
			var ch = char.ToLowerInvariant( id[i] );
			if ( ch is >= 'a' and <= 'z' or >= '0' and <= '9' )
			{
				buffer[n++] = ch;
				continue;
			}

			if ( ch is '_' or '-' or ' ' )
				buffer[n++] = '_';
		}

		return n <= 0 ? "" : new string( buffer, 0, n );
	}
}
