namespace Sandbox;



/// <summary>

/// Compiles layered tile blueprints into <see cref="ThornsProcBuildingLayout"/>:

/// floors → auto interior walls → facade → ramps/openings → door → validation.

/// </summary>

public static class ThornsProcTileBlueprintCompiler

{

	public static bool TryCompile(

		ThornsProcTileBlueprint blueprint,

		ThornsProcBuildingDistrict district,

		Random rnd,

		bool ruinVariant,

		out ThornsProcBuildingLayout layout,

		ThornsProcBuildingCompilePolicy policy = ThornsProcBuildingCompilePolicy.Strict )

	{

		layout = null;

		if ( blueprint?.Layers is null || blueprint.Layers.Count == 0 )

			return false;



		var stories = ThornsProcBuildingPoc.RollStoriesForBlueprint( blueprint, rnd );

		var w = blueprint.Width;

		var d = blueprint.Depth;

		if ( w < 1 || d < 1 )

			return false;



		var occ = new bool[stories * w * d];

		var opening = new bool[stories * w * d];

		var ramps = new List<ThornsProcRampSpec>( 8 );



		for ( var s = 0; s < stories; s++ )

		{

			var layer = blueprint.Layers[s];

			if ( layer.Width != w || layer.Depth != d )

				return false;



			for ( var x = 0; x < w; x++ )

			for ( var y = 0; y < d; y++ )

			{

				ref var c = ref layer.Cell( x, y );

				var i = Idx( s, x, y, w, d );



				if ( c.Opening )

				{

					opening[i] = true;

					continue;

				}



				if ( c.HasFloorLike )

					occ[i] = true;



				if ( c.Ramp != ThornsProcRampDirection.None )

				{

					ramps.Add( new ThornsProcRampSpec

					{

						Story = s,

						X = x,

						Y = y,

						Direction = c.Ramp

					} );

				}

			}

		}



		ThornsProcTileRampHeadroom.ApplyRequiredOpenings( opening, w, d, ramps );



		if ( ruinVariant && blueprint.Type == ThornsProcBuildingType.Ruin )

			ThornsProcTileRuinMutator.Apply( occ, opening, w, d, stories, rnd );



		layout = ThornsProcBuildingLayout.CreateFromBlueprint( w, d, stories, occ, opening, ramps );

		if ( layout is null )

			return false;



		var interior = ThornsProcTileAutoWalls.Build( layout, blueprint );

		layout.InteriorWalls = interior;



		var facade = ThornsProcTileFacadeBuilder.Build( layout, blueprint, rnd );

		var meta = new ThornsProcBuildingIdentityMeta

		{

			Type = blueprint.Type,

			District = district,

			IsRuinVariant = ruinVariant,

			Facade = facade

		};

		layout.Identity = meta;



		var allowSettlementDoorFallback = policy is ThornsProcBuildingCompilePolicy.FallbackAllowed

		                                  or ThornsProcBuildingCompilePolicy.LenientDebug;



		if ( !TryAssignPrimaryDoor( layout, blueprint, out var doorSide, out var doorIndex ) )

		{

			if ( !allowSettlementDoorFallback

			     || !TryAssignSettlementFallbackDoor( layout, out doorSide, out doorIndex ) )

				return false;

		}



		layout.SetDoor( doorSide, doorIndex );



		return policy switch

		{

			ThornsProcBuildingCompilePolicy.LenientDebug => true,

			ThornsProcBuildingCompilePolicy.FallbackAllowed => true,

			_ => ThornsProcBuildingStrictValidation.TryValidate(

				layout,

				blueprint,

				out _,

				out _ )

		};

	}



	static bool TryAssignPrimaryDoor(

		ThornsProcBuildingLayout layout,

		ThornsProcTileBlueprint blueprint,

		out int doorSide,

		out int doorIndex )

	{

		doorSide = -1;

		doorIndex = -1;

		var facade = layout.Identity?.Facade;

		if ( facade is null || blueprint.Layers.Count == 0 )

			return false;



		var layer = blueprint.Layers[0];

		for ( var x = 0; x < layer.Width; x++ )

		for ( var y = 0; y < layer.Depth; y++ )

		{

			ref var c = ref layer.Cell( x, y );

			if ( c.DoorSouth && TryDoor( 0, x, layout, blueprint ) )

			{

				doorSide = 0;

				doorIndex = x;

				return true;

			}



			if ( c.DoorNorth && TryDoor( 2, x, layout, blueprint ) )

			{

				doorSide = 2;

				doorIndex = x;

				return true;

			}



			if ( c.DoorWest && TryDoor( 3, y, layout, blueprint ) )

			{

				doorSide = 3;

				doorIndex = y;

				return true;

			}



			if ( c.DoorEast && TryDoor( 1, y, layout, blueprint ) )

			{

				doorSide = 1;

				doorIndex = y;

				return true;

			}

		}



		var w = layout.WidthCells;

		var d = layout.DepthCells;

		for ( var side = 0; side < 4; side++ )

		{

			var count = side is 0 or 2 ? w : d;

			for ( var idx = 0; idx < count; idx++ )

			{

				if ( !TryDoor( side, idx, layout, blueprint ) )

					continue;



				doorSide = side;

				doorIndex = idx;

				return true;

			}

		}



		return false;

	}



	static bool TryDoor( int side, int index, ThornsProcBuildingLayout layout, ThornsProcTileBlueprint blueprint ) =>

		ThornsProcBuildingInteriorSample.IsEnterableDoorPlacement( side, index, layout );



	static bool TryAssignSettlementFallbackDoor(

		ThornsProcBuildingLayout layout,

		out int doorSide,

		out int doorIndex )

	{

		doorSide = -1;

		doorIndex = -1;

		var w = layout.WidthCells;

		var d = layout.DepthCells;

		for ( var side = 0; side < 4; side++ )

		{

			var count = side is 0 or 2 ? w : d;

			for ( var idx = 0; idx < count; idx++ )

			{

				if ( !ThornsProcBuildingInteriorSample.IsEnterableDoorPlacement( side, idx, layout ) )

					continue;



				doorSide = side;

				doorIndex = idx;

				return true;

			}

		}



		return false;

	}



	static int Idx( int s, int x, int y, int w, int d ) => s * w * d + y * w + x;

}


