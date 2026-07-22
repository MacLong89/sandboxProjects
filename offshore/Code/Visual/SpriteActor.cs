namespace Offshore;

/// <summary>Billboarded point-filtered sprite. Size follows PNG aspect unless overridden.</summary>
public sealed class SpriteActor : Component
{
	[Property] public string Path { get; set; }
	[Property] public Vector2 Size { get; set; } = new( 64, 64 );
	[Property] public bool Flip { get; set; }
	[Property] public Color Tint { get; set; } = Color.White;
	[Property] public int Sort { get; set; }
	/// <summary>When true, Size is rebuilt from texture pixels * PixelScale on every reload.</summary>
	[Property] public bool LockNativeAspect { get; set; } = true;
	[Property] public float PixelScale { get; set; } = 1f;

	SpriteRenderer _renderer;
	string _loaded;

	protected override void OnStart()
	{
		_renderer = Components.GetOrCreate<SpriteRenderer>();
		Configure( _renderer );
		Reload();
	}

	protected override void OnUpdate()
	{
		if ( _renderer is null ) return;
		if ( _loaded != Path ) Reload();
		Configure( _renderer );
		_renderer.Size = Size;
		_renderer.FlipHorizontal = Flip;
		_renderer.Color = Tint;
	}

	public void Set( string path, Vector2? size = null, bool? lockNative = null )
	{
		Path = path;
		if ( lockNative.HasValue )
			LockNativeAspect = lockNative.Value;
		if ( size.HasValue )
		{
			Size = size.Value;
			LockNativeAspect = false;
		}
		Reload();
	}

	public void SetNative( string path, float pixelScale = 1f )
	{
		Path = path;
		PixelScale = pixelScale;
		LockNativeAspect = true;
		Reload();
	}

	public void SetFitHeight( string path, float worldHeight )
	{
		Path = path;
		LockNativeAspect = false;
		Size = SpriteSizer.FitHeight( path, worldHeight );
		Reload();
		// Reload would not overwrite Size when LockNativeAspect is false — keep FitHeight size.
		Size = SpriteSizer.FitHeight( path, worldHeight );
	}

	void Reload()
	{
		_loaded = Path;
		if ( string.IsNullOrEmpty( Path ) ) return;
		var texture = SpriteSizer.LoadTexture( Path );
		if ( texture is null || !texture.IsValid )
		{
			Log.Warning( $"[OFFSHORE] Missing sprite '{Path}'" );
			return;
		}

		if ( LockNativeAspect && texture.Width > 0 && texture.Height > 0 )
			Size = new Vector2( texture.Width, texture.Height ) * PixelScale;

		if ( _renderer is not null )
			_renderer.Sprite = Sprite.FromTexture( texture );
	}

	public static void Configure( SpriteRenderer r )
	{
		if ( r is null ) return;
		r.IsSorted = true;
		r.Shadows = false;
		r.Lighting = false;
		r.Opaque = false;
		r.Billboard = SpriteRenderer.BillboardMode.Always;
		r.TextureFilter = Sandbox.Rendering.FilterMode.Point;
	}

	public static Sprite Load( string path )
	{
		var texture = SpriteSizer.LoadTexture( path );
		return texture is not null && texture.IsValid ? Sprite.FromTexture( texture ) : null;
	}
}
