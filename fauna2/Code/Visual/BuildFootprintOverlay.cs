namespace Fauna2;

/// <summary>Highlights build-grid cells occupied by a placeable ghost or preview.</summary>
public static class BuildFootprintOverlay
{
	public static void Attach( GameObject parent, Vector2 footprint, float alpha = 0.28f, float? layer = null )
	{
		var hx = footprint.x * 0.5f;
		var hy = footprint.y * 0.5f;
		var tile = GameConstants.TileSize;
		var draw = tile * PixelArt.TileCoverage;
		const float z = 0.05f;
		var sortLayer = layer ?? WorldSprites.BuildingLayer;

		foreach ( var center in GroundGrid.TileCentersInRect( -hx, -hy, hx, hy, tile ) )
		{
			var renderer = WorldSprites.SpawnGroundTile(
				parent,
				PixelArt.TilePath,
				draw,
				new Vector3( center.x, center.y, z ),
				"FootprintTile",
				layer: sortLayer,
				depthSort: true );
			renderer.Color = new Color( 0.45f, 0.82f, 1f, alpha );
			renderer.GameObject.Tags.Add( "footprint_preview" );
		}
	}
}
