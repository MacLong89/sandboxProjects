namespace Offshore;

/// <summary>Sizes sprites from real PNG dimensions so art is never stretched.</summary>
public static class SpriteSizer
{
	const float DefaultPixelsPerUnit = 1f;

	public static Vector2 Native( string path, float pixelsPerUnit = DefaultPixelsPerUnit )
	{
		var tex = LoadTexture( path );
		if ( tex is null || !tex.IsValid || tex.Height <= 0 )
			return new Vector2( 64, 64 ) * pixelsPerUnit;
		return new Vector2( tex.Width, tex.Height ) * pixelsPerUnit;
	}

	public static Vector2 FitHeight( string path, float worldHeight )
	{
		var tex = LoadTexture( path );
		if ( tex is null || !tex.IsValid || tex.Height <= 0 )
			return new Vector2( worldHeight, worldHeight );
		var ar = (float)tex.Width / tex.Height;
		return new Vector2( worldHeight * ar, worldHeight );
	}

	public static Vector2 FitWidth( string path, float worldWidth )
	{
		var tex = LoadTexture( path );
		if ( tex is null || !tex.IsValid || tex.Width <= 0 )
			return new Vector2( worldWidth, worldWidth );
		var ar = (float)tex.Width / tex.Height;
		return new Vector2( worldWidth, worldWidth / ar );
	}

	public static Texture LoadTexture( string path )
	{
		if ( string.IsNullOrEmpty( path ) ) return null;
		path = path.Replace( '\\', '/' );
		if ( !path.EndsWith( ".png", StringComparison.OrdinalIgnoreCase ) )
			path += ".png";
		if ( !path.StartsWith( "textures/" ) && !path.StartsWith( "/textures/" ) )
			path = "textures/" + path.TrimStart( '/' );

		return Texture.LoadFromFileSystem( path, FileSystem.Mounted, false )
			?? Texture.LoadFromFileSystem( "/" + path.TrimStart( '/' ), FileSystem.Mounted, false );
	}
}
