namespace Sandbox;

/// <summary>Interior prop placement for proc-building floors — footprint clearance, separation, and spread.</summary>
public static class ThornsInteriorFurniturePlacement
{
	/// <summary>Extra inset so scaled meshes do not clip perimeter or interior walls.</summary>
	public const float FootprintClearanceInches = 6f;

	/// <summary>Minimum planar gap between furniture centers (inches).</summary>
	public const float PropSeparationGapInches = 8f;

	const float RelaxedFootprintClearanceInches = 4f;
	const float RelaxedPropSeparationGapInches = 6f;
	const int NormalPlacementAttempts = 56;
	const int RelaxedPlacementAttempts = 96;

	/// <summary>Smaller props only — spread fill on tight floors (3×3 cabin, 5×5 tower, etc.).</summary>
	public static readonly string[] CompactOnlyStructureIds = ["chair", "cabinet", "desk"];

	/// <summary>Relaxed planar gap for spread / anchor fill (still uses full overlap radii).</summary>
	public const float SpreadFillSeparationGapInches = 10f;

	public readonly struct PlacedProp
	{
		public Vector3 WorldPos { get; init; }
		public float YawDegrees { get; init; }
		public float PlanarRadius { get; init; }
		public int StoryIndex { get; init; }
		public int GridX { get; init; }
		public int GridY { get; init; }
	}

	public sealed class Batch
	{
		readonly List<PlacedProp> _placed = new();
		readonly HashSet<(GameObject Building, int Story, int Gx, int Gy)> _occupiedFloorCells = new();

		public ThornsInteriorFurnitureScatterDebug.RejectReason LastReject { get; private set; }

		/// <summary>Reserve space for loot crates / radios before furniture scatter (overlap only).</summary>
		public void RegisterReservedAnchor(
			GameObject buildingRoot,
			int widthCells,
			int depthCells,
			Vector3 worldPos,
			float planarRadius,
			int storyIndex )
		{
			var gx = -1;
			var gy = -1;
			if ( TryWorldPosToFloorCell( buildingRoot, widthCells, depthCells, worldPos, out gx, out gy ) )
				MarkFloorCellOccupied( buildingRoot, storyIndex, gx, gy );

			_placed.Add( new PlacedProp
			{
				WorldPos = worldPos,
				YawDegrees = 0f,
				PlanarRadius = planarRadius,
				StoryIndex = storyIndex,
				GridX = gx,
				GridY = gy
			} );
		}

		public void Register(
			GameObject buildingRoot,
			int widthCells,
			int depthCells,
			Vector3 worldPos,
			float yawDegrees,
			float planarRadius,
			int storyIndex,
			in ThornsPlaceableFurnitureCatalog.Entry profile,
			Rotation worldRotation,
			bool relaxed = false )
		{
			var markFullFootprint = !relaxed && widthCells * depthCells >= 30;
			if ( markFullFootprint )
				MarkFootprintFloorCells( buildingRoot, widthCells, depthCells, worldPos, worldRotation, in profile, storyIndex );

			var gx = -1;
			var gy = -1;
			if ( TryWorldPosToFloorCell( buildingRoot, widthCells, depthCells, worldPos, out gx, out gy ) )
				MarkFloorCellOccupied( buildingRoot, storyIndex, gx, gy );

			_placed.Add( new PlacedProp
			{
				WorldPos = worldPos,
				YawDegrees = yawDegrees,
				PlanarRadius = planarRadius,
				StoryIndex = storyIndex,
				GridX = gx,
				GridY = gy
			} );
		}

		public bool IsFloorCellFree( GameObject buildingRoot, int storyIndex, int gridX, int gridY ) =>
			buildingRoot is not null
			&& buildingRoot.IsValid()
			&& !_occupiedFloorCells.Contains( (buildingRoot, storyIndex, gridX, gridY) );

		public Func<int, int, bool> FloorCellFilter( GameObject buildingRoot, int storyIndex ) =>
			( gx, gy ) => IsFloorCellFree( buildingRoot, storyIndex, gx, gy );

		void MarkFloorCellOccupied( GameObject buildingRoot, int storyIndex, int gridX, int gridY )
		{
			if ( buildingRoot is null || !buildingRoot.IsValid() )
				return;

			_occupiedFloorCells.Add( (buildingRoot, storyIndex, gridX, gridY) );
		}

		/// <summary>
		/// Pick a walkable cell that maximizes distance from peers (grid + planar overlap).
		/// Does not register — caller spawns then calls <see cref="Register"/>.
		/// </summary>
		public bool TrySelectSpreadAnchorCell(
			Random rnd,
			GameObject buildingRoot,
			int widthCells,
			int depthCells,
			int stories,
			in ThornsProcBuildingInteriorSample.InteriorPlacementHints hints,
			in ThornsPlaceableFurnitureCatalog.Entry profile,
			int storyIndex,
			out Vector3 worldPos,
			out Rotation worldRotation,
			bool relaxed = true,
			int minGridSpacingOverride = -1 )
		{
			worldPos = default;
			worldRotation = Rotation.Identity;
			LastReject = ThornsInteriorFurnitureScatterDebug.RejectReason.None;
			if ( buildingRoot is null || !buildingRoot.IsValid() )
			{
				LastReject = ThornsInteriorFurnitureScatterDebug.RejectReason.InvalidArgs;
				return false;
			}

			var minGridSpacing = minGridSpacingOverride >= 0
				? minGridSpacingOverride
				: MinGridSpacingForSpread( widthCells, depthCells );
			var cellFilter = FloorCellFilter( buildingRoot, storyIndex );
			var walkable = 0;
			var bestScore = -1f;
			var found = false;
			Vector3 bestPos = default;
			Rotation bestRot = Rotation.Identity;
			var tieBreak = 0;

			for ( var gx = 0; gx < widthCells; gx++ )
			for ( var gy = 0; gy < depthCells; gy++ )
			{
				if ( !ThornsProcBuildingInteriorSample.CellHasFloorSlab(
					     storyIndex,
					     gx,
					     gy,
					     widthCells,
					     depthCells,
					     stories,
					     buildingRoot ) )
					continue;

				walkable++;
				if ( !cellFilter( gx, gy ) )
					continue;

				if ( ThornsProcBuildingInteriorSample.IsBlockedInteriorPropCellPublic(
					     gx,
					     gy,
					     widthCells,
					     depthCells,
					     stories,
					     hints,
					     buildingRoot,
					     relaxed ) )
					continue;

				if ( !HasMinGridSpacingFromPeers( storyIndex, gx, gy, minGridSpacing ) )
				{
					LastReject = ThornsInteriorFurnitureScatterDebug.RejectReason.OverlapPeer;
					continue;
				}

				if ( !ThornsProcBuildingInteriorSample.TryGridCellCenterLocalPublic(
					     gx,
					     gy,
					     widthCells,
					     depthCells,
					     ThornsBuildingModule.Cell,
					     out var lx,
					     out var ly ) )
					continue;

				var candidate = buildingRoot.WorldPosition
				                + buildingRoot.WorldRotation * new Vector3( lx, ly, ThornsProcBuildingInteriorSample.InteriorFloorWalkLocalZForStory( storyIndex ) );
				var rot = RandomGridAlignedRotation( buildingRoot, rnd, in hints, gx * 31 + gy * 17 + storyIndex * 97 );
				var rotFinal = ApplyInteriorDecorYawOffset( buildingRoot, rot, profile.InteriorDecorYawOffsetDegrees );
				candidate = ThornsPlaceableFurnitureCatalog.ApplyInteriorPlacementOffset(
					buildingRoot,
					candidate,
					rotFinal,
					in profile,
					in hints,
					widthCells,
					depthCells );

				if ( OverlapsPlacedOnStory( buildingRoot, storyIndex, candidate, in profile, relaxed: true, useSpreadGap: true ) )
				{
					LastReject = ThornsInteriorFurnitureScatterDebug.RejectReason.OverlapPeer;
					continue;
				}

				var score = ScoreSpreadCandidate(
					buildingRoot,
					storyIndex,
					candidate,
					widthCells,
					depthCells,
					in hints );
				var jitter = (float)rnd.NextDouble() * 0.01f;
				if ( score + jitter <= bestScore )
					continue;

				bestScore = score + jitter;
				bestPos = candidate;
				bestRot = rotFinal;
				found = true;
				tieBreak++;
			}

			if ( !found && minGridSpacing > 1 )
				return TrySelectSpreadAnchorCell(
					rnd,
					buildingRoot,
					widthCells,
					depthCells,
					stories,
					hints,
					profile,
					storyIndex,
					out worldPos,
					out worldRotation,
					relaxed,
					minGridSpacingOverride: 1 );

			if ( !found )
			{
				LastReject = walkable == 0
					? ThornsInteriorFurnitureScatterDebug.RejectReason.NoWalkableCells
					: ThornsInteriorFurnitureScatterDebug.RejectReason.NoDecorCandidate;
				return false;
			}

			worldPos = bestPos;
			worldRotation = bestRot;
			_ = tieBreak;
			return true;
		}

		/// <summary>Place at the center of a floor cell (scripted floorplans).</summary>
		public bool TryPlaceAtGridCell(
			GameObject buildingRoot,
			int widthCells,
			int depthCells,
			int stories,
			int storyIndex,
			int gridX,
			int gridY,
			in ThornsProcBuildingInteriorSample.InteriorPlacementHints hints,
			in ThornsPlaceableFurnitureCatalog.Entry profile,
			out Vector3 worldPos,
			out Rotation worldRotation,
			bool relaxed = true )
		{
			worldPos = default;
			worldRotation = Rotation.Identity;
			LastReject = ThornsInteriorFurnitureScatterDebug.RejectReason.None;

			if ( buildingRoot is null || !buildingRoot.IsValid() )
			{
				LastReject = ThornsInteriorFurnitureScatterDebug.RejectReason.InvalidArgs;
				return false;
			}

			if ( !ThornsProcBuildingInteriorSample.CellHasFloorSlab(
				     storyIndex,
				     gridX,
				     gridY,
				     widthCells,
				     depthCells,
				     stories,
				     buildingRoot ) )
			{
				LastReject = ThornsInteriorFurnitureScatterDebug.RejectReason.FootprintNoFloor;
				return false;
			}

			if ( !ThornsProcBuildingInteriorSample.TryGridCellCenterLocalPublic(
				     gridX,
				     gridY,
				     widthCells,
				     depthCells,
				     ThornsBuildingModule.Cell,
				     out var lx,
				     out var ly ) )
			{
				LastReject = ThornsInteriorFurnitureScatterDebug.RejectReason.FootprintOutOfBounds;
				return false;
			}

			var layout = ThornsProcBuildingLayoutHost.TryGet( buildingRoot );
			var hasTune = ThornsPlaceableFurnitureCatalog.TryGetInteriorPlacementTune(
				profile.StructureDefId,
				storyIndex,
				gridX,
				gridY,
				widthCells,
				depthCells,
				out var placementTune );
			var offsetBuildingLocal = hasTune
				? placementTune.OffsetFromCellCenterBuildingLocal
				: profile.InteriorPlacementLocalOffsetInches;

			var candidateLocal = ThornsPlaceableFurnitureCatalog.BuildInteriorFurnitureLocalPosition(
				storyIndex,
				lx,
				ly,
				offsetBuildingLocal );
			var candidate = buildingRoot.WorldPosition + buildingRoot.WorldRotation * candidateLocal;
			Rotation rotFinal;
			if ( hasTune )
			{
				rotFinal = buildingRoot.WorldRotation * Rotation.FromYaw( placementTune.BuildingLocalYawDegrees );
			}
			else
			{
				Rotation rot;
				if ( ThornsInteriorFurnitureCanonicalSlots.TryGetScriptedCornerYaw(
					     profile.StructureDefId,
					     storyIndex,
					     gridX,
					     gridY,
					     widthCells,
					     depthCells,
					     out var cornerYawDeg ) )
					rot = buildingRoot.WorldRotation * Rotation.FromYaw( cornerYawDeg );
				else if ( layout is not null
				          && ThornsProcBuildingInteriorSample.TryGetInteriorWallDeskYawPublic(
					          layout,
					          widthCells,
					          depthCells,
					          stories,
					          storyIndex,
					          gridX,
					          gridY,
					          buildingRoot,
					          out var wallYawDeg ) )
					rot = buildingRoot.WorldRotation * Rotation.FromYaw( wallYawDeg );
				else
					rot = RandomGridAlignedRotation(
						buildingRoot,
						new Random( gridX * 31 + gridY * 17 + storyIndex * 97 ),
						in hints,
						gridX * 31 + gridY * 17 + storyIndex * 97 );
				rotFinal = ApplyInteriorDecorYawOffset( buildingRoot, rot, profile.InteriorDecorYawOffsetDegrees );
				var profilePlanarOnly = profile with { InteriorPlacementLocalOffsetInches = Vector3.Zero };
				var hintsWithGrid = hints.WithPlacementGrid( gridX, gridY );
				candidate = ThornsPlaceableFurnitureCatalog.ApplyInteriorPlacementOffset(
					buildingRoot,
					candidate,
					rotFinal,
					in profilePlanarOnly,
					in hintsWithGrid,
					widthCells,
					depthCells );
			}

			if ( !TryValidatePlacement(
				     buildingRoot,
				     widthCells,
				     depthCells,
				     stories,
				     hints,
				     profile,
				     storyIndex,
				     candidate,
				     rotFinal,
				     relaxed,
				     out var reject,
				     gridX,
				     gridY ) )
			{
				LastReject = reject;
				return false;
			}

			worldPos = candidate;
			worldRotation = rotFinal;
			var radius = ThornsPlaceableFurniturePresentation.PlacementPlanarHalfExtent( in profile );
			Register(
				buildingRoot,
				widthCells,
				depthCells,
				worldPos,
				worldRotation.Angles().yaw,
				radius,
				storyIndex,
				in profile,
				worldRotation,
				relaxed );
			return true;
		}

		public void CommitSpreadAnchorPlacement(
			GameObject buildingRoot,
			int widthCells,
			int depthCells,
			Vector3 worldPos,
			Rotation worldRotation,
			in ThornsPlaceableFurnitureCatalog.Entry profile,
			int storyIndex,
			bool relaxed = true )
		{
			var radius = ThornsPlaceableFurniturePresentation.PlacementPlanarHalfExtent( in profile );
			Register(
				buildingRoot,
				widthCells,
				depthCells,
				worldPos,
				worldRotation.Angles().yaw,
				radius,
				storyIndex,
				in profile,
				worldRotation,
				relaxed );
		}

		static int MinGridSpacingForSpread( int widthCells, int depthCells )
		{
			var cells = widthCells * depthCells;
			if ( cells <= 9 )
				return 1;

			if ( cells <= 20 )
				return 2;

			return 2;
		}

		bool HasMinGridSpacingFromPeers( int storyIndex, int gridX, int gridY, int minChebyshevCells )
		{
			if ( minChebyshevCells <= 0 )
				return true;

			for ( var i = 0; i < _placed.Count; i++ )
			{
				var p = _placed[i];
				if ( p.StoryIndex != storyIndex || p.GridX < 0 || p.GridY < 0 )
					continue;

				var dx = Math.Abs( gridX - p.GridX );
				var dy = Math.Abs( gridY - p.GridY );
				if ( Math.Max( dx, dy ) < minChebyshevCells )
					return false;
			}

			return true;
		}

		public bool TryPlaceWallBacked(
			Random rnd,
			Scene scene,
			GameObject buildingRoot,
			int widthCells,
			int depthCells,
			int stories,
			in ThornsProcBuildingInteriorSample.InteriorPlacementHints hints,
			in ThornsPlaceableFurnitureCatalog.Entry profile,
			int storyIndex,
			out Vector3 worldPos,
			out Rotation worldRotation,
			bool relaxed = false )
		{
			_ = scene;
			LastReject = ThornsInteriorFurnitureScatterDebug.RejectReason.None;
			return TryPlaceBestDecor(
				rnd,
				buildingRoot,
				widthCells,
				depthCells,
				stories,
				hints,
				profile,
				storyIndex,
				preferWallBacked: false,
				relaxed,
				out worldPos,
				out worldRotation );
		}

		/// <summary>
		/// Perimeter wall slot with depth offset — back against the wall, front faces into the room
		/// (<see cref="ThornsProcBuildingInteriorSample.TrySampleWallAlignedLocal"/>).
		/// </summary>
		public bool TryPlaceWallFlush(
			Random rnd,
			Scene scene,
			GameObject buildingRoot,
			int widthCells,
			int depthCells,
			int stories,
			in ThornsProcBuildingInteriorSample.InteriorPlacementHints hints,
			in ThornsPlaceableFurnitureCatalog.Entry profile,
			int storyIndex,
			out Vector3 worldPos,
			out Rotation worldRotation,
			bool relaxed = false )
		{
			_ = scene;
			worldPos = default;
			worldRotation = Rotation.Identity;
			LastReject = ThornsInteriorFurnitureScatterDebug.RejectReason.None;
			if ( buildingRoot is null || !buildingRoot.IsValid() )
			{
				LastReject = ThornsInteriorFurnitureScatterDebug.RejectReason.InvalidArgs;
				return false;
			}

			var radius = ThornsPlaceableFurniturePresentation.PlacementPlanarHalfExtent( in profile );
			var cellFilter = FloorCellFilter( buildingRoot, storyIndex );
			var bestScore = -1f;
			var found = false;
			Vector3 bestPos = default;
			Rotation bestRot = Rotation.Identity;

			var maxAttempts = relaxed ? RelaxedPlacementAttempts : NormalPlacementAttempts;
			for ( var attempt = 0; attempt < maxAttempts; attempt++ )
			{
				var yawGuess = ThornsProcBuildingInteriorSample.PickDoorRelativeGridYawDegrees( rnd, in hints );
				ComputePlanarFootprintHalves( in profile, yawGuess, out var halfAlong, out var halfFrom );

				if ( !ThornsProcBuildingInteriorSample.TrySampleWallAlignedLocal(
					     rnd,
					     buildingRoot,
					     widthCells,
					     depthCells,
					     stories,
					     halfAlong,
					     halfFrom,
					     hints,
					     groundFloorOnly: false,
					     out var localPos,
					     out var localYaw,
					     out var sampledStory,
					     forceStoryIndex: storyIndex ) )
					continue;

				if ( sampledStory != storyIndex )
					continue;

				localPos = localPos with { z = ThornsProcBuildingInteriorSample.InteriorFloorWalkLocalZForStory( sampledStory ) };
				var candidate = buildingRoot.WorldPosition + buildingRoot.WorldRotation * localPos;
				if ( !TryWorldPosToFloorCell(
					     buildingRoot,
					     widthCells,
					     depthCells,
					     candidate,
					     out var gx,
					     out var gy )
				     || !cellFilter( gx, gy ) )
					continue;

				var rot = buildingRoot.WorldRotation * Rotation.FromYaw( localYaw );
				var rotFinal = ApplyInteriorDecorYawOffset( buildingRoot, rot, profile.InteriorDecorYawOffsetDegrees );
				candidate = ThornsPlaceableFurnitureCatalog.ApplyInteriorPlacementOffset(
					buildingRoot,
					candidate,
					rotFinal,
					in profile,
					in hints,
					widthCells,
					depthCells );
				if ( !TryValidatePlacement(
					     buildingRoot,
					     widthCells,
					     depthCells,
					     stories,
					     hints,
					     profile,
					     storyIndex,
					     candidate,
					     rotFinal,
					     relaxed,
					     out var reject ) )
				{
					LastReject = reject;
					continue;
				}

				var score = ScoreSpreadCandidate(
					buildingRoot,
					storyIndex,
					candidate,
					widthCells,
					depthCells,
					in hints );
				if ( score <= bestScore )
					continue;

				bestScore = score;
				bestPos = candidate;
				bestRot = rotFinal;
				found = true;
			}

			if ( !found )
			{
				LastReject = ThornsInteriorFurnitureScatterDebug.RejectReason.NoDecorCandidate;
				return false;
			}

			worldPos = bestPos;
			worldRotation = bestRot;
			Register(
				buildingRoot,
				widthCells,
				depthCells,
				worldPos,
				worldRotation.Angles().yaw,
				radius,
				storyIndex,
				in profile,
				worldRotation,
				relaxed );
			return true;
		}

		public bool TryPlaceCenterTile(
			Random rnd,
			Scene scene,
			GameObject buildingRoot,
			int widthCells,
			int depthCells,
			int stories,
			in ThornsProcBuildingInteriorSample.InteriorPlacementHints hints,
			in ThornsPlaceableFurnitureCatalog.Entry profile,
			int storyIndex,
			out Vector3 worldPos,
			out Rotation worldRotation,
			bool relaxed = false )
		{
			_ = scene;
			LastReject = ThornsInteriorFurnitureScatterDebug.RejectReason.None;
			if ( TryPlaceBestDecor(
				     rnd,
				     buildingRoot,
				     widthCells,
				     depthCells,
				     stories,
				     hints,
				     profile,
				     storyIndex,
				     preferWallBacked: false,
				     relaxed,
				     out worldPos,
				     out worldRotation ) )
				return true;

			return false;
		}

		public bool TryPlaceOnFreeFloorCell(
			Random rnd,
			GameObject buildingRoot,
			int widthCells,
			int depthCells,
			int stories,
			in ThornsProcBuildingInteriorSample.InteriorPlacementHints hints,
			in ThornsPlaceableFurnitureCatalog.Entry profile,
			int storyIndex,
			out Vector3 worldPos,
			out Rotation worldRotation,
			bool relaxed = false ) =>
			TryPlaceBestDecor(
				rnd,
				buildingRoot,
				widthCells,
				depthCells,
				stories,
				hints,
				profile,
				storyIndex,
				preferWallBacked: false,
				relaxed,
				out worldPos,
				out worldRotation );

		bool TryPlaceBestDecor(
			Random rnd,
			GameObject buildingRoot,
			int widthCells,
			int depthCells,
			int stories,
			in ThornsProcBuildingInteriorSample.InteriorPlacementHints hints,
			in ThornsPlaceableFurnitureCatalog.Entry profile,
			int storyIndex,
			bool preferWallBacked,
			bool relaxed,
			out Vector3 worldPos,
			out Rotation worldRotation )
		{
			worldPos = default;
			worldRotation = Rotation.Identity;
			LastReject = ThornsInteriorFurnitureScatterDebug.RejectReason.None;
			if ( buildingRoot is null || !buildingRoot.IsValid() )
			{
				LastReject = ThornsInteriorFurnitureScatterDebug.RejectReason.InvalidArgs;
				return false;
			}

			var radius = ThornsPlaceableFurniturePresentation.PlacementPlanarHalfExtent( in profile );
			var cellFilter = FloorCellFilter( buildingRoot, storyIndex );
			var bestScore = -1f;
			var found = false;
			Vector3 bestPos = default;
			Rotation bestRot = Rotation.Identity;
			var maxAttempts = relaxed ? RelaxedPlacementAttempts : NormalPlacementAttempts;
			var sawCandidate = false;

			for ( var attempt = 0; attempt < maxAttempts; attempt++ )
			{
				if ( !ThornsProcBuildingInteriorSample.TryFindInteriorDecorPlacementOnStory(
					     buildingRoot,
					     widthCells,
					     depthCells,
					     stories,
					     storyIndex,
					     hints,
					     rnd,
					     preferWallBacked,
					     out var candidate,
					     out var rot,
					     cellFilter ) )
				{
					LastReject = ThornsInteriorFurnitureScatterDebug.RejectReason.NoWalkableCells;
					continue;
				}

				sawCandidate = true;
				var rotFinal = ApplyInteriorDecorYawOffset( buildingRoot, rot, profile.InteriorDecorYawOffsetDegrees );
				candidate = ThornsPlaceableFurnitureCatalog.ApplyInteriorPlacementOffset(
					buildingRoot,
					candidate,
					rotFinal,
					in profile,
					in hints,
					widthCells,
					depthCells );
				if ( !TryValidatePlacement(
					     buildingRoot,
					     widthCells,
					     depthCells,
				     stories,
				     hints,
				     profile,
				     storyIndex,
				     candidate,
				     rotFinal,
				     relaxed,
				     out var reject ) )
				{
					LastReject = reject;
					continue;
				}

				var score = ScoreSpreadCandidate(
					buildingRoot,
					storyIndex,
					candidate,
					widthCells,
					depthCells,
					in hints );
				if ( score <= bestScore )
					continue;

				bestScore = score;
				bestPos = candidate;
				bestRot = rotFinal;
				found = true;
			}

			if ( !found )
			{
				LastReject = sawCandidate
					? ThornsInteriorFurnitureScatterDebug.RejectReason.NoDecorCandidate
					: ThornsInteriorFurnitureScatterDebug.RejectReason.NoWalkableCells;
				return false;
			}

			worldPos = bestPos;
			worldRotation = bestRot;
			Register(
				buildingRoot,
				widthCells,
				depthCells,
				worldPos,
				worldRotation.Angles().yaw,
				radius,
				storyIndex,
				in profile,
				worldRotation,
				relaxed );
			return true;
		}

		bool TryValidatePlacement(
			GameObject buildingRoot,
			int widthCells,
			int depthCells,
			int stories,
			in ThornsProcBuildingInteriorSample.InteriorPlacementHints hints,
			in ThornsPlaceableFurnitureCatalog.Entry profile,
			int storyIndex,
			Vector3 worldPos,
			Rotation worldRotation,
			bool relaxed,
			out ThornsInteriorFurnitureScatterDebug.RejectReason reject,
			int gridX = -1,
			int gridY = -1 )
		{
			reject = ThornsInteriorFurnitureScatterDebug.RejectReason.None;

			if ( hints.ScriptedFloorplanExactExclusions )
			{
				var checkGx = gridX;
				var checkGy = gridY;
				if ( checkGx < 0 || checkGy < 0 )
				{
					var scriptedLocal = buildingRoot.WorldRotation.Inverse * (worldPos - buildingRoot.WorldPosition);
					if ( !ThornsProcBuildingInteriorSample.TryLocalPlanarToGridCellPublic(
						     scriptedLocal.x,
						     scriptedLocal.y,
						     widthCells,
						     depthCells,
						     ThornsBuildingModule.Cell,
						     out checkGx,
						     out checkGy ) )
					{
						reject = ThornsInteriorFurnitureScatterDebug.RejectReason.FootprintOutOfBounds;
						return false;
					}
				}

				if ( ThornsProcBuildingInteriorSample.IsScriptedFloorplanBlockedCellPublic(
					     storyIndex,
					     checkGx,
					     checkGy,
					     widthCells,
					     depthCells,
					     stories,
					     hints,
					     buildingRoot ) )
				{
					reject = ThornsInteriorFurnitureScatterDebug.RejectReason.FootprintDoorOrRamp;
					return false;
				}

				if ( !IsFloorCellFree( buildingRoot, storyIndex, checkGx, checkGy ) )
				{
					reject = ThornsInteriorFurnitureScatterDebug.RejectReason.OverlapPeer;
					return false;
				}

				return true;
			}

			if ( !TryValidateInteriorFootprint(
				     buildingRoot,
				     widthCells,
				     depthCells,
				     stories,
				     hints,
				     profile,
				     storyIndex,
				     worldPos,
				     worldRotation,
				     relaxed,
				     out reject ) )
				return false;

			if ( OverlapsPlacedOnStory( buildingRoot, storyIndex, worldPos, in profile, relaxed ) )
			{
				reject = ThornsInteriorFurnitureScatterDebug.RejectReason.OverlapPeer;
				return false;
			}

			return true;
		}

		float ScoreSpreadCandidate(
			GameObject buildingRoot,
			int storyIndex,
			Vector3 candidateWorld,
			int widthCells,
			int depthCells,
			in ThornsProcBuildingInteriorSample.InteriorPlacementHints hints )
		{
			var localCand = buildingRoot.WorldRotation.Inverse * (candidateWorld - buildingRoot.WorldPosition);
			var cx = localCand.x;
			var cy = localCand.y;
			var minDistSq = float.MaxValue;
			var hasPeerOnStory = false;

			for ( var i = 0; i < _placed.Count; i++ )
			{
				var p = _placed[i];
				if ( p.StoryIndex != storyIndex )
					continue;

				hasPeerOnStory = true;
				var localPeer = buildingRoot.WorldRotation.Inverse * (p.WorldPos - buildingRoot.WorldPosition);
				var dx = cx - localPeer.x;
				var dy = cy - localPeer.y;
				var distSq = dx * dx + dy * dy;
				if ( distSq < minDistSq )
					minDistSq = distSq;
			}

			if ( hasPeerOnStory )
				return minDistSq;

			var cell = ThornsBuildingModule.Cell;
			var fromCenterSq = cx * cx + cy * cy;

			if ( hints.DoorSide >= 0 && storyIndex == 0 )
			{
				var alongCount = hints.DoorSide is 0 or 2 ? widthCells : depthCells;
				var doorAlong = ThornsProcBuildingInteriorSample.GridAxisLocalPublic(
					Math.Clamp( hints.DoorIndex, 0, Math.Max( 0, alongCount - 1 ) ),
					alongCount,
					cell );
				var doorX = hints.DoorSide is 0 or 2 ? doorAlong : ThornsProcBuildingInteriorSample.GridAxisLocalPublic( hints.DoorSide == 3 ? 0 : widthCells - 1, widthCells, cell );
				var doorY = hints.DoorSide is 0 or 2 ? ThornsProcBuildingInteriorSample.GridAxisLocalPublic( hints.DoorSide == 0 ? 0 : depthCells - 1, depthCells, cell ) : doorAlong;
				var ddx = cx - doorX;
				var ddy = cy - doorY;
				return ddx * ddx + ddy * ddy + fromCenterSq * 0.15f;
			}

			return fromCenterSq;
		}

		bool OverlapsPlacedOnStory(
			GameObject buildingRoot,
			int storyIndex,
			Vector3 candidateWorld,
			in ThornsPlaceableFurnitureCatalog.Entry profile,
			bool relaxed,
			bool useSpreadGap = false )
		{
			var radius = ThornsPlaceableFurniturePresentation.PlacementPlanarHalfExtent( in profile );
			var localCand = buildingRoot.WorldRotation.Inverse * (candidateWorld - buildingRoot.WorldPosition);
			var cx = localCand.x;
			var cy = localCand.y;
			var gap = useSpreadGap
				? SpreadFillSeparationGapInches
				: relaxed ? RelaxedPropSeparationGapInches : PropSeparationGapInches;

			for ( var i = 0; i < _placed.Count; i++ )
			{
				var p = _placed[i];
				if ( p.StoryIndex != storyIndex )
					continue;

				var peerRadius = p.PlanarRadius;
				var localPeer = buildingRoot.WorldRotation.Inverse * (p.WorldPos - buildingRoot.WorldPosition);
				var dx = cx - localPeer.x;
				var dy = cy - localPeer.y;
				var minSep = radius + peerRadius + gap;
				if ( dx * dx + dy * dy < minSep * minSep )
					return true;
			}

			return false;
		}

		void MarkFootprintFloorCells(
			GameObject buildingRoot,
			int widthCells,
			int depthCells,
			Vector3 worldPos,
			Rotation worldRotation,
			in ThornsPlaceableFurnitureCatalog.Entry profile,
			int storyIndex )
		{
			if ( !TryComputeFootprintCorners(
				     buildingRoot,
				     worldPos,
				     worldRotation,
				     in profile,
				     FootprintClearanceInches,
				     out var c0,
				     out var c1,
				     out var c2,
				     out var c3 ) )
				return;

			var minX = MathF.Min( MathF.Min( c0.x, c1.x ), MathF.Min( c2.x, c3.x ) );
			var maxX = MathF.Max( MathF.Max( c0.x, c1.x ), MathF.Max( c2.x, c3.x ) );
			var minY = MathF.Min( MathF.Min( c0.y, c1.y ), MathF.Min( c2.y, c3.y ) );
			var maxY = MathF.Max( MathF.Max( c0.y, c1.y ), MathF.Max( c2.y, c3.y ) );
			var cell = ThornsBuildingModule.Cell;

			if ( !ThornsProcBuildingInteriorSample.TryLocalPlanarToGridCellPublic(
				     minX,
				     minY,
				     widthCells,
				     depthCells,
				     cell,
				     out var gx0,
				     out var gy0 ) )
				return;

			if ( !ThornsProcBuildingInteriorSample.TryLocalPlanarToGridCellPublic(
				     maxX,
				     maxY,
				     widthCells,
				     depthCells,
				     cell,
				     out var gx1,
				     out var gy1 ) )
				return;

			if ( gx0 > gx1 )
				( gx0, gx1 ) = ( gx1, gx0 );
			if ( gy0 > gy1 )
				( gy0, gy1 ) = ( gy1, gy0 );

			for ( var gx = gx0; gx <= gx1; gx++ )
			for ( var gy = gy0; gy <= gy1; gy++ )
			{
				if ( !ThornsProcBuildingInteriorSample.TryGridCellCenterLocalPublic(
					     gx,
					     gy,
					     widthCells,
					     depthCells,
					     cell,
					     out var lx,
					     out var ly ) )
					continue;

				if ( IsPointInsideFootprint( lx, ly, c0, c1, c2, c3, cell * 0.08f ) )
					MarkFloorCellOccupied( buildingRoot, storyIndex, gx, gy );
			}
		}

		static bool IsPointInsideFootprint(
			float px,
			float py,
			Vector2 c0,
			Vector2 c1,
			Vector2 c2,
			Vector2 c3,
			float inflate )
		{
			return PointInTriangle( px, py, c0, c1, c2, inflate )
			       || PointInTriangle( px, py, c0, c2, c3, inflate );
		}

		static bool PointInTriangle( float px, float py, Vector2 a, Vector2 b, Vector2 c, float inflate )
		{
			if ( inflate > 0.001f )
			{
				var cx = ( a.x + b.x + c.x ) / 3f;
				var cy = ( a.y + b.y + c.y ) / 3f;
				a = InflateFrom( a, cx, cy, inflate );
				b = InflateFrom( b, cx, cy, inflate );
				c = InflateFrom( c, cx, cy, inflate );
			}

			var d0 = Sign( px, py, a, b );
			var d1 = Sign( px, py, b, c );
			var d2 = Sign( px, py, c, a );
			var hasNeg = d0 < 0f || d1 < 0f || d2 < 0f;
			var hasPos = d0 > 0f || d1 > 0f || d2 > 0f;
			return !( hasNeg && hasPos );
		}

		static Vector2 InflateFrom( Vector2 p, float cx, float cy, float amount )
		{
			var dx = p.x - cx;
			var dy = p.y - cy;
			var len = MathF.Sqrt( dx * dx + dy * dy );
			if ( len < 0.001f )
				return p;

			var scale = ( len + amount ) / len;
			return new Vector2( cx + dx * scale, cy + dy * scale );
		}

		static float Sign( float px, float py, Vector2 a, Vector2 b ) =>
			( px - b.x ) * ( a.y - b.y ) - ( a.x - b.x ) * ( py - b.y );

		static bool TryWorldPosToFloorCell(
			GameObject buildingRoot,
			int widthCells,
			int depthCells,
			Vector3 worldPos,
			out int gridX,
			out int gridY )
		{
			gridX = 0;
			gridY = 0;
			if ( buildingRoot is null || !buildingRoot.IsValid() )
				return false;

			var local = buildingRoot.WorldRotation.Inverse * (worldPos - buildingRoot.WorldPosition);
			var cell = ThornsBuildingModule.Cell;
			return ThornsProcBuildingInteriorSample.TryLocalPlanarToGridCellPublic(
				local.x,
				local.y,
				widthCells,
				depthCells,
				cell,
				out gridX,
				out gridY );
		}

		/// <summary>Local yaw snapped to 0°/90°/180°/270° relative to the building root.</summary>
		static Rotation SnapRotationToBuildingGrid( GameObject buildingRoot, Rotation rotation )
		{
			if ( buildingRoot is null || !buildingRoot.IsValid() )
				return rotation;

			var localYaw = ( buildingRoot.WorldRotation.Inverse * rotation ).Angles().yaw;
			var snapped = MathF.Round( localYaw / 90f ) * 90f;
			snapped = ( snapped % 360f + 360f ) % 360f;
			return buildingRoot.WorldRotation * Rotation.FromYaw( snapped );
		}

		static Rotation RandomGridAlignedRotation(
			GameObject buildingRoot,
			Random rnd,
			in ThornsProcBuildingInteriorSample.InteriorPlacementHints hints,
			int stableSalt = -1 )
		{
			var localYaw = ThornsProcBuildingInteriorSample.PickDoorRelativeGridYawDegrees( rnd, in hints, stableSalt );
			return buildingRoot.WorldRotation * Rotation.FromYaw( localYaw );
		}

		static Rotation ApplyInteriorDecorYawOffset( GameObject buildingRoot, Rotation baseRotation, float yawOffsetDegrees )
		{
			if ( buildingRoot is null || !buildingRoot.IsValid() )
				return baseRotation;

			var localYaw = ( buildingRoot.WorldRotation.Inverse * baseRotation ).Angles().yaw;
			if ( MathF.Abs( yawOffsetDegrees ) > 0.001f )
				localYaw += yawOffsetDegrees;

			localYaw = ( localYaw % 360f + 360f ) % 360f;
			return SnapRotationToBuildingGrid(
				buildingRoot,
				buildingRoot.WorldRotation * Rotation.FromYaw( localYaw ) );
		}

		static bool TryValidateInteriorFootprint(
			GameObject buildingRoot,
			int widthCells,
			int depthCells,
			int stories,
			in ThornsProcBuildingInteriorSample.InteriorPlacementHints hints,
			in ThornsPlaceableFurnitureCatalog.Entry profile,
			int storyIndex,
			Vector3 worldPos,
			Rotation worldRotation,
			bool relaxed,
			out ThornsInteriorFurnitureScatterDebug.RejectReason reject )
		{
			reject = ThornsInteriorFurnitureScatterDebug.RejectReason.None;
			if ( buildingRoot is null || !buildingRoot.IsValid() )
			{
				reject = ThornsInteriorFurnitureScatterDebug.RejectReason.InvalidArgs;
				return false;
			}

			if ( widthCells < 1 || depthCells < 1 || storyIndex < 0 || storyIndex >= stories )
			{
				reject = ThornsInteriorFurnitureScatterDebug.RejectReason.InvalidArgs;
				return false;
			}

			var clearance = relaxed ? RelaxedFootprintClearanceInches : FootprintClearanceInches;

			if ( !TryComputeFootprintCorners(
				     buildingRoot,
				     worldPos,
				     worldRotation,
				     in profile,
				     clearance,
				     out var c0,
				     out var c1,
				     out var c2,
				     out var c3 ) )
			{
				reject = ThornsInteriorFurnitureScatterDebug.RejectReason.FootprintOutOfBounds;
				return false;
			}

			Vector2[] corners = [c0, c1, c2, c3];
			var cell = ThornsBuildingModule.Cell;
			var wallClear = ThornsBuildingModule.WallThickness + ThornsProcBuildingInteriorSample.InteriorWallComfortMargin;
			var minX = ThornsProcBuildingInteriorSample.GridAxisLocalPublic( 0, widthCells, cell ) - cell * 0.5f + wallClear;
			var maxX = ThornsProcBuildingInteriorSample.GridAxisLocalPublic( widthCells - 1, widthCells, cell ) + cell * 0.5f - wallClear;
			var minY = ThornsProcBuildingInteriorSample.GridAxisLocalPublic( 0, depthCells, cell ) - cell * 0.5f + wallClear;
			var maxY = ThornsProcBuildingInteriorSample.GridAxisLocalPublic( depthCells - 1, depthCells, cell ) + cell * 0.5f - wallClear;

			var localCenter = buildingRoot.WorldRotation.Inverse * (worldPos - buildingRoot.WorldPosition);
			if ( ThornsProcBuildingInteriorSample.IsInteriorFurnitureExcludedPlanarLocal(
				     localCenter.x,
				     localCenter.y,
				     widthCells,
				     depthCells,
				     stories,
				     in hints,
				     buildingRoot,
				     relaxed ) )
			{
				reject = ThornsInteriorFurnitureScatterDebug.RejectReason.FootprintDoorOrRamp;
				return false;
			}

			for ( var c = 0; c < 4; c++ )
			{
				var p = corners[c];
				if ( p.x < minX || p.x > maxX || p.y < minY || p.y > maxY )
				{
					reject = ThornsInteriorFurnitureScatterDebug.RejectReason.FootprintOutOfBounds;
					return false;
				}

				if ( !ThornsProcBuildingInteriorSample.TryLocalPlanarToGridCellPublic(
					     p.x,
					     p.y,
					     widthCells,
					     depthCells,
					     cell,
					     out var gx,
					     out var gy ) )
				{
					reject = ThornsInteriorFurnitureScatterDebug.RejectReason.FootprintOutOfBounds;
					return false;
				}

				if ( !ThornsProcBuildingInteriorSample.CellHasFloorSlab( storyIndex, gx, gy, widthCells, depthCells, stories, buildingRoot ) )
				{
					reject = ThornsInteriorFurnitureScatterDebug.RejectReason.FootprintNoFloor;
					return false;
				}

				if ( ThornsProcBuildingInteriorSample.IsInteriorFurnitureExcludedPlanarLocal(
					     p.x,
					     p.y,
					     widthCells,
					     depthCells,
					     stories,
					     in hints,
					     buildingRoot,
					     relaxed ) )
				{
					reject = ThornsInteriorFurnitureScatterDebug.RejectReason.FootprintDoorOrRamp;
					return false;
				}
			}

			if ( FootprintCrossesInteriorWall( buildingRoot, widthCells, depthCells, storyIndex, corners ) )
			{
				reject = ThornsInteriorFurnitureScatterDebug.RejectReason.FootprintInteriorWall;
				return false;
			}

			return true;
		}

		static bool FootprintCrossesInteriorWall(
			GameObject buildingRoot,
			int widthCells,
			int depthCells,
			int storyIndex,
			Vector2[] corners )
		{
			var layout = ThornsProcBuildingLayoutHost.TryGet( buildingRoot );
			var walls = layout?.InteriorWalls;
			if ( walls is null )
				return false;

			var cell = ThornsBuildingModule.Cell;
			for ( var i = 0; i < 4; i++ )
			{
				var a = corners[i];
				var b = corners[(i + 1) % 4];
				var len = (b - a).Length;
				var steps = Math.Max( 2, (int)MathF.Ceiling( len / 6f ) );

				if ( !ThornsProcBuildingInteriorSample.TryLocalPlanarToGridCellPublic(
					    a.x,
					    a.y,
					    widthCells,
					    depthCells,
					    cell,
					    out var lastX,
					    out var lastY ) )
					return true;

				for ( var s = 1; s <= steps; s++ )
				{
					var t = s / (float)steps;
					var p = a + (b - a) * t;
					if ( !ThornsProcBuildingInteriorSample.TryLocalPlanarToGridCellPublic(
						    p.x,
						    p.y,
						    widthCells,
						    depthCells,
						    cell,
						    out var gx,
						    out var gy ) )
						return true;

					if ( gx == lastX && gy == lastY )
						continue;

					if ( CrossesBlockedCellEdge( walls, storyIndex, lastX, lastY, gx, gy ) )
						return true;

					lastX = gx;
					lastY = gy;
				}
			}

			return false;
		}

		static bool CrossesBlockedCellEdge(
			ThornsProcBuildingWallPlan walls,
			int storyIndex,
			int fromX,
			int fromY,
			int toX,
			int toY )
		{
			var dx = Math.Sign( toX - fromX );
			var dy = Math.Sign( toY - fromY );
			var x = fromX;
			var y = fromY;

			while ( x != toX || y != toY )
			{
				if ( x != toX )
				{
					var nextX = x + dx;
					var wallX = Math.Min( x, nextX );
					if ( walls.HasInteriorWallEast( storyIndex, wallX, y ) )
						return true;
					x = nextX;
				}

				if ( y != toY )
				{
					var nextY = y + dy;
					var wallY = Math.Min( y, nextY );
					if ( walls.HasInteriorWallNorth( storyIndex, x, wallY ) )
						return true;
					y = nextY;
				}
			}

			return false;
		}
	}

	/// <summary>Rotated planar half-extents from catalog world size (building-relative yaw, degrees).</summary>
	public static void ComputePlanarFootprintHalves(
		in ThornsPlaceableFurnitureCatalog.Entry profile,
		float yawDegreesBuildingRelative,
		out float halfAlong,
		out float halfFrom,
		float clearanceInches = FootprintClearanceInches )
	{
		var yaw = yawDegreesBuildingRelative * ( MathF.PI / 180f );
		var worldSize = ThornsPlaceableFurnitureScale.EffectiveWorldSizeInches( profile.WorldSizeInches, profile.StructureDefId );
		var hx = worldSize.x * 0.5f + clearanceInches;
		var hy = worldSize.y * 0.5f + clearanceInches;
		halfAlong = MathF.Abs( MathF.Cos( yaw ) ) * hx + MathF.Abs( MathF.Sin( yaw ) ) * hy;
		halfFrom = MathF.Abs( MathF.Sin( yaw ) ) * hx + MathF.Abs( MathF.Cos( yaw ) ) * hy;
	}

	static bool TryComputeFootprintCorners(
		GameObject buildingRoot,
		Vector3 worldPos,
		Rotation worldRotation,
		in ThornsPlaceableFurnitureCatalog.Entry profile,
		float clearanceInches,
		out Vector2 c0,
		out Vector2 c1,
		out Vector2 c2,
		out Vector2 c3 )
	{
		c0 = default;
		c1 = default;
		c2 = default;
		c3 = default;
		if ( buildingRoot is null || !buildingRoot.IsValid() )
			return false;

		var yaw = ( worldRotation.Angles().yaw - buildingRoot.WorldRotation.Angles().yaw ) * ( MathF.PI / 180f );
		ComputePlanarFootprintHalves( in profile, yaw * ( 180f / MathF.PI ), out var halfAlong, out var halfFrom, clearanceInches );

		var localCenter = buildingRoot.WorldRotation.Inverse * (worldPos - buildingRoot.WorldPosition);
		var alongAxis = new Vector2( MathF.Cos( yaw ), MathF.Sin( yaw ) );
		var fromAxis = new Vector2( -MathF.Sin( yaw ), MathF.Cos( yaw ) );
		var center = new Vector2( localCenter.x, localCenter.y );

		c0 = center - alongAxis * halfAlong - fromAxis * halfFrom;
		c1 = center + alongAxis * halfAlong - fromAxis * halfFrom;
		c2 = center + alongAxis * halfAlong + fromAxis * halfFrom;
		c3 = center - alongAxis * halfAlong + fromAxis * halfFrom;
		return true;
	}
}
