using System.Collections.Generic;

namespace Sandbox;

/// <summary>Interior placement helpers shared by loot crates, radios, and NPC spawns (chunk-local building roots).</summary>
public static class ThornsProcBuildingInteriorSample
{
	/// <summary>Proc-building interior radio shop storey (0-based). Fifth storey when the building has at least five.</summary>
	public const int InteriorRadioStationStoryIndex = 4;

	/// <summary>Min horizontal distance between interior loot crate roots and radio shop anchors (world XY).</summary>
	public static float InteriorLootRadioMinSeparationHoriz =>
		MathF.Max( ThornsLootCrate.ProceduralCrateWorldExtent * 2.4f, ThornsBuildingModule.Cell * 0.58f );

	/// <summary>Min horizontal distance between two loot crates on the same storey (world XY).</summary>
	public static float InteriorLootCrateMinSeparationHoriz =>
		MathF.Max( ThornsLootCrate.ProceduralCrateWorldExtent * 1.35f, ThornsBuildingModule.Cell * 0.22f );

	/// <summary>Extra gap between prop bounds and wall inner face (world units).</summary>
	public const float InteriorWallComfortMargin = 10f;

	/// <summary>Keep furniture this many floor cells away from the ground door (Chebyshev).</summary>
	public const int InteriorFurnitureDoorExclusionCells = 2;

	/// <summary>Keep furniture this many floor cells away from ramp landings / shafts (Chebyshev).</summary>
	public const int InteriorFurnitureRampExclusionCells = 2;

	/// <summary>Relaxed fill pass — smaller keep-out so small fallback footprints can still fit 3 props.</summary>
	public const int InteriorFurnitureRelaxedDoorExclusionCells = 1;

	public const int InteriorFurnitureRelaxedRampExclusionCells = 1;

	/// <summary>Skip the wide door-wall band below this many interior floor cells (small POI footprints).</summary>
	public const int InteriorFurnitureDoorWallBandMinFloorCells = 40;

	const int DefaultMaxPlacementAttempts = 64;

	/// <summary>Matches procedural building <c>doorSide</c> (0 front −Y, 1 right +X, 2 back +Y, 3 left −X).</summary>
	public readonly struct InteriorPlacementHints
	{
		public int DoorSide { get; init; }
		public int DoorIndex { get; init; }

		/// <summary>Scripted grid cell when known (retail corner wall clearance).</summary>
		public int PlacementGridX { get; init; } = -1;

		public int PlacementGridY { get; init; } = -1;

		/// <summary>ASCII floorplan scripts: block only door/ramp/shaft cells, not wide keep-out radii.</summary>
		public bool ScriptedFloorplanExactExclusions { get; init; }

		public InteriorPlacementHints( int doorSide = -1, int doorIndex = 0 )
		{
			DoorSide = doorSide;
			DoorIndex = doorIndex;
		}

		public InteriorPlacementHints WithPlacementGrid( int gridX, int gridY ) =>
			new( DoorSide, DoorIndex )
			{
				PlacementGridX = gridX,
				PlacementGridY = gridY,
				ScriptedFloorplanExactExclusions = ScriptedFloorplanExactExclusions
			};
	}

	/// <summary>
	/// Building-local yaw (degrees) for facing into the interior from the ground-floor door —
	/// same axis convention as <see cref="TryGetInteriorWallDeskYaw"/>.
	/// </summary>
	public static float GetInteriorForwardYawDegrees( int doorSide ) =>
		doorSide switch
		{
			0 => 180f,
			2 => 0f,
			3 => 270f,
			1 => 90f,
			_ => 0f
		};

	static float NormalizePlanarYawDegrees( float yawDegrees )
	{
		var n = yawDegrees % 360f;
		return n < 0f ? n + 360f : n;
	}

	/// <summary>
	/// Grid-aligned yaw in building space, anchored to the door-facing interior axis when known.
	/// <paramref name="stableSalt"/> &gt;= 0 picks a deterministic quarter turn (for scripted cells).
	/// </summary>
	public static float PickDoorRelativeGridYawDegrees(
		Random rnd,
		in InteriorPlacementHints hints,
		int stableSalt = -1 )
	{
		var baseYaw = hints.DoorSide is >= 0 and <= 3
			? GetInteriorForwardYawDegrees( hints.DoorSide )
			: 0f;
		var quarter = stableSalt >= 0
			? ( stableSalt % 4 ) * 90f
			: rnd.Next( 0, 4 ) * 90f;
		return NormalizePlanarYawDegrees( baseYaw + quarter );
	}

	/// <summary>Interior floor cell stepped onto when entering through the ground-floor door.</summary>
	public static bool TryGetDoorInteriorCell(
		int doorSide,
		int doorIndex,
		int widthCells,
		int depthCells,
		out int gridX,
		out int gridY )
	{
		gridX = 0;
		gridY = 0;
		if ( widthCells < 1 || depthCells < 1 || doorSide < 0 || doorSide > 3 )
			return false;

		var maxX = widthCells - 1;
		var maxY = depthCells - 1;
		var along = doorSide is 0 or 2 ? widthCells : depthCells;
		var idx = Math.Clamp( doorIndex, 0, Math.Max( 0, along - 1 ) );

		switch ( doorSide )
		{
			case 0:
				gridX = idx;
				gridY = 0;
				return true;
			case 2:
				gridX = idx;
				gridY = maxY;
				return true;
			case 3:
				gridX = 0;
				gridY = idx;
				return true;
			case 1:
				gridX = maxX;
				gridY = idx;
				return true;
			default:
				return false;
		}
	}

	/// <summary>Ground entrance must not open onto ramp / ramp-shaft tiles (multi-storey proc buildings).</summary>
	public static bool IsEnterableDoorPlacement( int doorSide, int doorIndex, ThornsProcBuildingLayout layout )
	{
		if ( layout is null )
			return false;

		if ( !TryGetDoorInteriorCell( doorSide, doorIndex, layout.WidthCells, layout.DepthCells, out var gx, out var gy ) )
			return false;

		if ( layout.Stories > 1 && layout.IsShaftCellForRampAtStory( 0, gx, gy ) )
			return false;

		return true;
	}

	/// <summary>Legacy door filter when only dimensions are known (no ramp facing).</summary>
	public static bool IsEnterableDoorPlacement(
		int doorSide,
		int doorIndex,
		int widthCells,
		int depthCells,
		int stories )
	{
		if ( !TryGetDoorInteriorCell( doorSide, doorIndex, widthCells, depthCells, out _, out _ ) )
			return false;

		return stories <= 1;
	}

	/// <summary>SW-corner legacy shaft test when layout host is unavailable.</summary>
	static bool IsRampShaftCellAt( int gridX, int gridY, int rampGridX, int rampGridY ) =>
		(gridX == rampGridX && gridY == rampGridY)
		|| (gridX == rampGridX && gridY == rampGridY + 1);

	/// <summary>Pick a door wall slot that stays walkable (avoids ramp corner on storey &gt; 1).</summary>
	public static bool TryPickEnterableDoor(
		Random rnd,
		int widthCells,
		int depthCells,
		int stories,
		out int doorSide,
		out int doorIndex )
	{
		doorSide = 0;
		doorIndex = 0;
		if ( rnd is null || widthCells < 1 || depthCells < 1 )
			return false;

		var candidates = new List<(int side, int index)>( 24 );
		for ( var side = 0; side < 4; side++ )
		{
			var count = side is 0 or 2 ? widthCells : depthCells;
			for ( var idx = 0; idx < count; idx++ )
			{
				if ( !IsEnterableDoorPlacement( side, idx, widthCells, depthCells, stories ) )
					continue;

				candidates.Add( (side, idx) );
			}
		}

		if ( candidates.Count == 0 )
			return false;

		var pick = candidates[rnd.Next( 0, candidates.Count )];
		doorSide = pick.side;
		doorIndex = pick.index;
		return true;
	}

	static bool IsWithinChebyshev( int x, int y, int cx, int cy, int radius ) =>
		Math.Max( Math.Abs( x - cx ), Math.Abs( y - cy ) ) <= radius;

	static bool IsDoorExclusionCell(
		int gridX,
		int gridY,
		int widthCells,
		int depthCells,
		in InteriorPlacementHints hints,
		bool relaxed )
	{
		if ( hints.DoorSide < 0 )
			return false;

		if ( !TryGetDoorInteriorCell( hints.DoorSide, hints.DoorIndex, widthCells, depthCells, out var dgx, out var dgy ) )
			return false;

		var chebyshev = relaxed
			? InteriorFurnitureRelaxedDoorExclusionCells
			: InteriorFurnitureDoorExclusionCells;

		if ( IsWithinChebyshev( gridX, gridY, dgx, dgy, chebyshev ) )
			return true;

		if ( relaxed || widthCells * depthCells < InteriorFurnitureDoorWallBandMinFloorCells )
			return false;

		// Widen along the door wall on larger footprints so props do not cluster beside the entry.
		var alongIndex = hints.DoorSide is 0 or 2 ? gridX : gridY;
		var doorAlong = hints.DoorSide is 0 or 2 ? dgx : dgy;
		if ( Math.Abs( alongIndex - doorAlong ) > chebyshev + 1 )
			return false;

		return hints.DoorSide switch
		{
			0 => gridY <= chebyshev,
			2 => gridY >= depthCells - 1 - chebyshev,
			3 => gridX <= chebyshev,
			1 => gridX >= widthCells - 1 - chebyshev,
			_ => false
		};
	}

	static bool IsRampExclusionCell(
		int gridX,
		int gridY,
		int stories,
		GameObject buildingRoot,
		bool relaxed )
	{
		var radius = relaxed
			? InteriorFurnitureRelaxedRampExclusionCells
			: InteriorFurnitureRampExclusionCells;
		var layout = ThornsProcBuildingLayoutHost.TryGet( buildingRoot );
		if ( layout is null )
		{
			return IsWithinChebyshev( gridX, gridY, 0, 0, radius )
			       || IsWithinChebyshev( gridX, gridY, 0, 1, radius );
		}

		if ( layout.Stories <= 1 )
			return false;

		var shaftScratch = new List<(int x, int y)>( 8 );
		for ( var rs = 0; rs < layout.Stories - 1; rs++ )
		{
			foreach ( var ramp in layout.GetRampsOnStory( rs ) )
			{
				if ( IsWithinChebyshev( gridX, gridY, ramp.X, ramp.Y, radius ) )
					return true;

				shaftScratch.Clear();
				ThornsProcBuildingRampGeometry.CollectShaftCells( layout, ramp, shaftScratch );
				for ( var i = 0; i < shaftScratch.Count; i++ )
				{
					var (sx, sy) = shaftScratch[i];
					if ( IsWithinChebyshev( gridX, gridY, sx, sy, radius ) )
						return true;
				}

				foreach ( var (ux, uy) in ThornsProcBuildingRampTraversal.EnumerateUpperEntryCellsForRamp( layout, ramp ) )
				{
					if ( IsWithinChebyshev( gridX, gridY, ux, uy, radius ) )
						return true;
				}
			}

			foreach ( var (lx, ly) in ThornsProcBuildingRampTraversal.EnumerateRampLandingCells( layout, rs ) )
			{
				if ( IsWithinChebyshev( gridX, gridY, lx, ly, radius ) )
					return true;
			}
		}

		return false;
	}

	static bool IsBlockedInteriorPropCell(
		int gridX,
		int gridY,
		int widthCells,
		int depthCells,
		int stories,
		InteriorPlacementHints hints,
		GameObject buildingRoot,
		bool relaxed = false,
		int storyIndex = -1 )
	{
		if ( hints.ScriptedFloorplanExactExclusions )
			return IsScriptedFloorplanBlockedCell(
				storyIndex,
				gridX,
				gridY,
				widthCells,
				depthCells,
				stories,
				in hints,
				buildingRoot );

		if ( IsDoorExclusionCell( gridX, gridY, widthCells, depthCells, in hints, relaxed ) )
			return true;

		if ( IsRampExclusionCell( gridX, gridY, stories, buildingRoot, relaxed ) )
			return true;

		return false;
	}

	/// <summary>
	/// Exact cells only for ASCII corner furniture — door tile, ramp tile on its storey,
	/// and upper-storey ramp headroom openings (not landing zones or entry neighbors).
	/// </summary>
	static bool IsScriptedFloorplanBlockedCell(
		int storyIndex,
		int gridX,
		int gridY,
		int widthCells,
		int depthCells,
		int stories,
		in InteriorPlacementHints hints,
		GameObject buildingRoot )
	{
		if ( storyIndex < 0 )
		{
			for ( var s = 0; s < stories; s++ )
			{
				if ( IsScriptedFloorplanBlockedCell(
					     s,
					     gridX,
					     gridY,
					     widthCells,
					     depthCells,
					     stories,
					     in hints,
					     buildingRoot ) )
					return true;
			}

			return false;
		}

		if ( storyIndex == 0
		     && hints.DoorSide >= 0
		     && TryGetDoorInteriorCell( hints.DoorSide, hints.DoorIndex, widthCells, depthCells, out var dgx, out var dgy )
		     && gridX == dgx
		     && gridY == dgy )
			return true;

		var layout = ThornsProcBuildingLayoutHost.TryGet( buildingRoot );
		if ( layout is null || layout.Stories <= 1 )
			return false;

		foreach ( var ramp in layout.Ramps )
		{
			if ( storyIndex == ramp.Story && gridX == ramp.X && gridY == ramp.Y )
				return true;
		}

		if ( storyIndex >= 1
		     && ThornsProcTileRampHeadroom.IsRampHeadroomCell( layout, storyIndex, gridX, gridY ) )
			return true;

		return false;
	}

	/// <summary>Story-aware scripted tile test (corner ASCII placements).</summary>
	public static bool IsScriptedFloorplanBlockedCellPublic(
		int storyIndex,
		int gridX,
		int gridY,
		int widthCells,
		int depthCells,
		int stories,
		InteriorPlacementHints hints,
		GameObject buildingRoot ) =>
		IsScriptedFloorplanBlockedCell(
			storyIndex,
			gridX,
			gridY,
			widthCells,
			depthCells,
			stories,
			in hints,
			buildingRoot );

	/// <summary>Planar exclusion for large furniture footprints (local X/Y, building root space).</summary>
	public static bool IsInteriorFurnitureExcludedPlanarLocal(
		float localX,
		float localY,
		int widthCells,
		int depthCells,
		int stories,
		in InteriorPlacementHints hints,
		GameObject buildingRoot,
		bool relaxed = false )
	{
		var cell = ThornsBuildingModule.Cell;
		if ( !TryLocalPlanarToGridCellPublic( localX, localY, widthCells, depthCells, cell, out var gx, out var gy ) )
			return true;

		if ( IsBlockedInteriorPropCell( gx, gy, widthCells, depthCells, stories, hints, buildingRoot, relaxed ) )
			return true;

		if ( hints.ScriptedFloorplanExactExclusions )
			return false;

		var doorCells = relaxed
			? InteriorFurnitureRelaxedDoorExclusionCells
			: InteriorFurnitureDoorExclusionCells;
		var rampCells = relaxed
			? InteriorFurnitureRelaxedRampExclusionCells
			: InteriorFurnitureRampExclusionCells;
		var doorRadius = cell * ( doorCells + 0.35f );
		var rampRadius = cell * ( rampCells + 0.35f );
		var doorRadiusSq = doorRadius * doorRadius;
		var rampRadiusSq = rampRadius * rampRadius;

		if ( hints.DoorSide >= 0
		     && TryGetDoorInteriorCell( hints.DoorSide, hints.DoorIndex, widthCells, depthCells, out var dgx, out var dgy ) )
		{
			var doorLx = GridAxisLocalPublic( dgx, widthCells, cell );
			var doorLy = GridAxisLocalPublic( dgy, depthCells, cell );
			var ddx = localX - doorLx;
			var ddy = localY - doorLy;
			if ( ddx * ddx + ddy * ddy < doorRadiusSq )
				return true;
		}

		var layout = ThornsProcBuildingLayoutHost.TryGet( buildingRoot );
		if ( layout is null || layout.Stories <= 1 )
		{
			if ( stories > 1 )
			{
				var legacyLx = GridAxisLocalPublic( 0, widthCells, cell );
				var legacyLy = GridAxisLocalPublic( 0, depthCells, cell );
				var ddx = localX - legacyLx;
				var ddy = localY - legacyLy;
				if ( ddx * ddx + ddy * ddy < rampRadiusSq )
					return true;
			}

			return false;
		}

		var shaftScratch = new List<(int x, int y)>( 8 );
		for ( var rs = 0; rs < layout.Stories - 1; rs++ )
		{
			foreach ( var ramp in layout.GetRampsOnStory( rs ) )
			{
				if ( IsPlanarNearGridCell( localX, localY, ramp.X, ramp.Y, widthCells, depthCells, cell, rampRadiusSq ) )
					return true;

				shaftScratch.Clear();
				ThornsProcBuildingRampGeometry.CollectShaftCells( layout, ramp, shaftScratch );
				for ( var i = 0; i < shaftScratch.Count; i++ )
				{
					var (sx, sy) = shaftScratch[i];
					if ( IsPlanarNearGridCell( localX, localY, sx, sy, widthCells, depthCells, cell, rampRadiusSq ) )
						return true;
				}

				foreach ( var (ux, uy) in ThornsProcBuildingRampTraversal.EnumerateUpperEntryCellsForRamp( layout, ramp ) )
				{
					if ( IsPlanarNearGridCell( localX, localY, ux, uy, widthCells, depthCells, cell, rampRadiusSq ) )
						return true;
				}
			}

			foreach ( var (lx, ly) in ThornsProcBuildingRampTraversal.EnumerateRampLandingCells( layout, rs ) )
			{
				if ( IsPlanarNearGridCell( localX, localY, lx, ly, widthCells, depthCells, cell, rampRadiusSq ) )
					return true;
			}
		}

		return false;
	}

	static bool IsPlanarNearGridCell(
		float localX,
		float localY,
		int gridX,
		int gridY,
		int widthCells,
		int depthCells,
		float cell,
		float radiusSq )
	{
		var lx = GridAxisLocalPublic( gridX, widthCells, cell );
		var ly = GridAxisLocalPublic( gridY, depthCells, cell );
		var dx = localX - lx;
		var dy = localY - ly;
		return dx * dx + dy * dy < radiusSq;
	}

	public static bool IsBlockedInteriorPropCellPublic(
		int gridX,
		int gridY,
		int widthCells,
		int depthCells,
		int stories,
		InteriorPlacementHints hints,
		GameObject buildingRoot,
		bool relaxed = false,
		int storyIndex = -1 ) =>
		IsBlockedInteriorPropCell(
			gridX,
			gridY,
			widthCells,
			depthCells,
			stories,
			hints,
			buildingRoot,
			relaxed,
			storyIndex );

	/// <summary>Tracks crate / radio positions inside one procedural building during scatter.</summary>
	public sealed class InteriorPlacementBatch
	{
		readonly List<Vector3> _anchors = new();

		public bool TrySampleLootAnchor(
			Random rnd,
			Scene scene,
			GameObject buildingRoot,
			int widthCells,
			int depthCells,
			int stories,
			InteriorPlacementHints hints,
			out Vector3 worldPos,
			int forceStoryIndex = -1 )
		{
			worldPos = default;
			if ( buildingRoot is null || !buildingRoot.IsValid() )
				return false;

			var half = InteriorLootCratePlanarHalfExtent();
			var crateMinSq = InteriorLootCrateMinSeparationHoriz * InteriorLootCrateMinSeparationHoriz;
			var storyBandZ = ThornsBuildingModule.StoryHeightWorld * 0.42f;
			var fixedStory = forceStoryIndex >= 0 && forceStoryIndex < stories;
			var maxAttempts = fixedStory ? DefaultMaxPlacementAttempts * 2 : DefaultMaxPlacementAttempts;

			for ( var attempt = 0; attempt < maxAttempts; attempt++ )
			{
				Vector3 candidate;
				Vector3 local;
				int storyIndex;
				if ( fixedStory )
				{
					storyIndex = forceStoryIndex;
					if ( attempt < maxAttempts / 2
					     && TrySampleWallAlignedLocal(
						     rnd,
						     buildingRoot,
						     widthCells,
						     depthCells,
						     stories,
						     half,
						     half,
						     hints,
						     groundFloorOnly: false,
						     out local,
						     out _,
						     out _,
						     forceStoryIndex ) )
						candidate = buildingRoot.WorldPosition + buildingRoot.WorldRotation * local;
					else if ( TrySampleInsetFloorLocal(
						          rnd,
						          buildingRoot,
						          widthCells,
						          depthCells,
						          stories,
						          half,
						          hints,
						          out var localFixed,
						          out _,
						          forceStoryIndex ) )
						candidate = buildingRoot.WorldPosition + buildingRoot.WorldRotation * localFixed;
					else
						continue;
				}
				else if ( attempt < DefaultMaxPlacementAttempts * 2 / 3
				     && TrySampleWallAlignedLocal(
					     rnd,
					     buildingRoot,
					     widthCells,
					     depthCells,
					     stories,
					     half,
					     half,
					     hints,
					     groundFloorOnly: false,
					     out local,
					     out _,
					     out storyIndex ) )
					candidate = buildingRoot.WorldPosition + buildingRoot.WorldRotation * local;
				else if ( !TrySampleInsetFloorLocal( rnd, buildingRoot, widthCells, depthCells, stories, half, hints, out local, out storyIndex ) )
					continue;
				else
					candidate = buildingRoot.WorldPosition + buildingRoot.WorldRotation * local;

				if ( !TryFinalizeInteriorAnchor( scene, buildingRoot, storyIndex, ref candidate ) )
					continue;

				if ( !IsLootAnchorFarEnough( candidate, crateMinSq, storyBandZ ) )
					continue;

				_anchors.Add( candidate );
				worldPos = candidate;
				return true;
			}

			if ( fixedStory
			     && TrySampleLootAnchorFallback(
				     rnd,
				     buildingRoot,
				     widthCells,
				     depthCells,
				     stories,
				     forceStoryIndex,
				     scene,
				     crateMinSq,
				     storyBandZ,
				     out worldPos ) )
				return true;

			return false;
		}

		bool TrySampleLootAnchorFallback(
			Random rnd,
			GameObject buildingRoot,
			int widthCells,
			int depthCells,
			int stories,
			int storyIndex,
			Scene scene,
			float crateMinSq,
			float storyBandZ,
			out Vector3 worldPos )
		{
			worldPos = default;
			for ( var attempt = 0; attempt < 24; attempt++ )
			{
				var candidate = SampleLootAnchorWorldPositionForStory(
					rnd,
					buildingRoot,
					widthCells,
					depthCells,
					stories,
					storyIndex );
				if ( !IsLootAnchorFarEnough( candidate, crateMinSq, storyBandZ ) )
					continue;

				_anchors.Add( candidate );
				worldPos = candidate;
				return true;
			}

			return false;
		}

		public bool TrySampleRadioWallAnchor(
			Random rnd,
			Scene scene,
			GameObject buildingRoot,
			int widthCells,
			int depthCells,
			int stories,
			InteriorPlacementHints hints,
			out Vector3 worldPos,
			out Rotation worldRotation,
			int forceStoryIndex = InteriorRadioStationStoryIndex ) =>
			TryFindRadioPlacementOnStory(
				buildingRoot,
				widthCells,
				depthCells,
				stories,
				forceStoryIndex,
				hints,
				rnd,
				out worldPos,
				out worldRotation );

		static bool TryFinalizeInteriorAnchor( Scene scene, GameObject buildingRoot, int storyIndex, ref Vector3 worldPos ) =>
			TrySnapToInteriorFloor( scene, buildingRoot, storyIndex, ref worldPos );

		bool IsHorizFarEnough( Vector3 candidate, float minSepSq ) =>
			IsLootAnchorFarEnough( candidate, minSepSq, ThornsBuildingModule.StoryHeightWorld * 0.42f );

		/// <summary>Crates on different storeys may share XY; same-storey uses horizontal separation only.</summary>
		bool IsLootAnchorFarEnough( Vector3 candidate, float minSepSq, float storyBandZ )
		{
			for ( var i = 0; i < _anchors.Count; i++ )
			{
				var a = _anchors[i];
				if ( MathF.Abs( candidate.z - a.z ) >= storyBandZ )
					continue;

				var dx = candidate.x - a.x;
				var dy = candidate.y - a.y;
				if ( dx * dx + dy * dy < minSepSq )
					return false;
			}

			return true;
		}

		/// <summary>Reserve XY bands already used by loot crates / radios so furniture scatter does not overlap.</summary>
		public void SeedFurniturePlacementBatch(
			ThornsInteriorFurniturePlacement.Batch furniture,
			GameObject buildingRoot,
			int widthCells,
			int depthCells,
			float planarRadius )
		{
			if ( furniture is null || buildingRoot is null || !buildingRoot.IsValid() )
				return;

			for ( var i = 0; i < _anchors.Count; i++ )
			{
				var a = _anchors[i];
				var local = buildingRoot.WorldRotation.Inverse * (a - buildingRoot.WorldPosition);
				var story = InferStoryIndexFromLocalZ( local.z );
				furniture.RegisterReservedAnchor( buildingRoot, widthCells, depthCells, a, planarRadius, story );
			}
		}

		static int InferStoryIndexFromLocalZ( float localZ )
		{
			if ( localZ < 0f )
				return 0;

			var storyH = ThornsBuildingModule.StoryHeightWorld;
			return storyH > 0.01f ? Math.Max( 0, (int)MathF.Floor( localZ / storyH ) ) : 0;
		}
	}

	public static float InteriorLootCratePlanarHalfExtent() =>
		MathF.Max( ThornsLootCrate.ProceduralCrateWorldExtent * 0.58f, ThornsBuildingModule.Cell * 0.12f );

	public static float InteriorRadioPlanarHalfAlongWall()
	{
		var s = ThornsBuildingVisuals.RadioPlaceableLocalScale;
		var r = ThornsBuildingModule.DevReferenceSize;
		return MathF.Max( MathF.Max( s.x, s.y ) * r * 0.5f, ThornsBuildingModule.Cell * 0.42f );
	}

	public static float InteriorRadioPlanarHalfFromWall() =>
		MathF.Max( InteriorRadioPlanarHalfAlongWall() * 0.72f, ThornsBuildingModule.Cell * 0.32f );

	/// <summary>Walkable floor top local Z — top of <c>wood_foundation</c> at <c>s * StoryHeightWorld</c> (see proc spawner).</summary>
	public static float InteriorFloorWalkLocalZForStory( int storyIndex ) =>
		storyIndex * ThornsBuildingModule.StoryHeightWorld + ThornsBuildingModule.FloorThickness;

	/// <summary>Best-effort storey from building-local Z on the walk plane (for prop refresh after catalog apply).</summary>
	public static int InferStoryIndexFromWalkLocalZ( GameObject buildingRoot, float localZ, int maxStories = 8 )
	{
		if ( maxStories < 1 )
			maxStories = 1;

		var layout = ThornsProcBuildingLayoutHost.TryGet( buildingRoot );
		if ( layout is not null )
			maxStories = Math.Max( 1, layout.Stories );

		var sh = ThornsBuildingModule.StoryHeightWorld;
		if ( sh < 0.01f )
			return 0;

		var story = (int)MathF.Round( ( localZ - ThornsBuildingModule.FloorThickness ) / sh );
		return Math.Clamp( story, 0, maxStories - 1 );
	}

	/// <summary>
	/// After <see cref="ThornsPlaceableFurniturePresentation.Apply"/>, sit mesh bottom on the storey walk plane.
	/// Loot-crate anchors use <see cref="FloorAnchorLocalZForStory"/>; furniture must use <see cref="InteriorFloorWalkLocalZForStory"/>.
	/// </summary>
	public static void SnapInteriorFurniturePivot(
		GameObject buildingRoot,
		int storyIndex,
		in ThornsPlaceableFurnitureCatalog.Entry entry,
		Rotation worldRotation,
		ref Vector3 worldPosition )
	{
		if ( buildingRoot is not null && buildingRoot.IsValid() && storyIndex >= 0 )
		{
			var local = buildingRoot.WorldRotation.Inverse * ( worldPosition - buildingRoot.WorldPosition );
			SnapInteriorFurnitureBuildingLocal( buildingRoot, storyIndex, in entry, worldRotation, ref local );
			worldPosition = buildingRoot.WorldPosition + buildingRoot.WorldRotation * local;
			return;
		}

		ThornsPlaceableFurniturePresentation.AlignPlacementPivotOnSurface( in entry, ref worldPosition, worldRotation );
	}

	/// <summary>Building-local pivot on the storey walk plane (matches floorplan test / <see cref="ThornsFloorplanFurnitureTuneItem"/>).</summary>
	public static void SnapInteriorFurnitureBuildingLocal(
		GameObject buildingRoot,
		int storyIndex,
		in ThornsPlaceableFurnitureCatalog.Entry entry,
		Rotation worldRotation,
		ref Vector3 buildingLocalPosition )
	{
		if ( buildingRoot is null || !buildingRoot.IsValid() || storyIndex < 0 )
			return;

		buildingLocalPosition = new Vector3(
			buildingLocalPosition.x,
			buildingLocalPosition.y,
			InteriorFloorWalkLocalZForStory( storyIndex ) );

		var worldPosition = buildingRoot.WorldPosition + buildingRoot.WorldRotation * buildingLocalPosition;
		ThornsPlaceableFurniturePresentation.AlignPlacementPivotOnSurface( in entry, ref worldPosition, worldRotation );
		buildingLocalPosition = buildingRoot.WorldRotation.Inverse * ( worldPosition - buildingRoot.WorldPosition );
	}

	/// <summary>Apply building-local position/rotation on a child of the proc building (decor root or building root).</summary>
	public static void ApplyInteriorFurnitureBuildingLocalPose(
		GameObject furnitureObject,
		GameObject buildingRoot,
		Vector3 buildingLocalPosition,
		Rotation buildingLocalRotation )
	{
		if ( furnitureObject is null || !furnitureObject.IsValid()
		     || buildingRoot is null || !buildingRoot.IsValid() )
			return;

		var parent = furnitureObject.Parent;
		if ( parent.IsValid() && parent == buildingRoot )
		{
			furnitureObject.LocalPosition = buildingLocalPosition;
			furnitureObject.LocalRotation = buildingLocalRotation;
			return;
		}

		if ( parent.IsValid()
		     && parent.Name == ThornsInteriorFurnitureScatter.DecorParentName
		     && parent.Parent == buildingRoot
		     && parent.LocalPosition.AlmostEqual( Vector3.Zero )
		     && parent.LocalRotation.AlmostEqual( Rotation.Identity ) )
		{
			furnitureObject.LocalPosition = buildingLocalPosition;
			furnitureObject.LocalRotation = buildingLocalRotation;
			return;
		}

		furnitureObject.WorldPosition = buildingRoot.WorldPosition + buildingRoot.WorldRotation * buildingLocalPosition;
		furnitureObject.WorldRotation = buildingRoot.WorldRotation * buildingLocalRotation;
	}

	/// <summary>Building-local pose for scripted corner scatter (matches <see cref="ThornsInteriorFurniturePlacement.Batch.TryPlaceAtGridCell"/>).</summary>
	public static bool TryComputeScriptedFurnitureBuildingLocal(
		in ThornsFloorplanFurnitureTuneItem.SpawnContext ctx,
		in ThornsPlaceableFurnitureCatalog.Entry profile,
		Rotation worldRotation,
		out Vector3 buildingLocal,
		out Rotation buildingLocalRotation )
	{
		buildingLocal = default;
		buildingLocalRotation = Rotation.Identity;

		var buildingRoot = ctx.BuildingRoot;
		if ( buildingRoot is null || !buildingRoot.IsValid() )
			return false;

		if ( ctx.Story < 0 || ctx.GridX < 0 || ctx.GridY < 0 || ctx.WidthCells < 1 || ctx.DepthCells < 1 )
			return false;

		if ( !TryGridCellCenterLocalPublic(
			     ctx.GridX,
			     ctx.GridY,
			     ctx.WidthCells,
			     ctx.DepthCells,
			     ThornsBuildingModule.Cell,
			     out var lx,
			     out var ly ) )
			return false;

		var hasTune = ThornsPlaceableFurnitureCatalog.TryGetInteriorPlacementTune(
			profile.StructureDefId,
			ctx.Story,
			ctx.GridX,
			ctx.GridY,
			ctx.WidthCells,
			ctx.DepthCells,
			out var placementTune );
		var offsetBuildingLocal = hasTune
			? placementTune.OffsetFromCellCenterBuildingLocal
			: profile.InteriorPlacementLocalOffsetInches;

		buildingLocal = ThornsPlaceableFurnitureCatalog.BuildInteriorFurnitureLocalPosition(
			ctx.Story,
			lx,
			ly,
			offsetBuildingLocal );

		float buildingYawDeg;
		if ( hasTune )
			buildingYawDeg = placementTune.BuildingLocalYawDegrees;
		else if ( ThornsInteriorFurnitureCanonicalSlots.TryGetScriptedCornerYaw(
			          profile.StructureDefId,
			          ctx.Story,
			          ctx.GridX,
			          ctx.GridY,
			          ctx.WidthCells,
			          ctx.DepthCells,
			          out var cornerYawDeg ) )
			buildingYawDeg = cornerYawDeg;
		else
			buildingYawDeg = (buildingRoot.WorldRotation.Inverse * worldRotation).Angles().yaw;

		buildingLocalRotation = Rotation.FromYaw( buildingYawDeg );

		if ( !hasTune )
		{
			var worldPos = buildingRoot.WorldPosition + buildingRoot.WorldRotation * buildingLocal;
			var profilePlanarOnly = profile with { InteriorPlacementLocalOffsetInches = Vector3.Zero };
			var hints = new InteriorPlacementHints( ctx.DoorSide, ctx.DoorIndex )
			{
				PlacementGridX = ctx.GridX,
				PlacementGridY = ctx.GridY
			};
			worldPos = ThornsPlaceableFurnitureCatalog.ApplyInteriorPlacementOffset(
				buildingRoot,
				worldPos,
				worldRotation,
				in profilePlanarOnly,
				in hints,
				ctx.WidthCells,
				ctx.DepthCells );
			buildingLocal = buildingRoot.WorldRotation.Inverse * ( worldPos - buildingRoot.WorldPosition );
		}

		SnapInteriorFurnitureBuildingLocal(
			buildingRoot,
			ctx.Story,
			in profile,
			worldRotation,
			ref buildingLocal );

		return true;
	}

	/// <summary>Parent on the proc building root and apply building-local pose (survives <see cref="ThornsNetworkReplication.TryNetworkSpawnHostOwned"/>).</summary>
	public static void SeatProcInteriorFurnitureOnBuilding(
		GameObject furnitureObject,
		GameObject buildingRoot,
		in ThornsFloorplanFurnitureTuneItem.SpawnContext ctx,
		in ThornsPlaceableFurnitureCatalog.Entry entry,
		Rotation worldRotation,
		Vector3 fallbackWorldPosition )
	{
		if ( furnitureObject is null || !furnitureObject.IsValid()
		     || buildingRoot is null || !buildingRoot.IsValid() )
			return;

		if ( furnitureObject.Parent != buildingRoot )
			furnitureObject.SetParent( buildingRoot );

		Vector3 buildingLocal;
		Rotation buildingLocalRotation;
		if ( !TryComputeScriptedFurnitureBuildingLocal(
			     in ctx,
			     in entry,
			     worldRotation,
			     out buildingLocal,
			     out buildingLocalRotation ) )
		{
			buildingLocal = buildingRoot.WorldRotation.Inverse * ( fallbackWorldPosition - buildingRoot.WorldPosition );
			buildingLocalRotation = buildingRoot.WorldRotation.Inverse * worldRotation;
			buildingLocalRotation = Rotation.FromYaw( buildingLocalRotation.Angles().yaw );
			if ( ctx.Story >= 0 )
			{
				SnapInteriorFurnitureBuildingLocal(
					buildingRoot,
					ctx.Story,
					in entry,
					worldRotation,
					ref buildingLocal );
			}
		}

		furnitureObject.LocalPosition = buildingLocal;
		furnitureObject.LocalRotation = buildingLocalRotation;
	}

	/// <summary>World pivot from grid cell + catalog offset on the storey walk plane.</summary>
	public static bool TryGetInteriorFurniturePivotWorld(
		GameObject buildingRoot,
		int storyIndex,
		int gridX,
		int gridY,
		int widthCells,
		int depthCells,
		Vector3 offsetFromCellCenterBuildingLocal,
		out Vector3 worldPosition )
	{
		worldPosition = default;
		if ( buildingRoot is null || !buildingRoot.IsValid() )
			return false;

		if ( !TryGridCellCenterLocalPublic( gridX, gridY, widthCells, depthCells, ThornsBuildingModule.Cell, out var lx, out var ly ) )
			return false;

		var local = ThornsPlaceableFurnitureCatalog.BuildInteriorFurnitureLocalPosition(
			storyIndex,
			lx,
			ly,
			offsetFromCellCenterBuildingLocal );
		worldPosition = buildingRoot.WorldPosition + buildingRoot.WorldRotation * local;
		return true;
	}

	/// <summary>
	/// Places a radio desk on storey <paramref name="storyIndex"/> (5th floor = index 4).
	/// Grid scan — not blocked by loot-crate spacing or failed wall sampling on upper storeys.
	/// </summary>
	public static bool TryFindRadioPlacementOnStory(
		GameObject buildingRoot,
		int widthCells,
		int depthCells,
		int stories,
		int storyIndex,
		InteriorPlacementHints hints,
		Random rnd,
		out Vector3 worldPos,
		out Rotation worldRotation )
	{
		worldPos = default;
		worldRotation = Rotation.Identity;
		if ( buildingRoot is null || !buildingRoot.IsValid() )
			return false;

		if ( storyIndex < 0 || storyIndex >= stories )
			return false;

		var layout = ThornsProcBuildingLayoutHost.TryGet( buildingRoot );
		var cell = ThornsBuildingModule.Cell;
		var walkable = new List<(int gx, int gy)>();
		var wallPreferred = new List<(int gx, int gy, float yawDeg)>();

		for ( var gx = 0; gx < widthCells; gx++ )
		for ( var gy = 0; gy < depthCells; gy++ )
		{
			if ( !CellHasFloorSlab( storyIndex, gx, gy, widthCells, depthCells, stories, buildingRoot ) )
				continue;
			if ( IsBlockedInteriorPropCell( gx, gy, widthCells, depthCells, stories, hints, buildingRoot ) )
				continue;

			walkable.Add( (gx, gy) );
			if ( TryGetInteriorWallDeskYaw(
				     layout,
				     widthCells,
				     depthCells,
				     stories,
				     storyIndex,
				     gx,
				     gy,
				     buildingRoot,
				     out var yawDeg ) )
				wallPreferred.Add( (gx, gy, yawDeg) );
		}

		if ( walkable.Count == 0 )
			return false;

		float lx;
		float ly;
		float yaw;
		if ( wallPreferred.Count > 0 )
		{
			var pick = wallPreferred[rnd.Next( 0, wallPreferred.Count )];
			yaw = pick.yawDeg;
			if ( layout is not null )
			{
				lx = layout.GridAxisLocalX( pick.gx );
				ly = layout.GridAxisLocalY( pick.gy );
			}
			else
			{
				lx = GridAxisLocal( pick.gx, widthCells, cell );
				ly = GridAxisLocal( pick.gy, depthCells, cell );
			}
		}
		else
		{
			var pick = walkable[rnd.Next( 0, walkable.Count )];
			yaw = 0f;
			if ( layout is not null )
			{
				lx = layout.GridAxisLocalX( pick.gx );
				ly = layout.GridAxisLocalY( pick.gy );
			}
			else
			{
				lx = GridAxisLocal( pick.gx, widthCells, cell );
				ly = GridAxisLocal( pick.gy, depthCells, cell );
			}
		}

		var local = new Vector3( lx, ly, InteriorFloorWalkLocalZForStory( storyIndex ) );
		worldPos = buildingRoot.WorldPosition + buildingRoot.WorldRotation * local;
		worldRotation = buildingRoot.WorldRotation * Rotation.FromYaw( yaw );
		return true;
	}

	/// <summary>
	/// Grid walk placement for interior decor (furniture, etc.) on any storey with a floor slab — same anchor as <see cref="TryFindRadioPlacementOnStory"/>.
	/// </summary>
	public static bool TryFindInteriorDecorPlacementOnStory(
		GameObject buildingRoot,
		int widthCells,
		int depthCells,
		int stories,
		int storyIndex,
		InteriorPlacementHints hints,
		Random rnd,
		bool preferWallBacked,
		out Vector3 worldPos,
		out Rotation worldRotation,
		Func<int, int, bool> isFloorCellAvailable = null )
	{
		worldPos = default;
		worldRotation = Rotation.Identity;
		if ( buildingRoot is null || !buildingRoot.IsValid() )
			return false;

		if ( storyIndex < 0 || storyIndex >= stories )
			return false;

		var layout = ThornsProcBuildingLayoutHost.TryGet( buildingRoot );
		var cell = ThornsBuildingModule.Cell;
		var walkable = new List<(int gx, int gy)>();
		var wallPreferred = new List<(int gx, int gy, float yawDeg)>();

		for ( var gx = 0; gx < widthCells; gx++ )
		for ( var gy = 0; gy < depthCells; gy++ )
		{
			if ( !CellHasFloorSlab( storyIndex, gx, gy, widthCells, depthCells, stories, buildingRoot ) )
				continue;
			if ( IsBlockedInteriorPropCell( gx, gy, widthCells, depthCells, stories, hints, buildingRoot ) )
				continue;
			if ( isFloorCellAvailable is not null && !isFloorCellAvailable( gx, gy ) )
				continue;

			walkable.Add( (gx, gy) );
			if ( preferWallBacked
			     && TryGetInteriorWallDeskYaw(
				     layout,
				     widthCells,
				     depthCells,
				     stories,
				     storyIndex,
				     gx,
				     gy,
				     buildingRoot,
				     out var yawDeg ) )
				wallPreferred.Add( (gx, gy, yawDeg) );
		}

		if ( walkable.Count == 0 )
			return false;

		float lx;
		float ly;
		float yaw;
		if ( preferWallBacked && wallPreferred.Count > 0 )
		{
			var pick = wallPreferred[rnd.Next( 0, wallPreferred.Count )];
			yaw = pick.yawDeg;
			if ( layout is not null )
			{
				lx = layout.GridAxisLocalX( pick.gx );
				ly = layout.GridAxisLocalY( pick.gy );
			}
			else
			{
				lx = GridAxisLocal( pick.gx, widthCells, cell );
				ly = GridAxisLocal( pick.gy, depthCells, cell );
			}
		}
		else
		{
			var pick = walkable[rnd.Next( 0, walkable.Count )];
			if ( !preferWallBacked
			     || !TryGetInteriorWallDeskYaw(
				     layout,
				     widthCells,
				     depthCells,
				     stories,
				     storyIndex,
				     pick.gx,
				     pick.gy,
				     buildingRoot,
				     out yaw ) )
			{
				yaw = PickDoorRelativeGridYawDegrees(
					rnd,
					hints,
					stableSalt: pick.gx * 31 + pick.gy * 17 + storyIndex * 97 );
			}

			if ( layout is not null )
			{
				lx = layout.GridAxisLocalX( pick.gx );
				ly = layout.GridAxisLocalY( pick.gy );
			}
			else
			{
				lx = GridAxisLocal( pick.gx, widthCells, cell );
				ly = GridAxisLocal( pick.gy, depthCells, cell );
			}
		}

		var local = new Vector3( lx, ly, InteriorFloorWalkLocalZForStory( storyIndex ) );
		worldPos = buildingRoot.WorldPosition + buildingRoot.WorldRotation * local;
		worldRotation = buildingRoot.WorldRotation * Rotation.FromYaw( yaw );
		return true;
	}

	static bool TryGetInteriorWallDeskYaw(
		ThornsProcBuildingLayout layout,
		int widthCells,
		int depthCells,
		int stories,
		int storyIndex,
		int gridX,
		int gridY,
		GameObject buildingRoot,
		out float yawDegrees )
	{
		yawDegrees = 0f;
		if ( IsExteriorFaceOnStory( layout, widthCells, depthCells, stories, storyIndex, gridX, gridY, buildingRoot, 0 ) )
		{
			yawDegrees = 0f;
			return true;
		}

		if ( IsExteriorFaceOnStory( layout, widthCells, depthCells, stories, storyIndex, gridX, gridY, buildingRoot, 2 ) )
		{
			yawDegrees = 180f;
			return true;
		}

		if ( IsExteriorFaceOnStory( layout, widthCells, depthCells, stories, storyIndex, gridX, gridY, buildingRoot, 3 ) )
		{
			yawDegrees = 90f;
			return true;
		}

		if ( IsExteriorFaceOnStory( layout, widthCells, depthCells, stories, storyIndex, gridX, gridY, buildingRoot, 1 ) )
		{
			yawDegrees = 270f;
			return true;
		}

		return false;
	}

	/// <summary>True when cell has no walkable neighbor on that side (exterior wall face).</summary>
	static bool IsExteriorFaceOnStory(
		ThornsProcBuildingLayout layout,
		int widthCells,
		int depthCells,
		int stories,
		int storyIndex,
		int gridX,
		int gridY,
		GameObject buildingRoot,
		int side )
	{
		var nx = gridX;
		var ny = gridY;
		switch ( side )
		{
			case 0:
				ny--;
				break;
			case 2:
				ny++;
				break;
			case 3:
				nx--;
				break;
			default:
				nx++;
				break;
		}

		if ( nx < 0 || nx >= widthCells || ny < 0 || ny >= depthCells )
			return true;

		return !CellHasFloorSlab( storyIndex, nx, ny, widthCells, depthCells, stories, buildingRoot );
	}

	public static Vector3 SampleLootAnchorWorldPosition(
		Random rnd,
		GameObject buildingRoot,
		int widthCells,
		int depthCells,
		int stories ) =>
		SampleLootAnchorWorldPositionForStory( rnd, buildingRoot, widthCells, depthCells, stories, storyIndex: -1 );

	public static Vector3 SampleLootAnchorWorldPositionForStory(
		Random rnd,
		GameObject buildingRoot,
		int widthCells,
		int depthCells,
		int stories,
		int storyIndex )
	{
		if ( TrySampleInsetFloorLocal(
			     rnd,
			     buildingRoot,
			     widthCells,
			     depthCells,
			     stories,
			     InteriorLootCratePlanarHalfExtent(),
			     default,
			     out var local,
			     out _,
			     storyIndex ) )
			return buildingRoot.WorldPosition + buildingRoot.WorldRotation * local;

		return SampleLootAnchorWorldPositionLegacy( rnd, buildingRoot, widthCells, depthCells, stories, storyIndex );
	}

	public static Vector3 SampleInteriorNpcWorldPosition( Scene scene, GameObject buildingRoot, int widthCells, int depthCells, int stories, Random rnd ) =>
		SampleInteriorNpcWorldPositionOnStory( scene, buildingRoot, widthCells, depthCells, stories, storyIndex: -1, rnd );

	/// <summary>Interior NPC spawn on a specific storey (<c>storyIndex &lt; 0</c> = any floor).</summary>
	public static Vector3 SampleInteriorNpcWorldPositionOnStory(
		Scene scene,
		GameObject buildingRoot,
		int widthCells,
		int depthCells,
		int stories,
		int storyIndex,
		Random rnd )
	{
		if ( buildingRoot is null || !buildingRoot.IsValid() || scene is null || !scene.IsValid() )
			return default;

		Vector3 approxWorld;
		if ( storyIndex >= 0 && storyIndex < stories
		     && TrySampleInsetFloorLocal(
			     rnd,
			     buildingRoot,
			     widthCells,
			     depthCells,
			     stories,
			     InteriorLootCratePlanarHalfExtent(),
			     default,
			     out var local,
			     out _,
			     forceStoryIndex: storyIndex ) )
			approxWorld = buildingRoot.WorldPosition + buildingRoot.WorldRotation * local;
		else
			approxWorld = SampleLootAnchorWorldPosition( rnd, buildingRoot, widthCells, depthCells, stories );

		var traceStart = approxWorld + Vector3.Up * 220f;
		var tr = ThornsTraceUtility.RunRay( scene, new Ray( traceStart, Vector3.Down ), 900f, ThornsTraceProfile.TerrainInteriorSampleDown, null );

		var worldPos = tr.Hit
			? tr.HitPosition + tr.Normal * 2f
			: approxWorld;

		if ( storyIndex >= 0 && storyIndex < stories )
			TrySnapToInteriorFloor( scene, buildingRoot, storyIndex, ref worldPos );

		return worldPos;
	}

	public static bool TrySampleInsetFloorLocal(
		Random rnd,
		GameObject buildingRoot,
		int widthCells,
		int depthCells,
		int stories,
		float planarHalfExtent,
		InteriorPlacementHints hints,
		out Vector3 localPos,
		out int storyIndex,
		int forceStoryIndex = -1 )
	{
		localPos = default;
		storyIndex = 0;
		if ( widthCells < 1 || depthCells < 1 || stories < 1 )
			return false;

		var cell = ThornsBuildingModule.Cell;
		var maxX = widthCells - 1;
		var maxY = depthCells - 1;
		var inset = cell * 0.5f + ThornsBuildingModule.WallThickness + planarHalfExtent + InteriorWallComfortMargin;

		var minLx = GridAxisLocal( 0, widthCells, cell ) + inset;
		var maxLx = GridAxisLocal( maxX, widthCells, cell ) - inset;
		var minLy = GridAxisLocal( 0, depthCells, cell ) + inset;
		var maxLy = GridAxisLocal( maxY, depthCells, cell ) - inset;
		if ( maxLx <= minLx || maxLy <= minLy )
		{
			var slim = cell * 0.32f + planarHalfExtent;
			minLx = GridAxisLocal( 0, widthCells, cell ) + slim;
			maxLx = GridAxisLocal( maxX, widthCells, cell ) - slim;
			minLy = GridAxisLocal( 0, depthCells, cell ) + slim;
			maxLy = GridAxisLocal( maxY, depthCells, cell ) - slim;
		}

		if ( maxLx <= minLx || maxLy <= minLy )
		{
			var cx = GridAxisLocal( maxX / 2, widthCells, cell );
			var cy = GridAxisLocal( maxY / 2, depthCells, cell );
			storyIndex = forceStoryIndex >= 0 && forceStoryIndex < stories
				? forceStoryIndex
				: stories <= 1 ? 0 : rnd.Next( 0, stories );
			if ( TryLocalPlanarToGridCell( cx, cy, widthCells, depthCells, cell, out var gx, out var gy )
			     && CellHasFloorSlab( storyIndex, gx, gy, widthCells, depthCells, stories, buildingRoot )
			     && !IsBlockedInteriorPropCell( gx, gy, widthCells, depthCells, stories, hints, buildingRoot ) )
			{
				localPos = new Vector3( cx, cy, FloorAnchorLocalZForStory( storyIndex ) );
				return true;
			}

			return false;
		}

		for ( var attempt = 0; attempt < 32; attempt++ )
		{
			storyIndex = forceStoryIndex >= 0 && forceStoryIndex < stories
				? forceStoryIndex
				: stories <= 1 ? 0 : rnd.Next( 0, stories );
			var lx = minLx + (float)rnd.NextDouble() * ( maxLx - minLx );
			var ly = minLy + (float)rnd.NextDouble() * ( maxLy - minLy );
			if ( !TryLocalPlanarToGridCell( lx, ly, widthCells, depthCells, cell, out var gx, out var gy ) )
				continue;
			if ( !CellHasFloorSlab( storyIndex, gx, gy, widthCells, depthCells, stories, buildingRoot ) )
				continue;
			if ( IsBlockedInteriorPropCell( gx, gy, widthCells, depthCells, stories, hints, buildingRoot ) )
				continue;

			localPos = new Vector3( lx, ly, FloorAnchorLocalZForStory( storyIndex ) );
			return true;
		}

		return false;
	}

	public static bool TrySampleWallAlignedLocal(
		Random rnd,
		GameObject buildingRoot,
		int widthCells,
		int depthCells,
		int stories,
		float halfAlongWall,
		float halfFromWall,
		InteriorPlacementHints hints,
		bool groundFloorOnly,
		out Vector3 localPos,
		out float localYawDegrees,
		out int storyIndex,
		int forceStoryIndex = -1 )
	{
		localPos = default;
		localYawDegrees = 0f;
		storyIndex = 0;
		if ( widthCells < 1 || depthCells < 1 || stories < 1 )
			return false;

		var cell = ThornsBuildingModule.Cell;
		var wallT = ThornsBuildingModule.WallThickness;
		var maxX = widthCells - 1;
		var maxY = depthCells - 1;
		var fromWall = cell * 0.5f + wallT + halfFromWall + InteriorWallComfortMargin;
		var alongMargin = cell * 0.28f + halfAlongWall;
		storyIndex = forceStoryIndex >= 0 && forceStoryIndex < stories
			? forceStoryIndex
			: groundFloorOnly ? 0 : PickRandomStoryWithAnyFloor( rnd, buildingRoot, widthCells, depthCells, stories );
		var floorZ = FloorAnchorLocalZForStory( storyIndex );

		for ( var pick = 0; pick < 16; pick++ )
		{
			var side = rnd.Next( 0, 4 );
			if ( hints.DoorSide >= 0 && side == hints.DoorSide && storyIndex == 0 )
				continue;

			float lx;
			float ly;
			float yaw;
			switch ( side )
			{
				case 0:
					ly = GridAxisLocal( 0, depthCells, cell ) - fromWall;
					if ( !TrySampleAlongAxis( rnd, 0, maxX, widthCells, cell, alongMargin, out lx ) )
						continue;
					yaw = 0f;
					if ( IsNearDoorAlongWall( hints, side, lx, ly, widthCells, depthCells, cell ) )
						continue;
					break;
				case 2:
					ly = GridAxisLocal( maxY, depthCells, cell ) + fromWall;
					if ( !TrySampleAlongAxis( rnd, 0, maxX, widthCells, cell, alongMargin, out lx ) )
						continue;
					yaw = 180f;
					if ( IsNearDoorAlongWall( hints, side, lx, ly, widthCells, depthCells, cell ) )
						continue;
					break;
				case 3:
					lx = GridAxisLocal( 0, widthCells, cell ) - fromWall;
					if ( !TrySampleAlongAxis( rnd, 0, maxY, depthCells, cell, alongMargin, out ly ) )
						continue;
					yaw = 90f;
					if ( IsNearDoorAlongWall( hints, side, lx, ly, widthCells, depthCells, cell ) )
						continue;
					break;
				default:
					lx = GridAxisLocal( maxX, widthCells, cell ) + fromWall;
					if ( !TrySampleAlongAxis( rnd, 0, maxY, depthCells, cell, alongMargin, out ly ) )
						continue;
					yaw = 270f;
					if ( IsNearDoorAlongWall( hints, side, lx, ly, widthCells, depthCells, cell ) )
						continue;
					break;
			}

			if ( !IsInsideInsetInterior( lx, ly, widthCells, depthCells, cell, halfAlongWall, halfFromWall ) )
				continue;

			if ( !TryLocalPlanarToGridCell( lx, ly, widthCells, depthCells, cell, out var gx, out var gy ) )
				continue;

			if ( !CellHasFloorSlab( storyIndex, gx, gy, widthCells, depthCells, stories, buildingRoot ) )
				continue;

			if ( IsBlockedInteriorPropCell( gx, gy, widthCells, depthCells, stories, hints, buildingRoot ) )
				continue;

			localPos = new Vector3( lx, ly, floorZ );
			localYawDegrees = yaw;
			return true;
		}

		return false;
	}

	/// <summary>True when <paramref name="gridX"/>/<paramref name="gridY"/> is under a ramp shaft (uses layout host when present).</summary>
	public static bool IsRampShaftCell( int gridX, int gridY, GameObject buildingRoot = null )
	{
		var layout = ThornsProcBuildingLayoutHost.TryGet( buildingRoot );
		if ( layout is not null )
		{
			for ( var rs = 0; rs < layout.Stories - 1; rs++ )
			{
				if ( layout.IsShaftCellForRampAtStory( rs, gridX, gridY ) )
					return true;
			}

			return false;
		}

		return IsRampShaftCellAt( gridX, gridY, 0, 0 );
	}

	public static bool CellHasFloorSlab(
		int storyIndex,
		int gridX,
		int gridY,
		int widthCells,
		int depthCells,
		int stories,
		GameObject buildingRoot = null )
	{
		if ( storyIndex < 0 || storyIndex >= stories )
			return false;
		if ( gridX < 0 || gridX >= widthCells || gridY < 0 || gridY >= depthCells )
			return false;

		var layout = ThornsProcBuildingLayoutHost.TryGet( buildingRoot );
		if ( layout is not null )
		{
			if ( !layout.IsCellOccupied( storyIndex, gridX, gridY ) )
				return false;

			if ( layout.CellNeedsRampShaftUpperCutAt( storyIndex, gridX, gridY ) )
				return false;

			return true;
		}

		if ( storyIndex == 0 )
			return true;
		if ( stories <= 1 || widthCells * depthCells <= 1 )
			return true;

		return !IsRampShaftCell( gridX, gridY, buildingRoot );
	}

	static int PickRandomStoryWithAnyFloor(
		Random rnd,
		GameObject buildingRoot,
		int widthCells,
		int depthCells,
		int stories )
	{
		for ( var attempt = 0; attempt < 12; attempt++ )
		{
			var s = rnd.Next( 0, stories );
			if ( s == 0 )
				return 0;

			for ( var gx = 0; gx < widthCells; gx++ )
			for ( var gy = 0; gy < depthCells; gy++ )
			{
				if ( CellHasFloorSlab( s, gx, gy, widthCells, depthCells, stories, buildingRoot ) )
					return s;
			}
		}

		return 0;
	}

	static bool TryLocalPlanarToGridCell(
		float localX,
		float localY,
		int widthCells,
		int depthCells,
		float cell,
		out int gridX,
		out int gridY )
	{
		gridX = (int)MathF.Round( localX / cell + ( widthCells - 1 ) * 0.5f );
		gridY = (int)MathF.Round( localY / cell + ( depthCells - 1 ) * 0.5f );
		return gridX >= 0 && gridX < widthCells && gridY >= 0 && gridY < depthCells;
	}

	public static bool TryLocalPlanarToGridCellPublic(
		float localX,
		float localY,
		int widthCells,
		int depthCells,
		float cell,
		out int gridX,
		out int gridY ) =>
		TryLocalPlanarToGridCell( localX, localY, widthCells, depthCells, cell, out gridX, out gridY );

	/// <summary>Ray onto proc-building floor; rejects snaps far from the intended storey (e.g. ramp shaft void).</summary>
	public static bool TrySnapToInteriorFloor(
		Scene scene,
		GameObject buildingRoot,
		int storyIndex,
		ref Vector3 worldPos )
	{
		if ( buildingRoot is null || !buildingRoot.IsValid() )
			return false;

		var expectedLocalZ = FloorAnchorLocalZForStory( storyIndex );
		var expectedWorldZ = (buildingRoot.WorldPosition + buildingRoot.WorldRotation * new Vector3( 0f, 0f, expectedLocalZ )).z;
		var maxStoryDelta = ThornsBuildingModule.WallHeight * 0.55f;

		// Proc-building floors are layout-driven; a down-ray often hits ground storey or exterior terrain.
		if ( ThornsProcBuildingLayoutHost.TryGet( buildingRoot ) is not null )
		{
			worldPos = new Vector3( worldPos.x, worldPos.y, expectedWorldZ );
			return true;
		}

		if ( scene is not null && scene.IsValid() )
		{
			var start = worldPos + Vector3.Up * 96f;
			var tr = ThornsTraceUtility.RunRay(
				scene,
				new Ray( start, Vector3.Down ),
				280f,
				ThornsTraceProfile.TerrainInteriorSampleDown,
				null );
			if ( tr.Hit )
			{
				worldPos = tr.HitPosition + Vector3.Up * ThornsLootCrate.ProceduralFloorTopToCrateRootZ;
				return MathF.Abs( worldPos.z - expectedWorldZ ) <= maxStoryDelta;
			}
		}

		worldPos = new Vector3( worldPos.x, worldPos.y, expectedWorldZ );
		return true;
	}

	static bool TrySampleAlongAxis(
		Random rnd,
		int indexMin,
		int indexMax,
		int count,
		float cell,
		float margin,
		out float axisLocal )
	{
		axisLocal = 0f;
		var minL = GridAxisLocal( indexMin, count, cell ) + margin;
		var maxL = GridAxisLocal( indexMax, count, cell ) - margin;
		if ( maxL <= minL )
			return false;

		axisLocal = minL + (float)rnd.NextDouble() * ( maxL - minL );
		return true;
	}

	static bool IsInsideInsetInterior(
		float lx,
		float ly,
		int widthCells,
		int depthCells,
		float cell,
		float halfAlongWall,
		float halfFromWall )
	{
		var maxX = widthCells - 1;
		var maxY = depthCells - 1;
		var inset = cell * 0.5f + ThornsBuildingModule.WallThickness
		            + MathF.Max( halfAlongWall, halfFromWall ) + InteriorWallComfortMargin;

		var minLx = GridAxisLocal( 0, widthCells, cell ) + inset;
		var maxLx = GridAxisLocal( maxX, widthCells, cell ) - inset;
		var minLy = GridAxisLocal( 0, depthCells, cell ) + inset;
		var maxLy = GridAxisLocal( maxY, depthCells, cell ) - inset;

		return lx >= minLx && lx <= maxLx && ly >= minLy && ly <= maxLy;
	}

	static bool IsNearDoorAlongWall(
		InteriorPlacementHints hints,
		int side,
		float lx,
		float ly,
		int widthCells,
		int depthCells,
		float cell )
	{
		if ( hints.DoorSide < 0 || side != hints.DoorSide )
			return false;

		var alongCount = side is 0 or 2 ? widthCells : depthCells;
		var doorCoord = GridAxisLocal(
			Math.Clamp( hints.DoorIndex, 0, Math.Max( 0, alongCount - 1 ) ),
			alongCount,
			cell );
		var coord = side is 0 or 2 ? lx : ly;
		return MathF.Abs( coord - doorCoord ) < cell * 1.05f;
	}

	static float FloorAnchorLocalZ( Random rnd, int stories )
	{
		var sFloor = stories <= 1 ? 0 : rnd.Next( 0, stories );
		return FloorAnchorLocalZForStory( sFloor );
	}

	static float FloorAnchorLocalZForStory( int storyIndex )
	{
		var ft = ThornsBuildingModule.FloorThickness;
		var wallH = ThornsBuildingModule.WallHeight;
		var storyHeight = wallH + ft;
		return storyIndex * storyHeight + ft * 0.5f + ThornsLootCrate.ProceduralFloorTopToCrateRootZ;
	}

	static Vector3 SampleLootAnchorWorldPositionLegacy(
		Random rnd,
		GameObject buildingRoot,
		int widthCells,
		int depthCells,
		int stories,
		int storyIndex = -1 )
	{
		var cell = ThornsBuildingModule.Cell;
		var ix = rnd.Next( 0, widthCells );
		var iy = rnd.Next( 0, depthCells );
		var lx = GridAxisLocal( ix, widthCells, cell );
		var ly = GridAxisLocal( iy, depthCells, cell );
		var maxJit = cell * 0.18f;
		lx += ((float)rnd.NextDouble() * 2f - 1f ) * maxJit;
		ly += ((float)rnd.NextDouble() * 2f - 1f ) * maxJit;
		var floorZ = storyIndex >= 0 && storyIndex < stories
			? FloorAnchorLocalZForStory( storyIndex )
			: FloorAnchorLocalZ( rnd, stories );
		var localTop = new Vector3( lx, ly, floorZ );
		return buildingRoot.WorldPosition + buildingRoot.WorldRotation * localTop;
	}

	static float GridAxisLocal( int index, int count, float cell ) =>
		( index - ( count - 1 ) * 0.5f ) * cell;

	public static float GridAxisLocalPublic( int index, int count, float cell ) =>
		GridAxisLocal( index, count, cell );

	public static bool TryGridCellCenterLocalPublic(
		int gridX,
		int gridY,
		int widthCells,
		int depthCells,
		float cell,
		out float localX,
		out float localY )
	{
		localX = 0f;
		localY = 0f;
		if ( widthCells < 1 || depthCells < 1 )
			return false;
		if ( gridX < 0 || gridX >= widthCells || gridY < 0 || gridY >= depthCells )
			return false;

		localX = GridAxisLocal( gridX, widthCells, cell );
		localY = GridAxisLocal( gridY, depthCells, cell );
		return true;
	}

	public static bool TryGetInteriorWallDeskYawPublic(
		ThornsProcBuildingLayout layout,
		int widthCells,
		int depthCells,
		int stories,
		int storyIndex,
		int gridX,
		int gridY,
		GameObject buildingRoot,
		out float yawDegrees ) =>
		TryGetInteriorWallDeskYaw(
			layout,
			widthCells,
			depthCells,
			stories,
			storyIndex,
			gridX,
			gridY,
			buildingRoot,
			out yawDegrees );
}
