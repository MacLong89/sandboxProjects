namespace UnderPressure;

/// <summary>
/// Builds the large themed world around each job: layered terrain, perimeter dressing,
/// and a distant horizon so levels read as real places instead of floating pads.
/// </summary>
public static class WorldMapBuilder
{
	public static void Build( GameObject root, JobDef job, int jobIndex )
	{
		var theme = MapThemes.Get( job.Theme );
		var center = job.WorkCenter;

		BuildTerrain( root, job, theme, center );
		BuildHorizon( root, job, theme, center );
		BuildPerimeter( root, job, theme, center, jobIndex );
	}

	private static void BuildTerrain( GameObject root, JobDef job, MapThemeInfo theme, Vector3 center )
	{
		var play = job.GroundSize;
		var map = job.MapSize;
		var transition = new Vector2(
			play.x + GameConstants.MapTransitionWidth * 2f,
			play.y + GameConstants.MapTransitionWidth * 2f );

		FlatQuad( root, "MapField", center, map, theme.FieldMaterial, theme.FieldColor, collider: true, z: DepthLayers.MapField );
		FlatQuad( root, "MapTransition", center, transition, theme.TransitionMaterial, theme.TransitionColor, z: DepthLayers.MapTransition );

		var playMat = job.Theme is MapTheme.Suburban or MapTheme.Backyard
			? GameMaterials.Grass
			: GameMaterials.Concrete;
		FlatQuad( root, "PlayPad", center, play, playMat, job.GroundColor, collider: true, z: DepthLayers.PlayPad );
	}

	private static void FlatQuad( GameObject parent, string name, Vector3 center, Vector2 size, Material mat, Color tint, bool collider = false, float z = 0f )
	{
		var go = new GameObject( parent, true, name );
		go.WorldPosition = center.WithZ( z );
		go.LocalScale = MeshPrimitives.QuadScale( size.x, size.y );
		var mr = go.Components.Create<ModelRenderer>();
		mr.Model = MeshPrimitives.Quad;
		mr.MaterialOverride = mat;
		mr.Tint = tint;

		if ( collider )
		{
			var col = go.Components.Create<BoxCollider>();
			col.Center = Vector3.Zero;
			col.Scale = new Vector3( size.x, size.y, 4f );
			col.Static = true;
		}
	}

	private static void BuildHorizon( GameObject root, JobDef job, MapThemeInfo theme, Vector3 center )
	{
		var horizonRoot = new GameObject( root, true, "Horizon" );
		var radius = MathF.Min( job.MapSize.x, job.MapSize.y ) * GameConstants.HorizonDistanceFactor;
		var segments = 28;

		// Distant silhouettes only — the scene skybox + fog handle the actual sky.
		switch ( job.Theme )
		{
			case MapTheme.UrbanPlaza:
			case MapTheme.Storefront:
			case MapTheme.Alley:
			case MapTheme.ParkingGarage:
				BuildCitySilhouette( horizonRoot, center, radius * 0.92f, segments, job.Theme );
				break;
			case MapTheme.Industrial:
			case MapTheme.GasStation:
			case MapTheme.Waterfront:
				BuildIndustrialSilhouette( horizonRoot, center, radius * 0.90f, segments, job.Theme );
				break;
			case MapTheme.Underground:
			case MapTheme.Interior:
				// Fully enclosed — the perimeter shell is the horizon.
				break;
			case MapTheme.Rooftop:
				BuildRooftopSkyline( horizonRoot, center, radius * 0.80f, segments );
				break;
			default:
				BuildHillSilhouette( horizonRoot, center, radius * 0.88f, segments, theme.HorizonGround );
				break;
		}
	}

	/// <summary>Neighboring towers seen from a high roof — tops end below the deck so the site reads as the tallest point.</summary>
	private static void BuildRooftopSkyline( GameObject parent, Vector3 center, float radius, int segments )
	{
		var wall = new Color( 0.16f, 0.17f, 0.22f );
		var lit = new Color( 0.92f, 0.78f, 0.42f );

		for ( var i = 0; i < segments; i++ )
		{
			var yaw = i * 360f / segments;
			var dir = Rotation.FromYaw( yaw ).Forward;
			var dist = radius + (Hash( i, 12, 59 ) % 160) - 80f;
			var pos = center + dir * dist;
			var h = 200f + Hash( i, 13, 61 ) % 380;
			var w = 120f + Hash( i, 14, 67 ) % 140;

			// Sunk below grade so only the top ~40% pokes above the void plane.
			Scenery.Box( parent, $"Tower_{i}", pos + Vector3.Up * (h * 0.5f - h * 0.55f), new Vector3( w, w * 0.8f, h ), wall, new Angles( 0, yaw, 0 ), GameMaterials.Concrete );

			if ( i % 3 == 0 )
				Scenery.Box( parent, $"TowerWin_{i}", pos + dir * (w * 0.4f + 3f) + Vector3.Up * (h * 0.3f), new Vector3( w * 0.5f, 4f, 30f ), lit, new Angles( 0, yaw, 0 ), GameMaterials.Metal );
		}
	}

	private static void BuildHillSilhouette( GameObject parent, Vector3 center, float radius, int segments, Color hillColor )
	{
		for ( var i = 0; i < segments; i++ )
		{
			var yaw = i * 360f / segments;
			var dir = Rotation.FromYaw( yaw ).Forward;
			var dist = radius + (Hash( i, 1, 7 ) % 120) - 60f;
			var pos = center + dir * dist;
			var h = 60f + (Hash( i, 2, 11 ) % 140);
			var w = 180f + (Hash( i, 3, 13 ) % 160);

			Scenery.Box( parent, $"Hill_{i}", pos + Vector3.Up * (h * 0.5f), new Vector3( w, w * 0.6f, h ), hillColor, default, GameMaterials.Grass );
		}
	}

	private static void BuildCitySilhouette( GameObject parent, Vector3 center, float radius, int segments, MapTheme theme )
	{
		var wall = new Color( 0.54f, 0.46f, 0.38f );
		var accent = theme switch
		{
			MapTheme.Alley => new Color( 0.62f, 0.32f, 0.22f ),
			MapTheme.ParkingGarage => new Color( 0.72f, 0.68f, 0.58f ),
			_ => new Color( 0.78f, 0.62f, 0.44f ),
		};

		for ( var i = 0; i < segments; i++ )
		{
			var yaw = i * 360f / segments;
			var dir = Rotation.FromYaw( yaw ).Forward;
			var dist = radius + (Hash( i, 5, 17 ) % 80) - 40f;
			var basePos = center + dir * dist;
			var buildings = 1 + Hash( i, 6, 19 ) % 3;

			for ( var b = 0; b < buildings; b++ )
			{
				var side = (b - (buildings - 1) * 0.5f) * 90f;
				var offset = Rotation.FromYaw( yaw ).Right * side;
				var h = 120f + Hash( i, b, 23 ) % 220;
				var w = 70f + Hash( i, b, 29 ) % 90;
				var d = 50f + Hash( i, b, 31 ) % 40f;
				var pos = basePos + offset;

				Scenery.Box( parent, $"Bld_{i}_{b}", pos + Vector3.Up * (h * 0.5f), new Vector3( w, d, h ), wall, new Angles( 0, yaw, 0 ), GameMaterials.Concrete );

				// Lit windows near the top.
				if ( h > 160f )
					Scenery.Box( parent, $"Win_{i}_{b}", pos + dir * (d * 0.5f + 3f) + Vector3.Up * (h * 0.62f), new Vector3( w * 0.7f, 4f, h * 0.2f ), accent, new Angles( 0, yaw, 0 ), GameMaterials.Metal );
			}
		}
	}

	private static void BuildIndustrialSilhouette( GameObject parent, Vector3 center, float radius, int segments, MapTheme theme )
	{
		var shed = new Color( 0.52f, 0.48f, 0.42f );
		var stack = new Color( 0.46f, 0.44f, 0.40f );

		for ( var i = 0; i < segments; i++ )
		{
			var yaw = i * 360f / segments;
			var dir = Rotation.FromYaw( yaw ).Forward;
			var dist = radius + (Hash( i, 7, 37 ) % 100) - 50f;
			var pos = center + dir * dist;
			var w = 200f + Hash( i, 8, 41 ) % 180;
			var d = 120f + Hash( i, 9, 43 ) % 80;
			var h = 90f + Hash( i, 10, 47 ) % 70;

			Scenery.Box( parent, $"Shed_{i}", pos + Vector3.Up * (h * 0.5f), new Vector3( w, d, h ), shed, new Angles( 0, yaw, 0 ), GameMaterials.Metal );

			if ( theme == MapTheme.Industrial && i % 4 == 0 )
			{
				var stackH = 180f + Hash( i, 11, 53 ) % 120;
				Scenery.Box( parent, $"Stack_{i}", pos + offset2d( dir, 60f ) + Vector3.Up * (h + stackH * 0.5f), new Vector3( 28f, 28f, stackH ), stack, default, GameMaterials.Metal );
			}

			if ( theme == MapTheme.GasStation && i % 5 == 0 )
			{
				Scenery.Box( parent, $"Pylon_{i}", pos + offset2d( dir, 40f ) + Vector3.Up * 160f, new Vector3( 12f, 12f, 320f ), stack, default, GameMaterials.Metal );
				Scenery.Box( parent, $"Wire_{i}", pos + offset2d( dir, 40f ) + Vector3.Up * 300f, new Vector3( 220f, 4f, 4f ), stack, new Angles( 0, yaw + 90f, 0 ), GameMaterials.Metal );
			}
		}
	}

	private static Vector3 offset2d( Vector3 dir, float amount ) => new( dir.x * amount, dir.y * amount, 0f );

	private static void BuildPerimeter( GameObject root, JobDef job, MapThemeInfo theme, Vector3 center, int jobIndex )
	{
		var ring = MathF.Min( job.GroundSize.x, job.GroundSize.y ) * 0.5f + 280f;
		var seed = jobIndex * 7919 + (int)job.Theme * 104729;

		switch ( job.Theme )
		{
			case MapTheme.Suburban:
			case MapTheme.Backyard:
				ScatterTrees( root, center, ring, ring + 900f, 22, seed );
				ScatterHouses( root, center, ring + 520f, 6, seed + 3 );
				break;

			case MapTheme.UrbanPlaza:
				ScatterTrees( root, center, ring + 200f, ring + 700f, 10, seed );
				PlaceRoadStrip( root, center, job.GroundSize.y * 0.5f + 180f, job.MapSize.x * 0.7f );
				break;

			case MapTheme.GasStation:
				PlaceParkingLines( root, center, job.GroundSize * 0.55f );
				ScatterTrees( root, center, ring + 400f, ring + 1100f, 8, seed );
				break;

			case MapTheme.Alley:
				BuildAlleyWalls( root, center, job.MapSize, job.GroundSize );
				break;

			case MapTheme.Storefront:
				PlaceSidewalk( root, center, job.GroundSize );
				BuildStorefrontRow( root, center, job.GroundSize.y * 0.5f + 420f, job.MapSize.x * 0.55f );
				break;

			case MapTheme.Industrial:
				BuildChainFence( root, center, job.GroundSize * 0.62f );
				ScatterTrees( root, center, ring + 600f, ring + 1000f, 4, seed );
				break;

			case MapTheme.ParkingGarage:
				BuildParkingGarageShell( root, center, job.GroundSize );
				PlaceParkingLines( root, center, job.GroundSize * 0.48f );
				break;

			case MapTheme.Waterfront:
				BuildWaterEdge( root, center, job.GroundSize, job.MapSize, new Color( 0.09f, 0.16f, 0.22f ) );
				break;

			case MapTheme.Snowfield:
				ScatterTrees( root, center, ring, ring + 1000f, 26, seed, new Color( 0.78f, 0.86f, 0.90f ) );
				break;

			case MapTheme.Underground:
				BuildEnclosedShell( root, center, job.GroundSize,
					wall: new Color( 0.20f, 0.20f, 0.23f ), ceiling: new Color( 0.10f, 0.10f, 0.12f ), wallH: 340f );
				break;

			case MapTheme.Interior:
				BuildEnclosedShell( root, center, job.GroundSize,
					wall: new Color( 0.44f, 0.44f, 0.48f ), ceiling: new Color( 0.30f, 0.30f, 0.34f ), wallH: 360f );
				break;

			case MapTheme.Rooftop:
				BuildRoofParapet( root, center, job.GroundSize );
				break;

			case MapTheme.Highway:
				ScatterTrees( root, center, ring + 300f, ring + 1100f, 14, seed, new Color( 0.10f, 0.16f, 0.10f ) );
				break;

			case MapTheme.Dam:
				BuildWaterEdge( root, center, job.GroundSize, job.MapSize, new Color( 0.10f, 0.30f, 0.34f ) );
				ScatterTrees( root, center, ring + 500f, ring + 1200f, 8, seed, new Color( 0.16f, 0.34f, 0.20f ) );
				break;
		}
	}

	/// <summary>Dark water plane filling the map north of the play pad, plus a bollard-dotted quay edge.</summary>
	private static void BuildWaterEdge( GameObject parent, Vector3 center, Vector2 playSize, Vector2 mapSize, Color water )
	{
		var edgeY = playSize.y * 0.5f + 60f;
		var waterDepth = mapSize.y * 0.5f - edgeY;
		var waterCy = edgeY + waterDepth * 0.5f;

		FlatQuad( parent, "Harbor", center + new Vector3( 0f, waterCy, 0f ), new Vector2( mapSize.x, waterDepth ), GameMaterials.Metal, water, z: DepthLayers.MapTransition + 1f );

		// Quay curb along the waterline with mooring bollards.
		Scenery.Box( parent, "QuayCurb", center + new Vector3( 0f, edgeY, 12f ), new Vector3( playSize.x + 300f, 30f, 24f ), new Color( 0.55f, 0.55f, 0.53f ), default, GameMaterials.Concrete );
		var bollards = 6;
		for ( var i = 0; i < bollards; i++ )
		{
			var x = -playSize.x * 0.5f + playSize.x * (i + 0.5f) / bollards;
			Scenery.Box( parent, $"Bollard_{i}", center + new Vector3( x, edgeY, 40f ), new Vector3( 22f, 22f, 34f ), new Color( 0.16f, 0.16f, 0.18f ), default, GameMaterials.Metal );
		}
	}

	/// <summary>Walls + ceiling slab wrapping the pad — used for tunnels, labs, and studio floors.</summary>
	private static void BuildEnclosedShell( GameObject parent, Vector3 center, Vector2 groundSize, Color wall, Color ceiling, float wallH )
	{
		var half = groundSize * 0.5f;
		var wallT = 40f;

		Scenery.Box( parent, "ShellN", center + new Vector3( 0f, half.y + 20f, wallH * 0.5f ), new Vector3( groundSize.x + 80f, wallT, wallH ), wall, default, GameMaterials.Concrete );
		Scenery.Box( parent, "ShellS", center + new Vector3( 0f, -(half.y + 20f), wallH * 0.5f ), new Vector3( groundSize.x + 80f, wallT, wallH ), wall, default, GameMaterials.Concrete );
		Scenery.Box( parent, "ShellE", center + new Vector3( half.x + 20f, 0f, wallH * 0.5f ), new Vector3( wallT, groundSize.y + 80f, wallH ), wall, default, GameMaterials.Concrete );
		Scenery.Box( parent, "ShellW", center + new Vector3( -(half.x + 20f), 0f, wallH * 0.5f ), new Vector3( wallT, groundSize.y + 80f, wallH ), wall, default, GameMaterials.Concrete );
		Scenery.Box( parent, "ShellCeiling", center + new Vector3( 0f, 0f, wallH + 14f ), new Vector3( groundSize.x + 80f, groundSize.y + 80f, 28f ), ceiling, default, GameMaterials.Concrete );

		// Recessed light strips −1 below the ceiling underside so the box interior isn't pitch flat.
		var strips = 3;
		for ( var i = 0; i < strips; i++ )
		{
			var y = -half.y * 0.5f + half.y * i * 0.5f;
			Scenery.Box( parent, $"ShellLight_{i}", center + new Vector3( 0f, y, wallH - 1f ), new Vector3( groundSize.x * 0.6f, 24f, 4f ), new Color( 0.95f, 0.95f, 0.88f ), default, GameMaterials.Metal );
		}
	}

	/// <summary>Low parapet ring so a rooftop pad reads as a building edge, not a floating slab.</summary>
	private static void BuildRoofParapet( GameObject parent, Vector3 center, Vector2 groundSize )
	{
		var half = groundSize * 0.5f;
		var parapet = new Color( 0.30f, 0.31f, 0.35f );
		var h = 52f;

		Scenery.Box( parent, "ParapetN", center + new Vector3( 0f, half.y + 14f, h * 0.5f ), new Vector3( groundSize.x + 60f, 28f, h ), parapet, default, GameMaterials.Concrete );
		Scenery.Box( parent, "ParapetS", center + new Vector3( 0f, -(half.y + 14f), h * 0.5f ), new Vector3( groundSize.x + 60f, 28f, h ), parapet, default, GameMaterials.Concrete );
		Scenery.Box( parent, "ParapetE", center + new Vector3( half.x + 14f, 0f, h * 0.5f ), new Vector3( 28f, groundSize.y + 60f, h ), parapet, default, GameMaterials.Concrete );
		Scenery.Box( parent, "ParapetW", center + new Vector3( -(half.x + 14f), 0f, h * 0.5f ), new Vector3( 28f, groundSize.y + 60f, h ), parapet, default, GameMaterials.Concrete );
	}

	private static void ScatterTrees( GameObject parent, Vector3 center, float minR, float maxR, int count, int seed, Color? leaf = null )
	{
		for ( var i = 0; i < count; i++ )
		{
			var t = Hash( seed, i, 101 );
			var angle = t % 360;
			var dist = minR + (Hash( seed, i, 103 ) % (int)(maxR - minR));
			var dir = Rotation.FromYaw( angle ).Forward;
			var scale = 0.85f + (Hash( seed, i, 107 ) % 40) / 100f;

			Scenery.Build( parent, new DecorDef
			{
				Kind = DecorKind.Tree,
				Position = center + dir * dist,
				Yaw = angle,
				Size = new Vector3( scale, 1f, 1f ),
				Color = leaf ?? Color.White,
			} );
		}
	}

	private static void ScatterHouses( GameObject parent, Vector3 center, float radius, int count, int seed )
	{
		var wall = new Color( 0.92f, 0.68f, 0.38f );
		for ( var i = 0; i < count; i++ )
		{
			var angle = (360f / count) * i + (Hash( seed, i, 131 ) % 30) - 15f;
			var dir = Rotation.FromYaw( angle ).Forward;
			var scale = 0.75f + (Hash( seed, i, 137 ) % 35) / 100f;

			Scenery.Build( parent, new DecorDef
			{
				Kind = DecorKind.House,
				Position = center + dir * radius,
				Yaw = angle + 180f,
				Size = new Vector3( 420f * scale, 240f * scale, 140f * scale ),
				Color = wall,
			} );
		}
	}

	private static void PlaceRoadStrip( GameObject parent, Vector3 center, float northY, float width )
	{
		var asphalt = new Color( 0.34f, 0.36f, 0.38f );
		var line = new Color( 1f, 0.82f, 0.08f );
		var pos = center + new Vector3( 0f, northY, 0f );

		Scenery.Box( parent, "Road", pos.WithZ( DepthLayers.RoadAbovePad ), new Vector3( width, 120f, 2f ), asphalt, default, GameMaterials.Concrete );
		Scenery.Box( parent, "RoadLine", pos.WithZ( DepthLayers.RoadMarking ), new Vector3( width * 0.9f, 6f, 1f ), line );
	}

	private static void PlaceParkingLines( GameObject parent, Vector3 center, Vector2 halfSize )
	{
		var line = new Color( 0.98f, 0.82f, 0.38f );
		for ( var i = -2; i <= 2; i++ )
		{
			var x = i * 130f;
			Scenery.Box( parent, $"ParkLine_{i}", center + new Vector3( x, halfSize.y * 0.35f, DepthLayers.PerimeterDecal ), new Vector3( 8f, halfSize.y * 0.55f, 1f ), line, default, GameMaterials.Concrete );
		}
	}

	private static void BuildAlleyWalls( GameObject parent, Vector3 center, Vector2 mapSize, Vector2 playSize )
	{
		var brick = new Color( 0.72f, 0.28f, 0.20f );
		var wallH = 280f;
		var wallT = 36f;
		var span = playSize.x * 0.5f + 80f;
		var depth = mapSize.y * 0.42f;

		foreach ( var side in new[] { -1f, 1f } )
		{
			var x = side * span;
			Scenery.Box( parent, side < 0 ? "AlleyWallL" : "AlleyWallR", center + new Vector3( x, 0f, wallH * 0.5f ), new Vector3( wallT, depth, wallH ), brick, default, GameMaterials.Concrete );
		}
	}

	private static void PlaceSidewalk( GameObject parent, Vector3 center, Vector2 playSize )
	{
		var slab = new Color( 0.76f, 0.66f, 0.50f );
		var south = center + new Vector3( 0f, -(playSize.y * 0.5f + 70f), 1f );
		Scenery.Box( parent, "Sidewalk", south.WithZ( DepthLayers.PerimeterDecal ), new Vector3( playSize.x + 160f, 90f, 2f ), slab, default, GameMaterials.Concrete );
	}

	private static void BuildStorefrontRow( GameObject parent, Vector3 center, float northY, float width )
	{
		var brick = new Color( 0.82f, 0.32f, 0.24f );
		var pos = center + new Vector3( 0f, northY, 0f );
		Scenery.Box( parent, "StoreRow", pos.WithZ( DepthLayers.PropAbovePad + 80f ), new Vector3( width, 80f, 180f ), brick, default, GameMaterials.Concrete );

		var bays = 4;
		var bayW = width / bays;
		for ( var i = 0; i < bays; i++ )
		{
			var x = -width * 0.5f + bayW * (i + 0.5f);
			Scenery.Box( parent, $"StoreWin_{i}", pos + new Vector3( x, -44f, 110f ), new Vector3( bayW * 0.55f, 6f, 70f ), new Color( 0.45f, 0.78f, 0.95f ), default, GameMaterials.Metal );
		}
	}

	private static void BuildChainFence( GameObject parent, Vector3 center, Vector2 halfSize )
	{
		var lenN = halfSize.x * 2f + 200f;
		var lenE = halfSize.y * 2f + 200f;
		var y = halfSize.y + 40f;
		var x = halfSize.x + 40f;

		Scenery.Build( parent, new DecorDef { Kind = DecorKind.Fence, Position = center + new Vector3( 0f, y, 0f ), Yaw = 0f, Size = new Vector3( lenN, 1f, 1f ) } );
		Scenery.Build( parent, new DecorDef { Kind = DecorKind.Fence, Position = center + new Vector3( 0f, -y, 0f ), Yaw = 0f, Size = new Vector3( lenN, 1f, 1f ) } );
		Scenery.Build( parent, new DecorDef { Kind = DecorKind.Fence, Position = center + new Vector3( x, 0f, 0f ), Yaw = 90f, Size = new Vector3( lenE, 1f, 1f ) } );
		Scenery.Build( parent, new DecorDef { Kind = DecorKind.Fence, Position = center + new Vector3( -x, 0f, 0f ), Yaw = 90f, Size = new Vector3( lenE, 1f, 1f ) } );
	}

	private static void BuildParkingGarageShell( GameObject parent, Vector3 center, Vector2 groundSize )
	{
		var half = groundSize * 0.5f;
		var wall = new Color( 0.46f, 0.46f, 0.48f );
		var pillar = new Color( 0.54f, 0.54f, 0.56f );
		var line = new Color( 0.98f, 0.82f, 0.38f );
		var wallH = 240f;
		var wallT = 40f;

		// Low perimeter walls — reads as an enclosed deck, not an open alley.
		Scenery.Box( parent, "GarageWallN", center + new Vector3( 0f, half.y + 20f, wallH * 0.5f ), new Vector3( groundSize.x + 80f, wallT, wallH ), wall, default, GameMaterials.Concrete );
		Scenery.Box( parent, "GarageWallS", center + new Vector3( 0f, -(half.y + 20f), wallH * 0.5f ), new Vector3( groundSize.x + 80f, wallT, wallH ), wall, default, GameMaterials.Concrete );
		Scenery.Box( parent, "GarageWallE", center + new Vector3( half.x + 20f, 0f, wallH * 0.5f ), new Vector3( wallT, groundSize.y + 80f, wallH ), wall, default, GameMaterials.Concrete );
		Scenery.Box( parent, "GarageWallW", center + new Vector3( -(half.x + 20f), 0f, wallH * 0.5f ), new Vector3( wallT, groundSize.y + 80f, wallH ), wall, default, GameMaterials.Concrete );

		// Overhead slab + support columns.
		Scenery.Box( parent, "GarageCeiling", center + new Vector3( 0f, 0f, 230f ), new Vector3( groundSize.x + 40f, groundSize.y + 40f, 28f ), new Color( 0.34f, 0.34f, 0.36f ), default, GameMaterials.Concrete );

		foreach ( var (x, y) in new (float x, float y)[] { (-220f, 160f), (220f, 160f), (-220f, -160f), (220f, -160f), (0f, 260f), (0f, -260f) } )
			Scenery.Box( parent, $"GaragePillar_{x}_{y}", center + new Vector3( x, y, 115f ), new Vector3( 46f, 46f, 230f ), pillar, default, GameMaterials.Concrete );

		// Ramp arrow markings at the south entrance.
		Scenery.Box( parent, "GarageRampMark", center + new Vector3( 0f, -(half.y - 40f), DepthLayers.PerimeterDecal ), new Vector3( 120f, 80f, 2f ), line, default, GameMaterials.Concrete );
	}

	private static int Hash( params int[] values )
	{
		var h = 17;
		foreach ( var v in values )
			h = h * 31 + v;
		return Math.Abs( h );
	}
}
