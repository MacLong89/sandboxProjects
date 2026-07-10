namespace Fauna2;

/// <summary>Loads Fauna pixel textures and builds billboard sprites.</summary>
public static class PixelArt
{
	public const float SpriteSourcePixels = 32f;
	public const float SuppliedSpriteSourcePixels = 8f;
	// s&box renders the supplied PNG tile sprites at about 1/4 cell coverage when using the normal 32px sprite baseline.
	public const float TileSourcePixels = 8f;
	public const float TileCoverage = 1.1f;
	/// <summary>Extra depth-sort lift so opaque grass never clips supplied prop feet.</summary>
	public const float SuppliedPropSortClearance = 6f;
	public const int WalkFrameCount = 4;
	public const float WalkFrameRate = 8f;

	public const string IdleAnimationName = SpriteWalkAnimator.IdleAnimation;
	public const string WalkAnimationName = SpriteWalkAnimator.WalkAnimation;

	private static readonly Dictionary<string, Texture> _cache = new();
	private static readonly Dictionary<string, Sprite> _spriteCache = new();
	private static readonly HashSet<string> _missing = new( StringComparer.OrdinalIgnoreCase );
	private static readonly Dictionary<string, Texture> _playerIdle = new();
	private static readonly Dictionary<string, Texture> _playerWalk = new();
	private static readonly HashSet<string> _loggedLoads = new();
	private static bool _loggedTileManifest;
	private static bool _loggedSpriteManifest;

	public static void ResetRuntimeCaches()
	{
		_cache.Clear();
		_spriteCache.Clear();
		_missing.Clear();
		_playerIdle.Clear();
		_playerWalk.Clear();
		_loggedLoads.Clear();
		_loggedTileManifest = false;
		_loggedSpriteManifest = false;
	}

	public static Texture Critter( string key = "deer" ) =>
		_cache.GetOrCreate( $"critter:{key}", () =>
		{
			var path = CritterPath( key );
			if ( FileSystem.Mounted.FileExists( path ) )
				return LoadSuppliedSprite( path, key );
			return Load( $"textures/critters/{key}.png" );
		} );

	private static string CritterPath( string key ) => key switch
	{
		"rabbit" => SuppliedSpriteManifest.RabbitPath,
		"squirrel" => SuppliedSpriteManifest.SquirrelPath,
		"fox" => SuppliedSpriteManifest.FoxPath,
		"deer" => SuppliedSpriteManifest.DeerPath,
		"wolf" => SuppliedSpriteManifest.WolfPath,
		"black_bear" or "blackbear" => SuppliedSpriteManifest.BlackBearPath,
		"moose" => SuppliedSpriteManifest.MoosePath,
		"alligator" => SuppliedSpriteManifest.AlligatorPath,
		_ => SuppliedSpriteManifest.AnimalModelsRoot + $"{key}.png",
	};

	public static Texture PlayerSprite =>
		_cache.GetOrCreate( "player:static", () =>
			LoadSuppliedSprite( SuppliedSpriteManifest.PlayerSpritePath, "player_sprite" ) );

	public static Sprite PlayerSpriteResource( PlayerFacing facing = PlayerFacing.Down ) =>
		_spriteCache.GetOrCreate( $"player:sprite:{facing.ToKey()}", () =>
			BuildWalkSprite(
				PlayerIdleTexture( facing ),
				SuppliedSpriteManifest.PlayerAnimationDir( facing ),
				"player",
				facing.ToKey() ) );

	public static Texture PlayerIdle( PlayerFacing facing ) =>
		PlayerIdleTexture( facing );

	public static Texture PlayerWalk( PlayerFacing facing ) =>
		PlayerWalkTexture( facing, 0 );

	public static Texture PlayerWalkFrame( PlayerFacing facing, int frame ) =>
		PlayerWalkTexture( facing, frame );

	private static Texture PlayerIdleTexture( PlayerFacing facing ) =>
		_cache.GetOrCreate( $"player:idle:{facing.ToKey()}", () =>
			TryLoadAnimationFrame( SuppliedSpriteManifest.PlayerAnimationDir( facing ), "idle" )
			?? PlayerSprite );

	private static Texture PlayerWalkTexture( PlayerFacing facing, int frame ) =>
		_cache.GetOrCreate( $"player:walk:{facing.ToKey()}:{frame}", () =>
			TryLoadAnimationFrame( SuppliedSpriteManifest.PlayerAnimationDir( facing ), "walk", frame )
			?? PlayerIdleTexture( facing ) );

	public static Texture TileGrass => LoadTile( SuppliedTileManifest.GrassPath, "grass" );
	public static Texture TileGrassAlt => TileGrass;
	public static Texture TileDirt => LoadTile( SuppliedTileManifest.DirtPath, "dirt" );
	public static Texture TilePath => LoadTile( SuppliedTileManifest.PathPath, "path" );
	public static Texture TilePathStone => TilePath;
	public static Texture TilePathWood => TilePath;
	public static Texture TileWilderness => LoadTile( SuppliedTileManifest.WildernessPath, "wilderness" );
	public static Texture TileCliff => TileDirt;
	public static Texture TileWater => LoadTile( SuppliedTileManifest.WaterPath, "water" );
	public static Texture TileSnow => LoadTile( SuppliedTileManifest.SnowPath, "snow" );
	public static Texture TileSand => LoadTile( SuppliedTileManifest.SandPath, "sand" );
	public static Texture TileMud => LoadTile( SuppliedTileManifest.MudPath, "mud" );
	public static Texture TileBeach => LoadTile( SuppliedTileManifest.BeachPath, "beach" );
	public static Texture TileRainforest => LoadTile( SuppliedTileManifest.RainforestPath, "rainforest" );
	public static Texture TileRock => LoadTile( SuppliedTileManifest.RockPath, "rock" );
	public static Texture TileForest => LoadTile( SuppliedTileManifest.ForestPath, "forest" );
	public static Texture TileHabitatGround => TileDirt;

	public static Texture PathTile( string propKey ) => TilePath;

	public static Texture Prop( string name )
	{
		InvalidatePropCacheIfFileAppeared( name );
		return _cache.GetOrCreate( $"prop:{name}", () => LoadProp( name ) ?? Texture.White );
	}

	public static bool TryProp( string name, out Texture texture )
	{
		InvalidatePropCacheIfFileAppeared( name );
		texture = LoadProp( name );
		return texture is not null && texture.IsValid();
	}

	private static void InvalidatePropCacheIfFileAppeared( string name )
	{
		if ( !SuppliedSpriteManifest.TryGetSuppliedPropPath( name, out var path ) )
			return;

		if ( !FileSystem.Mounted.FileExists( path ) )
			return;

		_missing.Remove( name );
		_cache.Remove( $"prop:{name}" );
	}

	public static bool IsSuppliedProp( string name ) =>
		SuppliedPropNames.Contains( name );

	/// <summary>Decor props that may only appear on player-placed buildings — never wilderness scatter.</summary>
	public static bool IsPlayerPlacedPropOnly( string name ) =>
		name.Equals( "pond", StringComparison.OrdinalIgnoreCase );

	public static bool IsFenceProp( string name ) =>
		name is "fence_h" or "fence_v" or "fence_post"
			or "fence_top_left" or "fence_top_right"
			or "fence_bottom_left" or "fence_bottom_right";

	public static bool IsFenceCornerProp( string name ) =>
		name is "fence_top_left" or "fence_top_right" or "fence_bottom_left" or "fence_bottom_right";

	/// <summary>1024² supplied corner posts — scaled like edge rails so they meet fence_h/fence_v.</summary>
	public static bool TryApplySuppliedFenceCornerScale(
		SpriteRenderer renderer,
		string name,
		float worldSize,
		Texture texture,
		out float feetOffsetZ )
	{
		feetOffsetZ = 0f;
		if ( !renderer.IsValid() || !IsFenceCornerProp( name ) )
			return false;

		renderer.Size = new Vector2( worldSize, worldSize );
		renderer.GameObject.LocalScale = Vector3.One;
		return true;
	}

	public static float PropSourcePixels( string name ) =>
		IsFenceProp( name ) || IsSuppliedWorldProp( name )
			? SuppliedSpriteSourcePixels
			: SpriteSourcePixels;

	public static bool IsGeneratedBuildingProp( string name ) => name is
		"restroom" or "shop" or "cafe" or "cafeteria" or "restaurant" or "research"
		or "building" or "playground" or "gazebo" or "entrance";

	public static bool IsSuppliedBuildingProp( string name ) =>
		IsSuppliedProp( name ) && !IsFenceProp( name ) && !IsSuppliedWorldProp( name );

	/// <summary>Sprite quad matches placement footprint exactly (same cells as the build ghost).</summary>
	public static float BuildingDrawSize( Vector2 footprint ) =>
		MathF.Max( BuildingDrawDimensions( footprint ).x, BuildingDrawDimensions( footprint ).y );

	public static Vector2 BuildingDrawDimensions( Vector2 footprint ) => footprint;

	private static bool IsSuppliedWorldProp( string name ) =>
		name is "oak_tree" or "tree" or "aspen_tree" or "cherry_tree" or "pine_tree" or "pine" or "palm"
			or "dead_tree" or "bush" or "cactus" or "rock";

	/// <summary>Opaque bounds inside the 1024² supplied prop canvases.</summary>
	private readonly record struct SuppliedPropLayout(
		float ContentSpanWidthPx,
		float ContentSpanHeightPx,
		float FeetPadBottomPx )
	{
		public const float CanvasPx = 1024f;

		public float CanvasToContentScale => CanvasPx / ContentSpanHeightPx;

		/// <summary>Local Z offset so opaque feet sit on the placement point (center pivot).</summary>
		public float FeetAlignOffset( float quadWorldSize ) =>
			quadWorldSize * (0.5f - FeetPadBottomPx / CanvasPx);
	}

	private static bool TrySuppliedPropLayout( string name, out SuppliedPropLayout layout )
	{
		layout = name switch
		{
			"oak_tree" or "tree" => new SuppliedPropLayout( 676f, 671f, 192f ),
			"aspen_tree" => new SuppliedPropLayout( 552f, 797f, 145f ),
			"pine_tree" or "pine" => new SuppliedPropLayout( 571f, 786f, 170f ),
			"bush" => new SuppliedPropLayout( 624f, 390f, 338f ),
			"rock" => new SuppliedPropLayout( 406f, 400f, 331f ),
			_ => default,
		};

		return layout.ContentSpanHeightPx > 0f;
	}

	/// <summary>
	/// Scales supplied 1024² props by their opaque content span and returns a Z offset
	/// so the visible base aligns with the placement feet.
	/// </summary>
	public static bool TryApplySuppliedPropScale(
		SpriteRenderer renderer,
		string name,
		float contentWorldHeight,
		Texture texture,
		out float feetOffsetZ )
	{
		feetOffsetZ = 0f;
		if ( !renderer.IsValid() || !TrySuppliedPropLayout( name, out var layout ) )
			return false;

		var quadSize = contentWorldHeight * layout.CanvasToContentScale;
		renderer.Size = new Vector2( quadSize, quadSize );
		renderer.GameObject.LocalScale = Vector3.One;
		feetOffsetZ = layout.FeetAlignOffset( quadSize );

		if ( !texture.IsValid() && !renderer.Texture.IsValid() )
			Log.Warning( $"[Fauna2 RenderDiag] Supplied prop scale on '{name}' with invalid texture; quad={quadSize:0.##}, feetOffset={feetOffsetZ:0.##}." );

		return true;
	}

	/// <summary>Entrance art fills the 4×6 placement footprint like other supplied buildings.</summary>
	public static bool TryApplySuppliedEntranceScale(
		SpriteRenderer renderer,
		string name,
		Vector2 footprint,
		Texture texture,
		out Vector3 localOffset ) =>
		TryApplySuppliedBuildingScale( renderer, name, footprint, texture, out localOffset );

	/// <summary>Opaque bounds inside 1024² supplied building canvases (min/max pixel coords).</summary>
	private readonly record struct SuppliedBuildingLayout(
		float ContentMinX,
		float ContentMinY,
		float ContentMaxX,
		float ContentMaxY )
	{
		public const float CanvasPx = 1024f;

		public float ContentWidth => ContentMaxX - ContentMinX + 1f;
		public float ContentHeight => ContentMaxY - ContentMinY + 1f;
		public float ContentCenterX => (ContentMinX + ContentMaxX) * 0.5f;
		public float ContentCenterY => (ContentMinY + ContentMaxY) * 0.5f;
	}

	private static bool TrySuppliedBuildingLayout( string name, out SuppliedBuildingLayout layout )
	{
		layout = name switch
		{
			"shop" or "kiosk" => new SuppliedBuildingLayout( 246f, 160f, 777f, 705f ),
			"cafe" => new SuppliedBuildingLayout( 187f, 141f, 860f, 770f ),
			"restaurant" => new SuppliedBuildingLayout( 170f, 211f, 855f, 762f ),
			"cafeteria" => new SuppliedBuildingLayout( 163f, 200f, 860f, 735f ),
			"restroom" => new SuppliedBuildingLayout( 167f, 196f, 856f, 749f ),
			"playground" => new SuppliedBuildingLayout( 180f, 174f, 844f, 748f ),
			"entrance" => new SuppliedBuildingLayout( 229f, 256f, 792f, 643f ),
			"food_stand" => new SuppliedBuildingLayout( 209f, 101f, 814f, 689f ),
			_ => default,
		};

		return layout.ContentWidth > 0f;
	}

	/// <summary>World Z of the roof line for supplied building sprites (center pivot at feet).</summary>
	public static float BuildingRoofZ( string name, Vector2 footprint )
	{
		if ( !TrySuppliedBuildingLayout( name, out var layout ) )
			return footprint.x * 0.85f;

		var quadH = footprint.x * (SuppliedBuildingLayout.CanvasPx / layout.ContentHeight);
		return quadH * 0.9f;
	}

	/// <summary>Local anchor above opaque art center — matches supplied building sprite layout.</summary>
	public static bool TryGetBuildingTipAnchorLocal( string name, Vector2 footprint, out Vector3 localAnchor )
	{
		localAnchor = default;
		if ( !TrySuppliedBuildingLayout( name, out var layout ) )
			return false;

		const float canvas = SuppliedBuildingLayout.CanvasPx;
		var canvasCenter = canvas * 0.5f;
		var quadW = footprint.y * (canvas / layout.ContentWidth);
		var quadH = footprint.x * (canvas / layout.ContentHeight);

		var contentMinLocalX = (layout.ContentMinY - canvasCenter) / canvas * quadH;
		var contentMinLocalY = (layout.ContentMinX - canvasCenter) / canvas * quadW;
		var localOffset = new Vector3(
			-footprint.x * 0.5f - contentMinLocalX,
			-footprint.y * 0.5f - contentMinLocalY,
			0f );

		var contentCenterLocalX = (layout.ContentCenterY - canvasCenter) / canvas * quadH;
		var contentCenterLocalY = (layout.ContentCenterX - canvasCenter) / canvas * quadW;
		localAnchor = localOffset + new Vector3(
			contentCenterLocalX,
			contentCenterLocalY,
			BuildingRoofZ( name, footprint ) + 1f );

		return true;
	}

	/// <summary>
	/// Scales supplied building PNGs so opaque art exactly covers the snapped footprint cells.
	/// Footprint X = depth (north–south), Y = width (east–west); texture width → world Y, height → world X.
	/// Anchors opaque min corner to the footprint south-west so every building sits on the same subgrid.
	/// </summary>
	public static bool TryApplySuppliedBuildingScale(
		SpriteRenderer renderer,
		string name,
		Vector2 footprint,
		Texture texture,
		out Vector3 localOffset )
	{
		localOffset = Vector3.Zero;
		if ( !renderer.IsValid() || !TrySuppliedBuildingLayout( name, out var layout ) )
			return false;

		const float canvas = SuppliedBuildingLayout.CanvasPx;
		var quadW = footprint.y * (canvas / layout.ContentWidth);
		var quadH = footprint.x * (canvas / layout.ContentHeight );
		renderer.Size = new Vector2( quadW, quadH );
		renderer.GameObject.LocalScale = Vector3.One;

		var canvasCenter = canvas * 0.5f;
		var contentMinLocalX = (layout.ContentMinY - canvasCenter) / canvas * quadH;
		var contentMinLocalY = (layout.ContentMinX - canvasCenter) / canvas * quadW;
		localOffset = new Vector3(
			-footprint.x * 0.5f - contentMinLocalX,
			-footprint.y * 0.5f - contentMinLocalY,
			0f );

		if ( !texture.IsValid() && !renderer.Texture.IsValid() )
			Log.Warning( $"[Fauna2 RenderDiag] Supplied building scale on '{name}' with invalid texture; quad=({quadW:0.##},{quadH:0.##}), offset={localOffset}." );

		return true;
	}

	public static bool IsSuppliedCritter( string name ) =>
		SuppliedCritterNames.Contains( name )
		|| FileSystem.Mounted.FileExists( SuppliedSpriteManifest.AnimalModelsRoot + $"{name}.png" );

	public static Texture GuestSprite =>
		_cache.GetOrCreate( "guest:static", () =>
			LoadSuppliedSprite( SuppliedSpriteManifest.GuestSpritePath, "guest_sprite" ) );

	public static Sprite GuestSpriteResource( int variantIndex = 0 )
	{
		var animDir = SuppliedSpriteManifest.GuestAnimationDir( variantIndex );
		return _spriteCache.GetOrCreate( $"guest:sprite:{variantIndex}:{animDir}", () =>
		{
			var sprite = BuildWalkSprite(
				GuestIdleTexture( variantIndex ),
				animDir,
				"guest",
				$"guest_{variantIndex}" );

			if ( _loggedLoads.Add( $"guest:sprite-built:{variantIndex}" ) )
			{
				var spritePath = SuppliedSpriteManifest.GuestVariantSpritePaths[
					Math.Clamp( variantIndex, 0, SuppliedSpriteManifest.GuestVariantSpritePaths.Length - 1 )];
				Log.Info( $"[Fauna2 Art] Guest variant {variantIndex} sprite uses '{spritePath}', anim dir '{animDir}'." );
			}

			return sprite;
		} );
	}

	public static Sprite RandomGuestSpriteResource() =>
		GuestSpriteResource( Game.Random.Int( 0, SuppliedSpriteManifest.GuestVariantSpritePaths.Length - 1 ) );

	private static Texture GuestTexture( int variantIndex )
	{
		var paths = SuppliedSpriteManifest.GuestVariantSpritePaths;
		var index = Math.Clamp( variantIndex, 0, paths.Length - 1 );
		var path = paths[index];
		var key = $"guest_variant_{index}";
		return _cache.GetOrCreate( $"guest:static:{index}", () => LoadSuppliedSprite( path, key ) );
	}

	private static Texture GuestIdleTexture( int variantIndex = 0 ) =>
		_cache.GetOrCreate( $"guest:idle:{variantIndex}", () =>
			TryLoadAnimationFrame( SuppliedSpriteManifest.GuestAnimationDir( variantIndex ), "idle" )
			?? GuestTexture( variantIndex ) );

	public static Texture FxSparkle() => Load( "textures/fx/sparkle.png" );

	public static Texture FxBadge( string key ) =>
		_cache.GetOrCreate( $"badge:{key}", () => Load( $"textures/fx/badge_{key}.png" ) );

	public static Texture FxIcon( string key ) =>
		_cache.GetOrCreate( $"icon:{key}", () => Load( $"textures/fx/icon_{key}.png" ) );

	public static string IconPath( string name ) => $"ui/icons/{name}.png";

	public static Sprite MakeSprite( Texture texture )
	{
		if ( texture is null || !texture.IsValid() )
			return new Sprite();

		var key = string.IsNullOrEmpty( texture.ResourcePath )
			? $"tex:{texture.GetHashCode()}"
			: $"tex:{texture.ResourcePath}";

		return _spriteCache.GetOrCreate( key, () => new Sprite
		{
			Animations =
			[
				new Sprite.Animation
				{
					Name = "Default",
					Frames = [new Sprite.Frame { Texture = texture }],
				},
			],
		} );
	}

	public static Sprite CritterSprite( string key = "deer" )
	{
		var stem = NormalizeCritterStem( key );
		return _spriteCache.GetOrCreate( $"critter:sprite:{stem}", () =>
		{
			var idle = Critter( stem );
			return BuildWalkSprite(
				idle,
				SuppliedSpriteManifest.AnimalAnimationDir( stem ),
				"critter",
				stem );
		} );
	}

	public static Sprite MakeWalkSprite( Texture idle, Texture[] walkFrames, float walkFrameRate = WalkFrameRate ) =>
		new()
		{
			Animations =
			[
				new Sprite.Animation
				{
					Name = IdleAnimationName,
					Frames = [new Sprite.Frame { Texture = idle }],
					FrameRate = 1f,
				},
				new Sprite.Animation
				{
					Name = WalkAnimationName,
					Frames = walkFrames.Select( t => new Sprite.Frame { Texture = t } ).ToList(),
					FrameRate = walkFrameRate,
					LoopMode = Sprite.LoopMode.Loop,
				},
			],
		};

	private static Sprite BuildWalkSprite( Texture fallbackIdle, string animationDir, string category, string key )
	{
		var idle = TryLoadAnimationFrame( animationDir, "idle" ) ?? fallbackIdle;
		var walkFrames = TryLoadWalkFrames( animationDir, category, key );
		if ( walkFrames is null )
			return MakeSprite( idle );

		return MakeWalkSprite( idle, walkFrames );
	}

	private static Texture[] TryLoadWalkFrames( string animationDir, string category, string key )
	{
		if ( !HasWalkFrames( animationDir ) )
			return null;

		var frames = new Texture[WalkFrameCount];
		for ( var i = 0; i < WalkFrameCount; i++ )
		{
			var frame = TryLoadAnimationFrame( animationDir, "walk", i );
			if ( frame is null )
				return null;

			frames[i] = frame;
			if ( _loggedLoads.Add( $"{category}:walk:{key}:{i}" ) )
				Log.Info( $"[Fauna2 Art] Walk frame '{key}' #{i} uses '{WalkFramePath( animationDir, i )}'." );
		}

		return frames;
	}

	private static bool HasWalkFrames( string animationDir ) =>
		FileSystem.Mounted.FileExists( WalkFramePath( animationDir, 0 ) );

	private static string WalkFramePath( string animationDir, int frame ) =>
		$"{animationDir}walk_{frame}.png";

	private static Texture TryLoadAnimationFrame( string animationDir, string prefix, int? frame = null )
	{
		var path = frame.HasValue
			? WalkFramePath( animationDir, frame.Value )
			: $"{animationDir}{prefix}.png";

		if ( !FileSystem.Mounted.FileExists( path ) )
			return null;

		return LoadSuppliedSprite( path, $"{animationDir}:{prefix}{( frame.HasValue ? frame.Value.ToString() : "" )}" );
	}

	private static string NormalizeCritterStem( string key ) => key switch
	{
		"blackbear" => "black_bear",
		"mountainlion" => "cougar",
		"polarbear" => "polar_bear",
		"sealion" => "sea_lion",
		_ => key,
	};

	public static void ApplyWorldScale( SpriteRenderer renderer, float worldSize, float sourcePixels = SpriteSourcePixels, Texture texture = null, float? contentAspect = null )
	{
		ApplyWorldScale( renderer, new Vector2( worldSize, worldSize ), sourcePixels, texture, contentAspect );
	}

	public static void ApplyWorldScale( SpriteRenderer renderer, Vector2 worldSize, float sourcePixels = SpriteSourcePixels, Texture texture = null, float? contentAspect = null )
	{
		if ( !renderer.IsValid() ) return;

		var tex = texture;
		if ( !tex.IsValid() )
			tex = renderer.Texture;

		var aspect = contentAspect
			?? (tex.IsValid() && tex.Height > 0 ? tex.Width / (float)tex.Height : 1f);

		if ( contentAspect.HasValue || MathF.Abs( aspect - 1f ) < 0.01f )
			renderer.Size = worldSize;
		else
			renderer.Size = new Vector2( worldSize.x, worldSize.x / aspect );

		renderer.GameObject.LocalScale = Vector3.One;

		if ( !tex.IsValid() )
			Log.Warning( $"[Fauna2 RenderDiag] ApplyWorldScale on '{renderer.GameObject.Name}' with invalid texture; size={renderer.Size}." );
	}

	/// <summary>Edge rails are drawn larger than one cell so segments overlap and connect visually.</summary>
	public const float FenceEdgeOverscale = 2f;

	public static bool IsFenceEdgeRail( string name ) => name is "fence_h" or "fence_v";

	public static float FenceDrawSize( string fenceKey )
	{
		var baseDraw = GameConstants.TileSize * TileCoverage;
		return IsFenceEdgeRail( fenceKey ) ? baseDraw * FenceEdgeOverscale : baseDraw;
	}

	/// <summary>Opaque rail spans inside the 1024² fence PNGs (centered on canvas).</summary>
	private readonly record struct FenceRailLayout( float ContentWidthPx, float ContentHeightPx )
	{
		public const float CanvasPx = 1024f;

		public float ContentMinX => (CanvasPx - ContentWidthPx) * 0.5f;
		public float ContentMinY => (CanvasPx - ContentHeightPx) * 0.5f;
		public float ContentMaxX => ContentMinX + ContentWidthPx - 1f;
		public float ContentMaxY => ContentMinY + ContentHeightPx - 1f;
		public float ContentCenterX => (ContentMinX + ContentMaxX) * 0.5f;
		public float ContentCenterY => (ContentMinY + ContentMaxY) * 0.5f;
	}

	private static bool TryFenceRailLayout( string name, out FenceRailLayout layout )
	{
		layout = name switch
		{
			"fence_h" => new FenceRailLayout( 595f, 126f ),
			"fence_v" => new FenceRailLayout( 115f, 475f ),
			_ => default,
		};

		return layout.ContentWidthPx > 0f;
	}

	/// <summary>
	/// Scale fence rails by their opaque art span (not the full 1024² canvas) and align
	/// the rail junction to the placement point so stacked h+v corners meet cleanly.
	/// </summary>
	public static bool TryApplyFenceRailScale(
		SpriteRenderer renderer,
		string name,
		float railSpanWorld,
		out Vector2 localOffset )
	{
		localOffset = Vector2.Zero;
		if ( !renderer.IsValid() || !TryFenceRailLayout( name, out var layout ) )
			return false;

		Vector2 worldSize;
		if ( name == "fence_h" )
		{
			worldSize = new Vector2(
				railSpanWorld,
				railSpanWorld * (layout.ContentHeightPx / layout.ContentWidthPx) );
		}
		else
		{
			worldSize = new Vector2(
				railSpanWorld * (layout.ContentWidthPx / layout.ContentHeightPx),
				railSpanWorld );
		}

		renderer.Size = worldSize;
		renderer.GameObject.LocalScale = Vector3.One;

		var canvasCenter = FenceRailLayout.CanvasPx * 0.5f;
		localOffset = new Vector2(
			(canvasCenter - layout.ContentCenterX) / FenceRailLayout.CanvasPx * worldSize.x,
			(canvasCenter - layout.ContentCenterY) / FenceRailLayout.CanvasPx * worldSize.y );

		return true;
	}

	public static float FenceContentAspect( string fenceKey ) => fenceKey switch
	{
		"fence_h" => 595f / 126f,
		"fence_v" => 115f / 475f,
		"fence_top_left" or "fence_top_right" or "fence_bottom_left" or "fence_bottom_right" or "fence_post" => 1f,
		_ => 1f,
	};

	public static Texture Load( string path )
	{
		try
		{
			var texture = Texture.Load( path );
			if ( texture.IsValid() )
			{
				if ( _loggedLoads.Add( path ) )
					Log.Info( $"[Fauna2 Art] Loaded texture '{path}'." );
				return texture;
			}
		}
		catch ( Exception e )
		{
			Log.Warning( $"[Fauna] Missing pixel texture '{path}': {e.Message}" );
		}

		return Texture.White;
	}

	private static Texture LoadTile( string path, string logicalName )
	{
		LogTileManifestOnce();

		if ( FileSystem.Mounted.FileExists( path ) )
		{
			var texture = Load( path );
			if ( _loggedLoads.Add( $"tile:{logicalName}" ) )
				Log.Info( $"[Fauna2 Tiles] Runtime tile '{logicalName}' uses '{path}'." );
			return texture;
		}

		var fallback = logicalName switch
		{
			"grass" => PlaceholderTiles.Grass,
			"dirt" => PlaceholderTiles.Dirt,
			"path" => PlaceholderTiles.Path,
			"wilderness" => PlaceholderTiles.Wilderness,
			"water" => PlaceholderTiles.Water,
			"snow" => PlaceholderTiles.Snow,
			"sand" => PlaceholderTiles.Sand,
			"mud" => PlaceholderTiles.Mud,
			"beach" => PlaceholderTiles.Beach,
			"rainforest" => PlaceholderTiles.Rainforest,
			"rock" => PlaceholderTiles.Rock,
			"forest" => PlaceholderTiles.Forest,
			_ => Texture.White,
		};

		if ( _loggedLoads.Add( $"tile-fallback:{logicalName}" ) )
			Log.Warning( $"[Fauna2 Tiles] Missing '{path}' — using placeholder for '{logicalName}'." );
		return fallback;
	}

	private static Texture LoadSuppliedSprite( string path, string logicalName )
	{
		LogSpriteManifestOnce();

		var texture = Load( path );
		if ( _loggedLoads.Add( $"supplied:{logicalName}" ) )
			Log.Info( $"[Fauna2 Art] Runtime supplied sprite '{logicalName}' uses '{path}'." );
		return texture;
	}

	private static string PropPath( string name ) => $"textures/props/{name}.png";

	private static Texture LoadProp( string name )
	{
		if ( SuppliedSpriteManifest.TryGetSuppliedPropPath( name, out var suppliedPath )
			&& FileSystem.Mounted.FileExists( suppliedPath ) )
		{
			_missing.Remove( name );
			return LoadSuppliedSprite( suppliedPath, name );
		}

		var generatedPath = PropPath( name );
		if ( FileSystem.Mounted.FileExists( generatedPath ) )
		{
			_missing.Remove( name );
			return Load( generatedPath );
		}

		_missing.Add( name );
		Log.Warning( $"[Fauna2 Art] Missing prop texture for '{name}' (expected supplied asset under models/)." );
		return null;
	}

	private static readonly HashSet<string> SuppliedPropNames = new( StringComparer.OrdinalIgnoreCase )
	{
		"oak_tree", "tree", "aspen_tree", "pine_tree", "pine", "bush", "cactus", "rock", "pond",
		"cafe", "cafeteria", "restaurant", "restroom", "food_stand", "shop", "kiosk",
		"playground", "entrance", "fence_h", "fence_v", "fence_post",
		"fence_top_left", "fence_top_right", "fence_bottom_left", "fence_bottom_right",
	};

	private static readonly HashSet<string> SuppliedCritterNames = new( StringComparer.OrdinalIgnoreCase )
	{
		"rabbit", "squirrel", "fox", "deer", "wolf", "black_bear", "blackbear", "moose", "alligator",
		"snow_leopard", "polar_bear", "sea_lion",
	};

	private static void LogSpriteManifestOnce()
	{
		if ( _loggedSpriteManifest ) return;
		_loggedSpriteManifest = true;
		Log.Info( $"[Fauna2 Art] {SuppliedSpriteManifest.Summary}" );
	}

	private static void LogTileManifestOnce()
	{
		if ( _loggedTileManifest ) return;
		_loggedTileManifest = true;
		Log.Info( $"[Fauna2 Tiles] {SuppliedTileManifest.Summary}" );
	}
}

public enum PlayerFacing
{
	Down,
	Left,
	Right,
	Up,
}

public static class PlayerFacingExtensions
{
	public static string ToKey( this PlayerFacing facing ) => facing switch
	{
		PlayerFacing.Down => "down",
		PlayerFacing.Left => "left",
		PlayerFacing.Right => "right",
		PlayerFacing.Up => "up",
		_ => "down",
	};

	public static PlayerFacing FromMove( Vector3 move )
	{
		if ( move.Length < 0.01f )
			return PlayerFacing.Down;

		if ( MathF.Abs( move.x ) > MathF.Abs( move.y ) )
			return move.x >= 0f ? PlayerFacing.Right : PlayerFacing.Left;

		return move.y >= 0f ? PlayerFacing.Up : PlayerFacing.Down;
	}
}

internal static class TextureCacheExtensions
{
	public static TValue GetOrCreate<TKey, TValue>( this Dictionary<TKey, TValue> dict, TKey key, Func<TValue> factory )
	{
		if ( dict.TryGetValue( key, out var existing ) )
			return existing;

		var value = factory();
		dict[key] = value;
		return value;
	}
}
