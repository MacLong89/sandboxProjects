namespace Fauna2;

/// <summary>
/// Paints a habitat interior with the same biome ground tiles used on the world map.
/// </summary>
public static class HabitatGroundOverlay
{
	public static void Attach( GameObject parent, Vector2 size, Biome biome, float alpha = 1f )
	{
		if ( parent is null || size.x <= 0f || size.y <= 0f )
			return;

		var texture = WildernessBiomeMap.GroundTile( biome );
		var tileSize = GameConstants.GroundRenderTileSize;
		var draw = GroundGrid.BaseDrawSize;
		var layer = WorldSprites.HabitatGroundLayer;
		var hx = size.x * 0.5f;
		var hy = size.y * 0.5f;
		const float z = 0.05f;

		var groundRoot = new GameObject( parent, true, "Ground" );

		foreach ( var center in GroundGrid.TileCentersCoveringRect( -hx, -hy, hx, hy, tileSize ) )
		{
			var renderer = WorldSprites.SpawnGroundTile(
				groundRoot,
				texture,
				draw,
				new Vector3( center.x, center.y, z ),
				"HabitatGroundTile",
				layer: layer,
				depthSort: true );
			renderer.GameObject.Tags.Add( "habitat_ground" );

			if ( alpha < 0.99f )
				renderer.Color = renderer.Color.WithAlpha( alpha );
		}
	}
}
