namespace Offshore;

/// <summary>Loads PNG sprites and spawns camera-facing SpriteRenderers on the XZ play plane.</summary>
public static class OffshoreSprites
{
	private static readonly Dictionary<string, Texture> TextureCache = new();
	private static readonly Dictionary<string, Sprite> SpriteCache = new();
	private static Texture _missing;

	/// <summary>Clears cached GPU textures after hotload to avoid stale handle crashes.</summary>
	public static void ResetCaches()
	{
		TextureCache.Clear();
		SpriteCache.Clear();
		_missing = null;
	}

	public static class Paths
	{
		public const string Angler = "textures/sprites/angler.png";
		public const string Dock = "textures/props/dock.png";
		public const string Crate = "textures/props/crate.png";
		public const string Barrel = "textures/props/barrel.png";
		public const string Cooler = "textures/props/cooler.png";
		public const string Buoy = "textures/props/buoy.png";
		public const string Hook = "textures/props/hook.png";
		public const string Bobber = "textures/props/bobber.png";
		public const string Rod = "textures/props/rod.png";
		public const string Rowboat = "textures/props/boats/rowboat.png";
		public const string Boat = "textures/props/boats/bay_boat.png";
		// Empty hulls at the pier tip (not boarded).
		public const string BoatRow = "textures/props/boats/rowboat.png";
		public const string BoatBay = "textures/props/boats/bay_boat.png";
		public const string BoatSport = "textures/props/boats/sport_fisher.png";
		public const string BoatTrawler = "textures/props/boats/trawler.png";
		// Fisherman-in-boat avatars while boarded.
		public const string BoatRowBoarded = "textures/props/boats/rowboat_boarded.png";
		public const string BoatBayBoarded = "textures/props/boats/bay_boat_boarded.png";
		public const string BoatSportBoarded = "textures/props/boats/sport_fisher_boarded.png";
		public const string BoatTrawlerBoarded = "textures/props/boats/trawler_boarded.png";
		// Legacy paths (pre-boats/ folder) — kept so old references still resolve.
		public const string RowboatLegacy = "textures/props/rowboat.png";
		public const string BoatLegacy = "textures/props/boat.png";
		public const string Seaweed = "textures/props/seaweed.png";
		public const string Rock = "textures/props/rock.png";
		public const string FishBluegill = "textures/fish/fish_bluegill.png";
		public const string FishPerch = "textures/fish/fish_perch.png";
		public const string FishBass = "textures/fish/fish_bass.png";
		public const string FishTrout = "textures/fish/fish_trout.png";
		public const string FishRedSnapper = "textures/fish/fish_redsnapper.png";
		public const string FishTuna = "textures/fish/fish_tuna.png";
		public const string FishMarlin = "textures/fish/fish_marlin.png";
		public const string FishLargemouthBass = "textures/fish/fish_largemouth_bass.png";
		public const string IconJournal = "textures/ui/icon_journal.png";
		public const string IconSettings = "textures/ui/icon_settings.png";
		public const string BannerNewCatch = "textures/ui/banner_new_catch.png";
		public const string Sunburst = "textures/ui/sunburst.png";
		public const string Coin = "textures/ui/coin.png";
		public const string PanelFrame = "textures/ui/panel_frame.png";
		public const string IconRod = "textures/ui/icon_rod.png";
		public const string IconReel = "textures/ui/icon_reel.png";
		public const string IconLine = "textures/ui/icon_line.png";
		public const string IconHook = "textures/ui/icon_hook.png";
		public const string IconCooler = "textures/ui/icon_cooler.png";
		public const string IconBoat = "textures/ui/icon_boat.png";
		public const string IconFinder = "textures/ui/icon_finder.png";
		public const string CastMeter = "textures/ui/cast_meter.png";
		public const string BtnKeep = "textures/ui/btn_keep.png";
		public const string BtnRelease = "textures/ui/btn_release.png";
		public const string Sonar = "textures/ui/sonar.png";
		public const string IconPin = "textures/ui/icon_pin.png";
		public const string IconSun = "textures/ui/icon_sun.png";
		public const string IconCloud = "textures/ui/icon_cloud.png";
		public const string IconBag = "textures/ui/icon_bag.png";
		public const string IconDollar = "textures/ui/icon_dollar.png";
		public const string IconHelm = "textures/ui/icon_helm.png";
		public const string IconGem = "textures/ui/icon_gem.png";
		public const string IconBackpack = "textures/ui/icon_backpack.png";
		public const string BgOldDockDawn = "textures/backgrounds/old_dock_dawn.png";

		// Scene refresh (goal art pack)
		public const string BaitShop = "textures/props/bait_shop.png";
		public const string DockPier = "textures/props/dock_pier.png";
		public const string DockHub = "textures/props/dock_hub_v2.png";
		public const string Fisherman = "textures/props/fisherman.png";
		public const string FishermanIdle = "textures/props/fisherman/idle.png";
		public const string FishermanWalk01 = "textures/props/fisherman/walk_01.png";
		public const string FishermanWalk02 = "textures/props/fisherman/walk_02.png";
		public const string FishermanWalk03 = "textures/props/fisherman/walk_03.png";
		public const string FishermanWalk04 = "textures/props/fisherman/walk_04.png";
		public const string FishermanCast01 = "textures/props/fisherman/cast_01.png";
		public const string FishermanCast02 = "textures/props/fisherman/cast_02.png";
		public const string FishermanCast03 = "textures/props/fisherman/cast_03.png";
		public const string DockPiles = "textures/props/dock_piles.png";
		public const string Kelp = "textures/props/kelp.png";
		public const string SeabedCluster = "textures/props/seabed_cluster.png";
		public const string DistantBoat = "textures/props/distant_boat.png";
		public const string Seagull = "textures/props/seagull.png";
		public const string BobberPixel = "textures/props/bobber_pixel.png";
		public const string GodRays = "textures/environment/godrays.png";
		public const string MountainsDawn = "textures/environment/mountains_dawn.png";
		public const string WaterFill = "textures/environment/water_fill.png";
		public const string SunriseWaterBg = "textures/environment/sunrise_water_bg_v2.png";
		public const string SeafloorSlope = "textures/environment/seafloor_slope.png";
		public const string SunPixel = "textures/environment/sun_pixel.png";
		public const string MoonPixel = "textures/environment/moon_pixel.png";
		public const string CloudA = "textures/environment/cloud_a.png";
		public const string CloudB = "textures/environment/cloud_b.png";
		public const string CloudC = "textures/environment/cloud_c.png";
	}

	public static Texture Load( string path )
	{
		if ( string.IsNullOrWhiteSpace( path ) )
			return MissingTexture();

		if ( TextureCache.TryGetValue( path, out var cached ) && cached.IsValid() )
			return cached;

		try
		{
			var tex = Texture.Load( path );
			if ( tex is not null && tex.IsValid() )
			{
				TextureCache[path] = tex;
				Log.Info( $"[Offshore Art] Loaded '{path}'" );
				return tex;
			}
		}
		catch ( System.Exception e )
		{
			Log.Warning( $"[Offshore Art] Texture.Load failed '{path}': {e.Message}" );
		}

		Log.Warning( $"[Offshore Art] Missing sprite '{path}' — using magenta fallback" );
		return MissingTexture();
	}

	/// <summary>Try <paramref name="path"/>, then <paramref name="fallbackPath"/>, then magenta.</summary>
	public static Texture LoadOrFallback( string path, string fallbackPath )
	{
		if ( TryLoadQuiet( path, out var primary ) )
			return primary;

		Log.Warning( $"[Offshore Art] Primary missing '{path}' — falling back to '{fallbackPath}'" );
		return Load( fallbackPath );
	}

	public static bool HasTexture( string path ) => TryLoadQuiet( path, out _ );

	private static bool TryLoadQuiet( string path, out Texture texture )
	{
		texture = null;
		if ( string.IsNullOrWhiteSpace( path ) )
			return false;

		if ( TextureCache.TryGetValue( path, out var cached ) && cached.IsValid() )
		{
			texture = cached;
			return true;
		}

		try
		{
			var tex = Texture.Load( path );
			if ( tex is not null && tex.IsValid() )
			{
				TextureCache[path] = tex;
				texture = tex;
				return true;
			}
		}
		catch
		{
			// missing art — caller supplies fallback
		}

		return false;
	}

	public static Sprite MakeSprite( Texture texture )
	{
		if ( texture is null || !texture.IsValid() )
			texture = MissingTexture();

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
					Name = "Default",
					Frames = [new Sprite.Frame { Texture = texture }],
				},
			],
		};
		SpriteCache[key] = sprite;
		return sprite;
	}

	/// <summary>
	/// World size from a target width, preserving the texture's pixel aspect ratio (no stretch).
	/// </summary>
	public static Vector2 WorldSizeKeepingAspect( Texture texture, float worldWidth, float fallbackAspect = 2f )
	{
		worldWidth = MathF.Max( 0.1f, worldWidth );
		var aspect = MathF.Max( 0.05f, fallbackAspect );
		if ( texture is not null && texture.IsValid() && texture.Width > 0 && texture.Height > 0 )
			aspect = (float)texture.Width / texture.Height;
		return new Vector2( worldWidth, worldWidth / aspect );
	}

	/// <summary>
	/// World size from a target height, preserving the texture's pixel aspect ratio (no stretch).
	/// Prefer this for wide boat art so height stays readable without giant widths.
	/// </summary>
	public static Vector2 WorldSizeFromHeight( Texture texture, float worldHeight, float fallbackAspect = 2.5f )
	{
		worldHeight = MathF.Max( 0.1f, worldHeight );
		var aspect = MathF.Max( 0.05f, fallbackAspect );
		if ( texture is not null && texture.IsValid() && texture.Width > 0 && texture.Height > 0 )
			aspect = (float)texture.Width / texture.Height;
		return new Vector2( worldHeight * aspect, worldHeight );
	}

	/// <summary>Known source PNG pixel sizes (authoritative aspect — avoids bad runtime Width/Height).</summary>
	public static bool TryGetBoatPixelSize( string path, out int pixelW, out int pixelH )
	{
		pixelW = 0;
		pixelH = 0;
		if ( string.IsNullOrWhiteSpace( path ) )
			return false;

		var key = path.Replace( '\\', '/' ).ToLowerInvariant();
		(int w, int h)? size = key switch
		{
			"textures/props/boats/rowboat.png" => (1474, 579),
			"textures/props/boats/rowboat_boarded.png" => (1209, 853),
			"textures/props/boats/bay_boat.png" => (1457, 739),
			"textures/props/boats/bay_boat_boarded.png" => (1431, 863),
			"textures/props/boats/sport_fisher.png" => (1436, 903),
			"textures/props/boats/sport_fisher_boarded.png" => (1395, 858),
			"textures/props/boats/trawler.png" => (1406, 962),
			"textures/props/boats/trawler_boarded.png" => (1355, 867),
			_ => null
		};

		if ( size is null )
			return false;

		pixelW = size.Value.w;
		pixelH = size.Value.h;
		return true;
	}

	/// <summary>
	/// Boat world size. Size aspect MUST match the PNG exactly — SpriteRenderer fits
	/// the texture inside Size (contain). A shorter Size box than the PNG aspect
	/// vertically scrunches the art (exactly what we were seeing in-game).
	/// </summary>
	public static Vector2 BoatWorldSize( string path, Texture texture, float worldHeight )
	{
		worldHeight = MathF.Max( 0.1f, worldHeight );
		float aspect;
		if ( TryGetBoatPixelSize( path, out var pw, out var ph ) && ph > 0 )
			aspect = (float)pw / ph;
		else if ( texture is not null && texture.IsValid() && texture.Width > 0 && texture.Height > 0 )
			aspect = (float)texture.Width / texture.Height;
		else
			aspect = 2.2f;

		return new Vector2( worldHeight * aspect, worldHeight );
	}

	/// <summary>Same SpriteRenderer setup as fisherman / dock props (Billboard Always).</summary>
	public static void ConfigureBoatRenderer( SpriteRenderer renderer )
	{
		if ( renderer is null || !renderer.IsValid() )
			return;

		renderer.StartingAnimationName = "Default";
		renderer.Billboard = SpriteRenderer.BillboardMode.Always;
		renderer.Lighting = false;
		renderer.FogStrength = 0f;
		renderer.Opaque = false;
		renderer.AlphaCutoff = 0.05f;
		renderer.IsSorted = true;
		renderer.TextureFilter = Sandbox.Rendering.FilterMode.Bilinear;
		renderer.Color = Color.White;
	}

	/// <summary>No-op kept for call sites — boats use Billboard Always like other props.</summary>
	public static void FaceSideCamera( GameObject go )
	{
		if ( go is null || !go.IsValid() )
			return;
		go.LocalScale = Vector3.One;
		go.LocalRotation = Rotation.Identity;
	}

	public static void ApplyBoatSprite(
		SpriteRenderer renderer,
		GameObject go,
		Texture tex,
		Vector2 size,
		bool flipHorizontal )
	{
		if ( renderer is null || !renderer.IsValid() )
			return;

		ConfigureBoatRenderer( renderer );
		renderer.Sprite = MakeSprite( tex );
		renderer.StartingAnimationName = "Default";
		renderer.Size = size;
		renderer.FlipHorizontal = flipHorizontal;
		if ( go is not null && go.IsValid() )
		{
			go.LocalScale = Vector3.One;
			go.LocalRotation = Rotation.Identity;
		}

		Log.Info(
			$"[Offshore Boat] ApplySprite size={size} aspect={size.x / MathF.Max( 0.001f, size.y ):0.00} " +
			$"billboard=Always flip={flipHorizontal}" );
	}

	/// <summary>
	/// Idle / Walk / CastCharge / CastRelease for the side-view angler.
	/// Animation indices: 0 Idle, 1 Walk, 2 CastCharge, 3 CastRelease.
	/// Built like DEEP's diver sprite (one clip per pose) — multi-frame Walk/Cast
	/// previously hard-killed the process with no managed exception.
	/// </summary>
	public static Sprite MakeFishermanSprite()
	{
		const string key = "fisherman_anim_v3_safe";
		if ( SpriteCache.TryGetValue( key, out var cached ) )
			return cached;

		Log.Info( "[Offshore Art] Building fisherman sprite clips..." );

		var idle = Load( Paths.FishermanIdle );
		var walk = Load( Paths.FishermanWalk01 );
		var charge = Load( Paths.FishermanCast01 );
		var release = Load( Paths.FishermanCast02 );

		var sprite = new Sprite
		{
			Animations =
			[
				Clip( "Idle", idle, 1f ),
				Clip( "Walk", walk, 8f ),
				Clip( "CastCharge", charge, 1f ),
				Clip( "CastRelease", release, 1f ),
			],
		};

		SpriteCache[key] = sprite;
		Log.Info( "[Offshore Art] Fisherman sprite ready" );
		return sprite;
	}

	private static Sprite.Animation Clip( string name, Texture texture, float frameRate ) => new()
	{
		Name = name,
		Frames = [new Sprite.Frame { Texture = texture }],
		FrameRate = frameRate,
		LoopMode = Sprite.LoopMode.Loop,
	};

	public static SpriteRenderer SpawnFisherman(
		GameObject parent,
		Vector2 worldSize,
		string name = "FishermanAvatar",
		bool animated = true )
	{
		var go = new GameObject( parent, true, name );
		go.LocalPosition = Vector3.Zero;

		var renderer = go.Components.Create<SpriteRenderer>();
		renderer.Size = worldSize;
		renderer.Billboard = SpriteRenderer.BillboardMode.Always;
		renderer.Lighting = false;
		renderer.FogStrength = 0f;
		renderer.Opaque = false;
		renderer.AlphaCutoff = 0.05f;
		renderer.TextureFilter = Sandbox.Rendering.FilterMode.Bilinear;
		renderer.IsSorted = true;
		renderer.PlaybackSpeed = 1f;

		if ( animated )
		{
			try
			{
				renderer.Sprite = MakeFishermanSprite();
				renderer.StartingAnimationName = "Idle";
				Log.Info( "[Offshore Art] Fisherman renderer assigned (animated)" );
			}
			catch ( Exception e )
			{
				Log.Warning( $"[Offshore Art] Fisherman anim failed, idle only: {e.Message}" );
				renderer.Sprite = MakeSprite( Load( Paths.FishermanIdle ) );
				renderer.StartingAnimationName = "Default";
			}
		}
		else
		{
			renderer.Sprite = MakeSprite( Load( Paths.FishermanIdle ) );
			renderer.StartingAnimationName = "Default";
			Log.Info( "[Offshore Art] Fisherman renderer assigned (idle boot sprite)" );
		}

		return renderer;
	}

	/// <summary>Spawns a camera-facing sprite on the XZ play plane.</summary>
	public static SpriteRenderer Spawn(
		GameObject parent,
		string texturePath,
		Vector2 worldSize,
		Vector3 localPosition,
		string name = "Sprite",
		float depthBias = 0f )
	{
		var go = new GameObject( parent, true, name );
		// Y = into scene (toward/away from camera). Positive Y = toward camera on Deep layout.
		go.LocalPosition = new Vector3( localPosition.x, localPosition.y + depthBias, localPosition.z );

		var renderer = go.Components.Create<SpriteRenderer>();
		renderer.Sprite = MakeSprite( Load( texturePath ) );
		renderer.StartingAnimationName = "Default";
		renderer.Size = worldSize;
		// Always face the side camera — avoids XY/XZ orientation bugs and stretch.
		renderer.Billboard = SpriteRenderer.BillboardMode.Always;
		renderer.Lighting = false;
		renderer.FogStrength = 0f;
		renderer.Opaque = false;
		renderer.AlphaCutoff = 0.05f;
		renderer.TextureFilter = Sandbox.Rendering.FilterMode.Bilinear;
		renderer.IsSorted = true;

		return renderer;
	}

	private static Texture MissingTexture()
	{
		if ( _missing is not null && _missing.IsValid() )
			return _missing;

		var pixels = new byte[8 * 8 * 4];
		for ( var i = 0; i < pixels.Length; i += 4 )
		{
			pixels[i] = 255;
			pixels[i + 1] = 0;
			pixels[i + 2] = 255;
			pixels[i + 3] = 255;
		}

		_missing = Texture.Create( 8, 8, ImageFormat.RGBA8888 )
			.WithName( "offshore_missing_magenta" )
			.WithData( pixels )
			.Finish();

		return _missing ?? Texture.White;
	}
}
