namespace Fauna2;

/// <summary>Pixel tree/rock sprites for terrain obstacles.</summary>
public static class TerrainObstacleVisuals
{
	private static int _loggedBuilds;

	internal static void ResetDiagnostics() => _loggedBuilds = 0;

	public static void Build( GameObject root, TerrainObstacleType type, Biome biome, int seed )
	{
		var rng = new Random( seed );
		var prop = type == TerrainObstacleType.Tree ? BiomeEcology.PickTreeProp( biome, rng ) : "rock";
		var scale = Range( rng, 0.85f, 1.2f );
		var baseTiles = type == TerrainObstacleType.Tree
			? GameConstants.TreeSpriteTiles
			: GameConstants.RockSpriteTiles;
		var size = GameConstants.Tiles( baseTiles * scale );

		WorldSprites.Spawn(
			root,
			PixelArt.Prop( prop ),
			size,
			name: prop,
			layer: WorldSprites.EnrichmentLayer,
			sourcePixels: PixelArt.IsSuppliedProp( prop ) ? PixelArt.SuppliedSpriteSourcePixels : PixelArt.SpriteSourcePixels );

		var sprite = root.Children.LastOrDefault()?.Components.Get<SpriteRenderer>();
		if ( _loggedBuilds < 8 )
		{
			_loggedBuilds++;
			Log.Info( $"[Fauna2 Props] Built terrain obstacle sprite sample {_loggedBuilds}: type={type}, prop={prop}, supplied={PixelArt.IsSuppliedProp( prop )}, size={GameConstants.FormatTiles( size )} tiles, worldPos={root.WorldPosition}, children={root.Children.Count}." );
			if ( sprite.IsValid() )
				Fauna2RenderDiagnostics.LogRendererState( $"obstacle-sample-{_loggedBuilds}", sprite );
			else
				Log.Warning( $"[Fauna2 Props] Obstacle sample {_loggedBuilds} has no SpriteRenderer child on '{root.Name}'." );
		}
	}

	private static float Range( Random rng, float min, float max ) =>
		min + rng.NextSingle() * (max - min);
}
