namespace DeepDive;

/// <summary>Load pixel textures and build Sprite animation clips for DEEP.</summary>
public static class DeepDivePixelArt
{
	public const string IdleAnimation = "Idle";
	public const string SwimAnimation = "Swim";
	public const string SwimUpAnimation = "SwimUp";
	public const string SwimDownAnimation = "SwimDown";
	public const string HarpoonAnimation = "Harpoon";
	public const string DefaultAnimation = "Default";

	public const float DiverSourcePixels = 64f;

	private static readonly Dictionary<string, Texture> TextureCache = new();
	private static readonly Dictionary<string, Sprite> SpriteCache = new();
	private static readonly HashSet<string> LoggedLoads = new();
	private static readonly HashSet<string> LoggedMissing = new();

	public static Texture DiverIdle() => Load( "textures/diver/idle.png" );
	public static Texture DiverSwim() => Load( "textures/diver/swim.png" );
	public static Texture DiverSwimUp() => Load( "textures/diver/swim_up.png" );
	public static Texture DiverSwimDown() => Load( "textures/diver/swim_down.png" );
	public static Texture DiverHarpoon1() => Load( "textures/diver/harpoon_1.png" );
	public static Texture DiverHarpoon2() => Load( "textures/diver/harpoon_2.png" );
	public static Texture DiverHarpoon3() => Load( "textures/diver/harpoon_3.png" );
	public static Texture HarpoonSpear() => Load( "textures/effects/harpoon_spear.png" );
	public static Texture Boat() => Load( "textures/world/boat.png" );
	public static Texture Seaweed() => Load( "textures/world/seaweed.png" );
	public static Texture Ruins() => Load( "textures/world/ruins.png" );
	public static Texture Rocks() => Load( "textures/world/rocks.png" );
	public static Texture OceanBackdrop() => Load( "textures/world/ocean_backdrop.png" );
	public static Texture SeabedChunk() => Load( "textures/world/seabed_chunk.png" );
	public static Texture SeabedFill() => Load( "textures/world/seabed_fill.png" );
	public static Texture SeabedRidge() => Load( "textures/world/seabed_ridge.png" );
	public static Texture AbyssSilhouette() => Load( "textures/world/abyss_silhouette.png" );
	public static Texture CoralCluster() => Load( "textures/world/coral_cluster.png" );
	public static Texture CaveOverhang() => Load( "textures/world/cave_overhang.png" );

	public static Texture Jellyfish() => Load( "textures/creatures/jellyfish.png" );
	public static Texture ReefFish() => Load( "textures/creatures/reef_fish.png" );
	public static Texture Mine() => Load( "textures/creatures/mine.png" );
	public static Texture Puffer() => Load( "textures/creatures/puffer.png" );
	public static Texture Angler() => Load( "textures/creatures/angler.png" );

	public static Texture Loot( string id ) => Load( $"textures/loot/{id}.png" );

	public static Texture Load( string path )
	{
		if ( TextureCache.TryGetValue( path, out var cached ) && cached.IsValid() )
			return cached;

		try
		{
			var texture = Texture.Load( path );
			if ( texture.IsValid() )
			{
				TextureCache[path] = texture;
				if ( LoggedLoads.Add( path ) )
					Log.Info( $"[DeepDive Art] Loaded '{path}'." );
				return texture;
			}
		}
		catch ( System.Exception e )
		{
			if ( LoggedMissing.Add( path ) )
				Log.Warning( $"[DeepDive Art] Failed to load '{path}': {e.Message}" );
		}

		if ( LoggedMissing.Add( path ) )
			Log.Warning( $"[DeepDive Art] Missing '{path}' — using fallback." );

		var fallback = Texture.White;
		TextureCache[path] = fallback;
		return fallback;
	}

	public static Sprite MakeSprite( Texture texture )
	{
		if ( texture is null || !texture.IsValid() )
			return new Sprite();

		var key = string.IsNullOrEmpty( texture.ResourcePath )
			? $"tex:{texture.GetHashCode()}"
			: $"tex:{texture.ResourcePath}";

		if ( SpriteCache.TryGetValue( key, out var cached ) )
			return cached;

		var sprite = new Sprite
		{
			Animations =
			[
				new Sprite.Animation
				{
					Name = DefaultAnimation,
					Frames = [new Sprite.Frame { Texture = texture }],
					FrameRate = 1f,
				},
			],
		};
		SpriteCache[key] = sprite;
		return sprite;
	}

	public static Sprite DiverSprite()
	{
		const string key = "diver:anims";
		if ( SpriteCache.TryGetValue( key, out var cached ) )
			return cached;

		var idle = DiverIdle();
		var swim = DiverSwim();
		var up = DiverSwimUp();
		var down = DiverSwimDown();
		var harpoon1 = DiverHarpoon1();
		var harpoon2 = DiverHarpoon2();
		var harpoon3 = DiverHarpoon3();

		var sprite = new Sprite
		{
			Animations =
			[
				Clip( IdleAnimation, idle ),
				Clip( SwimAnimation, swim ),
				Clip( SwimUpAnimation, up ),
				Clip( SwimDownAnimation, down ),
				HarpoonClip( HarpoonAnimation, harpoon1, harpoon2, harpoon3 ),
			],
		};
		SpriteCache[key] = sprite;
		return sprite;
	}

	private static Sprite.Animation Clip( string name, Texture texture ) => new()
	{
		Name = name,
		Frames = [new Sprite.Frame { Texture = texture }],
		FrameRate = 1f,
		LoopMode = Sprite.LoopMode.Loop,
	};

	private static Sprite.Animation HarpoonClip( string name, params Texture[] textures )
	{
		var frames = textures
			.Where( t => t is not null && t.IsValid() && t != Texture.White )
			.Select( t => new Sprite.Frame { Texture = t } )
			.ToArray();

		if ( frames.Length == 0 )
			frames = [new Sprite.Frame { Texture = DiverSwim() }];

		return new Sprite.Animation
		{
			Name = name,
			Frames = frames.ToList(),
			FrameRate = 12f,
			LoopMode = Sprite.LoopMode.Loop,
		};
	}

	public static void ApplyWorldScale( SpriteRenderer renderer, float worldHeight, Texture texture = null )
	{
		if ( !renderer.IsValid() )
			return;

		var tex = texture;
		if ( tex is null || !tex.IsValid() )
			tex = renderer.Texture;

		var aspect = tex.IsValid() && tex.Height > 0
			? tex.Width / (float)tex.Height
			: 1f;

		renderer.Size = new Vector2( worldHeight * aspect, worldHeight );
		renderer.GameObject.LocalScale = Vector3.One;
	}

	public static void ApplyWorldWidth( SpriteRenderer renderer, float worldWidth, Texture texture = null )
	{
		if ( !renderer.IsValid() )
			return;

		var tex = texture;
		if ( tex is null || !tex.IsValid() )
			tex = renderer.Texture;

		var aspect = tex.IsValid() && tex.Height > 0
			? tex.Width / (float)tex.Height
			: 1f;

		renderer.Size = new Vector2( worldWidth, worldWidth / MathF.Max( aspect, 0.01f ) );
		renderer.GameObject.LocalScale = Vector3.One;
	}
}
