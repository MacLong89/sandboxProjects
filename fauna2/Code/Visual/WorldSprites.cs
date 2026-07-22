namespace Fauna2;

/// <summary>Spawns billboard pixel sprites in the world.</summary>
public static class WorldSprites
{
	/// <summary>
	/// Depth-sort bands (back → front). Within each band, higher world Y (south / bottom of screen)
	/// draws on top via <see cref="YSortSpread"/>.
	/// </summary>
	public const float YSortSpread = 0.75f;

	public const float WildernessLayer = -200f;
	public const float GrassLayer = -199f;
	/// <summary>Habitat interior floor — above owned plot grass, below paths.</summary>
	public const float HabitatGroundLayer = GrassLayer + 2f;
	public const float PathLayer = -50f;
	public const float BuildingLayer = 0f;
	public const float DecorationLayer = 5f;
	public const float EnrichmentLayer = 10f;
	public const float HabitatLayer = 12f;
	/// <summary>North/west/east fence segments — behind decorations, enrichment, and animals.</summary>
	public const float FenceBackLayer = 4f;
	/// <summary>South fence row — draws over animals (Stardew-style enclosure depth).</summary>
	public const float FenceFrontLayer = 21f;
	public const float AnimalLayer = 20f;
	public const float GuestLayer = 22f;
	public const float PlayerLayer = 24f;
	public const float OverlayLayer = 28f;

	public static float SortLayerFor( PlaceableDefinition def )
	{
		if ( def is null ) return BuildingLayer;
		if ( def.IsPathTile ) return PathLayer;
		if ( def.IsEntrance ) return BuildingLayer;
		return SortLayerFor( def.Category );
	}

	public static float SortLayerFor( BuildCategory category ) => category switch
	{
		BuildCategory.Utility => BuildingLayer,
		BuildCategory.Decorations => DecorationLayer,
		BuildCategory.Nature => EnrichmentLayer,
		BuildCategory.Habitats => HabitatLayer,
		BuildCategory.Paths => PathLayer,
		_ => BuildingLayer,
	};

	private static int _loggedDetachedSprites;

	internal static void ResetDiagnostics() => _loggedDetachedSprites = 0;

	public static SpriteRenderer Spawn(
		GameObject parent,
		Texture texture,
		float worldSize,
		Vector3 localPosition = default,
		string name = "Sprite",
		bool depthSort = true,
		bool dynamicDepthSort = false,
		float layer = BuildingLayer,
		float sourcePixels = PixelArt.SpriteSourcePixels,
		float yawDegrees = 0f,
		float? contentAspect = null,
		bool pathFloorSort = false,
		Vector2? drawSize = null )
	{
		return Spawn(
			parent,
			PixelArt.MakeSprite( texture ),
			worldSize,
			localPosition,
			name,
			depthSort,
			dynamicDepthSort,
			layer,
			sourcePixels,
			yawDegrees,
			contentAspect,
			pathFloorSort,
			drawSize,
			movementRoot: null,
			walkAnimator: false );
	}

	public static SpriteRenderer Spawn(
		GameObject parent,
		Sprite sprite,
		float worldSize,
		Vector3 localPosition = default,
		string name = "Sprite",
		bool depthSort = true,
		bool dynamicDepthSort = false,
		float layer = BuildingLayer,
		float sourcePixels = PixelArt.SpriteSourcePixels,
		float yawDegrees = 0f,
		float? contentAspect = null,
		bool pathFloorSort = false,
		Vector2? drawSize = null,
		GameObject movementRoot = null,
		bool walkAnimator = false,
		bool flipFacingHorizontal = false )
	{
		var texture = sprite?.Animations?.FirstOrDefault()?.Frames?.FirstOrDefault()?.Texture;
		var go = new GameObject( parent, true, name );
		if ( yawDegrees != 0f )
			go.LocalRotation = Rotation.FromYaw( yawDegrees );
		var renderer = go.AddComponent<SpriteRenderer>();
		ConfigureRenderer( renderer, depthSort );
		AssignSprite( renderer, sprite ?? PixelArt.MakeSprite( texture ) );

		var feetOffsetZ = 0f;
		if ( PixelArt.TryApplySuppliedPropScale( renderer, name, worldSize, texture, out feetOffsetZ ) )
			localPosition = localPosition.WithZ( localPosition.z + feetOffsetZ );
		else if ( PixelArt.TryApplySuppliedFenceCornerScale( renderer, name, worldSize, texture, out feetOffsetZ ) )
			localPosition = localPosition.WithZ( localPosition.z + feetOffsetZ );
		else if ( drawSize.HasValue && PixelArt.TryApplySuppliedEntranceScale( renderer, name, drawSize.Value, texture, out var entranceOffset ) )
			localPosition += entranceOffset;
		else if ( drawSize.HasValue && PixelArt.TryApplySuppliedBuildingScale( renderer, name, drawSize.Value, texture, out var buildingOffset ) )
			localPosition += buildingOffset;
		else if ( PixelArt.TryApplyFenceRailScale( renderer, name, worldSize, out var fenceOffset ) )
			localPosition += new Vector3( fenceOffset.x, fenceOffset.y, 0f );
		else if ( drawSize.HasValue )
			PixelArt.ApplyWorldScale( renderer, drawSize.Value, sourcePixels, texture, contentAspect: 1f );
		else
			PixelArt.ApplyWorldScale( renderer, worldSize, sourcePixels, texture, contentAspect );

		go.LocalPosition = localPosition;
		Fauna2RenderDiagnostics.LogSpriteSpawn( renderer, name, texture, parent, worldSize, depthSort, layer );

		if ( depthSort )
		{
			if ( pathFloorSort )
				go.AddComponent<PathFloorDepthSorter>();
			else
			{
				var sorter = go.AddComponent<PixelDepthSorter>();
				sorter.BaseLayer = layer;
				sorter.Dynamic = dynamicDepthSort;
				sorter.SortOrigin = feetOffsetZ > 0.001f && parent.IsValid()
					? parent
					: layer >= AnimalLayer && parent.IsValid() ? parent : go;
			}
		}

		if ( _loggedDetachedSprites < 12 )
		{
			_loggedDetachedSprites++;
			Log.Info( $"[Fauna2 Render] Sprite sample {_loggedDetachedSprites}: name={name}, parent={parent?.Name ?? "null"}, worldSize={worldSize:0.##}, sourcePixels={sourcePixels:0.##}, localPos={localPosition}, depthSort={depthSort}, layer={layer:0.##}." );
		}

		if ( walkAnimator || sprite?.Animations?.Any( a => a.Name == PixelArt.WalkAnimationName ) == true )
		{
			var animator = go.AddComponent<SpriteWalkAnimator>();
			animator.MovementRoot = movementRoot.IsValid() ? movementRoot : parent;
			animator.FlipFacingHorizontal = flipFacingHorizontal;
		}

		return renderer;
	}

	/// <summary>Flat ground tile — opaque; opt into depth sort for floor overlays.</summary>
	public static SpriteRenderer SpawnGroundTile(
		GameObject parent,
		Texture texture,
		float worldSize,
		Vector3 localPosition,
		string name = "GroundTile",
		float layer = WildernessLayer,
		bool depthSort = true )
	{
		var renderer = Spawn(
			parent,
			texture,
			worldSize,
			localPosition,
			name,
			depthSort: depthSort,
			layer: layer,
			sourcePixels: PixelArt.TileSourcePixels );
		ConfigureFloorSprite( renderer, depthSort );
		return renderer;
	}

	/// <summary>Horizontal floor quads in the XY plane (Z-up). Yaw 180° keeps supplied tile art right-side up.</summary>
	public static readonly Rotation FloorTileRotation = Rotation.From( -90f, 180f, 0f );

	/// <summary>
	/// Camera sits on −X looking toward +X, so screen-up is world +X (not ±Y).
	/// <see cref="FloorTileRotation"/> biases the flat quad toward the camera (−X); nudge +X
	/// so habitat floors line up with fence feet.
	/// </summary>
	public static Vector3 FloorTileAlignFix => new Vector3( GameConstants.TileSize, 0f, 0f );

	/// <summary>SpriteRenderer on ground tile roots — child object, same layout as prop sprites.</summary>
	public static SpriteRenderer GetGroundSpriteRenderer( GameObject root )
	{
		if ( !root.IsValid() )
			return default;

		foreach ( var renderer in root.GetComponentsInChildren<SpriteRenderer>( true ) )
		{
			if ( renderer.IsValid() )
				return renderer;
		}

		return default;
	}

	/// <summary>
	/// Flat ground tile root (owned grass / wilderness). Returns the tile root (not the child sprite).
	/// HABITAT GROUND FIX: must match habitat floors — flat XY + unsorted. Sorted Always-billboards
	/// depth-fight the flat habitat pad under the pitched camera and cut it into horizontal rows.
	/// </summary>
	public static GameObject SpawnGroundTileWorld(
		Vector3 worldPosition,
		Texture texture,
		float worldSize,
		string name = "GroundTile",
		float layer = WildernessLayer ) =>
		SpawnGroundTileWorld( worldPosition, texture, new Vector2( worldSize, worldSize ), name, layer );

	public static GameObject SpawnGroundTileWorld(
		Vector3 worldPosition,
		Texture texture,
		Vector2 drawSize,
		string name = "GroundTile",
		float layer = WildernessLayer )
	{
		var feet = worldPosition.WithZ( 0f );
		var root = new GameObject( true, name );
		root.WorldPosition = feet;
		root.Enabled = false;

		var spriteGo = new GameObject( root, true, "Sprite" );
		spriteGo.LocalRotation = FloorTileRotation;
		var renderer = spriteGo.AddComponent<SpriteRenderer>();
		AssignSprite( renderer, texture );
		PixelArt.ApplyWorldScale( renderer, drawSize, PixelArt.TileSourcePixels, texture );
		ConfigureWorldFloorSprite( renderer );
		renderer.Opaque = true;
		renderer.AlphaCutoff = 0.08f;

		// Z band only — IsSorted stays false. Bias breaks coplanar fights between overlapping tiles.
		var sorter = spriteGo.AddComponent<PixelDepthSorter>();
		sorter.BaseLayer = layer + GroundGrid.FloorDepthBias( feet.x, feet.y );
		sorter.SortOrigin = root;

		return root;
	}

	/// <summary>
	/// Habitat interior floor tile.
	/// HABITAT GROUND FIX: flat XY quad (not Always-billboard). Billboard habitat
	/// tiles read as thin horizontal strips under the pitched camera and left
	/// OwnedTile grass visible between bands.
	///
	/// LAYER / PRIORITY (2026-07): Keep the pad in the HabitatGroundLayer depth band
	/// (~GrassLayer+2), NOT at a near-camera Z like 1.5. A flat opaque plane at Z≈1.5
	/// sits between grass (~−199) and enrichment (~10) and depth-clips trees/nature
	/// billboards in parts of the pen under the pitched ortho camera. Floor must stay
	/// behind EnrichmentLayer / FenceBackLayer / animals.
	/// </summary>
	public static GameObject SpawnHabitatGroundTile(
		GameObject parent,
		Texture texture,
		Vector2 drawSize,
		Vector3 localPosition,
		string name = "HabitatGroundTile",
		float layer = HabitatGroundLayer )
	{
		var root = new GameObject( parent, true, name );
		// XY only — HabitatFloorDepthSorter pushes this into the ground band.
		// Nudge along +X (screen-up under the zoo camera) — see FloorTileAlignFix.
		root.LocalPosition = localPosition.WithZ( 0f ) + FloorTileAlignFix;

		var spriteGo = new GameObject( root, true, "Sprite" );
		spriteGo.LocalRotation = FloorTileRotation;
		var renderer = spriteGo.AddComponent<SpriteRenderer>();
		AssignSprite( renderer, texture );
		PixelArt.ApplyWorldScale( renderer, drawSize, PixelArt.TileSourcePixels, texture );
		ConfigureWorldFloorSprite( renderer );
		renderer.Opaque = true;
		renderer.AlphaCutoff = 0.08f;

		var sorter = root.AddComponent<HabitatFloorDepthSorter>();
		sorter.SortOrigin = root;
		_ = layer; // depth comes from HabitatGroundLayer via HabitatFloorDepthSorter

		root.Tags.Add( "habitat_ground" );
		return root;
	}

	/// <summary>Habitat interior floor tile — sortOrigin anchors one enclosure-wide depth.</summary>
	public static GameObject SpawnHabitatGroundTile(
		GameObject parent,
		Texture texture,
		Vector2 drawSize,
		Vector3 localPosition,
		GameObject sortOrigin,
		string name = "HabitatGroundTile",
		float layer = HabitatGroundLayer )
	{
		var root = SpawnHabitatGroundTile( parent, texture, drawSize, localPosition, name, layer );
		var sorter = root.Components.Get<HabitatFloorDepthSorter>();
		if ( sorter.IsValid() && sortOrigin.IsValid() )
			sorter.SortOrigin = sortOrigin;
		return root;
	}

	/// <summary>Habitat interior floor tile — square draw size.</summary>
	public static GameObject SpawnHabitatGroundTile(
		GameObject parent,
		Texture texture,
		float worldSize,
		Vector3 localPosition,
		string name = "HabitatGroundTile",
		float layer = HabitatGroundLayer ) =>
		SpawnHabitatGroundTile( parent, texture, new Vector2( worldSize, worldSize ), localPosition, name, layer );

	/// <summary>Semi-transparent biome fringe — softens hard edges between neighboring ground tiles.</summary>
	public static GameObject SpawnGroundBlendOverlayWorld(
		Vector3 worldPosition,
		Texture texture,
		float worldSize,
		float alpha,
		string name = "GroundBlend",
		float layer = WildernessLayer )
	{
		var root = SpawnGroundTileWorld( worldPosition, texture, worldSize, name, layer + 0.015f );
		var renderer = GetGroundSpriteRenderer( root );
		if ( renderer.IsValid() )
		{
			renderer.Opaque = false;
			renderer.AlphaCutoff = 0.01f;
			renderer.Color = Color.White.WithAlpha( alpha );
		}

		return root;
	}

	public static void ConfigureGroundSprite( SpriteRenderer renderer ) =>
		ConfigureWorldFloorSprite( renderer );

	public static void ConfigureFloorSprite( SpriteRenderer renderer, bool depthSort )
	{
		if ( depthSort )
			ConfigureSortedFloorOverlay( renderer );
		else
			ConfigureWorldFloorSprite( renderer );
	}

	/// <summary>Wilderness/grass/path base — opaque, flat, always behind sorted props.</summary>
	private static void ConfigureWorldFloorSprite( SpriteRenderer renderer )
	{
		if ( !renderer.IsValid() ) return;

		renderer.TextureFilter = Sandbox.Rendering.FilterMode.Point;
		renderer.Lighting = false;
		renderer.FogStrength = 0f;
		renderer.IsSorted = false;
		renderer.Billboard = SpriteRenderer.BillboardMode.None;
		renderer.Opaque = true;
		renderer.AlphaCutoff = 0.08f;
	}

	/// <summary>Build/plot preview overlays that still ride the sorted path band.</summary>
	private static void ConfigureSortedFloorOverlay( SpriteRenderer renderer )
	{
		if ( !renderer.IsValid() ) return;

		renderer.TextureFilter = Sandbox.Rendering.FilterMode.Point;
		renderer.Lighting = false;
		renderer.FogStrength = 0f;
		renderer.IsSorted = true;
		renderer.Billboard = SpriteRenderer.BillboardMode.Always;
		renderer.Opaque = true;
		renderer.AlphaCutoff = 0.08f;
	}

	public static GameObject SpawnWorld(
		Vector3 worldPosition,
		Texture texture,
		float worldSize,
		string name = "WorldSprite",
		float localZ = 0f,
		bool depthSort = true,
		float layer = BuildingLayer,
		float sourcePixels = PixelArt.SpriteSourcePixels )
	{
		var root = new GameObject( true, name );
		root.WorldPosition = worldPosition.WithZ( 0f );

		Spawn(
			root,
			texture,
			worldSize,
			depthSort ? Vector3.Zero : new Vector3( 0, 0, localZ ),
			name,
			depthSort: depthSort,
			layer: layer,
			sourcePixels: sourcePixels );

		return root;
	}

	public static GameObject SpawnProp( Vector3 worldPosition, string propName, float worldSize, float layer = EnrichmentLayer )
	{
		if ( PixelArt.IsPlayerPlacedPropOnly( propName ) )
		{
			Fauna2Debug.Warn( "Assets", $"Skipped prop '{propName}' — player-placed only" );
			return null;
		}

		if ( !PixelArt.TryProp( propName, out var texture ) )
		{
			texture = PlaceholderTiles.Prop( propName );
			Fauna2Debug.Warn( "Assets", $"Prop '{propName}' missing — using color placeholder" );
		}

		return SpawnWorld(
			worldPosition,
			texture,
			worldSize,
			propName,
			layer: layer,
			sourcePixels: PixelArt.IsSuppliedProp( propName ) ? PixelArt.SuppliedSpriteSourcePixels : PixelArt.SpriteSourcePixels );
	}

	public static void SpawnFenceRect(
		GameObject parent,
		Vector2 size,
		float gateWidth = 128f )
	{
		var tile = GameConstants.TileSize;
		var hx = size.x * 0.5f;
		var hy = size.y * 0.5f;
		var draw = tile * PixelArt.TileCoverage;
		const float fenceLayer = HabitatLayer;
		const float fenceYaw = 90f;
		var halfGate = gateWidth * 0.5f;

		var centers = GroundGrid.TileCentersInRect( -hx, -hy, hx, hy, tile ).ToList();
		if ( centers.Count == 0 ) return;

		var northY = centers.Max( c => c.y );
		var southY = centers.Min( c => c.y );
		var eastX = centers.Max( c => c.x );
		var westX = centers.Min( c => c.x );

		foreach ( var center in centers )
		{
			if ( MathF.Abs( center.y - northY ) < 0.01f )
				SpawnFenceSegment( parent, "fence_v", center.x, center.y, draw, fenceLayer, fenceYaw, alongX: true );

			if ( MathF.Abs( center.y - southY ) < 0.01f && MathF.Abs( center.x ) >= halfGate - 0.01f )
				SpawnFenceSegment( parent, "fence_v", center.x, center.y, draw, fenceLayer, fenceYaw, alongX: true );

			if ( MathF.Abs( center.x - eastX ) < 0.01f )
				SpawnFenceSegment( parent, "fence_h", center.x, center.y, draw, fenceLayer, fenceYaw, alongX: false );

			if ( MathF.Abs( center.x - westX ) < 0.01f )
				SpawnFenceSegment( parent, "fence_h", center.x, center.y, draw, fenceLayer, fenceYaw, alongX: false );
		}
	}

	private static void SpawnFenceSegment(
		GameObject parent,
		string fenceKey,
		float x,
		float y,
		float draw,
		float layer,
		float yawDegrees,
		bool alongX )
	{
		var tex = PixelArt.Prop( fenceKey );
		var aspect = PixelArt.FenceContentAspect( fenceKey );
		Spawn(
			parent,
			tex,
			draw,
			new Vector3( x, y, 0f ),
			alongX ? "FenceRailH" : "FenceRailV",
			layer: layer,
			sourcePixels: PixelArt.TileSourcePixels,
			yawDegrees: yawDegrees,
			contentAspect: aspect );
	}

	private static void AssignSprite( SpriteRenderer renderer, Texture texture ) =>
		AssignSprite( renderer, PixelArt.MakeSprite( texture ) );

	private static void AssignSprite( SpriteRenderer renderer, Sprite sprite )
	{
		renderer.Sprite = sprite;
		renderer.StartingAnimationName = sprite?.Animations?.Any( a => a.Name == PixelArt.IdleAnimationName ) == true
			? PixelArt.IdleAnimationName
			: "Default";
	}

	private static void ConfigureRenderer( SpriteRenderer renderer, bool depthSort )
	{
		renderer.TextureFilter = Sandbox.Rendering.FilterMode.Point;
		renderer.Lighting = false;
		renderer.FogStrength = 0f;

		// Background tiles must not enter the sorted pass (see ConfigureFloorSprite).
		renderer.IsSorted = depthSort;

		if ( depthSort )
		{
			renderer.Billboard = SpriteRenderer.BillboardMode.Always;
			renderer.Opaque = true;
			renderer.AlphaCutoff = 0.08f;
		}
	}
}
