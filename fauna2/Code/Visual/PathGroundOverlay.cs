namespace Fauna2;

/// <summary>
/// Paths replace buildable grass by painting path.png at the ground layer
/// instead of floating a decal above the terrain.
/// </summary>
public static class PathGroundOverlay
{
	/// <summary>Paths sit in PathLayer — depth-sorted above grass, below everything else.</summary>
	public static float PathSortLayer => WorldSprites.PathLayer;

	public static float CellSize( PlaceableDefinition def ) =>
		GameConstants.TileSize;

	public static float PathDrawSize( PlaceableDefinition def )
	{
		if ( def is null ) return GroundGrid.BuildableDrawSize * PixelArt.TileCoverage;
		var fp = def.EffectiveFootprint;
		return MathF.Max( fp.x, fp.y ) * PixelArt.TileCoverage;
	}

	public static GameObject Attach(
		GameObject parent,
		PlaceableDefinition def,
		float alpha = 1f,
		float parentWorldZ = 0f,
		Vector3? worldPosition = null )
	{
		var draw = PathDrawSize( def );

		var renderer = WorldSprites.Spawn(
			parent,
			PixelArt.TilePath,
			draw,
			Vector3.Zero,
			"PathGround",
			depthSort: true,
			layer: PathSortLayer,
			sourcePixels: PixelArt.TileSourcePixels,
			pathFloorSort: true );
		var go = renderer.GameObject;
		go.Tags.Add( "path_ground" );

		if ( alpha < 0.99f && parent.IsValid() )
			parent.LocalScale = Vector3.One * alpha;

		return go;
	}

	public static void SyncParentElevation( GameObject parent, float parentWorldZ )
	{
		// PathFloorDepthSorter tracks the build ghost / placeable root each frame.
	}
}
