namespace Fauna2;

/// <summary>Maps data definitions to supplied pixel sprites in Assets/models/.</summary>
public static class WorldSpriteCatalog
{
	public static string PropFor( PlaceableDefinition def )
	{
		if ( def is null ) return "building";

		var stem = Defs.ResourceStem( def.ResourceName ?? "" ).ToLowerInvariant();

		if ( def.IsEntrance || stem == "entrance" ) return "entrance";
		if ( def.ProvidesShop || stem.Contains( "kiosk" ) || stem.Contains( "shop" ) ) return "shop";
		if ( stem.Contains( "food_stand" ) || stem.Contains( "snack" ) ) return "food_stand";

		if ( def.ProvidesRestaurant || stem.StartsWith( "restaurant" ) )
		{
			if ( stem.Contains( "cafe" ) ) return "cafe";
			if ( stem.Contains( "cafeteria" ) ) return "cafeteria";
			return "restaurant";
		}

		if ( def.ProvidesRestroom || stem.Contains( "restroom" ) ) return "restroom";
		if ( stem.Contains( "playground" ) ) return "playground";

		if ( ( def.Category == BuildCategory.Paths || stem.StartsWith( "path" ) ) && !def.IsEntrance )
			return "path";

		if ( stem.Contains( "aspen" ) ) return "aspen_tree";
		if ( stem.Contains( "pine" ) ) return "pine_tree";
		if ( stem.Contains( "oak" ) || stem.Contains( "tree" ) ) return "oak_tree";
		if ( stem.Contains( "bush" ) ) return "bush";
		if ( stem.Contains( "rock" ) ) return "rock";
		if ( stem.Contains( "pond" ) ) return "pond";

		if ( def.Category == BuildCategory.Habitats ) return "habitat_ground";
		if ( def.Category == BuildCategory.Nature ) return "oak_tree";

		return "building";
	}

	public static bool UsesFootprintDrawDimensions( PlaceableDefinition def, string prop )
	{
		if ( def is null || def.IsPathTile ) return false;
		return !IsNatureProp( prop );
	}

	private static bool IsNatureProp( string prop ) => prop is
		"oak_tree" or "aspen_tree" or "pine_tree" or "pine" or "bush" or "cactus" or "rock" or "pond";

	public static Vector2 DrawDimensionsFor( PlaceableDefinition def )
	{
		if ( def is null ) return new Vector2( GameConstants.TileSize, GameConstants.TileSize );

		var prop = PropFor( def );
		if ( !UsesFootprintDrawDimensions( def, prop ) )
		{
			var scalar = SizeFor( def );
			return new Vector2( scalar, scalar );
		}

		if ( def.IsEntrance )
			return GameConstants.EntranceFootprint;

		return def.EffectiveFootprint;
	}

	public static float SizeFor( PlaceableDefinition def )
	{
		if ( def is null ) return GameConstants.TileSize;

		var prop = PropFor( def );

		if ( UsesFootprintDrawDimensions( def, prop ) )
		{
			var fp = def.EffectiveFootprint;
			return MathF.Max( fp.x, fp.y );
		}

		if ( def.IsPathTile )
			return GameConstants.Tiles( 1f );

		if ( prop is "oak_tree" or "aspen_tree" or "pine_tree" or "pine" )
			return GameConstants.Tiles( GameConstants.TreeSpriteTiles );

		if ( prop is "pond" )
			return GameConstants.Tiles( 4f );

		if ( prop is "bush" or "cactus" )
			return GameConstants.Tiles( GameConstants.BushSpriteTiles );

		if ( prop is "rock" )
			return GameConstants.Tiles( GameConstants.RockSpriteTiles );

		var footprint = def.EffectiveFootprint;
		return MathF.Max( footprint.x, footprint.y ).Clamp( GameConstants.Tiles( 0.9f ), GameConstants.Tiles( 2.5f ) );
	}

	public static string CritterFor( string animalStem )
	{
		if ( string.IsNullOrEmpty( animalStem ) ) return "deer";
		var stem = animalStem.ToLowerInvariant();

		return stem switch
		{
			"blackbear" or "black_bear" => "black_bear",
			"snowleopard" or "snow_leopard" => "snow_leopard",
			"mountainlion" or "cougar" => "cougar",
			"polarbear" or "polar_bear" => "polar_bear",
			"seal_lion" or "sealion" or "sea_lion" => "sea_lion",
			_ => stem,
		};
	}

	public static float AnimalTileSize( AnimalDefinition def )
	{
		var meters = RealWorldHeightMeters( def );
		return GameConstants.PlayerSpriteTiles * (meters / GameConstants.HumanReferenceHeightMeters) * GameConstants.AnimalScaleMultiplier;
	}

	public static float AnimalWorldSize( AnimalDefinition def ) =>
		GameConstants.Tiles( AnimalTileSize( def ) );

	public static void ConfigurePickBox( BoxCollider collider, float worldSize )
	{
		collider.Scale = new Vector3( worldSize * 0.55f, worldSize * 0.30f, worldSize * 0.24f );
		collider.Center = new Vector3( worldSize * 0.28f, 0, worldSize * 0.16f );
	}

	public static void ConfigurePickSphere( SphereCollider collider, float worldSize )
	{
		collider.Radius = worldSize * 0.36f;
		collider.Center = new Vector3( 0, 0, worldSize * 0.18f );
	}

	static float RealWorldHeightMeters( AnimalDefinition def )
	{
		if ( def is null ) return GameConstants.HumanReferenceHeightMeters * 0.5f;
		if ( def.RealWorldHeightMeters > 0f ) return def.RealWorldHeightMeters;

		var stem = Defs.ResourceStem( def.ResourceName );
		var key = CritterFor( stem );

		return key switch
		{
			"rabbit" => 0.25f,
			"squirrel" => 0.20f,
			"fox" => 0.38f,
			"deer" => 1.00f,
			"wolf" => 0.85f,
			"black_bear" => 0.95f,
			"moose" => 1.90f,
			"alligator" => 2.70f,
			"elephant" => 3.20f,
			"giraffe" => 4.70f,
			"rhino" => 1.70f,
			"hippo" => 1.50f,
			"lion" or "tiger" => 1.10f,
			"polar_bear" => 1.45f,
			"shark" => 1.50f,
			"dolphin" => 0.95f,
			"octopus" => 0.35f,
			_ => GameConstants.HumanReferenceHeightMeters * 0.5f,
		};
	}
}
