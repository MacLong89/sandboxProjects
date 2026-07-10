namespace Fauna2;

/// <summary>
/// Two aligned grids: 64-unit build cells (paths/props) and 512-unit ground textures (8×8 build cells each).
/// </summary>
public static class GroundGrid
{
	public static float BuildTileSize => GameConstants.TileSize;

	public static float GroundTileSize => GameConstants.GroundTileSize;

	/// <summary>Path paint size — one build tile.</summary>
	public static float BuildableDrawSize => BuildTileSize;

	/// <summary>Grass/wilderness sprite size — overlap slightly so chunked tiles never show gaps.</summary>
	public static float BaseDrawSize => GroundRenderTileSize * PixelArt.TileCoverage;

	/// <summary>Break coplanar z-fighting between overlapping floor quads.</summary>
	public static float FloorDepthBias( float worldX, float worldY, float tileSize = 0f )
	{
		if ( tileSize <= 0f ) tileSize = GroundRenderTileSize;
		var ix = (int)MathF.Floor( worldX / tileSize );
		var iy = (int)MathF.Floor( worldY / tileSize );
		return ((ix + iy) & 31) * 0.0125f;
	}

	public static float GroundRenderTileSize => GameConstants.GroundRenderTileSize;

	public static float SnapAxisToTileCenter( float worldAxis, float tileSize = 0f )
	{
		if ( tileSize <= 0f ) tileSize = BuildTileSize;
		return MathF.Floor( worldAxis / tileSize ) * tileSize + tileSize * 0.5f;
	}

	public static Vector3 SnapToTileCenter( Vector3 worldPosition, float tileSize = 0f )
	{
		if ( tileSize <= 0f ) tileSize = BuildTileSize;
		return new Vector3(
			SnapAxisToTileCenter( worldPosition.x, tileSize ),
			SnapAxisToTileCenter( worldPosition.y, tileSize ),
			worldPosition.z );
	}

	public static Vector3 TileCenterOffset( Vector3 worldPosition, float tileSize = 0f ) =>
		SnapToTileCenter( worldPosition, tileSize ) - worldPosition;

	public static IEnumerable<Vector2> TileCentersInRect(
		float minX,
		float minY,
		float maxX,
		float maxY,
		float tileSize = 0f )
	{
		if ( tileSize <= 0f ) tileSize = BuildTileSize;
		var half = tileSize * 0.5f;
		var xStart = SnapAxisToTileCenter( minX + 0.001f, tileSize );
		var yStart = SnapAxisToTileCenter( minY + 0.001f, tileSize );

		for ( var x = xStart; x <= maxX - half + 0.01f; x += tileSize )
		{
			for ( var y = yStart; y <= maxY - half + 0.01f; y += tileSize )
				yield return new Vector2( x, y );
		}
	}

	/// <summary>Tile centers for any cell that intersects the rect (fills plot edges).</summary>
	public static IEnumerable<Vector2> TileCentersCoveringRect(
		float minX,
		float minY,
		float maxX,
		float maxY,
		float tileSize = 0f )
	{
		if ( tileSize <= 0f ) tileSize = BuildTileSize;
		var half = tileSize * 0.5f;
		var xStart = SnapAxisToTileCenter( minX + half - 0.001f, tileSize );
		var yStart = SnapAxisToTileCenter( minY + half - 0.001f, tileSize );

		for ( var x = xStart; x <= maxX + half - 0.01f; x += tileSize )
		{
			for ( var y = yStart; y <= maxY + half - 0.01f; y += tileSize )
				yield return new Vector2( x, y );
		}
	}
}
