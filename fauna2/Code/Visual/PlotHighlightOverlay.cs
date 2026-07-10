namespace Fauna2;

/// <summary>
/// Tints wilderness ground tiles inside a plot footprint for expand-land preview.
/// </summary>
public static class PlotHighlightOverlay
{
	public static IReadOnlyList<SpriteRenderer> Attach(
		GameObject parent,
		float plotSize,
		Color tint,
		float parentWorldZ = 0f )
	{
		var renderers = new List<SpriteRenderer>();
		var half = plotSize * 0.5f;
		var draw = GroundGrid.BaseDrawSize;
		var tile = GroundGrid.GroundTileSize;

		foreach ( var tileCenter in GroundGrid.TileCentersInRect( -half, -half, half, half, tile ) )
		{
			var renderer = WorldSprites.SpawnGroundTile(
				parent,
				PixelArt.TileWilderness,
				draw,
				new Vector3( tileCenter.x, tileCenter.y, 0f ),
				"PlotHighlightTile",
				layer: WorldSprites.GrassLayer + 0.5f,
				depthSort: true );
			renderer.Color = tint;
			renderers.Add( renderer );
		}

		return renderers;
	}
}
