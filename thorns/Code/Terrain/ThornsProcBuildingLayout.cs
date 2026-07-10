using System;

using System.Collections.Generic;



namespace Sandbox;



/// <summary>

/// Per-storey cell occupancy for world-generated buildings — setbacks, vertical support, alternating ramp corners, reachability validation.

/// </summary>

public sealed class ThornsProcBuildingLayout

{

	readonly bool[] _occupied;

	readonly bool[] _opening;

	readonly List<ThornsProcRampSpec>[] _rampsByStory;

	List<ThornsProcRampSpec> _ramps;



	public int WidthCells { get; }

	public int DepthCells { get; }

	public int Stories { get; }



	/// <summary>Ground-floor primary ramp anchor (first ramp on story 0).</summary>
	public int RampGridX => GetRampGridX( 0 );

	/// <summary>Ground-floor primary ramp anchor (first ramp on story 0).</summary>
	public int RampGridY => GetRampGridY( 0 );



	public int DoorSide { get; private set; } = -1;

	public int DoorIndex { get; private set; }

	/// <summary>Dev / ASCII floorplans — set primary ground door after construction.</summary>
	public void SetGroundDoor( int side, int index )
	{
		DoorSide = side;
		DoorIndex = index;
	}

	/// <summary>Deterministic interior dividers — validated with <see cref="ThornsProcBuildingValidation"/>.</summary>
	public ThornsProcBuildingWallPlan InteriorWalls { get; set; }

	/// <summary>Building identity (type, district, facade) when generated from <see cref="ThornsProcBuildingIdentityGenerator"/>.</summary>
	public ThornsProcBuildingIdentityMeta Identity { get; set; }



	public static ThornsProcBuildingLayout CreateDraft( int widthCells, int depthCells, int stories, bool[] occupied )
	{
		var byStory = new List<ThornsProcRampSpec>[stories];
		for ( var s = 0; s < stories; s++ )
			byStory[s] = new List<ThornsProcRampSpec>();

		return new ThornsProcBuildingLayout( widthCells, depthCells, stories, occupied, byStory, null, null );
	}

	/// <summary>Layout produced by <see cref="ThornsProcTileBlueprintCompiler"/>.</summary>
	public static ThornsProcBuildingLayout CreateFromBlueprint(
		int widthCells,
		int depthCells,
		int stories,
		bool[] occupied,
		bool[] opening,
		List<ThornsProcRampSpec> ramps )
	{
		if ( !TryNormalizeBlueprintRamps( widthCells, depthCells, stories, occupied, ramps, out var byStory, out var flat ) )
			return null;

		return new( widthCells, depthCells, stories, occupied, byStory, opening, flat );
	}

	public static bool TryNormalizeBlueprintRamps(
		int widthCells,
		int depthCells,
		int stories,
		bool[] occupied,
		List<ThornsProcRampSpec> ramps,
		out List<ThornsProcRampSpec>[] rampsByStory,
		out List<ThornsProcRampSpec> flatRamps )
	{
		rampsByStory = new List<ThornsProcRampSpec>[stories];
		flatRamps = new List<ThornsProcRampSpec>( ramps?.Count ?? 0 );
		for ( var s = 0; s < stories; s++ )
			rampsByStory[s] = new List<ThornsProcRampSpec>();

		if ( ramps is null )
			return true;

		var seenPerStory = new HashSet<(int x, int y)>[stories];
		for ( var s = 0; s < stories; s++ )
			seenPerStory[s] = new HashSet<(int x, int y)>();

		foreach ( var r in ramps )
		{
			if ( r.Story < 0 || r.Story >= stories )
				return false;

			if ( r.X < 0 || r.X >= widthCells || r.Y < 0 || r.Y >= depthCells )
				return false;

			if ( r.Direction == ThornsProcRampDirection.None )
				return false;

			if ( !seenPerStory[r.Story].Add( (r.X, r.Y) ) )
				return false;

			var i = r.Story * widthCells * depthCells + r.Y * widthCells + r.X;
			if ( occupied is null || i < 0 || i >= occupied.Length || !occupied[i] )
				return false;

			rampsByStory[r.Story].Add( r );
			flatRamps.Add( r );
		}

		return true;
	}

	public IReadOnlyList<ThornsProcRampSpec> Ramps => _ramps;

	public int GetRampCountOnStory( int story ) =>
		story >= 0 && story < Stories ? _rampsByStory[story].Count : 0;

	public IReadOnlyList<ThornsProcRampSpec> GetRampsOnStory( int story ) =>
		story >= 0 && story < Stories ? _rampsByStory[story] : Array.Empty<ThornsProcRampSpec>();

	public bool TryGetPrimaryRamp( int story, out ThornsProcRampSpec ramp )
	{
		ramp = default;
		if ( story < 0 || story >= Stories )
			return false;

		var list = _rampsByStory[story];
		if ( list.Count == 0 )
			return false;

		ramp = list[0];
		return true;
	}

	public ThornsProcRampSpec GetPrimaryRampOrDefault( int story ) =>
		TryGetPrimaryRamp( story, out var ramp ) ? ramp : default;

	public bool TryGetRampAt( int story, int x, int y, out ThornsProcRampSpec ramp )
	{
		ramp = default;
		if ( story < 0 || story >= Stories )
			return false;

		foreach ( var r in _rampsByStory[story] )
		{
			if ( r.X == x && r.Y == y )
			{
				ramp = r;
				return true;
			}
		}

		return false;
	}



	ThornsProcBuildingLayout(
		int widthCells,
		int depthCells,
		int stories,
		bool[] occupied,
		List<ThornsProcRampSpec>[] rampsByStory,
		bool[] opening = null,
		List<ThornsProcRampSpec> flatRamps = null,
		ThornsProcBuildingWallPlan interiorWalls = null )
	{
		WidthCells = widthCells;
		DepthCells = depthCells;
		Stories = stories;
		_occupied = occupied;
		_rampsByStory = rampsByStory ?? new List<ThornsProcRampSpec>[stories];
		_opening = opening;
		_ramps = flatRamps ?? BuildFlatRampList( _rampsByStory );
		InteriorWalls = interiorWalls ?? ThornsProcBuildingWallPlan.Empty( stories, widthCells, depthCells );
	}

	static List<ThornsProcRampSpec> BuildFlatRampList( List<ThornsProcRampSpec>[] byStory )
	{
		var flat = new List<ThornsProcRampSpec>();
		if ( byStory is null )
			return flat;

		for ( var s = 0; s < byStory.Length; s++ )
		{
			if ( byStory[s] is null )
				continue;

			flat.AddRange( byStory[s] );
		}

		return flat;
	}

	static List<ThornsProcRampSpec>[] RampsByStoryFromLegacyArrays(
		int stories,
		int[] rampGridX,
		int[] rampGridY,
		ThornsProcRampDirection[] rampDirections = null )
	{
		var byStory = new List<ThornsProcRampSpec>[stories];
		for ( var s = 0; s < stories; s++ )
		{
			byStory[s] = new List<ThornsProcRampSpec>( 1 );
			if ( rampGridX is null || rampGridY is null )
				continue;

			var dir = rampDirections is not null && s < rampDirections.Length
				? rampDirections[s]
				: ThornsProcRampDirection.None;
			byStory[s].Add( new ThornsProcRampSpec
			{
				Story = s,
				X = rampGridX[s],
				Y = rampGridY[s],
				Direction = dir
			} );
		}

		return byStory;
	}

	public ThornsProcRampDirection GetRampDirection( int story ) =>
		TryGetPrimaryRamp( story, out var ramp ) ? ramp.Direction : ThornsProcRampDirection.None;

	public bool IsOpeningCell( int story, int x, int y )
	{
		if ( _opening is null || story < 0 || story >= Stories || x < 0 || x >= WidthCells || y < 0 || y >= DepthCells )
			return false;

		return _opening[Index( story, x, y )];
	}

	public void SetDoor( int side, int index )
	{
		DoorSide = side;
		DoorIndex = index;
	}



	int Index( int story, int x, int y ) => story * WidthCells * DepthCells + y * WidthCells + x;



	public int GetRampGridX( int story ) => TryGetPrimaryRamp( story, out var ramp ) ? ramp.X : 0;

	public int GetRampGridY( int story ) => TryGetPrimaryRamp( story, out var ramp ) ? ramp.Y : 0;



	public bool IsCellOccupied( int story, int x, int y )

	{

		if ( story < 0 || story >= Stories || x < 0 || x >= WidthCells || y < 0 || y >= DepthCells )

			return false;



		return _occupied[Index( story, x, y )];

	}



	/// <summary>Cells cleared on the floor above ramp <paramref name="rampStory"/> (vertical shaft).</summary>

	public bool IsShaftCellForRampAtStory( int rampStory, int x, int y ) =>
		ThornsProcBuildingRampGeometry.IsShaftCellForAnyRampOnStory( this, rampStory, x, y );



	public bool IsRampShaftCell( int x, int y ) => IsShaftCellForRampAtStory( 0, x, y );



	/// <summary>No walkable floor slab — opening above a ramp landing.</summary>

	public bool CellNeedsRampShaftUpperCutAt( int story, int x, int y )

	{

		if ( Stories <= 1 || story < 1 )

			return false;

		if ( IsOpeningCell( story, x, y ) )
			return true;

		if ( _opening is null )
			return IsShaftCellForRampAtStory( story - 1, x, y );

		foreach ( var ramp in GetRampsOnStory( story - 1 ) )
		{
			ThornsProcTileRampHeadroom.GetRiseDelta( ramp.Direction, out var riseDx, out var riseDy );
			if ( x == ramp.X + riseDx && y == ramp.Y + riseDy )
				return true;
		}

		return false;

	}



	public bool HasWalkableFloorAt( int story, int x, int y ) =>

		IsCellOccupied( story, x, y ) && !CellNeedsRampShaftUpperCutAt( story, x, y );

	/// <summary>Walkable floor or ramp shaft opening — still gets exterior walls and roof.</summary>
	public bool HasPerimeterShellCell( int story, int x, int y ) =>
		IsCellOccupied( story, x, y ) || IsOpeningCell( story, x, y );



	public void GetOccupiedBounds( int story, out int minX, out int minY, out int maxX, out int maxY )

	{

		minX = WidthCells;

		minY = DepthCells;

		maxX = -1;

		maxY = -1;

		for ( var x = 0; x < WidthCells; x++ )

		for ( var y = 0; y < DepthCells; y++ )

		{

			if ( !IsCellOccupied( story, x, y ) )

				continue;



			if ( x < minX ) minX = x;

			if ( y < minY ) minY = y;

			if ( x > maxX ) maxX = x;

			if ( y > maxY ) maxY = y;

		}



		if ( maxX < 0 )

		{

			minX = minY = 0;

			maxX = maxY = 0;

		}

	}



	public void GetFootprintHalfExtents( out float halfW, out float halfD )

	{

		GetOccupiedBounds( 0, out var minX, out var minY, out var maxX, out var maxY );

		var cell = ThornsBuildingModule.Cell;

		halfW = ( maxX - minX + 1 ) * cell * 0.5f;

		halfD = ( maxY - minY + 1 ) * cell * 0.5f;

	}



	public float GridAxisLocalX( int x ) => ( x - ( WidthCells - 1 ) * 0.5f ) * ThornsBuildingModule.Cell;



	public float GridAxisLocalY( int y ) => ( y - ( DepthCells - 1 ) * 0.5f ) * ThornsBuildingModule.Cell;



	/// <summary>Yaw for <c>wood_ramp</c> seated at the given storey ramp corner.</summary>

	public float GetRampYawDegrees( int rampStory )
	{
		if ( TryGetPrimaryRamp( rampStory, out var ramp ) )
			return ThornsProcBuildingRampGeometry.GetRampYawDegrees( ramp );

		return ThornsProcBuildingRampGeometry.GetRampYawDegrees( this, rampStory );
	}



	/// <summary>Roll storey count (1–3) with taller bias in clusters.</summary>

	public static int RollStoryCount( Random rnd, bool townCluster )

	{

		var t = rnd.NextDouble();

		if ( townCluster )

		{

			if ( t < 0.2 ) return 1;

			if ( t < 0.5 ) return 2;

			return 3;

		}



		if ( t < 0.34 ) return 1;

		if ( t < 0.67 ) return 2;

		return 3;

	}



	public static ThornsProcBuildingLayout Generate( Random rnd, int stories, bool preferLargeFootprint )

	{

		stories = ThornsProcBuildingPoc.EffectiveStories( Math.Clamp( stories, 1, 5 ) );

		ThornsProcBuildingLayout best = null;

		var bestScore = -1;

		for ( var attempt = 0; attempt < ThornsProcBuildingRules.MaxGenerationAttempts; attempt++ )

		{

			if ( !TryGenerateOnce( rnd, stories, preferLargeFootprint, out var layout ) )

				continue;



			var report = ThornsProcBuildingValidation.Validate( layout, layout.InteriorWalls );

			if ( !report.Passed )

				continue;



			if ( report.QualityScore <= bestScore )

				continue;



			bestScore = report.QualityScore;

			best = layout;

			if ( bestScore >= ThornsProcBuildingQuality.ExcellentThreshold )

				break;

		}



		if ( best is not null && bestScore >= ThornsProcBuildingRules.MinQualityScoreToAccept )

			return best;



		Log.Warning( $"[Thorns ProcBuilding] No valid layout after {ThornsProcBuildingRules.MaxGenerationAttempts} attempts (stories={stories}); using fallback." );

		return BuildFallbackRect( stories );

	}

	/// <summary>Small validated 3×3 footprint — no procedural search.</summary>
	public static ThornsProcBuildingLayout CreateGuaranteedFallback( int stories ) =>
		BuildFallbackRectSized( stories, 3, 3, relaxValidation: false );

	/// <summary>Rectangular switchback fallback for dev scenes and sized archetype spawns.</summary>
	public static ThornsProcBuildingLayout CreateFallbackRectSized(
		int stories,
		int widthCells,
		int depthCells,
		bool relaxValidation = false ) =>
		BuildFallbackRectSized( stories, widthCells, depthCells, relaxValidation );

	/// <summary>Settlement spawn: sized to archetype with full story count (skyscrapers stay tall).</summary>
	public static ThornsProcBuildingLayout CreateSettlementArchetypeFallback( ThornsProcBuildingType type )
	{
		var def = ThornsProcBuildingIdentityRegistry.Get( type );
		var blueprint = ThornsProcTileBlueprintLibrary.Get( type );
		var stories = ThornsProcBuildingPoc.RollStoriesForBlueprint( blueprint, Random.Shared );
		var w = Math.Clamp( ( def.WidthMin + def.WidthMax + 1 ) / 2, 4, def.PreferLargeFootprint ? 7 : 6 );
		var d = Math.Clamp( ( def.DepthMin + def.DepthMax + 1 ) / 2, 4, def.PreferLargeFootprint ? 7 : 6 );
		return stories > 1
			? BuildSettlementStairwellFallback( stories, w, d )
			: BuildFallbackRectSized( stories, w, d, relaxValidation: false );
	}

	/// <summary>Switchback stairwell — each storey ramp on its own anchor with headroom above.</summary>
	public static ThornsProcBuildingLayout BuildSettlementStairwellFallback( int stories, int widthCells, int depthCells )
	{
		stories = Math.Clamp( stories, 2, 8 );
		var w = Math.Clamp( widthCells, 4, 9 );
		var d = Math.Clamp( depthCells, 4, 9 );
		return BuildSwitchbackFallbackLayout( stories, w, d, doorSide: 2 );
	}

	static ThornsProcBuildingLayout BuildSwitchbackFallbackLayout( int stories, int w, int d, int doorSide )
	{
		var occupied = new bool[stories * w * d];
		for ( var s = 0; s < stories; s++ )
		for ( var x = 0; x < w; x++ )
		for ( var y = 0; y < d; y++ )
			occupied[s * w * d + y * w + x] = true;

		var byStory = ThornsProcBuildingRampPlanner.BuildSwitchbackRampsByStory( stories, w, d );
		var flatRamps = ThornsProcBuildingRampPlanner.BuildFlatRampList( byStory );
		var opening = new bool[stories * w * d];
		ThornsProcTileRampHeadroom.ApplyRequiredOpenings( opening, w, d, flatRamps );

		var layout = new ThornsProcBuildingLayout( w, d, stories, occupied, byStory, opening, flatRamps );
		layout.DoorSide = doorSide;
		layout.DoorIndex = Math.Clamp( w / 2, 1, w - 2 );
		layout.InteriorWalls = ThornsProcBuildingWallPlan.Empty( stories, w, d );
		return layout;
	}

	static ThornsProcBuildingLayout BuildFallbackRect( int stories ) =>
		BuildFallbackRectSized( stories, 3, 3, relaxValidation: false );

	static ThornsProcBuildingLayout BuildFallbackRectSized( int stories, int widthCells, int depthCells, bool relaxValidation )
	{
		stories = Math.Clamp( stories, 1, 8 );
		var w = Math.Clamp( widthCells, 2, 9 );
		var d = Math.Clamp( depthCells, 2, 9 );

		var occupied = new bool[stories * w * d];

		for ( var s = 0; s < stories; s++ )

		for ( var x = 0; x < w; x++ )

		for ( var y = 0; y < d; y++ )

			occupied[s * w * d + y * w + x] = true;



		var byStory = stories > 1
			? ThornsProcBuildingRampPlanner.BuildSwitchbackRampsByStory( stories, w, d )
			: new List<ThornsProcRampSpec>[stories];
		if ( stories == 1 )
			byStory[0] = new List<ThornsProcRampSpec>( 0 );

		var flatRamps = ThornsProcBuildingRampPlanner.BuildFlatRampList( byStory );
		var opening = new bool[stories * w * d];
		ThornsProcTileRampHeadroom.ApplyRequiredOpenings( opening, w, d, flatRamps );

		var layout = new ThornsProcBuildingLayout( w, d, stories, occupied, byStory, opening, flatRamps );
		layout.DoorSide = 0;
		layout.DoorIndex = 1;
		layout.InteriorWalls = ThornsProcBuildingWallPlan.Empty( stories, w, d );

		if ( !relaxValidation
		     && stories > 1
		     && !ThornsProcBuildingRampValidation.PassesStrictRules( layout ) )
			return BuildFallbackRectSized( 1, w, d, relaxValidation: false );

		if ( !relaxValidation
		     && !ThornsProcBuildingValidation.Validate( layout, layout.InteriorWalls ).Passed
		     && stories > 1 )
			return BuildFallbackRectSized( 1, w, d, relaxValidation: false );

		return layout;

	}



	static bool TryGenerateOnce( Random rnd, int stories, bool preferLargeFootprint, out ThornsProcBuildingLayout layout )

	{

		layout = null;

		var minDim = stories >= 4 ? 4 : stories >= 3 ? 3 : 2;

		var maxDim = preferLargeFootprint

			? Math.Min( 9, 5 + stories )

			: Math.Min( 8, 4 + stories );



		var w = rnd.Next( minDim, maxDim + 1 );

		var d = rnd.Next( minDim, maxDim + 1 );

		var occupied = new bool[stories * w * d];



		void SetOcc( int s, int x, int y, bool v )

		{

			if ( x < 0 || x >= w || y < 0 || y >= d )

				return;



			occupied[s * w * d + y * w + x] = v;

		}



		bool GetOcc( int s, int x, int y ) =>

			x >= 0 && x < w && y >= 0 && y < d && occupied[s * w * d + y * w + x];



		// Ground floor silhouette.

		var shape = rnd.Next( 0, 6 );

		for ( var x = 0; x < w; x++ )

		for ( var y = 0; y < d; y++ )

			SetOcc( 0, x, y, true );



		switch ( shape )

		{

			case 1 when w >= 3 && d >= 3:

			{

				var cutX = rnd.Next( 1, w );

				var cutY = rnd.Next( 1, d );

				var corner = rnd.Next( 0, 4 );

				for ( var x = 0; x < w; x++ )

				for ( var y = 0; y < d; y++ )

				{

					var inCorner = corner switch

					{

						0 => x < cutX && y < cutY,

						1 => x >= w - cutX && y < cutY,

						2 => x < cutX && y >= d - cutY,

						_ => x >= w - cutX && y >= d - cutY

					};

					if ( inCorner )

						SetOcc( 0, x, y, false );

				}



				break;

			}

			case 2 when w >= 4 && d >= 4:

			{

				for ( var x = 1; x < w - 1; x++ )

				for ( var y = 1; y < d - 1; y++ )

					SetOcc( 0, x, y, false );



				break;

			}

			case 3 when w >= 4:

			{

				var keepRows = rnd.Next( 2, Math.Min( d, 4 ) );

				for ( var y = keepRows; y < d; y++ )

				for ( var x = 0; x < w; x++ )

					SetOcc( 0, x, y, false );



				break;

			}

			case 4 when d >= 4:

			{

				var keepCols = rnd.Next( 2, Math.Min( w, 4 ) );

				for ( var x = keepCols; x < w; x++ )

				for ( var y = 0; y < d; y++ )

					SetOcc( 0, x, y, false );



				break;

			}

			case 5 when w >= 3 && d >= 3:

			{

				var spineX = rnd.Next( 0, w );

				for ( var x = 0; x < w; x++ )

				for ( var y = 0; y < d; y++ )

				{

					if ( x != spineX && y != 0 && y != d - 1 )

						SetOcc( 0, x, y, false );

				}



				break;

			}

		}



		EnsureMinOccupiedCount( 0, w, d, occupied, Math.Max( 4, w + d - 1 ) );



		// Upper floors: only stack on supported cells, then setback trims.

		for ( var s = 1; s < stories; s++ )

		{

			for ( var x = 0; x < w; x++ )

			for ( var y = 0; y < d; y++ )

				SetOcc( s, x, y, GetOcc( s - 1, x, y ) );



			var trims = rnd.Next( 1, stories >= 4 ? 3 : 2 );

			for ( var t = 0; t < trims; t++ )

			{

				var edge = rnd.Next( 0, 4 );

				TrimOneEdge( s, edge, w, d, occupied );

				PruneUnsupportedCells( s, w, d, occupied );

				var minCells = Math.Max( 3, 6 - ( stories - s ) );

				if ( CountOccupied( s, w, d, occupied ) < minCells )

					break;

			}



			PruneUnsupportedCells( s, w, d, occupied );

			EnsureMinOccupiedCountSupported( s, w, d, occupied, Math.Max( 3, 5 - ( stories - s ) ) );

		}



		var byStory = new List<ThornsProcRampSpec>[stories];
		for ( var s = 0; s < stories; s++ )
			byStory[s] = new List<ThornsProcRampSpec>();

		layout = new ThornsProcBuildingLayout( w, d, stories, occupied, byStory );



		if ( !layout.TryAssignRampCorners( rnd ) )

			return false;



		layout.EnsureRampLandingCellsOccupied();

		if ( !layout.TryPickDoor( rnd ) )

			return false;



		layout.InteriorWalls = ThornsProcBuildingWallPlan.Generate(

			layout,

			rnd,

			layout.DoorSide,

			layout.DoorIndex );



		var report = ThornsProcBuildingValidation.Validate( layout, layout.InteriorWalls );

		return report.Passed;

	}



	public bool TryAssignRampCorners( Random rnd )
	{
		for ( var s = 0; s < Stories; s++ )
			_rampsByStory[s].Clear();

		if ( Stories <= 1 )
		{
			_rampsByStory[0].Add( new ThornsProcRampSpec { Story = 0, X = 0, Y = 0, Direction = ThornsProcRampDirection.None } );
			RebuildFlatRampCache();
			return true;
		}

		if ( !TryPickOccupiedCorner( 0, rnd, out var x0, out var y0 ) )
			return false;

		_rampsByStory[0].Add( new ThornsProcRampSpec { Story = 0, X = x0, Y = y0, Direction = ThornsProcRampDirection.None } );

		for ( var s = 1; s < Stories; s++ )
		{
			var prev = _rampsByStory[s - 1][0];
			var wantX = WidthCells - 1 - prev.X;
			var wantY = DepthCells - 1 - prev.Y;
			if ( IsCellOccupied( s, wantX, wantY ) )
			{
				_rampsByStory[s].Add( new ThornsProcRampSpec { Story = s, X = wantX, Y = wantY, Direction = ThornsProcRampDirection.None } );
				continue;
			}

			if ( TryPickOccupiedCorner( s, rnd, out var cx, out var cy ) )
			{
				_rampsByStory[s].Add( new ThornsProcRampSpec { Story = s, X = cx, Y = cy, Direction = ThornsProcRampDirection.None } );
				continue;
			}

			return false;
		}

		InferMissingRampDirections();
		RebuildFlatRampCache();
		return true;
	}

	internal void InferMissingRampDirections()
	{
		for ( var s = 0; s < Stories; s++ )
		{
			for ( var i = 0; i < _rampsByStory[s].Count; i++ )
			{
				var ramp = _rampsByStory[s][i];
				if ( ramp.Direction != ThornsProcRampDirection.None )
					continue;

				ThornsProcBuildingRampGeometry.GetRiseDirection( this, ramp, out var riseDx, out var riseDy );
				var dir = RiseDeltaToDirection( riseDx, riseDy );
				if ( dir == ThornsProcRampDirection.None )
					continue;

				_rampsByStory[s][i] = new ThornsProcRampSpec
				{
					Story = ramp.Story,
					X = ramp.X,
					Y = ramp.Y,
					Direction = dir
				};
			}
		}
	}

	static ThornsProcRampDirection RiseDeltaToDirection( int riseDx, int riseDy ) =>
		riseDx switch
		{
			< 0 => ThornsProcRampDirection.West,
			> 0 => ThornsProcRampDirection.East,
			_ => riseDy switch
			{
				< 0 => ThornsProcRampDirection.South,
				> 0 => ThornsProcRampDirection.North,
				_ => ThornsProcRampDirection.None
			}
		};

	internal void RebuildFlatRampCache()
	{
		_ramps.Clear();
		_ramps.AddRange( BuildFlatRampList( _rampsByStory ) );
	}



	bool TryPickOccupiedCorner( int story, Random rnd, out int cx, out int cy )

	{

		var corners = new List<(int x, int y)>( 4 );

		if ( IsCellOccupied( story, 0, 0 ) ) corners.Add( (0, 0) );

		if ( IsCellOccupied( story, WidthCells - 1, 0 ) ) corners.Add( (WidthCells - 1, 0) );

		if ( IsCellOccupied( story, 0, DepthCells - 1 ) ) corners.Add( (0, DepthCells - 1) );

		if ( IsCellOccupied( story, WidthCells - 1, DepthCells - 1 ) ) corners.Add( (WidthCells - 1, DepthCells - 1) );



		if ( corners.Count == 0 )

		{

			for ( var x = 0; x < WidthCells; x++ )

			for ( var y = 0; y < DepthCells; y++ )

			{

				if ( !IsCellOccupied( story, x, y ) )

					continue;



				cx = x;

				cy = y;

				return true;

			}



			cx = 0;

			cy = 0;

			return false;

		}



		var c = corners[rnd.Next( corners.Count )];

		cx = c.x;

		cy = c.y;

		return true;

	}



	public void EnsureRampLandingCellsOccupied()
	{
		for ( var s = 0; s < Stories; s++ )
		foreach ( var ramp in _rampsByStory[s] )
		{
			ForceOcc( s, ramp.X, ramp.Y );
			if ( ramp.Y + 1 < DepthCells )
				ForceOcc( s, ramp.X, ramp.Y + 1 );
		}



		void ForceOcc( int story, int x, int y )

		{

			if ( story < 0 || story >= Stories || x < 0 || x >= WidthCells || y < 0 || y >= DepthCells )

				return;



			if ( story > 0 && !IsCellOccupied( story - 1, x, y ) )

				return;



			_occupied[Index( story, x, y )] = true;

		}

	}



	public bool TryPickDoor( Random rnd )

	{

		var candidates = new List<(int side, int index)>( 32 );

		for ( var x = 0; x < WidthCells; x++ )

		for ( var y = 0; y < DepthCells; y++ )

		{

			if ( !IsCellOccupied( 0, x, y ) )

				continue;



			if ( !IsCellOccupied( 0, x, y - 1 ) )

				TryAddDoorCandidate( candidates, 0, x );

			if ( !IsCellOccupied( 0, x, y + 1 ) )

				TryAddDoorCandidate( candidates, 2, x );

			if ( !IsCellOccupied( 0, x - 1, y ) )

				TryAddDoorCandidate( candidates, 3, y );

			if ( !IsCellOccupied( 0, x + 1, y ) )

				TryAddDoorCandidate( candidates, 1, y );

		}



		if ( candidates.Count == 0 )

			return false;



		ShuffleCandidates( candidates, rnd );



		foreach ( var pick in candidates )

		{

			if ( !ThornsProcBuildingInteriorSample.IsEnterableDoorPlacement(

				     pick.side,

				     pick.index,

				     this ) )

				continue;



			DoorSide = pick.side;

			DoorIndex = pick.index;

			if ( ThornsProcBuildingConnectivity.IsFullyReachableFromDoor( this, null, DoorSide, DoorIndex ) )

				return true;

		}



		return false;

	}



	static void ShuffleCandidates( List<(int side, int index)> list, Random rnd )

	{

		for ( var i = list.Count - 1; i > 0; i-- )

		{

			var j = rnd.Next( i + 1 );

			(list[i], list[j]) = (list[j], list[i]);

		}

	}



	/// <summary>True when <paramref name="x"/>/<paramref name="y"/> can use the ramp to reach the storey above.</summary>
	public bool IsInRampLandingZone( int story, int x, int y )

	{

		foreach ( var (lx, ly) in EnumerateRampLandingCells( story ) )

		{

			if ( lx == x && ly == y )

				return true;

		}



		return false;

	}



	public IEnumerable<(int x, int y)> EnumerateRampLandingCells( int story ) =>
		ThornsProcBuildingRampTraversal.EnumerateRampLandingCells( this, story );

	/// <summary>
	/// Walkable cells on <c>rampStory + 1</c> you enter after climbing the ramp rooted on <paramref name="rampStory"/>.
	/// </summary>
	public IEnumerable<(int x, int y)> EnumerateUpperEntryCellsFromRamp( int rampStory ) =>
		ThornsProcBuildingRampTraversal.EnumerateUpperEntryCellsFromRamp( this, rampStory );



	static void TryAddDoorCandidate( List<(int side, int index)> list, int side, int index )

	{

		for ( var i = 0; i < list.Count; i++ )

		{

			if ( list[i].side == side && list[i].index == index )

				return;

		}



		list.Add( (side, index) );

	}



	static void TrimOneEdge( int story, int edge, int w, int d, bool[] occ )

	{

		var stride = w * d;

		var baseIdx = story * stride;



		switch ( edge )

		{

			case 0:

				for ( var x = 0; x < w; x++ )

					occ[baseIdx + 0 * w + x] = false;

				break;

			case 1:

				for ( var y = 0; y < d; y++ )

					occ[baseIdx + y * w + ( w - 1 )] = false;

				break;

			case 2:

				for ( var x = 0; x < w; x++ )

					occ[baseIdx + ( d - 1 ) * w + x] = false;

				break;

			default:

				for ( var y = 0; y < d; y++ )

					occ[baseIdx + y * w + 0] = false;

				break;

		}

	}



	static void PruneUnsupportedCells( int story, int w, int d, bool[] occ )

	{

		if ( story <= 0 )

			return;



		var stride = w * d;

		var baseIdx = story * stride;

		var belowIdx = ( story - 1 ) * stride;

		for ( var i = 0; i < stride; i++ )

		{

			if ( occ[baseIdx + i] && !occ[belowIdx + i] )

				occ[baseIdx + i] = false;

		}

	}



	static int CountOccupied( int story, int w, int d, bool[] occ )

	{

		var n = 0;

		var baseIdx = story * w * d;

		for ( var i = 0; i < w * d; i++ )

		{

			if ( occ[baseIdx + i] )

				n++;

		}



		return n;

	}



	static void EnsureMinOccupiedCount( int story, int w, int d, bool[] occ, int minCount )

	{

		if ( CountOccupied( story, w, d, occ ) >= minCount )

			return;



		var baseIdx = story * w * d;

		for ( var x = 0; x < w; x++ )

		for ( var y = 0; y < d; y++ )

		{

			occ[baseIdx + y * w + x] = true;

			if ( CountOccupied( story, w, d, occ ) >= minCount )

				return;

		}

	}



	static void EnsureMinOccupiedCountSupported( int story, int w, int d, bool[] occ, int minCount )

	{

		if ( story <= 0 )

		{

			EnsureMinOccupiedCount( story, w, d, occ, minCount );

			return;

		}



		if ( CountOccupied( story, w, d, occ ) >= minCount )

			return;



		var baseIdx = story * w * d;

		var belowIdx = ( story - 1 ) * w * d;

		for ( var x = 0; x < w; x++ )

		for ( var y = 0; y < d; y++ )

		{

			var i = y * w + x;

			if ( !occ[belowIdx + i] )

				continue;



			occ[baseIdx + i] = true;

			if ( CountOccupied( story, w, d, occ ) >= minCount )

				return;

		}

	}



	public static string DisplayNameFor( int stories, bool destroyed )

	{

		var prefix = destroyed ? "Ruined " : "Abandoned ";

		return stories switch

		{

			>= 5 => prefix + "High-Rise",

			4 => prefix + "Tower",

			3 => prefix + "Loft Block",

			2 => prefix + "Duplex",

			_ => prefix + "Shack"

		};

	}

}



/// <summary>Replicated layout metadata on proc-building roots for interior loot / NPC sampling.</summary>

[Title( "Thorns — Proc building layout" )]

[Category( "Thorns/Terrain" )]

[Icon( "domain" )]

public sealed class ThornsProcBuildingLayoutHost : Component

{

	public ThornsProcBuildingLayout Layout { get; set; }

	/// <summary>Facade vmat slug (e.g. <c>brick</c>, <c>glass_panes_light</c>).</summary>
	public string MaterialSlug { get; set; } = ThornsProcBuildingMaterialPalette.AllSlugs[0];

	public static ThornsProcBuildingLayout TryGet( GameObject buildingRoot ) =>

		buildingRoot is not null && buildingRoot.IsValid()

		&& buildingRoot.Components.Get<ThornsProcBuildingLayoutHost>( FindMode.EnabledInSelf ) is { IsValid: true } host

		&& host.Layout is not null

			? host.Layout

			: null;

	protected override void OnStart()
	{
		if ( !Game.IsPlaying || Layout is null || !GameObject.IsValid() )
			return;

		var trimCount = CountPerimeterTrimChildren();
		var isLocalGallery = GameObject.NetworkMode == NetworkMode.Never;
		if ( trimCount >= 4 && HasPerimeterBandTrimChildren( GameObject ) )
			return;

		var spawned = ThornsProcBuildingSceneSpawner.RefreshPerimeterTrims( GameObject );
		if ( isLocalGallery )
			ThornsNetworkReplication.SetSubtreeNetworkModeNever( GameObject );

		if ( spawned > 0 )
		{
			Log.Info(
				$"[Thorns ProcBuilding] {GameObject.Name}: {spawned} perimeter trim(s) "
				+ $"(layout host refresh, was {trimCount})" );
		}
	}

	static int CountPerimeterTrimChildren( GameObject buildingRoot )
	{
		var n = 0;
		foreach ( var ch in buildingRoot.Children )
		{
			if ( ch.IsValid() && ch.Tags.Has( "thorns_proc_building_trim" ) )
				n++;
		}

		return n;
	}

	static bool HasPerimeterBandTrimChildren( GameObject buildingRoot )
	{
		foreach ( var ch in buildingRoot.Children )
		{
			if ( ch.IsValid() && ch.Tags.Has( "thorns_proc_building_trim_band" ) )
				return true;
		}

		return false;
	}

	int CountPerimeterTrimChildren() => CountPerimeterTrimChildren( GameObject );

}


