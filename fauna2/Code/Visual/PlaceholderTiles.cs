namespace Fauna2;

/// <summary>
/// Solid-color fallback textures when PNGs are not mounted yet.
/// Distinct hues so biomes, paths, and missing props stay readable.
/// </summary>
public static class PlaceholderTiles
{
	private static readonly Dictionary<string, Texture> Cache = new( StringComparer.OrdinalIgnoreCase );

	public static Texture Grass => Solid( "grass", new Color( 0.34f, 0.58f, 0.30f ) );
	public static Texture Dirt => Solid( "dirt", new Color( 0.55f, 0.40f, 0.26f ) );
	public static Texture Path => Solid( "path", new Color( 0.72f, 0.66f, 0.52f ) );
	public static Texture Wilderness => Solid( "wilderness", new Color( 0.28f, 0.42f, 0.24f ) );
	public static Texture Water => Solid( "water", new Color( 0.22f, 0.48f, 0.72f ) );
	public static Texture Snow => Solid( "snow", new Color( 0.88f, 0.92f, 0.96f ) );
	public static Texture Sand => Solid( "sand", new Color( 0.86f, 0.76f, 0.48f ) );
	public static Texture Mud => Solid( "mud", new Color( 0.42f, 0.32f, 0.22f ) );
	public static Texture Beach => Solid( "beach", new Color( 0.90f, 0.82f, 0.58f ) );
	public static Texture Rainforest => Solid( "rainforest", new Color( 0.18f, 0.48f, 0.28f ) );
	public static Texture Rock => Solid( "rock", new Color( 0.52f, 0.52f, 0.56f ) );
	public static Texture Forest => Solid( "forest", new Color( 0.24f, 0.40f, 0.22f ) );

	/// <summary>Tinted block used when a prop/building sprite is missing.</summary>
	public static Texture Prop( string name, Color? tint = null )
	{
		var color = tint ?? GuessPropColor( name );
		return Solid( $"prop:{name}:{ColorKey( color )}", color );
	}

	public static void ResetCache() => Cache.Clear();

	private static Texture Solid( string key, Color color )
	{
		if ( Cache.TryGetValue( key, out var cached ) && cached.IsValid() )
			return cached;

		const int size = 16;
		var pixels = new byte[size * size * 4];
		var r = (byte)Math.Clamp( (int)(color.r * 255f), 0, 255 );
		var g = (byte)Math.Clamp( (int)(color.g * 255f), 0, 255 );
		var b = (byte)Math.Clamp( (int)(color.b * 255f), 0, 255 );
		for ( var i = 0; i < size * size; i++ )
		{
			var o = i * 4;
			pixels[o] = r;
			pixels[o + 1] = g;
			pixels[o + 2] = b;
			pixels[o + 3] = 255;
		}

		var texture = Texture.Create( size, size, ImageFormat.RGBA8888 )
			.WithName( $"fauna_placeholder_{key.Replace( ':', '_' )}" )
			.WithData( pixels )
			.Finish();

		Cache[key] = texture;
		return texture;
	}

	private static Color GuessPropColor( string name )
	{
		if ( string.IsNullOrEmpty( name ) )
			return new Color( 0.55f, 0.55f, 0.50f );

		var n = name.ToLowerInvariant();
		if ( n.Contains( "tree" ) || n is "pine" or "palm" or "bush" )
			return new Color( 0.22f, 0.48f, 0.26f );
		if ( n.Contains( "fence" ) )
			return new Color( 0.62f, 0.48f, 0.32f );
		if ( n.Contains( "rock" ) || n.Contains( "stone" ) )
			return new Color( 0.50f, 0.50f, 0.54f );
		if ( n.Contains( "pond" ) || n.Contains( "water" ) )
			return new Color( 0.28f, 0.55f, 0.78f );
		if ( n.Contains( "cactus" ) )
			return new Color( 0.35f, 0.62f, 0.32f );
		if ( n is "cafe" or "cafeteria" or "restaurant" or "food_stand" or "shop" or "kiosk" or "restroom" or "playground" or "entrance" )
			return new Color( 0.72f, 0.55f, 0.38f );

		return new Color( 0.58f, 0.52f, 0.42f );
	}

	private static string ColorKey( Color color ) =>
		$"{(int)(color.r * 255)}_{(int)(color.g * 255)}_{(int)(color.b * 255)}";
}
