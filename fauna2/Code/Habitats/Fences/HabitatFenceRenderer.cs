namespace Fauna2;

/// <summary>Spawns one normal fence sprite per perimeter cell — no corner PNGs or rail scaling.</summary>
public static class HabitatFenceRenderer
{
	/// <summary>Draw scale for habitat fence art only — grid placement and colliders stay one cell.</summary>
	public const float VisualOverscale = 1.5f;
	public const float FenceHVisualOverscale = 3f;
	public static IReadOnlyList<FenceTile> Attach(
		GameObject parent,
		Vector3 habitatWorldCenter,
		Vector2 size,
		float alpha = 1f,
		bool collision = true )
	{
		var tiles = HabitatFenceGenerator.CreateHabitatFence( habitatWorldCenter, size );
		Build( parent, tiles, habitatWorldCenter, alpha, collision );
		return tiles;
	}

	public static void Build(
		GameObject parent,
		IReadOnlyList<FenceTile> tiles,
		Vector3 habitatWorldCenter,
		float alpha = 1f,
		bool collision = true )
	{
		if ( parent is null || tiles is null || tiles.Count == 0 )
			return;

		var backRoot = new GameObject( parent, true, "FenceBack" );
		var frontRoot = new GameObject( parent, true, "FenceFront" );

		foreach ( var tile in tiles )
		{
			var layerRoot = tile.RenderLayer == FenceRenderLayer.Front ? frontRoot : backRoot;
			SpawnTile( layerRoot, tile, habitatWorldCenter, alpha, collision && tile.CollisionEnabled );
		}
	}

	private static void SpawnTile( GameObject parent, FenceTile tile, Vector3 habitatWorldCenter, float alpha, bool collision )
	{
		if ( !PixelArt.TryProp( tile.Sprite, out var texture ) )
		{
			if ( SuppliedSpriteManifest.TryGetSuppliedPropPath( tile.Sprite, out var path ) )
				Log.Warning( $"[Fauna2 Fence] Missing habitat fence sprite '{tile.Sprite}' — expected '{path}'." );
			else
				Log.Warning( $"[Fauna2 Fence] Unknown habitat fence sprite key '{tile.Sprite}'." );
			return;
		}

		var overscale = tile.Sprite == "fence_h" ? FenceHVisualOverscale : VisualOverscale;
		var draw = GameConstants.TileSize * PixelArt.TileCoverage * overscale;
		var sortLayer = tile.RenderLayer == FenceRenderLayer.Front
			? WorldSprites.FenceFrontLayer
			: WorldSprites.FenceBackLayer;

		var local = (tile.WorldCenter - habitatWorldCenter).WithZ( 0f );
		var renderer = SpawnCellFence(
			parent,
			texture,
			tile.Sprite,
			draw,
			local,
			sortLayer );

		var go = renderer.GameObject;
		go.Tags.Add( "habitat_fence" );

		if ( alpha < 0.99f )
			renderer.Color = renderer.Color.WithAlpha( alpha );

		if ( collision )
			AttachCollider( go );
	}

	/// <summary>One cell, correct aspect — bypasses TryApplyFenceRailScale used by generic props.</summary>
	private static SpriteRenderer SpawnCellFence(
		GameObject parent,
		Texture texture,
		string spriteKey,
		float draw,
		Vector3 localPosition,
		float layer )
	{
		var go = new GameObject( parent, true, spriteKey );
		var renderer = go.AddComponent<SpriteRenderer>();
		renderer.Sprite = PixelArt.MakeSprite( texture );
		renderer.TextureFilter = Sandbox.Rendering.FilterMode.Point;
		renderer.Lighting = false;
		renderer.FogStrength = 0f;
		renderer.IsSorted = true;
		renderer.Billboard = SpriteRenderer.BillboardMode.Always;
		renderer.Opaque = true;
		renderer.AlphaCutoff = 0.08f;

		PixelArt.ApplyWorldScale(
			renderer,
			draw,
			PixelArt.PropSourcePixels( spriteKey ),
			texture,
			PixelArt.FenceContentAspect( spriteKey ) );

		go.LocalPosition = localPosition;

		var sorter = go.AddComponent<PixelDepthSorter>();
		sorter.BaseLayer = layer;

		return renderer;
	}

	private static void AttachCollider( GameObject go )
	{
		var tile = GameConstants.TileSize;
		var collider = go.AddComponent<BoxCollider>();
		collider.Scale = new Vector3( tile * 0.88f, tile * 0.88f, 56f );
		collider.Center = new Vector3( 0f, 0f, 28f );
		collider.Static = true;
		go.Tags.Add( "walk_block" );
	}
}
