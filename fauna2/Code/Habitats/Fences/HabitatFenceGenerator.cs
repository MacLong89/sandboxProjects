namespace Fauna2;

/// <summary>
/// Closed rectangular habitat fences — one fence_h or fence_v per perimeter grid cell.
/// </summary>
public static class HabitatFenceGenerator
{
	/// <summary>
	/// Generate perimeter fence tiles for a rectangle in tile coordinates.
	/// <paramref name="startX"/> / <paramref name="startY"/> is the south-west corner index.
	/// </summary>
	public static IReadOnlyList<FenceTile> CreateHabitatFence( int startX, int startY, int width, int height, float tileSize = 0f )
	{
		if ( tileSize <= 0f ) tileSize = GameConstants.TileSize;
		width = Math.Max( 2, width );
		height = Math.Max( 2, height );

		var tiles = new List<FenceTile>( (width + height) * 2 );

		for ( var gy = 0; gy < height; gy++ )
		{
			for ( var gx = 0; gx < width; gx++ )
			{
				var onNorth = gy == 0;
				var onSouth = gy == height - 1;
				var onWest = gx == 0;
				var onEast = gx == width - 1;
				if ( !onNorth && !onSouth && !onWest && !onEast )
					continue;

				var worldX = (startX + gx + 0.5f) * tileSize;
				var worldY = (startY + gy + 0.5f) * tileSize;

				AddPerimeterCell(
					tiles,
					new Vector2Int( startX + gx, startY + gy ),
					new Vector3( worldX, worldY, 0f ),
					onNorth, onSouth, onWest, onEast );
			}
		}

		return tiles;
	}

	/// <summary>World-space wrapper — one rail centered on each outer perimeter cell.</summary>
	public static IReadOnlyList<FenceTile> CreateHabitatFence( Vector3 center, Vector2 size )
	{
		var tile = GameConstants.TileSize;
		var hx = size.x * 0.5f;
		var hy = size.y * 0.5f;

		var centers = GroundGrid.TileCentersCoveringRect(
			center.x - hx, center.y - hy, center.x + hx, center.y + hy, tile ).ToList();

		if ( centers.Count == 0 )
			return Array.Empty<FenceTile>();

		var northY = centers.Min( c => c.y );
		var southY = centers.Max( c => c.y );
		var eastX = centers.Max( c => c.x );
		var westX = centers.Min( c => c.x );
		const float eps = 0.01f;

		var tiles = new List<FenceTile>( centers.Count );

		foreach ( var cell in centers )
		{
			var onNorth = MathF.Abs( cell.y - northY ) < eps;
			var onSouth = MathF.Abs( cell.y - southY ) < eps;
			var onEast = MathF.Abs( cell.x - eastX ) < eps;
			var onWest = MathF.Abs( cell.x - westX ) < eps;
			if ( !onNorth && !onSouth && !onEast && !onWest )
				continue;

			var gx = (int)MathF.Round( cell.x / tile - 0.5f );
			var gy = (int)MathF.Round( cell.y / tile - 0.5f );

			AddPerimeterCell(
				tiles,
				new Vector2Int( gx, gy ),
				new Vector3( cell.x, cell.y, 0f ),
				onNorth, onSouth, onWest, onEast );
		}

		return tiles;
	}

	/// <summary>North/south rows use fence_v; east/west columns use fence_h.</summary>
	private static void AddPerimeterCell(
		List<FenceTile> tiles,
		Vector2Int gridPosition,
		Vector3 worldCenter,
		bool north,
		bool south,
		bool west,
		bool east )
	{
		var sprite = north || south ? "fence_v" : "fence_h";
		var layer = PickRenderLayer( south, north );

		tiles.Add( new FenceTile
		{
			GridPosition = gridPosition,
			WorldCenter = worldCenter,
			Sprite = sprite,
			CollisionEnabled = true,
			RenderLayer = layer,
		} );
	}

	private static FenceRenderLayer PickRenderLayer( bool south, bool north )
	{
		if ( south && !north )
			return FenceRenderLayer.Front;

		return FenceRenderLayer.Back;
	}
}
