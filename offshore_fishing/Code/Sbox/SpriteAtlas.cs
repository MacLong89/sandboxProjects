namespace Sandbox;

/// <summary>Resolves art ids to textures: file first, procedural fallback second.</summary>
public static class SpriteAtlas
{
	private static readonly Dictionary<string, string> FilePaths = new()
	{
		["bg_harbor"] = "textures/art/bg_harbor.png",
		["bg_kelp"] = "textures/art/bg_kelp.png",
		["bg_bluewater"] = "textures/art/bg_bluewater.png",
		["bg_shelf"] = "textures/art/bg_shelf.png",
		["bg_trench"] = "textures/art/bg_trench.png",
		["boat_skiff"] = "textures/art/boat_skiff.png",
		["boat_fisher"] = "textures/art/boat_fisher.png",
		["boat_explorer"] = "textures/art/boat_explorer.png",
		["boat_oceanic"] = "textures/art/boat_oceanic.png",
		["player"] = "textures/art/player.png",
		["shopkeeper"] = "textures/art/shopkeeper.png",
		["dock"] = "textures/art/dock.png",
		["shop_interior"] = "textures/art/shop_interior.png",
	};

	public static Texture Resolve( string id )
	{
		if ( string.IsNullOrEmpty( id ) )
			return ProceduralSpriteFactory.Solid( "missing", 8, 8, Color.Magenta );

		if ( FilePaths.TryGetValue( id, out var path ) )
		{
			var fileTex = Texture.Load( path );
			if ( fileTex != null && fileTex.IsValid() )
				return fileTex;
		}

		// Try generic art path
		var generic = Texture.Load( $"textures/art/{id}.png" );
		if ( generic != null && generic.IsValid() )
			return generic;

		return ProceduralFallback( id );
	}

	private static Texture ProceduralFallback( string id )
	{
		if ( id.StartsWith( "bg_" ) )
		{
			return id switch
			{
				"bg_kelp" => ProceduralSpriteFactory.Background( id, new Color( 0.45f, 0.7f, 0.95f ), new Color( 0.7f, 0.85f, 1f ), new Color( 0.1f, 0.45f, 0.4f ), new Color( 0.35f, 0.3f, 0.15f ) ),
				"bg_bluewater" => ProceduralSpriteFactory.Background( id, new Color( 0.35f, 0.6f, 0.95f ), new Color( 0.6f, 0.8f, 1f ), new Color( 0.05f, 0.25f, 0.55f ), new Color( 0.2f, 0.25f, 0.3f ) ),
				"bg_shelf" => ProceduralSpriteFactory.Background( id, new Color( 0.3f, 0.4f, 0.55f ), new Color( 0.5f, 0.55f, 0.65f ), new Color( 0.05f, 0.15f, 0.35f ), new Color( 0.25f, 0.22f, 0.2f ) ),
				"bg_trench" => ProceduralSpriteFactory.Background( id, new Color( 0.05f, 0.05f, 0.12f ), new Color( 0.1f, 0.1f, 0.2f ), new Color( 0.02f, 0.05f, 0.12f ), new Color( 0.08f, 0.06f, 0.1f ) ),
				_ => ProceduralSpriteFactory.Background( id, new Color( 0.55f, 0.75f, 0.95f ), new Color( 0.85f, 0.9f, 0.7f ), new Color( 0.15f, 0.45f, 0.55f ), new Color( 0.45f, 0.4f, 0.25f ) )
			};
		}

		if ( id.StartsWith( "boat_" ) )
		{
			var hull = id.Contains( "oceanic" ) ? new Color( 0.85f, 0.85f, 0.9f ) :
				id.Contains( "explorer" ) ? new Color( 0.9f, 0.75f, 0.45f ) :
				id.Contains( "fisher" ) ? new Color( 0.95f, 0.95f, 0.98f ) :
				new Color( 0.8f, 0.55f, 0.3f );
			return ProceduralSpriteFactory.Boat( id, hull, new Color( 0.2f, 0.45f, 0.7f ) );
		}

		if ( id == "player" ) return ProceduralSpriteFactory.Player( id );
		if ( id == "shopkeeper" ) return ProceduralSpriteFactory.Player( id );

		if ( id.Contains( "fish" ) || id.StartsWith( "harbor_" ) || id.StartsWith( "kelp_" )
			|| id.StartsWith( "blue_" ) || id.StartsWith( "shelf_" ) || id.StartsWith( "trench_" ) )
		{
			var hash = id.GetHashCode();
			var body = Color.FromBytes( (byte)(60 + (hash & 0x7F)), (byte)(80 + ((hash >> 8) & 0x7F)), (byte)(100 + ((hash >> 16) & 0x7F)) );
			var fin = body * 0.75f;
			return ProceduralSpriteFactory.Fish( id, body, fin );
		}

		return ProceduralSpriteFactory.Solid( id, 16, 16, new Color( 0.7f, 0.55f, 0.3f ) );
	}
}
